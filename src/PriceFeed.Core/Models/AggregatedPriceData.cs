namespace PriceFeed.Core.Models;

/// <summary>
/// Represents aggregated price data from multiple sources
/// </summary>
public class AggregatedPriceData
{
    /// <summary>
    /// The symbol/ticker of the asset
    /// </summary>
    public string Symbol { get; set; } = string.Empty;

    /// <summary>
    /// The aggregated price value
    /// </summary>
    public decimal Price { get; set; }

    /// <summary>
    /// Timestamp when the aggregation was performed
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// List of individual price data points used in the aggregation
    /// </summary>
    public List<PriceData> SourceData { get; set; } = new List<PriceData>();

    /// <summary>
    /// Standard deviation of prices from different sources
    /// </summary>
    public decimal? StandardDeviation { get; set; }

    /// <summary>
    /// Confidence score of the aggregated price (0-100)
    /// </summary>
    public int ConfidenceScore { get; set; }
}

// For backward compatibility with tests
public class AggregatedPrice : AggregatedPriceData { }
