namespace PriceFeed.Core.Models;

/// <summary>
/// Represents detailed information about the status of a price batch submission
/// </summary>
public class BatchStatusInfo
{
    /// <summary>
    /// Gets or sets the batch ID
    /// </summary>
    public Guid BatchId { get; set; }

    /// <summary>
    /// Gets or sets the current status of the batch
    /// </summary>
    public BatchStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the transaction hash if the batch was submitted to blockchain
    /// </summary>
    public string? TransactionHash { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the status was last updated
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Gets or sets any error message if the batch failed
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Gets or sets the number of prices successfully processed
    /// </summary>
    public int ProcessedCount { get; set; }

    /// <summary>
    /// Gets or sets the total number of prices in the batch
    /// </summary>
    public int TotalCount { get; set; }

    /// <summary>
    /// Gets or sets the block number where the transaction was included (if confirmed)
    /// </summary>
    public uint? BlockNumber { get; set; }

    /// <summary>
    /// Gets or sets the gas cost for the transaction
    /// </summary>
    public decimal? GasCost { get; set; }
}
