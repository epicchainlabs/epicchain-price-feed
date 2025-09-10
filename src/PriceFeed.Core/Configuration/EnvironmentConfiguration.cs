using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Configuration;

namespace PriceFeed.Core.Configuration;

/// <summary>
/// Helper class for loading configuration from environment variables
/// </summary>
public static class EnvironmentConfiguration
{
    /// <summary>
    /// Applies environment variable overrides to configuration
    /// </summary>
    public static IConfigurationBuilder AddEnvironmentVariableOverrides(this IConfigurationBuilder builder)
    {
        // Add environment variables with custom mapping
        builder.AddEnvironmentVariables();

        // Add in-memory configuration for mapping environment variables to settings
        var environmentOverrides = new Dictionary<string, string?>();

        // Map TEE account settings
        AddEnvironmentMapping(environmentOverrides, "TEE_ACCOUNT_ADDRESS", "BatchProcessing:TeeAccountAddress");
        AddEnvironmentMapping(environmentOverrides, "TEE_ACCOUNT_PRIVATE_KEY", "BatchProcessing:TeeAccountPrivateKey");

        // Map Master account settings
        AddEnvironmentMapping(environmentOverrides, "MASTER_ACCOUNT_ADDRESS", "BatchProcessing:MasterAccountAddress");
        AddEnvironmentMapping(environmentOverrides, "MASTER_ACCOUNT_PRIVATE_KEY", "BatchProcessing:MasterAccountPrivateKey");

        // Map network settings
        AddEnvironmentMapping(environmentOverrides, "NEO_RPC_ENDPOINT", "BatchProcessing:RpcEndpoint");
        AddEnvironmentMapping(environmentOverrides, "CONTRACT_SCRIPT_HASH", "BatchProcessing:ContractScriptHash");

        // Map API keys
        AddEnvironmentMapping(environmentOverrides, "COINMARKETCAP_API_KEY", "CoinMarketCap:ApiKey");
        AddEnvironmentMapping(environmentOverrides, "COINGECKO_API_KEY", "CoinGecko:ApiKey");
        AddEnvironmentMapping(environmentOverrides, "KRAKEN_API_KEY", "Kraken:ApiKey");
        AddEnvironmentMapping(environmentOverrides, "KRAKEN_API_SECRET", "Kraken:ApiSecret");

        // Map monitoring settings
        AddEnvironmentMapping(environmentOverrides, "OTLP_ENDPOINT", "OpenTelemetry:OtlpEndpoint");

        builder.AddInMemoryCollection(environmentOverrides!);

        return builder;
    }

    private static void AddEnvironmentMapping(Dictionary<string, string?> overrides, string envVar, string configPath)
    {
        var value = Environment.GetEnvironmentVariable(envVar);
        if (!string.IsNullOrEmpty(value))
        {
            overrides[configPath] = value;
        }
    }

    /// <summary>
    /// Validates that required environment variables are set
    /// </summary>
    public static void ValidateRequiredEnvironmentVariables()
    {
        var requiredVars = new[]
        {
            "TEE_ACCOUNT_ADDRESS",
            "TEE_ACCOUNT_PRIVATE_KEY",
            "MASTER_ACCOUNT_ADDRESS",
            "MASTER_ACCOUNT_PRIVATE_KEY"
        };

        var missingVars = new List<string>();

        foreach (var varName in requiredVars)
        {
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(varName)))
            {
                missingVars.Add(varName);
            }
        }

        if (missingVars.Any())
        {
            throw new InvalidOperationException(
                $"Required environment variables are missing: {string.Join(", ", missingVars)}. " +
                "Please set these environment variables or create a .env file. " +
                "See .env.example for reference.");
        }
    }

    /// <summary>
    /// Checks if running in development mode where hardcoded keys might be acceptable
    /// </summary>
    public static bool IsDevelopmentMode()
    {
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";
        return environment.Equals("Development", StringComparison.OrdinalIgnoreCase);
    }
}
