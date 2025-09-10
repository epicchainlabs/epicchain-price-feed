using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using PriceFeed.Core.Interfaces;
using PriceFeed.Core.Models;

namespace PriceFeed.Infrastructure.Services;

/// <summary>
/// Provides caching functionality for price data to reduce API calls
/// </summary>
public class CachedPriceService : IDataSourceAdapter
{
    private readonly IDataSourceAdapter _innerAdapter;
    private readonly IMemoryCache _cache;
    private readonly ILogger<CachedPriceService> _logger;
    private readonly TimeSpan _cacheExpiration;

    public string SourceName => _innerAdapter.SourceName;

    public CachedPriceService(
        IDataSourceAdapter innerAdapter,
        IMemoryCache cache,
        ILogger<CachedPriceService> logger,
        TimeSpan? cacheExpiration = null)
    {
        _innerAdapter = innerAdapter;
        _cache = cache;
        _logger = logger;
        _cacheExpiration = cacheExpiration ?? TimeSpan.FromSeconds(30);
    }

    public bool IsEnabled() => _innerAdapter.IsEnabled();

    public async Task<IEnumerable<string>> GetSupportedSymbolsAsync()
    {
        var cacheKey = $"{SourceName}:supported_symbols";

        if (_cache.TryGetValue<IEnumerable<string>>(cacheKey, out var cachedSymbols))
        {
            _logger.LogDebug("Cache hit for supported symbols from {Source}", SourceName);
            return cachedSymbols!;
        }

        var symbols = await _innerAdapter.GetSupportedSymbolsAsync();

        // Cache supported symbols for longer as they don't change frequently
        _cache.Set(cacheKey, symbols, TimeSpan.FromMinutes(5));

        return symbols;
    }

    public async Task<PriceData> GetPriceDataAsync(string symbol)
    {
        var cacheKey = $"{SourceName}:price:{symbol}";

        if (_cache.TryGetValue<PriceData>(cacheKey, out var cachedPrice))
        {
            _logger.LogDebug("Cache hit for {Symbol} from {Source}", symbol, SourceName);
            return cachedPrice!;
        }

        _logger.LogDebug("Cache miss for {Symbol} from {Source}", symbol, SourceName);

        try
        {
            var priceData = await _innerAdapter.GetPriceDataAsync(symbol);

            // Only cache successful responses
            if (priceData != null && priceData.Price > 0)
            {
                _cache.Set(cacheKey, priceData, _cacheExpiration);
            }

            return priceData!;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching price data for {Symbol} from {Source}", symbol, SourceName);

            // Try to return stale data if available
            if (_cache.TryGetValue<PriceData>(cacheKey, out var stalePrice))
            {
                _logger.LogWarning("Returning stale cached data for {Symbol} from {Source}", symbol, SourceName);
                return stalePrice!;
            }

            throw;
        }
    }

    public async Task<IEnumerable<PriceData>> GetPriceDataBatchAsync(IEnumerable<string> symbols)
    {
        var symbolList = symbols.ToList();
        var results = new List<PriceData>();
        var symbolsToFetch = new List<string>();

        // Check cache for each symbol
        foreach (var symbol in symbolList)
        {
            var cacheKey = $"{SourceName}:price:{symbol}";

            if (_cache.TryGetValue<PriceData>(cacheKey, out var cachedPrice))
            {
                _logger.LogDebug("Cache hit for {Symbol} from {Source}", symbol, SourceName);
                results.Add(cachedPrice!);
            }
            else
            {
                symbolsToFetch.Add(symbol);
            }
        }

        // Fetch missing symbols
        if (symbolsToFetch.Any())
        {
            _logger.LogDebug("Fetching {Count} symbols from {Source}", symbolsToFetch.Count, SourceName);

            try
            {
                var fetchedPrices = await _innerAdapter.GetPriceDataBatchAsync(symbolsToFetch);

                foreach (var priceData in fetchedPrices)
                {
                    // Cache the fetched data
                    if (priceData != null && priceData.Price > 0)
                    {
                        var cacheKey = $"{SourceName}:price:{priceData.Symbol}";
                        _cache.Set(cacheKey, priceData, _cacheExpiration);
                    }

                    if (priceData != null)
                    {
                        results.Add(priceData);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching batch price data from {Source}", SourceName);

                // Try to return stale data for missing symbols
                foreach (var symbol in symbolsToFetch)
                {
                    var cacheKey = $"{SourceName}:price:{symbol}";
                    if (_cache.TryGetValue<PriceData>(cacheKey, out var stalePrice))
                    {
                        _logger.LogWarning("Returning stale cached data for {Symbol} from {Source}", symbol, SourceName);
                        results.Add(stalePrice!);
                    }
                }

                if (!results.Any())
                {
                    throw;
                }
            }
        }

        return results;
    }
}
