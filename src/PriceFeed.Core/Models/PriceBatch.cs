namespace PriceFeed.Core.Models;

/// <summary>
/// Represents a batch of price data to be sent to the smart contract
/// </summary>
public class PriceBatch
{
    /// <summary>
    /// Unique identifier for the batch
    /// </summary>
    public Guid BatchId { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Timestamp when the batch was created
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// List of aggregated price data in this batch
    /// </summary>
    public List<AggregatedPriceData> Prices { get; set; } = new List<AggregatedPriceData>();

    /// <summary>
    /// For backward compatibility with tests
    /// </summary>
    public List<AggregatedPriceData> PricesData
    {
        get => Prices;
        set => Prices = value;
    }
}
