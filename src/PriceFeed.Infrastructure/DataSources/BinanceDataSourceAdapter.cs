using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using PriceFeed.Core.Interfaces;
using PriceFeed.Core.Models;
using PriceFeed.Core.Options;

namespace PriceFeed.Infrastructure.DataSources;

/// <summary>
/// Data source adapter for fetching price data from Binance
/// </summary>
public class BinanceDataSourceAdapter : IDataSourceAdapter
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<BinanceDataSourceAdapter> _logger;
    private readonly BinanceOptions _options;
    private readonly SymbolMappingOptions _symbolMappings;

    /// <summary>
    /// Gets the name of the data source
    /// </summary>
    public string SourceName => "Binance";

    /// <summary>
    /// Checks if the data source is enabled (has API key configured)
    /// </summary>
    /// <returns>True if the data source is enabled, false otherwise</returns>
    public bool IsEnabled()
    {
        // Binance can work with public API for price data, but we'll check for API key
        // to determine if it's explicitly enabled
        bool hasApiKey = !string.IsNullOrEmpty(_options.ApiKey);

        if (!hasApiKey)
        {
            _logger.LogWarning("Binance API key is not configured. Binance data source is disabled.");
        }

        return hasApiKey;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="BinanceDataSourceAdapter"/> class
    /// </summary>
    /// <param name="httpClientFactory">The HTTP client factory</param>
    /// <param name="logger">The logger</param>
    /// <param name="options">The Binance options</param>
    public BinanceDataSourceAdapter(
        IHttpClientFactory httpClientFactory,
        ILogger<BinanceDataSourceAdapter> logger,
        IOptions<BinanceOptions> options,
        IOptions<PriceFeedOptions> priceFeedOptions)
    {
        _httpClient = httpClientFactory.CreateClient("Binance");
        _logger = logger;
        _options = options.Value;
        _symbolMappings = priceFeedOptions.Value.SymbolMappings;

        _httpClient.BaseAddress = new Uri(_options.BaseUrl);
        _httpClient.Timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds);
    }

    /// <summary>
    /// Gets the list of symbols supported by Binance
    /// </summary>
    /// <returns>A list of supported symbols</returns>
    public Task<IEnumerable<string>> GetSupportedSymbolsAsync()
    {
        // Return all symbols that are supported by Binance according to the symbol mappings
        return Task.FromResult<IEnumerable<string>>(_symbolMappings.GetSymbolsForDataSource(SourceName));
    }

    /// <summary>
    /// Fetches the current price data for a specific symbol
    /// </summary>
    /// <param name="symbol">The standard symbol to fetch price data for</param>
    /// <returns>The price data for the specified symbol</returns>
    public async Task<PriceData> GetPriceDataAsync(string symbol)
    {
        // Validate input
        if (string.IsNullOrWhiteSpace(symbol))
        {
            _logger.LogError("Symbol cannot be null or empty");
            throw new ArgumentException("Symbol cannot be null or empty", nameof(symbol));
        }

        // Check if this symbol is supported by Binance
        if (!_symbolMappings.IsSymbolSupportedBySource(symbol, SourceName))
        {
            _logger.LogWarning("Symbol {Symbol} is not supported by {Source}", symbol, SourceName);
            throw new NotSupportedException($"Symbol {symbol} is not supported by {SourceName}");
        }

        // Get the Binance-specific symbol format
        var binanceSymbol = _symbolMappings.GetSourceSymbol(symbol, SourceName);

        // Sanitize input to prevent injection attacks
        binanceSymbol = SanitizeSymbol(binanceSymbol);

        try
        {
            var endpoint = $"{_options.TickerPriceEndpoint}?symbol={binanceSymbol}";
            var response = await _httpClient.GetAsync(endpoint);
            response.EnsureSuccessStatusCode();

            var tickerPrice = await response.Content.ReadFromJsonAsync<BinanceTickerPrice>();

            if (tickerPrice == null)
            {
                _logger.LogWarning("Failed to get price data for symbol {Symbol} from Binance: Response is null", symbol);
                throw new Exception($"Failed to get price data for symbol {symbol} from Binance");
            }

            // Get 24-hour statistics for volume information
            var volumeEndpoint = $"{_options.Ticker24hEndpoint}?symbol={binanceSymbol}";
            var volumeResponse = await _httpClient.GetAsync(volumeEndpoint);
            volumeResponse.EnsureSuccessStatusCode();

            var ticker24h = await volumeResponse.Content.ReadFromJsonAsync<BinanceTicker24h>();

            return new PriceData
            {
                Symbol = symbol, // Use the standard symbol, not the Binance-specific one
                Price = decimal.Parse(tickerPrice.Price),
                Source = SourceName,
                Timestamp = DateTime.UtcNow,
                Volume = ticker24h?.Volume != null ? decimal.Parse(ticker24h.Volume) : null,
                Metadata = new Dictionary<string, string>
                {
                    { "QuoteVolume", ticker24h?.QuoteVolume ?? "0" },
                    { "PriceChangePercent", ticker24h?.PriceChangePercent ?? "0" },
                    { "SourceSymbol", binanceSymbol } // Store the source-specific symbol in metadata
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting price data for symbol {Symbol} from Binance", symbol);
            throw;
        }
    }

    /// <summary>
    /// Fetches the current price data for multiple symbols
    /// </summary>
    /// <param name="symbols">The standard symbols to fetch price data for</param>
    /// <returns>A collection of price data for the specified symbols</returns>
    public async Task<IEnumerable<PriceData>> GetPriceDataBatchAsync(IEnumerable<string> symbols)
    {
        try
        {
            // Validate input
            if (symbols == null)
            {
                _logger.LogError("Symbols collection cannot be null");
                throw new ArgumentNullException(nameof(symbols));
            }

            // Filter out invalid symbols and symbols not supported by Binance
            var validSymbols = symbols
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Where(s => _symbolMappings.IsSymbolSupportedBySource(s, SourceName))
                .ToList();

            if (!validSymbols.Any())
            {
                _logger.LogWarning("No valid symbols provided for {Source}", SourceName);
                return Enumerable.Empty<PriceData>();
            }

            var tasks = validSymbols.Select(GetPriceDataAsync);
            var results = new List<PriceData>();

            foreach (var task in tasks)
            {
                try
                {
                    var result = await task;
                    results.Add(result);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error getting price data from Binance for a specific symbol");
                    // Collect errors but continue with other symbols to get partial data
                    // Consider implementing a threshold for acceptable failure rate
                }
            }

            return results;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP request error in GetPriceDataBatchAsync");
            throw new InvalidOperationException($"Failed to fetch price data from {SourceName} due to network error", ex);
        }
        catch (System.Text.Json.JsonException ex)
        {
            _logger.LogError(ex, "JSON parsing error in GetPriceDataBatchAsync");
            throw new InvalidOperationException($"Failed to parse price data from {SourceName}", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in GetPriceDataBatchAsync");
            throw new InvalidOperationException($"Unexpected error while fetching price data from {SourceName}", ex);
        }
    }

    /// <summary>
    /// Sanitizes a symbol to prevent injection attacks
    /// </summary>
    /// <param name="symbol">The symbol to sanitize</param>
    /// <returns>The sanitized symbol</returns>
    private string SanitizeSymbol(string symbol)
    {
        // Remove any characters that aren't alphanumeric
        return new string(symbol.Where(char.IsLetterOrDigit).ToArray());
    }
}

/// <summary>
/// Represents exchange information from Binance
/// </summary>
internal class BinanceExchangeInfo
{
    [JsonProperty("symbols")]
    public List<BinanceSymbolInfo> Symbols { get; set; } = new List<BinanceSymbolInfo>();
}

/// <summary>
/// Represents symbol information from Binance
/// </summary>
internal class BinanceSymbolInfo
{
    [JsonProperty("symbol")]
    public string Symbol { get; set; } = string.Empty;

    [JsonProperty("status")]
    public string Status { get; set; } = string.Empty;
}

/// <summary>
/// Represents ticker price information from Binance
/// </summary>
internal class BinanceTickerPrice
{
    [JsonProperty("symbol")]
    public string Symbol { get; set; } = string.Empty;

    [JsonProperty("price")]
    public string Price { get; set; } = string.Empty;
}

/// <summary>
/// Represents 24-hour ticker information from Binance
/// </summary>
internal class BinanceTicker24h
{
    [JsonProperty("symbol")]
    public string Symbol { get; set; } = string.Empty;

    [JsonProperty("volume")]
    public string? Volume { get; set; }

    [JsonProperty("quoteVolume")]
    public string? QuoteVolume { get; set; }

    [JsonProperty("priceChangePercent")]
    public string? PriceChangePercent { get; set; }
}
