using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using PriceFeed.Core.Interfaces;
using PriceFeed.Core.Models;
using PriceFeed.Core.Options;

namespace PriceFeed.Infrastructure.DataSources;

/// <summary>
/// Data source adapter for fetching price data from Kraken
/// </summary>
public class KrakenDataSourceAdapter : IDataSourceAdapter
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<KrakenDataSourceAdapter> _logger;
    private readonly KrakenOptions _options;
    private readonly SymbolMappingOptions _symbolMappings;

    /// <summary>
    /// Gets the name of the data source
    /// </summary>
    public string SourceName => "Kraken";

    /// <summary>
    /// Checks if the data source is enabled
    /// </summary>
    /// <returns>True if the data source is enabled, false otherwise</returns>
    public bool IsEnabled()
    {
        // Kraken works with public API without requiring API key
        // Always enabled unless explicitly disabled
        return true;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="KrakenDataSourceAdapter"/> class
    /// </summary>
    /// <param name="httpClientFactory">The HTTP client factory</param>
    /// <param name="logger">The logger</param>
    /// <param name="options">The Kraken options</param>
    /// <param name="priceFeedOptions">The price feed options</param>
    public KrakenDataSourceAdapter(
        IHttpClientFactory httpClientFactory,
        ILogger<KrakenDataSourceAdapter> logger,
        IOptions<KrakenOptions> options,
        IOptions<PriceFeedOptions> priceFeedOptions)
    {
        _httpClient = httpClientFactory.CreateClient("Kraken");
        _logger = logger;
        _options = options.Value;
        _symbolMappings = priceFeedOptions.Value.SymbolMappings;

        _httpClient.BaseAddress = new Uri(_options.BaseUrl);
        _httpClient.Timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds);

        // Add API key header if available (for private endpoints)
        if (!string.IsNullOrEmpty(_options.ApiKey))
        {
            _httpClient.DefaultRequestHeaders.Add("API-Key", _options.ApiKey);
        }
    }

    /// <summary>
    /// Gets the list of symbols supported by Kraken
    /// </summary>
    /// <returns>A list of supported symbols</returns>
    public Task<IEnumerable<string>> GetSupportedSymbolsAsync()
    {
        // Return all symbols that are supported by Kraken
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
            // Check if this symbol is supported by Kraken
            if (!_symbolMappings.IsSymbolSupportedBySource(symbol, SourceName))
            {
                _logger.LogWarning("Symbol {Symbol} is not supported by {Source}", symbol, SourceName);
                throw new NotSupportedException($"Symbol {symbol} is not supported by {SourceName}");
            }

            // Get the Kraken-specific symbol format
            var krakenSymbol = _symbolMappings.GetSourceSymbol(symbol, SourceName);

            // Build the request URL
            var endpoint = $"{_options.TickerEndpoint}?pair={krakenSymbol}";
            var response = await _httpClient.GetAsync(endpoint);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            var tickerResponse = JsonConvert.DeserializeObject<KrakenTickerResponse>(content);

            if (tickerResponse?.Result == null || !tickerResponse.Result.Any())
            {
                _logger.LogWarning("Failed to get price data for symbol {Symbol} from Kraken: Response is invalid", symbol);
                throw new Exception($"Failed to get price data for symbol {symbol} from Kraken");
            }

            // Kraken returns data with the pair name as key, which might be different from what we requested
            var tickerData = tickerResponse.Result.Values.First();

            return new PriceData
            {
                Symbol = symbol,
                Price = decimal.Parse(tickerData.LastTrade[0]),
                Source = SourceName,
                Timestamp = DateTime.UtcNow,
                Volume = decimal.Parse(tickerData.Volume[1]), // 24h volume
                Metadata = new Dictionary<string, string>
                {
                    { "Ask", tickerData.Ask[0] },
                    { "Bid", tickerData.Bid[0] },
                    { "High24h", tickerData.High[1] },
                    { "Low24h", tickerData.Low[1] },
                    { "Open", tickerData.Open },
                    { "SourceSymbol", krakenSymbol }
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting price data for symbol {Symbol} from Kraken", symbol);
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
            // Filter out symbols that are not supported by Kraken
            var supportedSymbols = symbols
                .Where(s => _symbolMappings.IsSymbolSupportedBySource(s, SourceName))
                .ToList();

            if (!supportedSymbols.Any())
            {
                _logger.LogWarning("No supported symbols found for {Source}", SourceName);
                return Enumerable.Empty<PriceData>();
            }

            // Get Kraken symbols for all supported symbols
            var krakenSymbols = supportedSymbols
                .Select(s => _symbolMappings.GetSourceSymbol(s, SourceName))
                .ToList();

            // Kraken supports batch requests with comma-separated pairs
            var pairsParam = string.Join(",", krakenSymbols);
            var endpoint = $"{_options.TickerEndpoint}?pair={pairsParam}";

            var response = await _httpClient.GetAsync(endpoint);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            var tickerResponse = JsonConvert.DeserializeObject<KrakenTickerResponse>(content);

            if (tickerResponse?.Result == null)
            {
                _logger.LogWarning("Failed to get batch price data from Kraken: Response is null");
                return Enumerable.Empty<PriceData>();
            }

            var results = new List<PriceData>();

            foreach (var symbol in supportedSymbols)
            {
                try
                {
                    var krakenSymbol = _symbolMappings.GetSourceSymbol(symbol, SourceName);

                    // Find the ticker data - Kraken might return different key format
                    var tickerData = tickerResponse.Result.Values.FirstOrDefault();
                    if (tickerData != null)
                    {
                        results.Add(new PriceData
                        {
                            Symbol = symbol,
                            Price = decimal.Parse(tickerData.LastTrade[0]),
                            Source = SourceName,
                            Timestamp = DateTime.UtcNow,
                            Volume = decimal.Parse(tickerData.Volume[1]),
                            Metadata = new Dictionary<string, string>
                            {
                                { "Ask", tickerData.Ask[0] },
                                { "Bid", tickerData.Bid[0] },
                                { "High24h", tickerData.High[1] },
                                { "Low24h", tickerData.Low[1] },
                                { "Open", tickerData.Open },
                                { "SourceSymbol", krakenSymbol }
                            }
                        });
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing symbol {Symbol} in batch request", symbol);
                    // Continue with other symbols
                }
            }

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetPriceDataBatchAsync for Kraken");

            // Fallback to individual requests
            var results = new List<PriceData>();
            var supportedSymbols = symbols
                .Where(s => _symbolMappings.IsSymbolSupportedBySource(s, SourceName))
                .ToList();

            foreach (var symbol in supportedSymbols)
            {
                try
                {
                    var result = await GetPriceDataAsync(symbol);
                    results.Add(result);
                }
                catch (Exception individualEx)
                {
                    _logger.LogError(individualEx, "Error getting price data for symbol {Symbol} from Kraken", symbol);
                    // Continue with other symbols
                }
            }

            return results;
        }
    }
}

/// <summary>
/// Response from Kraken ticker API
/// </summary>
internal class KrakenTickerResponse
{
    [JsonProperty("error")]
    public List<string> Error { get; set; } = new();

    [JsonProperty("result")]
    public Dictionary<string, KrakenTickerData> Result { get; set; } = new();
}

/// <summary>
/// Ticker data from Kraken
/// </summary>
internal class KrakenTickerData
{
    [JsonProperty("a")]
    public string[] Ask { get; set; } = new string[0]; // [price, whole_lot_volume, lot_volume]

    [JsonProperty("b")]
    public string[] Bid { get; set; } = new string[0]; // [price, whole_lot_volume, lot_volume]

    [JsonProperty("c")]
    public string[] LastTrade { get; set; } = new string[0]; // [price, lot_volume]

    [JsonProperty("v")]
    public string[] Volume { get; set; } = new string[0]; // [today, last_24_hours]

    [JsonProperty("p")]
    public string[] VolumeWeightedAverage { get; set; } = new string[0]; // [today, last_24_hours]

    [JsonProperty("t")]
    public int[] NumberOfTrades { get; set; } = new int[0]; // [today, last_24_hours]

    [JsonProperty("l")]
    public string[] Low { get; set; } = new string[0]; // [today, last_24_hours]

    [JsonProperty("h")]
    public string[] High { get; set; } = new string[0]; // [today, last_24_hours]

    [JsonProperty("o")]
    public string Open { get; set; } = string.Empty; // today's opening price
}
