using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using EpicChain;
using EpicChain.Network.RPC;
using Newtonsoft.Json;
using PriceFeed.R3E.EventIndexer.Data;
using PriceFeed.R3E.EventIndexer.Models;

namespace PriceFeed.R3E.EventIndexer.Services
{
    public class EventIndexerService : BackgroundService
    {
        private readonly ILogger<EventIndexerService> _logger;
        private readonly IConfiguration _configuration;
        private readonly IServiceProvider _serviceProvider;
        private readonly RpcClient _rpcClient;
        private readonly UInt160 _contractHash;
        private readonly int _pollIntervalSeconds;
        private readonly int _batchSize;

        public EventIndexerService(
            ILogger<EventIndexerService> logger,
            IConfiguration configuration,
            IServiceProvider serviceProvider)
        {
            _logger = logger;
            _configuration = configuration;
            _serviceProvider = serviceProvider;

            var rpcEndpoint = _configuration["EventIndexer:RpcEndpoint"] ?? "http://localhost:20332";
            _rpcClient = new RpcClient(new Uri(rpcEndpoint));

            var contractHashString = _configuration["EventIndexer:ContractHash"] ?? "";
            _contractHash = UInt160.Parse(contractHashString);

            _pollIntervalSeconds = _configuration.GetValue<int>("EventIndexer:PollIntervalSeconds", 30);
            _batchSize = _configuration.GetValue<int>("EventIndexer:BatchSize", 100);

            _logger.LogInformation("Event Indexer initialized");
            _logger.LogInformation("Contract: {ContractHash}", _contractHash);
            _logger.LogInformation("RPC: {RpcEndpoint}", rpcEndpoint);
            _logger.LogInformation("Poll Interval: {PollInterval}s", _pollIntervalSeconds);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Starting Event Indexer Service");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessNewBlocksAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing blocks");
                }

                await Task.Delay(TimeSpan.FromSeconds(_pollIntervalSeconds), stoppingToken);
            }
        }

        private async Task ProcessNewBlocksAsync()
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<EventIndexerContext>();

            // Get current blockchain height
            var currentBlock = await _rpcClient.GetBlockCountAsync() - 1;

            // Get last processed block
            var indexerState = await context.IndexerStates.FirstOrDefaultAsync();
            if (indexerState == null)
            {
                indexerState = new IndexerState
                {
                    LastProcessedBlock = Math.Max(0, currentBlock - 1000), // Start from 1000 blocks ago
                    LastUpdateTime = DateTime.UtcNow,
                    IsRunning = true
                };
                context.IndexerStates.Add(indexerState);
                await context.SaveChangesAsync();
            }

            var startBlock = indexerState.LastProcessedBlock + 1;
            var endBlock = Math.Min(startBlock + _batchSize - 1, currentBlock);

            if (startBlock > currentBlock)
            {
                _logger.LogDebug("No new blocks to process");
                return;
            }

            _logger.LogInformation("Processing blocks {StartBlock} to {EndBlock}", startBlock, endBlock);

            // Process blocks in batch
            for (long blockIndex = startBlock; blockIndex <= endBlock; blockIndex++)
            {
                await ProcessBlockAsync(context, blockIndex);
            }

            // Update indexer state
            indexerState.LastProcessedBlock = endBlock;
            indexerState.LastUpdateTime = DateTime.UtcNow;
            await context.SaveChangesAsync();

            _logger.LogInformation("Processed {Count} blocks, current height: {Height}", 
                endBlock - startBlock + 1, currentBlock);
        }

        private async Task ProcessBlockAsync(EventIndexerContext context, long blockIndex)
        {
            try
            {
                var block = await _rpcClient.GetBlockAsync(blockIndex.ToString());
                if (block?.Transactions == null) return;

                var blockTimestamp = DateTimeOffset.FromUnixTimeMilliseconds(block.Time).DateTime;

                foreach (var tx in block.Transactions)
                {
                    try
                    {
                        var appLog = await _rpcClient.GetApplicationLogAsync(tx.Hash.ToString());
                        if (appLog?.Executions == null) continue;

                        foreach (var execution in appLog.Executions)
                        {
                            if (execution.Notifications == null) continue;

                            foreach (var notification in execution.Notifications)
                            {
                                // Check if notification is from our contract
                                if (notification.Contract != _contractHash.ToString()) continue;

                                await ProcessNotificationAsync(context, notification, tx.Hash.ToString(), blockIndex, blockTimestamp);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error processing transaction {TxHash}", tx.Hash);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing block {BlockIndex}", blockIndex);
            }
        }

        private async Task ProcessNotificationAsync(
            EventIndexerContext context, 
            EpicChain.Network.RPC.Models.RpcNotification notification,
            string transactionHash,
            long blockIndex,
            DateTime timestamp)
        {
            var eventName = notification.EventName;
            var eventData = JsonConvert.SerializeObject(notification.State);

            // Check if we already processed this event
            var existingEvent = await context.ContractEvents
                .FirstOrDefaultAsync(e => e.TransactionHash == transactionHash && 
                                         e.EventName == eventName && 
                                         e.Data == eventData);

            if (existingEvent != null) return;

            var contractEvent = new ContractEvent
            {
                TransactionHash = transactionHash,
                ContractHash = notification.Contract,
                EventName = eventName,
                BlockIndex = blockIndex,
                Timestamp = timestamp,
                Data = eventData,
                Processed = false
            };

            context.ContractEvents.Add(contractEvent);

            _logger.LogDebug("Indexed event {EventName} in transaction {TxHash}", eventName, transactionHash);

            // Process specific event types
            await ProcessSpecificEventAsync(notification, transactionHash, blockIndex, timestamp);
        }

        private async Task ProcessSpecificEventAsync(
            EpicChain.Network.RPC.Models.RpcNotification notification,
            string transactionHash,
            long blockIndex,
            DateTime timestamp)
        {
            try
            {
                switch (notification.EventName)
                {
                    case "PriceUpdated":
                        await ProcessPriceUpdateEventAsync(notification, transactionHash, blockIndex);
                        break;

                    case "OracleAdded":
                    case "OracleRemoved":
                        await ProcessOracleEventAsync(notification, transactionHash, blockIndex, timestamp);
                        break;

                    case "TeeAccountAdded":
                    case "TeeAccountRemoved":
                        await ProcessTeeAccountEventAsync(notification, transactionHash, blockIndex, timestamp);
                        break;

                    case "ContractUpgraded":
                    case "ContractPaused":
                    case "OwnerChanged":
                        await ProcessContractManagementEventAsync(notification, transactionHash, blockIndex, timestamp);
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error processing specific event {EventName}", notification.EventName);
            }
        }

        private async Task ProcessPriceUpdateEventAsync(
            EpicChain.Network.RPC.Models.RpcNotification notification,
            string transactionHash,
            long blockIndex)
        {
            try
            {
                var state = notification.State;
                if (state?.Count >= 4)
                {
                    var priceEvent = new PriceUpdateEvent
                    {
                        Symbol = state[0]?.GetString() ?? "",
                        Price = decimal.Parse(state[1]?.GetString() ?? "0") / 100_000_000m, // Convert from 8 decimals
                        Timestamp = long.Parse(state[2]?.GetString() ?? "0"),
                        Confidence = int.Parse(state[3]?.GetString() ?? "0"),
                        TransactionHash = transactionHash,
                        BlockIndex = blockIndex
                    };

                    _logger.LogInformation("Price updated: {Symbol} = ${Price} (Confidence: {Confidence}%)",
                        priceEvent.Symbol, priceEvent.Price, priceEvent.Confidence);

                    // Here you could send to external systems, webhooks, etc.
                    await NotifyExternalSystemsAsync("PriceUpdated", priceEvent);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error processing PriceUpdated event");
            }
        }

        private async Task ProcessOracleEventAsync(
            EpicChain.Network.RPC.Models.RpcNotification notification,
            string transactionHash,
            long blockIndex,
            DateTime timestamp)
        {
            try
            {
                var state = notification.State;
                if (state?.Count >= 1)
                {
                    var oracleEvent = new OracleEvent
                    {
                        OracleAddress = state[0]?.GetString() ?? "",
                        Action = notification.EventName.Replace("Oracle", ""),
                        TransactionHash = transactionHash,
                        BlockIndex = blockIndex,
                        Timestamp = timestamp
                    };

                    _logger.LogInformation("Oracle {Action}: {Address}", oracleEvent.Action, oracleEvent.OracleAddress);
                    await NotifyExternalSystemsAsync("OracleUpdate", oracleEvent);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error processing Oracle event");
            }
        }

        private async Task ProcessTeeAccountEventAsync(
            EpicChain.Network.RPC.Models.RpcNotification notification,
            string transactionHash,
            long blockIndex,
            DateTime timestamp)
        {
            try
            {
                var state = notification.State;
                if (state?.Count >= 1)
                {
                    var teeEvent = new TeeAccountEvent
                    {
                        TeeAccountAddress = state[0]?.GetString() ?? "",
                        Action = notification.EventName.Replace("TeeAccount", ""),
                        TransactionHash = transactionHash,
                        BlockIndex = blockIndex,
                        Timestamp = timestamp
                    };

                    _logger.LogInformation("TEE Account {Action}: {Address}", teeEvent.Action, teeEvent.TeeAccountAddress);
                    await NotifyExternalSystemsAsync("TeeAccountUpdate", teeEvent);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error processing TEE Account event");
            }
        }

        private async Task ProcessContractManagementEventAsync(
            EpicChain.Network.RPC.Models.RpcNotification notification,
            string transactionHash,
            long blockIndex,
            DateTime timestamp)
        {
            try
            {
                var managementEvent = new ContractManagementEvent
                {
                    EventType = notification.EventName,
                    Details = JsonConvert.SerializeObject(notification.State),
                    TransactionHash = transactionHash,
                    BlockIndex = blockIndex,
                    Timestamp = timestamp
                };

                _logger.LogWarning("Contract Management Event: {EventType}", managementEvent.EventType);
                await NotifyExternalSystemsAsync("ContractManagement", managementEvent);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error processing Contract Management event");
            }
        }

        private async Task NotifyExternalSystemsAsync(string eventType, object eventData)
        {
            // Implementation for external notifications (webhooks, message queues, etc.)
            // This is where you'd integrate with monitoring systems, alerting, etc.
            
            var webhookUrl = _configuration["EventIndexer:WebhookUrl"];
            if (!string.IsNullOrEmpty(webhookUrl))
            {
                try
                {
                    // Send webhook notification
                    var payload = new
                    {
                        EventType = eventType,
                        Data = eventData,
                        Timestamp = DateTime.UtcNow
                    };

                    // Implementation would use HttpClient to send webhook
                    _logger.LogDebug("Would send webhook for {EventType}", eventType);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to send webhook for {EventType}", eventType);
                }
            }

            await Task.CompletedTask;
        }
    }
}