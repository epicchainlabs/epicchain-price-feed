using PriceFeed.Core.Models;

namespace PriceFeed.Core.Interfaces;

/// <summary>
/// Interface for services that process and send batches of price data to the smart contract
/// </summary>
public interface IBatchProcessingService
{
    /// <summary>
    /// Processes a batch of aggregated price data and sends it to the smart contract
    /// </summary>
    /// <param name="batch">The batch of price data to process</param>
    /// <returns>True if the batch was successfully processed and sent, false otherwise</returns>
    Task<bool> ProcessBatchAsync(PriceBatch batch);

    /// <summary>
    /// Gets the status of a previously submitted batch
    /// </summary>
    /// <param name="batchId">The ID of the batch to check</param>
    /// <returns>The status information of the batch</returns>
    Task<BatchStatusInfo> GetBatchStatusAsync(Guid batchId);
}
