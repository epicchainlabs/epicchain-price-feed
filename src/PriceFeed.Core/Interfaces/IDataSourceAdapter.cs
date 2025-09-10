using PriceFeed.Core.Models;

namespace PriceFeed.Core.Interfaces;

/// <summary>
/// Interface for data source adapters that fetch price data from external sources
/// </summary>
public interface IDataSourceAdapter
{
    /// <summary>
    /// Gets the name of the data source
    /// </summary>
    string SourceName { get; }

    /// <summary>
    /// Checks if the data source is enabled (has API key configured)
    /// </summary>
    /// <returns>True if the data source is enabled, false otherwise</returns>
    bool IsEnabled();

    /// <summary>
    /// Gets the list of symbols supported by this data source
    /// </summary>
    /// <returns>A list of supported symbols</returns>
    Task<IEnumerable<string>> GetSupportedSymbolsAsync();

    /// <summary>
    /// Fetches the current price data for a specific symbol
    /// </summary>
    /// <param name="symbol">The symbol to fetch price data for</param>
    /// <returns>The price data for the specified symbol</returns>
    Task<PriceData> GetPriceDataAsync(string symbol);

    /// <summary>
    /// Fetches the current price data for multiple symbols
    /// </summary>
    /// <param name="symbols">The symbols to fetch price data for</param>
    /// <returns>A collection of price data for the specified symbols</returns>
    Task<IEnumerable<PriceData>> GetPriceDataBatchAsync(IEnumerable<string> symbols);
}
