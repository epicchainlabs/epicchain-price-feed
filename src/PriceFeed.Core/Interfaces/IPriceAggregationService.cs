using PriceFeed.Core.Models;

namespace PriceFeed.Core.Interfaces;

/// <summary>
/// Interface for services that aggregate price data from multiple sources
/// </summary>
public interface IPriceAggregationService
{
    /// <summary>
    /// Aggregates price data from multiple sources for a specific symbol
    /// </summary>
    /// <param name="priceData">Collection of price data from different sources for the same symbol</param>
    /// <returns>The aggregated price data</returns>
    Task<AggregatedPriceData> AggregateAsync(IEnumerable<PriceData> priceData);

    /// <summary>
    /// Aggregates price data from multiple sources for multiple symbols
    /// </summary>
    /// <param name="priceDataBySymbol">Dictionary mapping symbols to collections of price data from different sources</param>
    /// <returns>Collection of aggregated price data for each symbol</returns>
    Task<IEnumerable<AggregatedPriceData>> AggregateBatchAsync(IDictionary<string, IEnumerable<PriceData>> priceDataBySymbol);
}
