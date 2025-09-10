using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using PriceFeed.Core.Interfaces;
using PriceFeed.Core.Models;
using PriceFeed.Core.Options;

namespace PriceFeed.Infrastructure.DataSources;

/// <summary>
/// Data source adapter for fetching price data from OKEx
/// </summary>
public class OKExDataSourceAdapter : IDataSourceAdapter
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<OKExDataSourceAdapter> _logger;
    private readonly OKExOptions _options;
    private readonly SymbolMappingOptions _symbolMappings;

    /// <summary>
    /// Gets the name of the data source
    /// </summary>
    public string SourceName => "OKEx";

    /// <summary>
    /// Checks if the data source is enabled (has API key configured)
    /// </summary>
    /// <returns>True if the data source is enabled, false otherwise</returns>
    public bool IsEnabled()
    {
        // OKEx can work with public API for price data, but we'll check for API key
        // to determine if it's explicitly enabled
        bool hasApiKey = !string.IsNullOrEmpty(_options.ApiKey);

        if (!hasApiKey)
        {
            _logger.LogWarning("OKEx API key is not configured. OKEx data source is disabled.");
        }

        return hasApiKey;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="OKExDataSourceAdapter"/> class
    /// </summary>
    /// <param name="httpClientFactory">The HTTP client factory</param>
    /// <param name="logger">The logger</param>
    /// <param name="options">The OKEx options</param>
    public OKExDataSourceAdapter(
        IHttpClientFactory httpClientFactory,
        ILogger<OKExDataSourceAdapter> logger,
        IOptions<OKExOptions> options,
        IOptions<PriceFeedOptions> priceFeedOptions)
    {
        _httpClient = httpClientFactory.CreateClient("OKEx");
        _logger = logger;
        _options = options.Value;
        _symbolMappings = priceFeedOptions.Value.SymbolMappings;

        _httpClient.BaseAddress = new Uri(_options.BaseUrl);
        _httpClient.Timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds);

        // Add API key header if available
        if (!string.IsNullOrEmpty(_options.ApiKey))
        {
            _httpClient.DefaultRequestHeaders.Add("OK-ACCESS-KEY", _options.ApiKey);

            // Additional authentication headers would be added for private API endpoints
            // Using public API endpoints for price data
        }
    }

    /// <summary>
    /// Gets the list of symbols supported by OKEx
    /// </summary>
    /// <returns>A list of supported symbols</returns>
    public Task<IEnumerable<string>> GetSupportedSymbolsAsync()
    {
        // Return all symbols that are supported by OKEx
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
            // Check if this symbol is supported by OKEx
            if (!_symbolMappings.IsSymbolSupportedBySource(symbol, SourceName))
            {
                _logger.LogWarning("Symbol {Symbol} is not supported by {Source}", symbol, SourceName);
                throw new NotSupportedException($"Symbol {symbol} is not supported by {SourceName}");
            }

            // Get the OKEx-specific symbol format
            var okexSymbol = _symbolMappings.GetSourceSymbol(symbol, SourceName);

            // Build the request URL
            var endpoint = $"{_options.TickerEndpoint}?instId={okexSymbol}";
            var response = await _httpClient.GetAsync(endpoint);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            var tickerResponse = JsonConvert.DeserializeObject<OKExTickerResponse>(content);

            if (tickerResponse?.Data == null || !tickerResponse.Data.Any())
            {
                _logger.LogWarning("Failed to get price data for symbol {Symbol} from OKEx: Response is invalid", symbol);
                throw new Exception($"Failed to get price data for symbol {symbol} from OKEx");
            }

            var tickerData = tickerResponse.Data[0];

            return new PriceData
            {
                Symbol = symbol,
                Price = decimal.Parse(tickerData.Last),
                Source = SourceName,
                Timestamp = DateTime.UtcNow,
                Volume = decimal.Parse(tickerData.Vol24h),
                Metadata = new Dictionary<string, string>
                {
                    { "High24h", tickerData.High24h },
                    { "Low24h", tickerData.Low24h },
                    { "OpenPrice", tickerData.Open24h },
                    { "SourceSymbol", okexSymbol } // Store the source-specific symbol in metadata
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting price data for symbol {Symbol} from OKEx", symbol);
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
        // Filter out symbols that are not supported by OKEx
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
                _logger.LogError(ex, "Error getting price data for symbol {Symbol} from OKEx", symbol);
                // Continue with other symbols
            }
        }

        return results;
    }
}

/// <summary>
/// Response from OKEx ticker API
/// </summary>
internal class OKExTickerResponse
{
    [JsonProperty("code")]
    public string Code { get; set; } = string.Empty;

    [JsonProperty("msg")]
    public string Message { get; set; } = string.Empty;

    [JsonProperty("data")]
    public List<OKExTickerData> Data { get; set; } = new();
}

/// <summary>
/// Ticker data from OKEx
/// </summary>
internal class OKExTickerData
{
    [JsonProperty("instId")]
    public string InstrumentId { get; set; } = string.Empty;

    [JsonProperty("last")]
    public string Last { get; set; } = string.Empty;

    [JsonProperty("open24h")]
    public string Open24h { get; set; } = string.Empty;

    [JsonProperty("high24h")]
    public string High24h { get; set; } = string.Empty;

    [JsonProperty("low24h")]
    public string Low24h { get; set; } = string.Empty;

    [JsonProperty("vol24h")]
    public string Vol24h { get; set; } = string.Empty;

    [JsonProperty("volCcy24h")]
    public string VolCcy24h { get; set; } = string.Empty;
}
