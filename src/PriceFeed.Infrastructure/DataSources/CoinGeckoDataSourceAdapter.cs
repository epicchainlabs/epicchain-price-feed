using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using PriceFeed.Core.Interfaces;
using PriceFeed.Core.Models;
using PriceFeed.Core.Options;

namespace PriceFeed.Infrastructure.DataSources;

/// <summary>
/// Data source adapter for fetching price data from CoinGecko
/// </summary>
public class CoinGeckoDataSourceAdapter : IDataSourceAdapter
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<CoinGeckoDataSourceAdapter> _logger;
    private readonly CoinGeckoOptions _options;
    private readonly SymbolMappingOptions _symbolMappings;

    /// <summary>
    /// Gets the name of the data source
    /// </summary>
    public string SourceName => "CoinGecko";

    /// <summary>
    /// Checks if the data source is enabled
    /// </summary>
    /// <returns>True if the data source is enabled, false otherwise</returns>
    public bool IsEnabled()
    {
        // CoinGecko works with public API without requiring API key
        // Always enabled unless explicitly disabled
        return true;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CoinGeckoDataSourceAdapter"/> class
    /// </summary>
    /// <param name="httpClientFactory">The HTTP client factory</param>
    /// <param name="logger">The logger</param>
    /// <param name="options">The CoinGecko options</param>
    /// <param name="priceFeedOptions">The price feed options</param>
    public CoinGeckoDataSourceAdapter(
        IHttpClientFactory httpClientFactory,
        ILogger<CoinGeckoDataSourceAdapter> logger,
        IOptions<CoinGeckoOptions> options,
        IOptions<PriceFeedOptions> priceFeedOptions)
    {
        _httpClient = httpClientFactory.CreateClient("CoinGecko");
        _logger = logger;
        _options = options.Value;
        _symbolMappings = priceFeedOptions.Value.SymbolMappings;

        _httpClient.BaseAddress = new Uri(_options.BaseUrl);
        _httpClient.Timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds);

        // Add API key header if available (for Pro tier)
        if (!string.IsNullOrEmpty(_options.ApiKey))
        {
            _httpClient.DefaultRequestHeaders.Add("x-cg-pro-api-key", _options.ApiKey);
        }
    }

    /// <summary>
    /// Gets the list of symbols supported by CoinGecko
    /// </summary>
    /// <returns>A list of supported symbols</returns>
    public Task<IEnumerable<string>> GetSupportedSymbolsAsync()
    {
        // Return all symbols that are supported by CoinGecko
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
            // Check if this symbol is supported by CoinGecko
            if (!_symbolMappings.IsSymbolSupportedBySource(symbol, SourceName))
            {
                _logger.LogWarning("Symbol {Symbol} is not supported by {Source}", symbol, SourceName);
                throw new NotSupportedException($"Symbol {symbol} is not supported by {SourceName}");
            }

            // Get the CoinGecko-specific symbol format (coin ID)
            var coinGeckoId = _symbolMappings.GetSourceSymbol(symbol, SourceName);

            // Determine the target currency based on the symbol
            string targetCurrency = "usd";
            if (symbol.EndsWith("BTC"))
            {
                targetCurrency = "btc";
            }

            // Build the request URL
            var endpoint = $"{_options.SimplePriceEndpoint}?ids={coinGeckoId}&vs_currencies={targetCurrency}&include_24hr_vol=true&include_24hr_change=true";
            var response = await _httpClient.GetAsync(endpoint);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            var priceResponse = JsonConvert.DeserializeObject<Dictionary<string, CoinGeckoPriceData>>(content);

            if (priceResponse == null || !priceResponse.ContainsKey(coinGeckoId))
            {
                _logger.LogWarning("Failed to get price data for symbol {Symbol} from CoinGecko: Response is invalid", symbol);
                throw new Exception($"Failed to get price data for symbol {symbol} from CoinGecko");
            }

            var coinData = priceResponse[coinGeckoId];
            decimal price = 0;
            decimal? volume = null;
            string? priceChange = null;

            if (targetCurrency == "usd")
            {
                price = coinData.Usd ?? 0;
                volume = coinData.UsdVolume24h;
                priceChange = coinData.UsdChange24h?.ToString();
            }
            else if (targetCurrency == "btc")
            {
                price = coinData.Btc ?? 0;
                volume = coinData.BtcVolume24h;
                priceChange = coinData.BtcChange24h?.ToString();
            }

            return new PriceData
            {
                Symbol = symbol,
                Price = price,
                Source = SourceName,
                Timestamp = DateTime.UtcNow,
                Volume = volume,
                Metadata = new Dictionary<string, string>
                {
                    { "PriceChange24h", priceChange ?? "0" },
                    { "TargetCurrency", targetCurrency },
                    { "SourceSymbol", coinGeckoId } // Store the source-specific symbol in metadata
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting price data for symbol {Symbol} from CoinGecko", symbol);
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
            // Filter out symbols that are not supported by CoinGecko
            var supportedSymbols = symbols
                .Where(s => _symbolMappings.IsSymbolSupportedBySource(s, SourceName))
                .ToList();

            if (!supportedSymbols.Any())
            {
                _logger.LogWarning("No supported symbols found for {Source}", SourceName);
                return Enumerable.Empty<PriceData>();
            }

            // Group symbols by target currency for batch requests
            var usdSymbols = supportedSymbols.Where(s => s.EndsWith("USDT") || s.EndsWith("USD")).ToList();
            var btcSymbols = supportedSymbols.Where(s => s.EndsWith("BTC")).ToList();

            var results = new List<PriceData>();

            // Process USD symbols in batch
            if (usdSymbols.Any())
            {
                var usdResults = await GetPriceDataBatchForCurrency(usdSymbols, "usd");
                results.AddRange(usdResults);
            }

            // Process BTC symbols in batch
            if (btcSymbols.Any())
            {
                var btcResults = await GetPriceDataBatchForCurrency(btcSymbols, "btc");
                results.AddRange(btcResults);
            }

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetPriceDataBatchAsync for CoinGecko");
            throw;
        }
    }

    /// <summary>
    /// Fetches price data for multiple symbols with the same target currency
    /// </summary>
    /// <param name="symbols">The symbols to fetch</param>
    /// <param name="targetCurrency">The target currency (usd or btc)</param>
    /// <returns>A collection of price data</returns>
    private async Task<IEnumerable<PriceData>> GetPriceDataBatchForCurrency(IEnumerable<string> symbols, string targetCurrency)
    {
        try
        {
            // Get CoinGecko IDs for all symbols
            var coinIds = symbols
                .Select(s => _symbolMappings.GetSourceSymbol(s, SourceName))
                .ToList();

            var idsParam = string.Join(",", coinIds);
            var endpoint = $"{_options.SimplePriceEndpoint}?ids={idsParam}&vs_currencies={targetCurrency}&include_24hr_vol=true&include_24hr_change=true";

            var response = await _httpClient.GetAsync(endpoint);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            var priceResponse = JsonConvert.DeserializeObject<Dictionary<string, CoinGeckoPriceData>>(content);

            if (priceResponse == null)
            {
                _logger.LogWarning("Failed to get batch price data from CoinGecko: Response is null");
                return Enumerable.Empty<PriceData>();
            }

            var results = new List<PriceData>();

            foreach (var symbol in symbols)
            {
                try
                {
                    var coinId = _symbolMappings.GetSourceSymbol(symbol, SourceName);
                    if (priceResponse.ContainsKey(coinId))
                    {
                        var coinData = priceResponse[coinId];
                        decimal price = 0;
                        decimal? volume = null;
                        string? priceChange = null;

                        if (targetCurrency == "usd")
                        {
                            price = coinData.Usd ?? 0;
                            volume = coinData.UsdVolume24h;
                            priceChange = coinData.UsdChange24h?.ToString();
                        }
                        else if (targetCurrency == "btc")
                        {
                            price = coinData.Btc ?? 0;
                            volume = coinData.BtcVolume24h;
                            priceChange = coinData.BtcChange24h?.ToString();
                        }

                        results.Add(new PriceData
                        {
                            Symbol = symbol,
                            Price = price,
                            Source = SourceName,
                            Timestamp = DateTime.UtcNow,
                            Volume = volume,
                            Metadata = new Dictionary<string, string>
                            {
                                { "PriceChange24h", priceChange ?? "0" },
                                { "TargetCurrency", targetCurrency },
                                { "SourceSymbol", coinId }
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
            _logger.LogError(ex, "Error in GetPriceDataBatchForCurrency for currency {Currency}", targetCurrency);
            return Enumerable.Empty<PriceData>();
        }
    }
}

/// <summary>
/// Price data from CoinGecko API
/// </summary>
internal class CoinGeckoPriceData
{
    [JsonProperty("usd")]
    public decimal? Usd { get; set; }

    [JsonProperty("btc")]
    public decimal? Btc { get; set; }

    [JsonProperty("usd_24h_vol")]
    public decimal? UsdVolume24h { get; set; }

    [JsonProperty("btc_24h_vol")]
    public decimal? BtcVolume24h { get; set; }

    [JsonProperty("usd_24h_change")]
    public decimal? UsdChange24h { get; set; }

    [JsonProperty("btc_24h_change")]
    public decimal? BtcChange24h { get; set; }
}
