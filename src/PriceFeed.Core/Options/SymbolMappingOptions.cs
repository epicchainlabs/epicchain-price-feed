using System.Collections.Generic;

namespace PriceFeed.Core.Options;

/// <summary>
/// Configuration options for symbol mappings across different data sources
/// </summary>
public class SymbolMappingOptions
{
    /// <summary>
    /// Dictionary mapping standard symbols to their data source-specific formats
    /// Key: Standard symbol (e.g., "BTCUSDT")
    /// Value: Dictionary of data source name to source-specific symbol format
    /// </summary>
    public Dictionary<string, Dictionary<string, string>> Mappings { get; set; } = new();

    /// <summary>
    /// Gets the source-specific symbol for a given standard symbol and data source
    /// </summary>
    /// <param name="standardSymbol">The standard symbol (e.g., "BTCUSDT")</param>
    /// <param name="dataSource">The data source name (e.g., "Binance")</param>
    /// <returns>The source-specific symbol, or null if not found</returns>
    public string GetSourceSymbol(string standardSymbol, string dataSource)
    {
        if (Mappings.TryGetValue(standardSymbol, out var sourceMappings) &&
            sourceMappings.TryGetValue(dataSource, out var sourceSymbol))
        {
            return sourceSymbol;
        }

        // If no specific mapping is found, return the standard symbol as a fallback
        return standardSymbol;
    }

    /// <summary>
    /// Checks if a symbol is supported by a specific data source
    /// </summary>
    /// <param name="standardSymbol">The standard symbol (e.g., "BTCUSDT")</param>
    /// <param name="dataSource">The data source name (e.g., "Binance")</param>
    /// <returns>True if the symbol is supported by the data source</returns>
    public bool IsSymbolSupportedBySource(string standardSymbol, string dataSource)
    {
        // If the symbol is not in the mappings, assume it's supported
        if (!Mappings.TryGetValue(standardSymbol, out var sourceMappings))
        {
            return true;
        }

        // If the data source is not in the mappings, assume it's supported
        if (!sourceMappings.TryGetValue(dataSource, out var sourceSymbol))
        {
            return true;
        }

        // If the source symbol is empty, it's explicitly not supported
        return !string.IsNullOrEmpty(sourceSymbol);
    }

    /// <summary>
    /// Gets all standard symbols that are supported by at least one data source
    /// </summary>
    /// <returns>A list of standard symbols</returns>
    public List<string> GetAllStandardSymbols()
    {
        return new List<string>(Mappings.Keys);
    }

    /// <summary>
    /// Gets all standard symbols that are supported by a specific data source
    /// </summary>
    /// <param name="dataSource">The data source name</param>
    /// <returns>A list of standard symbols supported by the data source</returns>
    public List<string> GetSymbolsForDataSource(string dataSource)
    {
        var result = new List<string>();

        foreach (var mapping in Mappings)
        {
            if (IsSymbolSupportedBySource(mapping.Key, dataSource))
            {
                result.Add(mapping.Key);
            }
        }

        return result;
    }
}
