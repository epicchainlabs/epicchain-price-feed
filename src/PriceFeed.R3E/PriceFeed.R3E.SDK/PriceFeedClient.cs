using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using EpicChain;
using EpicChain.Network.RPC;
using EpicChain.Network.RPC.Models;
using EpicChain.SmartContract;
using PriceFeed.R3E.SDK.Models;

namespace PriceFeed.R3E.SDK
{
    /// <summary>
    /// Client for interacting with the R3E PriceFeed Oracle contract
    /// </summary>
    public class PriceFeedClient : IDisposable
    {
        private readonly RpcClient _rpcClient;
        private readonly UInt160 _contractHash;
        private readonly PriceFeedConfig _config;
        private readonly ILogger<PriceFeedClient>? _logger;
        private readonly ConcurrentDictionary<string, (PriceData data, DateTime cacheTime)> _priceCache;
        private readonly SemaphoreSlim _semaphore;
        private readonly Timer? _cacheCleanupTimer;

        /// <summary>
        /// Creates a new instance of the PriceFeedClient
        /// </summary>
        /// <param name="config">Configuration for the client</param>
        /// <param name="logger">Optional logger instance</param>
        public PriceFeedClient(PriceFeedConfig config, ILogger<PriceFeedClient>? logger = null)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _logger = logger;
            _priceCache = new ConcurrentDictionary<string, (PriceData, DateTime)>();
            _semaphore = new SemaphoreSlim(1, 1);

            if (string.IsNullOrEmpty(config.ContractHash))
                throw new ArgumentException("Contract hash is required", nameof(config));

            if (string.IsNullOrEmpty(config.RpcEndpoint))
                throw new ArgumentException("RPC endpoint is required", nameof(config));

            _contractHash = UInt160.Parse(config.ContractHash);
            _rpcClient = new RpcClient(new Uri(config.RpcEndpoint))
            {
                Timeout = TimeSpan.FromSeconds(config.TimeoutSeconds)
            };

            // Setup cache cleanup timer (runs every 5 minutes)
            _cacheCleanupTimer = new Timer(CleanupCache, null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));

            _logger?.LogInformation("PriceFeedClient initialized for contract {ContractHash}", config.ContractHash);
        }

        /// <summary>
        /// Gets current price data for a symbol
        /// </summary>
        /// <param name="symbol">Trading pair symbol (e.g., "BTCUSDT")</param>
        /// <param name="useCache">Whether to use cached data</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Current price data</returns>
        public async Task<PriceData> GetPriceAsync(string symbol, bool useCache = true, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(symbol))
                throw new ArgumentException("Symbol cannot be null or empty", nameof(symbol));

            symbol = symbol.ToUpperInvariant();

            // Check cache first
            if (useCache && _priceCache.TryGetValue(symbol, out var cached))
            {
                var cacheAge = DateTime.UtcNow - cached.cacheTime;
                if (cacheAge.TotalSeconds < _config.CacheDurationSeconds)
                {
                    _logger?.LogDebug("Returning cached price for {Symbol} (age: {CacheAge}s)", symbol, cacheAge.TotalSeconds);
                    return cached.data;
                }
            }

            await _semaphore.WaitAsync(cancellationToken);
            try
            {
                _logger?.LogDebug("Fetching price data for {Symbol} from contract", symbol);

                // Call contract method
                var script = new ScriptBuilder()
                    .EmitDynamicCall(_contractHash, "getPriceData", symbol)
                    .ToArray();

                var result = await _rpcClient.InvokeScriptAsync(script, cancellationToken);

                if (result.State != RpcInvokeResult.VMStateEnum.HALT)
                {
                    throw new PriceDataException(symbol, $"Contract call failed with state: {result.State}");
                }

                if (result.Stack == null || result.Stack.Length == 0)
                {
                    throw new PriceDataException(symbol, "No data returned from contract");
                }

                // Parse result
                var stackItem = result.Stack[0];
                if (stackItem.Type != RpcStackItemType.Struct)
                {
                    throw new PriceDataException(symbol, "Invalid data format returned from contract");
                }

                var structData = stackItem.ToStackItemArray();
                if (structData.Length < 4)
                {
                    throw new PriceDataException(symbol, "Incomplete price data structure");
                }

                var priceData = new PriceData
                {
                    Symbol = structData[0].GetString(),
                    RawPrice = structData[1].GetInteger(),
                    Price = (decimal)structData[1].GetInteger() / 100_000_000m, // Convert from 8 decimals
                    Timestamp = (long)structData[2].GetInteger(),
                    Confidence = (int)structData[3].GetInteger()
                };

                // Validate data quality
                ValidatePriceData(priceData);

                // Cache the result
                _priceCache[symbol] = (priceData, DateTime.UtcNow);

                _logger?.LogDebug("Successfully retrieved price for {Symbol}: ${Price} (confidence: {Confidence}%)",
                    priceData.Symbol, priceData.Price, priceData.Confidence);

                return priceData;
            }
            catch (Exception ex) when (!(ex is PriceDataException))
            {
                _logger?.LogError(ex, "Error fetching price data for {Symbol}", symbol);
                throw new PriceDataException(symbol, $"Failed to fetch price data: {ex.Message}", ex);
            }
            finally
            {
                _semaphore.Release();
            }
        }

        /// <summary>
        /// Gets current prices for multiple symbols
        /// </summary>
        /// <param name="symbols">List of trading pair symbols</param>
        /// <param name="useCache">Whether to use cached data</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Dictionary of symbol to price data</returns>
        public async Task<Dictionary<string, PriceData>> GetPricesAsync(
            IEnumerable<string> symbols, 
            bool useCache = true, 
            CancellationToken cancellationToken = default)
        {
            var symbolList = symbols?.ToList() ?? throw new ArgumentNullException(nameof(symbols));
            var results = new Dictionary<string, PriceData>();

            // Process symbols in parallel with limited concurrency
            var semaphore = new SemaphoreSlim(5, 5); // Max 5 concurrent requests
            var tasks = symbolList.Select(async symbol =>
            {
                await semaphore.WaitAsync(cancellationToken);
                try
                {
                    var priceData = await GetPriceAsync(symbol, useCache, cancellationToken);
                    return new KeyValuePair<string, PriceData>(symbol, priceData);
                }
                catch (PriceDataException ex)
                {
                    _logger?.LogWarning("Failed to get price for {Symbol}: {Error}", symbol, ex.Message);
                    return new KeyValuePair<string, PriceData>(symbol, null!);
                }
                finally
                {
                    semaphore.Release();
                }
            });

            var taskResults = await Task.WhenAll(tasks);

            foreach (var result in taskResults)
            {
                if (result.Value != null)
                {
                    results[result.Key] = result.Value;
                }
            }

            return results;
        }

        /// <summary>
        /// Gets contract information
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Contract information</returns>
        public async Task<ContractInfo> GetContractInfoAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var script = new ScriptBuilder()
                    .EmitDynamicCall(_contractHash, "getVersion")
                    .ToArray();

                var result = await _rpcClient.InvokeScriptAsync(script, cancellationToken);

                if (result.State != RpcInvokeResult.VMStateEnum.HALT || result.Stack == null || result.Stack.Length == 0)
                {
                    throw new ContractException(_contractHash.ToString(), "Failed to get contract information");
                }

                var stackItem = result.Stack[0];
                if (stackItem.Type != RpcStackItemType.Struct)
                {
                    throw new ContractException(_contractHash.ToString(), "Invalid contract info format");
                }

                var structData = stackItem.ToStackItemArray();
                if (structData.Length < 6)
                {
                    throw new ContractException(_contractHash.ToString(), "Incomplete contract info structure");
                }

                return new ContractInfo
                {
                    Version = structData[0].GetString(),
                    Framework = structData[1].GetString(),
                    Compiler = structData[2].GetString(),
                    IsInitialized = structData[3].GetBoolean(),
                    Owner = structData[4].GetString(),
                    IsPaused = structData[5].GetBoolean()
                };
            }
            catch (Exception ex) when (!(ex is ContractException))
            {
                throw new ContractException(_contractHash.ToString(), $"Failed to get contract info: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Checks if the contract is healthy and operational
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>True if contract is healthy</returns>
        public async Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var contractInfo = await GetContractInfoAsync(cancellationToken);
                return contractInfo.IsInitialized && !contractInfo.IsPaused;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Subscribes to price update events
        /// </summary>
        /// <param name="symbols">Symbols to monitor (null for all)</param>
        /// <param name="onPriceUpdate">Callback for price updates</param>
        /// <param name="cancellationToken">Cancellation token</param>
        public async Task SubscribeToPriceUpdatesAsync(
            IEnumerable<string>? symbols,
            Action<PriceUpdateEvent> onPriceUpdate,
            CancellationToken cancellationToken = default)
        {
            var symbolSet = symbols?.Select(s => s.ToUpperInvariant()).ToHashSet();

            // This is a simplified implementation - in production you'd use WebSocket or polling
            var lastProcessedBlock = await _rpcClient.GetBlockCountAsync() - 1;

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var currentBlock = await _rpcClient.GetBlockCountAsync() - 1;
                    
                    // Process new blocks
                    for (long blockIndex = lastProcessedBlock + 1; blockIndex <= currentBlock; blockIndex++)
                    {
                        await ProcessBlockForPriceUpdates(blockIndex, symbolSet, onPriceUpdate, cancellationToken);
                    }

                    lastProcessedBlock = currentBlock;
                    await Task.Delay(TimeSpan.FromSeconds(15), cancellationToken); // Poll every 15 seconds
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error in price update subscription");
                    await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken); // Wait before retrying
                }
            }
        }

        private async Task ProcessBlockForPriceUpdates(
            long blockIndex,
            HashSet<string>? symbolFilter,
            Action<PriceUpdateEvent> onPriceUpdate,
            CancellationToken cancellationToken)
        {
            try
            {
                var block = await _rpcClient.GetBlockAsync(blockIndex.ToString(), cancellationToken);
                if (block?.Transactions == null) return;

                foreach (var tx in block.Transactions)
                {
                    try
                    {
                        var appLog = await _rpcClient.GetApplicationLogAsync(tx.Hash.ToString(), cancellationToken);
                        if (appLog?.Executions == null) continue;

                        foreach (var execution in appLog.Executions)
                        {
                            if (execution.Notifications == null) continue;

                            foreach (var notification in execution.Notifications)
                            {
                                if (notification.Contract != _contractHash.ToString() || 
                                    notification.EventName != "PriceUpdated") continue;

                                var eventData = ParsePriceUpdateEvent(notification, tx.Hash.ToString(), blockIndex);
                                if (eventData != null && 
                                    (symbolFilter == null || symbolFilter.Contains(eventData.Symbol)))
                                {
                                    onPriceUpdate(eventData);
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogWarning(ex, "Error processing transaction {TxHash}", tx.Hash);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error processing block {BlockIndex}", blockIndex);
            }
        }

        private PriceUpdateEvent? ParsePriceUpdateEvent(RpcNotification notification, string txHash, long blockIndex)
        {
            try
            {
                var state = notification.State;
                if (state?.Count >= 4)
                {
                    return new PriceUpdateEvent
                    {
                        Symbol = state[0]?.GetString() ?? "",
                        Price = decimal.Parse(state[1]?.GetString() ?? "0") / 100_000_000m,
                        Timestamp = long.Parse(state[2]?.GetString() ?? "0"),
                        Confidence = int.Parse(state[3]?.GetString() ?? "0"),
                        TransactionHash = txHash,
                        BlockIndex = blockIndex
                    };
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Error parsing price update event");
            }

            return null;
        }

        private void ValidatePriceData(PriceData data)
        {
            if (_config.ValidateFreshness && data.AgeSeconds > _config.MaxDataAgeSeconds)
            {
                throw new PriceDataException(data.Symbol, 
                    $"Price data is too old: {data.AgeSeconds}s (max: {_config.MaxDataAgeSeconds}s)");
            }

            if (data.Confidence < _config.MinConfidenceScore)
            {
                throw new PriceDataException(data.Symbol, 
                    $"Price confidence too low: {data.Confidence}% (min: {_config.MinConfidenceScore}%)");
            }

            if (data.Price <= 0)
            {
                throw new PriceDataException(data.Symbol, "Invalid price: must be greater than zero");
            }
        }

        private void CleanupCache(object? state)
        {
            try
            {
                var cutoffTime = DateTime.UtcNow.AddSeconds(-_config.CacheDurationSeconds * 2);
                var keysToRemove = _priceCache
                    .Where(kvp => kvp.Value.cacheTime < cutoffTime)
                    .Select(kvp => kvp.Key)
                    .ToList();

                foreach (var key in keysToRemove)
                {
                    _priceCache.TryRemove(key, out _);
                }

                if (keysToRemove.Count > 0)
                {
                    _logger?.LogDebug("Cleaned up {Count} expired cache entries", keysToRemove.Count);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Error during cache cleanup");
            }
        }

        public void Dispose()
        {
            _cacheCleanupTimer?.Dispose();
            _semaphore?.Dispose();
            _rpcClient?.Dispose();
        }
    }
}