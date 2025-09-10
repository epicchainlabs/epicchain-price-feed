namespace PriceFeed.Core.Models;

/// <summary>
/// Represents a price data point from a specific source
/// </summary>
public class PriceData
{
    /// <summary>
    /// The symbol/ticker of the asset (e.g., "NEO/USDT")
    /// </summary>
    public string Symbol { get; set; } = string.Empty;

    /// <summary>
    /// The price value
    /// </summary>
    public decimal Price { get; set; }

    /// <summary>
    /// The source of the price data (e.g., "Binance", "Huobi", etc.)
    /// </summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp when the price was recorded
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Optional volume information
    /// </summary>
    public decimal? Volume { get; set; }

    /// <summary>
    /// Any additional metadata about the price
    /// </summary>
    public Dictionary<string, string> Metadata { get; set; } = new Dictionary<string, string>();
}
