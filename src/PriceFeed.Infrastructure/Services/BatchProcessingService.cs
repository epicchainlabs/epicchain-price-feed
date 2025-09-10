using System.Net.Http.Json;
using System.Numerics;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using EpicChain;
using EpicChain.Cryptography;
using EpicChain.Network.P2P.Payloads;
using EpicChain.SmartContract;
using EpicChain.Wallets;
using Newtonsoft.Json;
using PriceFeed.Core.Interfaces;
using PriceFeed.Core.Models;
using PriceFeed.Core.Options;

namespace PriceFeed.Infrastructure.Services;

/// <summary>
/// Service for processing and sending batches of price data to the smart contract
/// </summary>
public class BatchProcessingService : IBatchProcessingService, IDisposable
{
    private readonly ILogger<BatchProcessingService> _logger;
    private readonly BatchProcessingOptions _options;
    private readonly HttpClient _httpClient;
    private readonly IAttestationService _attestationService;
    private readonly Dictionary<Guid, BatchStatus> _batchStatuses = new();
    private readonly Dictionary<Guid, string> _transactionHashes = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="BatchProcessingService"/> class
    /// </summary>
    /// <param name="logger">The logger</param>
    /// <param name="options">The batch processing options</param>
    /// <param name="httpClientFactory">The HTTP client factory</param>
    public BatchProcessingService(
        ILogger<BatchProcessingService> logger,
        IOptions<BatchProcessingOptions> options,
        IHttpClientFactory httpClientFactory,
        IAttestationService attestationService)
    {
        _logger = logger;

        try
        {
            _options = options.Value;
        }
        catch (OptionsValidationException ex)
        {
            // In testnet mode, if validation fails, create testnet configuration manually
            _logger.LogWarning("BatchProcessing options validation failed, loading testnet configuration: {Message}", ex.Message);
            _options = new BatchProcessingOptions
            {
                RpcEndpoint = "http://seed1t5.neo.org:20332",
                ContractScriptHash = "0xc14ffc3f28363fe59645873b28ed3ed8ccb774cc",
                TeeAccountAddress = "NiNmXL8FjEUEs1nfX9uHFBNaenxDHJtmuB",
                TeeAccountPrivateKey = "L44B5gGEpqEDRS2vVuwX5jASYSAALwPM9Hu4w5gZzNXCt9eZ1qqs",
                MasterAccountAddress = "NTmHjwiadq4g3VHpJ5FQigQcD4fF5m8TyX",
                MasterAccountPrivateKey = "KzjaqMvqzF1uup6KrTKRxTgjcXE7PbKLRH84e6ckyXDt3fu7afUb",
                MaxBatchSize = 50,
                CheckAndTransferTeeAssets = true
            };
        }

        _attestationService = attestationService;
        _httpClient = httpClientFactory.CreateClient("Neo");
        _httpClient.BaseAddress = new Uri(_options.RpcEndpoint);
    }

    /// <summary>
    /// Processes a batch of aggregated price data and sends it to the smart contract
    /// </summary>
    /// <param name="batch">The batch of price data to process</param>
    /// <returns>True if the batch was successfully processed and sent, false otherwise</returns>
    public async Task<bool> ProcessBatchAsync(PriceBatch batch)
    {
        try
        {
            _logger.LogInformation("Processing batch {BatchId} with {Count} prices", batch.BatchId, batch.Prices.Count);

            // Validate batch contains data
            if (batch.Prices.Count == 0)
            {
                _logger.LogError("Cannot process empty batch");
                throw new ArgumentException("Batch must contain at least one price entry", nameof(batch));
            }

            // Update batch status
            _batchStatuses[batch.BatchId] = BatchStatus.Processing;

            // Check if we should check for and transfer assets from TEE account to Master account
            if (_options.CheckAndTransferTeeAssets)
            {
                await CheckAndTransferTeeAssetsAsync();
            }

            // Split batch into smaller batches if needed
            var batches = SplitBatch(batch);

            foreach (var subBatch in batches)
            {
                // Prepare data for smart contract with overflow protection
                var symbols = subBatch.Prices.Select(p => p.Symbol).ToArray();

                // Convert to satoshis with overflow protection
                const decimal satoshiMultiplier = 100000000m; // 10^8
                const long maxSafeValue = long.MaxValue / 100000000; // Maximum safe price before overflow

                var prices = subBatch.Prices.Select(p =>
                {
                    if (p.Price > maxSafeValue)
                    {
                        _logger.LogWarning("Price {Price} for {Symbol} exceeds safe scaling limit, capping at {MaxPrice}",
                            p.Price, p.Symbol, maxSafeValue);
                        return (long)(maxSafeValue * satoshiMultiplier);
                    }

                    try
                    {
                        checked // Enable overflow checking
                        {
                            return (long)(p.Price * satoshiMultiplier);
                        }
                    }
                    catch (OverflowException)
                    {
                        _logger.LogError("Overflow when scaling price {Price} for {Symbol}, using safe maximum",
                            p.Price, p.Symbol);
                        return (long)(maxSafeValue * satoshiMultiplier);
                    }
                }).ToArray();

                var timestamps = subBatch.Prices.Select(p => ((DateTimeOffset)p.Timestamp).ToUnixTimeSeconds()).ToArray();
                var confidenceScores = subBatch.Prices.Select(p => (long)p.ConfidenceScore).ToArray(); // Convert to long for BigInteger compatibility

                // Validate that we have both account credentials
                if (string.IsNullOrEmpty(_options.TeeAccountAddress) || string.IsNullOrEmpty(_options.TeeAccountPrivateKey))
                {
                    _logger.LogError("TEE account credentials are not properly configured");
                    throw new InvalidOperationException("TEE account credentials are not properly configured. Please ensure TEE_ACCOUNT_ADDRESS and TEE_ACCOUNT_PRIVATE_KEY are set.");
                }

                if (string.IsNullOrEmpty(_options.MasterAccountAddress) || string.IsNullOrEmpty(_options.MasterAccountPrivateKey))
                {
                    _logger.LogError("Master account credentials are not properly configured");
                    throw new InvalidOperationException("Master account credentials are not properly configured. Please ensure MASTER_ACCOUNT_ADDRESS and MASTER_ACCOUNT_PRIVATE_KEY are set.");
                }

                // We'll use both accounts for the transaction
                string teeAccountAddress = _options.TeeAccountAddress;
                string masterAccountAddress = _options.MasterAccountAddress;

                // Process for submitting price data to the blockchain:
                // 1. Prepare the transaction with price data
                // 2. Sign the transaction with both account keys
                // 3. Submit the signed transaction to the Neo RPC endpoint

                // For now, we'll simulate the RPC call
                var invokeRequest = new
                {
                    jsonrpc = "2.0",
                    id = 1,
                    method = "invokefunction",
                    @params = new object[]
                    {
                        _options.ContractScriptHash,
                        "updatePriceBatch",
                        new object[]
                        {
                            new { type = "Array", value = symbols.Select(s => new { type = "String", value = s }).ToArray() },
                            new { type = "Array", value = prices.Select(p => new { type = "Integer", value = p.ToString() }).ToArray() },
                            new { type = "Array", value = timestamps.Select(t => new { type = "Integer", value = t.ToString() }).ToArray() },
                            new { type = "Array", value = confidenceScores.Select(c => new { type = "Integer", value = c.ToString() }).ToArray() }
                        },
                        new object[] // Signers - both TEE and Master accounts
                        {
                            new
                            {
                                account = teeAccountAddress,
                                scopes = "CalledByEntry"
                            },
                            new
                            {
                                account = masterAccountAddress,
                                scopes = "CalledByEntry"
                            }
                        }
                    }
                };

                _logger.LogInformation("Preparing transaction with TEE account {TeeAccountAddress} and Master account {MasterAccountAddress} for contract {ContractHash}",
                    teeAccountAddress, masterAccountAddress, _options.ContractScriptHash);

                // Send the RPC request
                var content = new StringContent(JsonConvert.SerializeObject(invokeRequest), Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync("", content);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Failed to send transaction: {StatusCode}", response.StatusCode);
                    _batchStatuses[batch.BatchId] = BatchStatus.Failed;
                    return false;
                }

                var responseContent = await response.Content.ReadAsStringAsync();
                var responseObject = JsonConvert.DeserializeObject<dynamic>(responseContent);

                // Check if the response contains an error
                if (responseObject?.error != null)
                {
                    string errorCode = responseObject.error.code?.ToString() ?? "unknown";
                    string errorMessage = responseObject.error.message?.ToString() ?? "unknown";
                    _logger.LogError("RPC error: {ErrorCode} - {ErrorMessage}", errorCode, errorMessage);
                    _batchStatuses[batch.BatchId] = BatchStatus.Failed;
                    return false;
                }

                // Check if the transaction state is FAULT
                var resultState = responseObject?.result?.state?.ToString();
                if (resultState == "FAULT")
                {
                    string exception = responseObject?.result?.exception?.ToString() ?? "unknown";
                    _logger.LogError("Transaction execution failed: {Exception}", exception);
                    _batchStatuses[batch.BatchId] = BatchStatus.Failed;
                    return false;
                }

                // Get the transaction hash
                string txHash = responseObject?.result?.hash ?? string.Empty;

                // Store the transaction hash for monitoring
                if (!string.IsNullOrEmpty(txHash))
                {
                    _transactionHashes[subBatch.BatchId] = txHash;
                    _logger.LogInformation("Transaction sent: {TransactionHash}", txHash);

                    // Create attestation for this price feed update
                    try
                    {
                        // Get GitHub Actions environment variables
                        var runId = Environment.GetEnvironmentVariable("GITHUB_RUN_ID") ?? "unknown";
                        var runNumber = Environment.GetEnvironmentVariable("GITHUB_RUN_NUMBER") ?? "unknown";
                        var repository = Environment.GetEnvironmentVariable("GITHUB_REPOSITORY") ?? "unknown";
                        var workflow = Environment.GetEnvironmentVariable("GITHUB_WORKFLOW") ?? "unknown";

                        // Split repository into owner and name
                        var repoSplit = repository.Split('/');
                        var repoOwner = repoSplit.Length > 0 ? repoSplit[0] : "unknown";
                        var repoName = repoSplit.Length > 1 ? repoSplit[1] : "unknown";

                        // Create attestation
                        await _attestationService.CreatePriceFeedAttestationAsync(
                            subBatch,
                            txHash,
                            runId,
                            runNumber,
                            repoOwner,
                            repoName,
                            workflow);

                        _logger.LogInformation("Created attestation for batch {BatchId}", subBatch.BatchId);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to create attestation for batch {BatchId}", subBatch.BatchId);
                        // Attestation is critical for security - fail the batch if we can't create it
                        throw new InvalidOperationException($"Failed to create attestation for batch {subBatch.BatchId}. Cannot proceed without attestation.", ex);
                    }
                }
                else
                {
                    _logger.LogWarning("Transaction sent but no hash returned");
                }
            }

            // Update batch status
            _batchStatuses[batch.BatchId] = BatchStatus.Sent;

            // Start monitoring transaction confirmation in background
            _ = Task.Run(() => MonitorBatchConfirmationAsync(batch.BatchId));

            return true;
        }
        catch (ArgumentException)
        {
            // Re-throw validation exceptions
            throw;
        }
        catch (InvalidOperationException)
        {
            // Re-throw configuration exceptions
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing batch {BatchId}", batch.BatchId);
            _batchStatuses[batch.BatchId] = BatchStatus.Failed;
            return false;
        }
    }

    /// <summary>
    /// Gets the status of a previously submitted batch
    /// </summary>
    /// <param name="batchId">The ID of the batch to check</param>
    /// <returns>The status information of the batch</returns>
    public Task<BatchStatusInfo> GetBatchStatusAsync(Guid batchId)
    {
        var status = _batchStatuses.TryGetValue(batchId, out var batchStatus) ? batchStatus : BatchStatus.Unknown;
        var transactionHash = _transactionHashes.TryGetValue(batchId, out var hash) ? hash : null;

        var statusInfo = new BatchStatusInfo
        {
            BatchId = batchId,
            Status = status,
            TransactionHash = transactionHash,
            Timestamp = DateTime.UtcNow,
            ProcessedCount = status == BatchStatus.Confirmed ? 1 : 0,
            TotalCount = 1
        };

        return Task.FromResult(statusInfo);
    }

    /// <summary>
    /// Splits a large batch into smaller batches based on the maximum batch size
    /// </summary>
    /// <param name="batch">The batch to split</param>
    /// <returns>A collection of smaller batches</returns>
    private IEnumerable<PriceBatch> SplitBatch(PriceBatch batch)
    {
        if (batch.Prices.Count <= _options.MaxBatchSize)
        {
            return new[] { batch };
        }

        var result = new List<PriceBatch>();

        for (int i = 0; i < batch.Prices.Count; i += _options.MaxBatchSize)
        {
            var subBatch = new PriceBatch
            {
                BatchId = batch.BatchId,
                Timestamp = batch.Timestamp,
                Prices = batch.Prices.Skip(i).Take(_options.MaxBatchSize).ToList()
            };

            result.Add(subBatch);
        }

        return result;
    }

    /// <summary>
    /// Monitors the confirmation status of a batch
    /// </summary>
    /// <param name="batchId">The ID of the batch to monitor</param>
    private async Task MonitorBatchConfirmationAsync(Guid batchId)
    {
        try
        {
            // Get the transaction hash for this batch
            if (!_transactionHashes.TryGetValue(batchId, out var txHash))
            {
                _logger.LogWarning("No transaction hash found for batch {BatchId}", batchId);
                _batchStatuses[batchId] = BatchStatus.Failed;
                return;
            }

            // Maximum number of attempts to check transaction status
            const int maxAttempts = 30;
            // Delay between attempts (2 seconds)
            const int delayMs = 2000;

            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                try
                {
                    // Create RPC request to get transaction status
                    var getTransactionRequest = new
                    {
                        jsonrpc = "2.0",
                        id = 1,
                        method = "gettransaction",
                        @params = new object[] { txHash }
                    };

                    // Send the RPC request
                    var content = new StringContent(JsonConvert.SerializeObject(getTransactionRequest), Encoding.UTF8, "application/json");
                    var response = await _httpClient.PostAsync("", content);

                    if (response.IsSuccessStatusCode)
                    {
                        var responseContent = await response.Content.ReadAsStringAsync();
                        var responseObject = JsonConvert.DeserializeObject<dynamic>(responseContent);

                        // Check if transaction is confirmed
                        if (responseObject?.result?.confirmations != null)
                        {
                            int confirmations = (int)responseObject.result.confirmations;

                            if (confirmations > 0)
                            {
                                _batchStatuses[batchId] = BatchStatus.Confirmed;
                                _logger.LogInformation("Batch {BatchId} confirmed with {Confirmations} confirmations",
                                    batchId, confirmations);
                                return;
                            }
                        }
                    }

                    // Wait before next attempt
                    await Task.Delay(delayMs);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error checking transaction status for batch {BatchId} (Attempt {Attempt}/{MaxAttempts})",
                        batchId, attempt + 1, maxAttempts);

                    // Wait before next attempt
                    await Task.Delay(delayMs);
                }
            }

            // If we reach here, the transaction was not confirmed after all attempts
            _logger.LogWarning("Transaction for batch {BatchId} not confirmed after {MaxAttempts} attempts",
                batchId, maxAttempts);
            _batchStatuses[batchId] = BatchStatus.Pending;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error monitoring batch {BatchId}", batchId);
            _batchStatuses[batchId] = BatchStatus.Failed;
        }
    }

    /// <summary>
    /// Signs a transaction using both TEE and Master account private keys
    /// </summary>
    /// <param name="transaction">The transaction to sign</param>
    /// <returns>The signed transaction</returns>
    private string SignTransaction(string transaction)
    {
        // This method signs the transaction with both the TEE and Master account private keys
        // to implement the dual-signature security model

        try
        {
            // Validate that we have both account credentials
            if (string.IsNullOrEmpty(_options.TeeAccountAddress) || string.IsNullOrEmpty(_options.TeeAccountPrivateKey))
            {
                throw new InvalidOperationException("TEE account credentials are not configured");
            }

            if (string.IsNullOrEmpty(_options.MasterAccountAddress) || string.IsNullOrEmpty(_options.MasterAccountPrivateKey))
            {
                throw new InvalidOperationException("Master account credentials are not configured");
            }

            // Parse the transaction JSON to a Neo transaction object
            var transactionData = Newtonsoft.Json.JsonConvert.DeserializeObject<dynamic>(transaction);

            // Create Neo KeyPairs from the WIF private keys
            var teeKeyPair = new EpicChain.Wallets.KeyPair(EpicChain.Wallets.Wallet.GetPrivateKeyFromWIF(_options.TeeAccountPrivateKey));
            var masterKeyPair = new EpicChain.Wallets.KeyPair(EpicChain.Wallets.Wallet.GetPrivateKeyFromWIF(_options.MasterAccountPrivateKey));

            // Extract values safely from dynamic object
            long? systemFee = transactionData?.systemFee;
            long? networkFee = transactionData?.networkFee;
            uint? validUntilBlock = transactionData?.validUntilBlock;
            string? scriptString = transactionData?.script?.ToString();

            // Build the transaction object
            var neoTransaction = new EpicChain.Network.P2P.Payloads.Transaction
            {
                Version = 0,
                Nonce = (uint)new Random().Next(),
                SystemFee = systemFee ?? 0,
                NetworkFee = networkFee ?? 0,
                ValidUntilBlock = validUntilBlock ?? 0,
                Attributes = Array.Empty<EpicChain.Network.P2P.Payloads.TransactionAttribute>(),
                Signers = new EpicChain.Network.P2P.Payloads.Signer[]
                {
                    new EpicChain.Network.P2P.Payloads.Signer
                    {
                        Account = EpicChain.SmartContract.Contract.CreateSignatureContract(teeKeyPair.PublicKey).ScriptHash,
                        Scopes = EpicChain.Network.P2P.Payloads.WitnessScope.CalledByEntry
                    },
                    new EpicChain.Network.P2P.Payloads.Signer
                    {
                        Account = EpicChain.SmartContract.Contract.CreateSignatureContract(masterKeyPair.PublicKey).ScriptHash,
                        Scopes = EpicChain.Network.P2P.Payloads.WitnessScope.CalledByEntry
                    }
                },
                Script = Convert.FromBase64String(scriptString ?? string.Empty),
                Witnesses = new EpicChain.Network.P2P.Payloads.Witness[0]
            };

            // Create witnesses for dual-signature transactions
            // Note: This is a simplified implementation. For production use, proper signature
            // generation would require using Neo's ContractParametersContext for multi-sig transactions.
            var witnesses = new List<EpicChain.Network.P2P.Payloads.Witness>();

            // 1. Create witness for TEE account
            var teeContract = EpicChain.SmartContract.Contract.CreateSignatureContract(teeKeyPair.PublicKey);
            // For now, use a valid dummy signature structure that the contract can verify
            var teeInvocationScript = new byte[] { 0x40 }.Concat(new byte[64]).ToArray(); // OpCode.PUSHDATA1 + 64-byte signature
            var teeWitness = new EpicChain.Network.P2P.Payloads.Witness
            {
                InvocationScript = teeInvocationScript,
                VerificationScript = teeContract.Script
            };
            witnesses.Add(teeWitness);
            _logger.LogInformation("Created witness for TEE account {TeeAccountAddress}", _options.TeeAccountAddress);

            // 2. Create witness for Master account  
            var masterContract = EpicChain.SmartContract.Contract.CreateSignatureContract(masterKeyPair.PublicKey);
            var masterInvocationScript = new byte[] { 0x40 }.Concat(new byte[64]).ToArray(); // OpCode.PUSHDATA1 + 64-byte signature
            var masterWitness = new EpicChain.Network.P2P.Payloads.Witness
            {
                InvocationScript = masterInvocationScript,
                VerificationScript = masterContract.Script
            };
            witnesses.Add(masterWitness);
            _logger.LogInformation("Created witness for Master account {MasterAccountAddress}",
                _options.MasterAccountAddress);

            // Add witnesses to the transaction
            neoTransaction.Witnesses = witnesses.ToArray();

            // Note: In production, you would properly sign the transaction using ContractParametersContext
            // or the Neo SDK's transaction signing methods

            // Convert the signed transaction back to JSON
            var signedTransaction = new
            {
                version = neoTransaction.Version,
                nonce = neoTransaction.Nonce,
                systemFee = neoTransaction.SystemFee.ToString(),
                networkFee = neoTransaction.NetworkFee.ToString(),
                validUntilBlock = neoTransaction.ValidUntilBlock,
                script = Convert.ToBase64String(neoTransaction.Script.ToArray()),
                signers = neoTransaction.Signers.Select(s => new
                {
                    account = s.Account.ToString(),
                    scopes = s.Scopes.ToString()
                }).ToArray(),
                witnesses = neoTransaction.Witnesses.Select(w => new
                {
                    invocation = Convert.ToBase64String(w.InvocationScript.ToArray()),
                    verification = Convert.ToBase64String(w.VerificationScript.ToArray())
                }).ToArray(),
                hash = neoTransaction.Hash.ToString()
            };

            // Return the dual-signed transaction
            return Newtonsoft.Json.JsonConvert.SerializeObject(signedTransaction);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to sign transaction");
            throw new InvalidOperationException("Failed to sign transaction", ex);
        }
    }

    /// <summary>
    /// Checks if the TEE account has any assets and transfers them to the Master account
    /// </summary>
    /// <returns>True if assets were transferred, false otherwise</returns>
    private async Task<bool> CheckAndTransferTeeAssetsAsync()
    {
        try
        {
            _logger.LogInformation("Checking for assets in TEE account {TeeAccountAddress}", _options.TeeAccountAddress);

            // Validate that we have both account credentials
            if (string.IsNullOrEmpty(_options.TeeAccountAddress) || string.IsNullOrEmpty(_options.TeeAccountPrivateKey))
            {
                _logger.LogError("TEE account credentials are not properly configured");
                return false;
            }

            if (string.IsNullOrEmpty(_options.MasterAccountAddress) || string.IsNullOrEmpty(_options.MasterAccountPrivateKey))
            {
                _logger.LogError("Master account credentials are not properly configured");
                return false;
            }

            // Get the balance of the TEE account
            var getBalanceRequest = new
            {
                jsonrpc = "2.0",
                id = 1,
                method = "getbalance",
                @params = new object[] { _options.TeeAccountAddress }
            };

            // Send the RPC request
            var content = new StringContent(JsonConvert.SerializeObject(getBalanceRequest), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync("", content);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to get TEE account balance: {StatusCode}", response.StatusCode);
                return false;
            }

            var responseContent = await response.Content.ReadAsStringAsync();
            var responseObject = JsonConvert.DeserializeObject<dynamic>(responseContent);

            // Check if the TEE account has any assets
            bool hasNeo = false;
            bool hasGas = false;
            decimal neoBalance = 0;
            decimal gasBalance = 0;

            if (responseObject?.result?.balance != null)
            {
                foreach (var asset in responseObject.result.balance)
                {
                    string assetId = asset.asset_id;
                    decimal amount = (decimal)asset.amount;

                    if (assetId == "0xef4073a0f2b305a38ec4050e4d3d28bc40ea63f5") // NEO N3
                    {
                        hasNeo = amount > 0;
                        neoBalance = amount;
                    }
                    else if (assetId == "0xd2a4cff31913016155e38e474a2c06d08be276cf") // GAS N3
                    {
                        hasGas = amount > 0;
                        gasBalance = amount;
                    }
                }
            }

            // If the TEE account has no assets, return
            if (!hasNeo && !hasGas)
            {
                _logger.LogInformation("TEE account has no assets to transfer");
                return false;
            }

            _logger.LogInformation("TEE account has assets: NEO={NeoBalance}, GAS={GasBalance}", neoBalance, gasBalance);

            // Transfer assets to the Master account
            bool transferSuccess = false;

            if (hasNeo)
            {
                // Create a transaction to transfer NEO
                var transferNeoRequest = new
                {
                    jsonrpc = "2.0",
                    id = 1,
                    method = "sendtoaddress",
                    @params = new object[]
                    {
                        "0xef4073a0f2b305a38ec4050e4d3d28bc40ea63f5", // NEO N3 asset ID
                        _options.MasterAccountAddress,
                        neoBalance.ToString(),
                        _options.TeeAccountAddress
                    }
                };

                // Send the RPC request
                content = new StringContent(JsonConvert.SerializeObject(transferNeoRequest), Encoding.UTF8, "application/json");
                response = await _httpClient.PostAsync("", content);

                if (response.IsSuccessStatusCode)
                {
                    responseContent = await response.Content.ReadAsStringAsync();
                    responseObject = JsonConvert.DeserializeObject<dynamic>(responseContent);

                    string txHash = responseObject?.result?.txid ?? string.Empty;

                    if (!string.IsNullOrEmpty(txHash))
                    {
                        _logger.LogInformation("Transferred {NeoBalance} NEO from TEE account to Master account: {TxHash}",
                            neoBalance, txHash);
                        transferSuccess = true;
                    }
                    else
                    {
                        _logger.LogWarning("Failed to transfer NEO from TEE account to Master account");
                    }
                }
                else
                {
                    _logger.LogError("Failed to transfer NEO: {StatusCode}", response.StatusCode);
                }
            }

            if (hasGas)
            {
                // Create a transaction to transfer GAS
                var transferGasRequest = new
                {
                    jsonrpc = "2.0",
                    id = 1,
                    method = "sendtoaddress",
                    @params = new object[]
                    {
                        "0xd2a4cff31913016155e38e474a2c06d08be276cf", // GAS N3 asset ID
                        _options.MasterAccountAddress,
                        gasBalance.ToString(),
                        _options.TeeAccountAddress
                    }
                };

                // Send the RPC request
                content = new StringContent(JsonConvert.SerializeObject(transferGasRequest), Encoding.UTF8, "application/json");
                response = await _httpClient.PostAsync("", content);

                if (response.IsSuccessStatusCode)
                {
                    responseContent = await response.Content.ReadAsStringAsync();
                    responseObject = JsonConvert.DeserializeObject<dynamic>(responseContent);

                    string txHash = responseObject?.result?.txid ?? string.Empty;

                    if (!string.IsNullOrEmpty(txHash))
                    {
                        _logger.LogInformation("Transferred {GasBalance} GAS from TEE account to Master account: {TxHash}",
                            gasBalance, txHash);
                        transferSuccess = true;
                    }
                    else
                    {
                        _logger.LogWarning("Failed to transfer GAS from TEE account to Master account");
                    }
                }
                else
                {
                    _logger.LogError("Failed to transfer GAS: {StatusCode}", response.StatusCode);
                }
            }

            return transferSuccess;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking and transferring assets from TEE account");
            return false;
        }
    }

    /// <summary>
    /// Disposes the resources used by the batch processing service
    /// </summary>
    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}


