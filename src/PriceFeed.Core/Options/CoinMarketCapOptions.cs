namespace PriceFeed.Core.Options;

/// <summary>
/// Configuration options for the CoinMarketCap data source adapter
/// </summary>
public class CoinMarketCapOptions
{
    /// <summary>
    /// The base URL for the CoinMarketCap API
    /// </summary>
    public string BaseUrl { get; set; } = "https://pro-api.coinmarketcap.com";

    /// <summary>
    /// The API endpoint for fetching latest quotes
    /// </summary>
    public string LatestQuotesEndpoint { get; set; } = "/v1/cryptocurrency/quotes/latest";

    /// <summary>
    /// The API key for CoinMarketCap
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// The timeout for API requests in seconds
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Initializes a new instance of the <see cref="CoinMarketCapOptions"/> class
    /// </summary>
    public CoinMarketCapOptions()
    {
        // Environment variables will only override configuration if explicitly set
        // This allows appsettings.json to work properly in testnet mode
    }

    /// <summary>
    /// Called after configuration binding to apply environment variable overrides
    /// </summary>
    public void ApplyEnvironmentOverrides()
    {
        var envApiKey = Environment.GetEnvironmentVariable("COINMARKETCAP_API_KEY");
        if (!string.IsNullOrEmpty(envApiKey))
        {
            ApiKey = envApiKey;
        }
    }
}
