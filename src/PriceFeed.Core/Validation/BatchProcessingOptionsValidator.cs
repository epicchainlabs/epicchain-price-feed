using Microsoft.Extensions.Options;
using PriceFeed.Core.Options;

namespace PriceFeed.Core.Validation;

/// <summary>
/// Validator for BatchProcessingOptions to ensure production-ready configuration
/// </summary>
public class BatchProcessingOptionsValidator : IValidateOptions<BatchProcessingOptions>
{
    public ValidateOptionsResult Validate(string? name, BatchProcessingOptions options)
    {
        var failures = new List<string>();

        // Check if this is testnet mode (relaxed validation)
        bool isTestnetMode = !string.IsNullOrEmpty(options.RpcEndpoint) &&
                           options.RpcEndpoint.Contains("seed1t5.neo.org", StringComparison.OrdinalIgnoreCase);

        // Validate RPC endpoint
        if (string.IsNullOrWhiteSpace(options.RpcEndpoint))
        {
            failures.Add("RPC endpoint must be configured");
        }
        else if (!Uri.TryCreate(options.RpcEndpoint, UriKind.Absolute, out var uri))
        {
            failures.Add("RPC endpoint must be a valid URI");
        }
        else if (!isTestnetMode && uri.Scheme != "https" && !uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase))
        {
            failures.Add("RPC endpoint must use HTTPS for production (except localhost)");
        }

        // Validate contract script hash (relaxed for testnet)
        if (string.IsNullOrWhiteSpace(options.ContractScriptHash))
        {
            failures.Add("Contract script hash must be configured");
        }
        else if (!isTestnetMode && options.ContractScriptHash.Contains("MUST_BE_SET", StringComparison.OrdinalIgnoreCase))
        {
            failures.Add("Contract script hash contains placeholder value");
        }
        else if (!options.ContractScriptHash.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            failures.Add("Contract script hash must start with '0x'");
        }

        // Validate TEE account configuration
        if (string.IsNullOrWhiteSpace(options.TeeAccountAddress))
        {
            failures.Add("TEE account address must be configured");
        }
        else if (!options.TeeAccountAddress.StartsWith("N", StringComparison.OrdinalIgnoreCase))
        {
            failures.Add("TEE account address must be a valid Neo address starting with 'N'");
        }

        if (string.IsNullOrWhiteSpace(options.TeeAccountPrivateKey))
        {
            failures.Add("TEE account private key must be configured");
        }
        else if (!isTestnetMode && options.TeeAccountPrivateKey.Length < 52)
        {
            failures.Add("TEE account private key appears to be invalid");
        }

        // Validate Master account configuration
        if (string.IsNullOrWhiteSpace(options.MasterAccountAddress))
        {
            failures.Add("Master account address must be configured");
        }
        else if (!options.MasterAccountAddress.StartsWith("N", StringComparison.OrdinalIgnoreCase))
        {
            failures.Add("Master account address must be a valid Neo address starting with 'N'");
        }

        if (string.IsNullOrWhiteSpace(options.MasterAccountPrivateKey))
        {
            failures.Add("Master account private key must be configured");
        }
        else if (!isTestnetMode && options.MasterAccountPrivateKey.Length < 52)
        {
            failures.Add("Master account private key appears to be invalid");
        }

        // Validate batch size
        if (options.MaxBatchSize <= 0)
        {
            failures.Add("Max batch size must be greater than 0");
        }
        else if (options.MaxBatchSize > 100)
        {
            failures.Add("Max batch size should not exceed 100 for optimal performance");
        }

        return failures.Any()
            ? ValidateOptionsResult.Fail(failures)
            : ValidateOptionsResult.Success;
    }
}
