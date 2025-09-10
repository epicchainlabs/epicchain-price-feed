using System;
using System.Collections.Generic;
using System.Linq;

namespace PriceFeed.Core.Options;

/// <summary>
/// Configuration options for the price feed service
/// </summary>
public class PriceFeedOptions
{
    /// <summary>
    /// The standard symbols to collect price data for
    /// </summary>
    public List<string> Symbols { get; set; } = new List<string>();

    /// <summary>
    /// Symbol mappings for different data sources
    /// </summary>
    public SymbolMappingOptions SymbolMappings { get; set; } = new SymbolMappingOptions();

    /// <summary>
    /// Initializes a new instance of the <see cref="PriceFeedOptions"/> class
    /// </summary>
    public PriceFeedOptions()
    {
        // Environment variables will only override configuration if explicitly set
        // This allows appsettings.json to work properly in testnet mode
    }

    /// <summary>
    /// Called after configuration binding to apply environment variable overrides
    /// </summary>
    public void ApplyEnvironmentOverrides()
    {
        // Try to get symbols from environment variable
        var symbolsEnv = Environment.GetEnvironmentVariable("SYMBOLS");
        if (!string.IsNullOrEmpty(symbolsEnv))
        {
            // Parse, sanitize, and validate symbols
            var envSymbols = symbolsEnv
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(SanitizeSymbol) // Sanitize symbols
                .Where(IsValidSymbol) // Filter out invalid symbols
                .Distinct() // Remove duplicates
                .ToList();

            // Only override if we have valid symbols from environment
            if (envSymbols.Count > 0)
            {
                Symbols = envSymbols;
            }
        }

        // Set defaults if no symbols are configured at all (use testnet-friendly subset)
        if (Symbols == null || Symbols.Count == 0)
        {
            Symbols = new List<string> { "BTCUSDT", "ETHUSDT", "NEOUSDT" };
        }

        // Set default symbol mappings if none are configured
        if (SymbolMappings == null || SymbolMappings.Mappings == null || SymbolMappings.Mappings.Count == 0)
        {
            SymbolMappings = new SymbolMappingOptions();
            SymbolMappings.Mappings = new Dictionary<string, Dictionary<string, string>>
            {
                ["BTCUSDT"] = new Dictionary<string, string>
                {
                    ["Binance"] = "BTCUSDT",
                    ["OKEx"] = "BTC-USDT",
                    ["Coinbase"] = "BTC-USD",
                    ["CoinMarketCap"] = "BTC"
                },
                ["ETHUSDT"] = new Dictionary<string, string>
                {
                    ["Binance"] = "ETHUSDT",
                    ["OKEx"] = "ETH-USDT",
                    ["Coinbase"] = "ETH-USD",
                    ["CoinMarketCap"] = "ETH"
                },
                ["NEOUSDT"] = new Dictionary<string, string>
                {
                    ["Binance"] = "NEOUSDT",
                    ["OKEx"] = "NEO-USDT",
                    ["Coinbase"] = "NEO-USD",
                    ["CoinMarketCap"] = "NEO"
                }
            };
        }
    }

    /// <summary>
    /// Validates a symbol format
    /// </summary>
    /// <param name="symbol">The symbol to validate</param>
    /// <returns>True if the symbol is valid</returns>
    private bool IsValidSymbol(string symbol)
    {
        // Symbols should only contain letters, digits, and be in a valid format
        if (string.IsNullOrWhiteSpace(symbol))
            return false;

        // Check length
        if (symbol.Length < 3 || symbol.Length > 10)
            return false;

        // Check characters - only alphanumeric allowed
        if (!symbol.All(c => char.IsLetterOrDigit(c)))
            return false;

        // Check for known valid patterns (e.g., BTCUSDT, ETHBTC)
        // This is a basic check - in production, you might want a more comprehensive validation
        string[] validBaseAssets = { "BTC", "ETH", "NEO", "GAS", "USDT", "USDC" };
        string[] validQuoteAssets = { "BTC", "ETH", "USDT", "USDC" };

        // Check if the symbol follows a valid pattern
        foreach (var baseAsset in validBaseAssets)
        {
            foreach (var quoteAsset in validQuoteAssets)
            {
                if (symbol.Equals($"{baseAsset}{quoteAsset}", StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }

        // If we're here, the symbol didn't match any known pattern
        // Allow symbols that pass the basic checks for flexibility
        return true;
    }

    /// <summary>
    /// Sanitizes a symbol to ensure it's safe for use
    /// </summary>
    /// <param name="symbol">The symbol to sanitize</param>
    /// <returns>The sanitized symbol</returns>
    private string SanitizeSymbol(string symbol)
    {
        if (string.IsNullOrWhiteSpace(symbol))
            return string.Empty;

        // Remove any non-alphanumeric characters
        var sanitized = new string(symbol.Where(char.IsLetterOrDigit).ToArray());

        // Convert to uppercase
        sanitized = sanitized.ToUpperInvariant();

        // Truncate if too long
        if (sanitized.Length > 10)
            sanitized = sanitized.Substring(0, 10);

        return sanitized;
    }
}
