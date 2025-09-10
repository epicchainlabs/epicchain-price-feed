using System;

namespace PriceFeed.Core.Options;

/// <summary>
/// Configuration options for the batch processing service
/// </summary>
public class BatchProcessingOptions
{
    /// <summary>
    /// The RPC endpoint URL for the Neo node
    /// </summary>
    public string RpcEndpoint { get; set; } = "http://localhost:10332";

    /// <summary>
    /// The script hash of the price oracle contract
    /// </summary>
    public string ContractScriptHash { get; set; } = string.Empty;

    /// <summary>
    /// The TEE account address (generated in GitHub Actions)
    /// </summary>
    public string TeeAccountAddress { get; set; } = string.Empty;

    /// <summary>
    /// The TEE account private key (generated in GitHub Actions)
    /// </summary>
    public string TeeAccountPrivateKey { get; set; } = string.Empty;

    /// <summary>
    /// The Master account address (set by user as GitHub Secret)
    /// </summary>
    public string MasterAccountAddress { get; set; } = string.Empty;

    /// <summary>
    /// The Master account private key (set by user as GitHub Secret)
    /// </summary>
    public string MasterAccountPrivateKey { get; set; } = string.Empty;

    /// <summary>
    /// Flag indicating whether to check for and transfer assets from TEE account to Master account
    /// </summary>
    public bool CheckAndTransferTeeAssets { get; set; } = true;

    /// <summary>
    /// The maximum number of prices to include in a single batch
    /// </summary>
    public int MaxBatchSize { get; set; } = 50;

    // For backward compatibility with tests
    public string AccountAddress
    {
        get => TeeAccountAddress;
        set => TeeAccountAddress = value;
    }

    public string AccountPrivateKey
    {
        get => TeeAccountPrivateKey;
        set => TeeAccountPrivateKey = value;
    }

    public string WalletWif
    {
        get => MasterAccountPrivateKey;
        set => MasterAccountPrivateKey = value;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="BatchProcessingOptions"/> class
    /// </summary>
    public BatchProcessingOptions()
    {
        // Environment variables will only override configuration if explicitly set
        // This allows appsettings.json to work properly in testnet mode
    }

    /// <summary>
    /// Called after configuration binding to apply environment variable overrides
    /// </summary>
    public void ApplyEnvironmentOverrides()
    {
        // Try to get values from environment variables (these can override config values)
        var rpcEndpoint = Environment.GetEnvironmentVariable("NEO_RPC_ENDPOINT");
        if (!string.IsNullOrEmpty(rpcEndpoint))
        {
            RpcEndpoint = rpcEndpoint;
        }

        var contractHash = Environment.GetEnvironmentVariable("CONTRACT_SCRIPT_HASH");
        if (!string.IsNullOrEmpty(contractHash))
        {
            ContractScriptHash = contractHash;
        }

        // Get the TEE account credentials from environment variables (if set)
        var teeAccountAddress = Environment.GetEnvironmentVariable("TEE_ACCOUNT_ADDRESS");
        if (!string.IsNullOrEmpty(teeAccountAddress))
        {
            TeeAccountAddress = teeAccountAddress;
        }

        var teeAccountPrivateKey = Environment.GetEnvironmentVariable("TEE_ACCOUNT_PRIVATE_KEY");
        if (!string.IsNullOrEmpty(teeAccountPrivateKey))
        {
            TeeAccountPrivateKey = teeAccountPrivateKey;
        }

        // Get the Master account credentials from environment variables (if set)
        var masterAccountAddress = Environment.GetEnvironmentVariable("MASTER_ACCOUNT_ADDRESS");
        if (!string.IsNullOrEmpty(masterAccountAddress))
        {
            MasterAccountAddress = masterAccountAddress;
        }

        var masterAccountPrivateKey = Environment.GetEnvironmentVariable("MASTER_ACCOUNT_PRIVATE_KEY");
        if (!string.IsNullOrEmpty(masterAccountPrivateKey))
        {
            MasterAccountPrivateKey = masterAccountPrivateKey;
        }

        // For backward compatibility, also check the old environment variables (only if not already set)
        if (string.IsNullOrEmpty(TeeAccountAddress))
        {
            var accountAddress = Environment.GetEnvironmentVariable("NEO_ACCOUNT_ADDRESS");
            if (!string.IsNullOrEmpty(accountAddress))
            {
                TeeAccountAddress = accountAddress;
            }
        }

        if (string.IsNullOrEmpty(TeeAccountPrivateKey))
        {
            var accountPrivateKey = Environment.GetEnvironmentVariable("NEO_ACCOUNT_PRIVATE_KEY");
            if (!string.IsNullOrEmpty(accountPrivateKey))
            {
                TeeAccountPrivateKey = accountPrivateKey;
            }
        }

        // Check if we should transfer assets from TEE account to Master account
        var checkAndTransfer = Environment.GetEnvironmentVariable("CHECK_AND_TRANSFER_TEE_ASSETS");
        if (!string.IsNullOrEmpty(checkAndTransfer) && bool.TryParse(checkAndTransfer, out bool transferAssets))
        {
            CheckAndTransferTeeAssets = transferAssets;
        }
    }
}
