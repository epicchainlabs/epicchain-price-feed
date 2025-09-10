namespace PriceFeed.Core.Options;

/// <summary>
/// Configuration options for the CoinGecko data source adapter
/// </summary>
public class CoinGeckoOptions
{
    /// <summary>
    /// The base URL for the CoinGecko API
    /// </summary>
    public string BaseUrl { get; set; } = "https://api.coingecko.com";

    /// <summary>
    /// The API endpoint for fetching simple prices
    /// </summary>
    public string SimplePriceEndpoint { get; set; } = "/api/v3/simple/price";

    /// <summary>
    /// The API endpoint for fetching coin list
    /// </summary>
    public string CoinListEndpoint { get; set; } = "/api/v3/coins/list";

    /// <summary>
    /// The timeout for API requests in seconds
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// The API key for CoinGecko Pro (optional for free tier)
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="CoinGeckoOptions"/> class
    /// </summary>
    public CoinGeckoOptions()
    {
        // Try to get API key from environment variable (optional for free tier)
        ApiKey = Environment.GetEnvironmentVariable("COINGECKO_API_KEY");
    }
}
