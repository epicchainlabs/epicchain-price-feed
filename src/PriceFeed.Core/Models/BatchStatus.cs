namespace PriceFeed.Core.Models;

/// <summary>
/// Represents the status of a price data batch
/// </summary>
public enum BatchStatus
{
    /// <summary>
    /// The batch is pending processing
    /// </summary>
    Pending,

    /// <summary>
    /// The batch is being processed
    /// </summary>
    Processing,

    /// <summary>
    /// The batch was successfully sent to the smart contract
    /// </summary>
    Sent,

    /// <summary>
    /// The batch was confirmed on the blockchain
    /// </summary>
    Confirmed,

    /// <summary>
    /// The batch failed to be processed or sent
    /// </summary>
    Failed,

    /// <summary>
    /// The batch was rejected by the smart contract
    /// </summary>
    Rejected,

    /// <summary>
    /// The batch status is unknown
    /// </summary>
    Unknown
}
