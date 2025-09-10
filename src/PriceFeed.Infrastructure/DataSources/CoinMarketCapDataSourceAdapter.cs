using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using PriceFeed.Core.Interfaces;
using PriceFeed.Core.Models;
using PriceFeed.Core.Options;

namespace PriceFeed.Infrastructure.DataSources;

/// <summary>
/// Data source adapter for fetching price data from CoinMarketCap
/// </summary>
public class CoinMarketCapDataSourceAdapter : IDataSourceAdapter
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<CoinMarketCapDataSourceAdapter> _logger;
    private readonly CoinMarketCapOptions _options;
    private readonly SymbolMappingOptions _symbolMappings;

    /// <summary>
    /// Gets the name of the data source
    /// </summary>
    public string SourceName => "CoinMarketCap";

    /// <summary>
    /// Checks if the data source is enabled (has API key configured)
    /// </summary>
    /// <returns>True if the data source is enabled, false otherwise</returns>
    public bool IsEnabled()
    {
        // CoinMarketCap requires an API key to function
        bool hasApiKey = !string.IsNullOrEmpty(_options.ApiKey);

        if (!hasApiKey)
        {
            _logger.LogWarning("CoinMarketCap API key is not configured. CoinMarketCap data source is disabled.");
        }

        return hasApiKey;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CoinMarketCapDataSourceAdapter"/> class
    /// </summary>
    /// <param name="httpClientFactory">The HTTP client factory</param>
    /// <param name="logger">The logger</param>
    /// <param name="options">The CoinMarketCap options</param>
    public CoinMarketCapDataSourceAdapter(
        IHttpClientFactory httpClientFactory,
        ILogger<CoinMarketCapDataSourceAdapter> logger,
        IOptions<CoinMarketCapOptions> options,
        IOptions<PriceFeedOptions> priceFeedOptions)
    {
        _httpClient = httpClientFactory.CreateClient("CoinMarketCap");
        _logger = logger;

        try
        {
            _options = options.Value;
        }
        catch (OptionsValidationException ex)
        {
            // In testnet mode, if validation fails, create testnet configuration manually
            _logger.LogWarning("CoinMarketCap options validation failed, loading testnet configuration: {Message}", ex.Message);
            _options = new CoinMarketCapOptions
            {
                BaseUrl = "https://pro-api.coinmarketcap.com",
                LatestQuotesEndpoint = "/v1/cryptocurrency/quotes/latest",
                ApiKey = Environment.GetEnvironmentVariable("COINMARKETCAP_API_KEY") ?? "",
                TimeoutSeconds = 30
            };
        }

        _symbolMappings = priceFeedOptions.Value.SymbolMappings;

        _httpClient.BaseAddress = new Uri(_options.BaseUrl);
        _httpClient.Timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds);

        // Add API key header if available
        if (!string.IsNullOrEmpty(_options.ApiKey))
        {
            _httpClient.DefaultRequestHeaders.Add("X-CMC_PRO_API_KEY", _options.ApiKey);
        }
    }

    /// <summary>
    /// Gets the list of symbols supported by CoinMarketCap
    /// </summary>
    /// <returns>A list of supported symbols</returns>
    public Task<IEnumerable<string>> GetSupportedSymbolsAsync()
    {
        // Return all symbols that are supported by CoinMarketCap
        return Task.FromResult<IEnumerable<string>>(_symbolMappings.GetSymbolsForDataSource(SourceName));
    }

    /// <summary>
    /// Fetches the current price data for a specific symbol
    /// </summary>
    /// <param name="symbol">The standard symbol to fetch price data for</param>
    /// <returns>The price data for the specified symbol</returns>
    public async Task<PriceData> GetPriceDataAsync(string symbol)
    {
        try
        {
            // Check if this symbol is supported by CoinMarketCap
            if (!_symbolMappings.IsSymbolSupportedBySource(symbol, SourceName))
            {
                _logger.LogWarning("Symbol {Symbol} is not supported by {Source}", symbol, SourceName);
                throw new NotSupportedException($"Symbol {symbol} is not supported by {SourceName}");
            }

            // Get the CoinMarketCap-specific symbol format
            var cmcSymbol = _symbolMappings.GetSourceSymbol(symbol, SourceName);

            // Build the request URL
            var endpoint = $"{_options.LatestQuotesEndpoint}?symbol={cmcSymbol}&convert=USD";
            var response = await _httpClient.GetAsync(endpoint);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            var quoteResponse = JsonConvert.DeserializeObject<CoinMarketCapQuoteResponse>(content);

            if (quoteResponse?.Data == null || !quoteResponse.Data.ContainsKey(cmcSymbol))
            {
                _logger.LogWarning("Failed to get price data for symbol {Symbol} from CoinMarketCap: Response is invalid", symbol);
                throw new Exception($"Failed to get price data for symbol {symbol} from CoinMarketCap");
            }

            var coinData = quoteResponse.Data[cmcSymbol];
            var quote = coinData.Quote["USD"];

            // Determine the price based on the symbol
            decimal price;
            if (symbol.EndsWith("USDT"))
            {
                // Direct USD price
                price = quote.Price;
            }
            else if (symbol.EndsWith("BTC"))
            {
                // Need to calculate price in BTC
                // Get BTC price first
                var btcEndpoint = $"{_options.LatestQuotesEndpoint}?symbol=BTC&convert=USD";
                var btcResponse = await _httpClient.GetAsync(btcEndpoint);
                btcResponse.EnsureSuccessStatusCode();

                var btcContent = await btcResponse.Content.ReadAsStringAsync();
                var btcQuoteResponse = JsonConvert.DeserializeObject<CoinMarketCapQuoteResponse>(btcContent);

                if (btcQuoteResponse?.Data == null || !btcQuoteResponse.Data.ContainsKey("BTC"))
                {
                    _logger.LogWarning("Failed to get BTC price from CoinMarketCap");
                    throw new Exception("Failed to get BTC price from CoinMarketCap");
                }

                var btcPrice = btcQuoteResponse.Data["BTC"].Quote["USD"].Price;
                price = quote.Price / btcPrice;
            }
            else
            {
                _logger.LogWarning("Unsupported symbol format: {Symbol}", symbol);
                throw new Exception($"Unsupported symbol format: {symbol}");
            }

            return new PriceData
            {
                Symbol = symbol,
                Price = price,
                Source = SourceName,
                Timestamp = DateTime.UtcNow,
                Volume = (decimal)quote.Volume24h,
                Metadata = new Dictionary<string, string>
                {
                    { "MarketCap", quote.MarketCap.ToString() },
                    { "PercentChange24h", quote.PercentChange24h.ToString() },
                    { "SourceSymbol", cmcSymbol } // Store the source-specific symbol in metadata
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting price data for symbol {Symbol} from CoinMarketCap", symbol);
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
        // Filter out symbols that are not supported by CoinMarketCap
        var supportedSymbols = symbols
            .Where(s => _symbolMappings.IsSymbolSupportedBySource(s, SourceName))
            .ToList();

        if (!supportedSymbols.Any())
        {
            _logger.LogWarning("No supported symbols found for {Source}", SourceName);
            return Enumerable.Empty<PriceData>();
        }

        var results = new List<PriceData>();

        foreach (var symbol in supportedSymbols)
        {
            try
            {
                var result = await GetPriceDataAsync(symbol);
                results.Add(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting price data for symbol {Symbol} from CoinMarketCap", symbol);
                // Continue with other symbols
            }
        }

        return results;
    }
}

/// <summary>
/// Response from CoinMarketCap quotes API
/// </summary>
internal class CoinMarketCapQuoteResponse
{
    [JsonProperty("data")]
    public Dictionary<string, CoinMarketCapCoinData> Data { get; set; } = new();
}

/// <summary>
/// Coin data from CoinMarketCap
/// </summary>
internal class CoinMarketCapCoinData
{
    [JsonProperty("id")]
    public int Id { get; set; }

    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty("symbol")]
    public string Symbol { get; set; } = string.Empty;

    [JsonProperty("quote")]
    public Dictionary<string, CoinMarketCapQuote> Quote { get; set; } = new();
}

/// <summary>
/// Quote data from CoinMarketCap
/// </summary>
internal class CoinMarketCapQuote
{
    [JsonProperty("price")]
    public decimal Price { get; set; }

    [JsonProperty("volume_24h")]
    public double Volume24h { get; set; }

    [JsonProperty("market_cap")]
    public double MarketCap { get; set; }

    [JsonProperty("percent_change_24h")]
    public double PercentChange24h { get; set; }
}
