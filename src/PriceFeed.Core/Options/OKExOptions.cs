namespace PriceFeed.Core.Options;

/// <summary>
/// Configuration options for the OKEx data source adapter
/// </summary>
public class OKExOptions
{
    /// <summary>
    /// The base URL for the OKEx API
    /// </summary>
    public string BaseUrl { get; set; } = "https://www.okex.com";

    /// <summary>
    /// The API endpoint for fetching ticker data
    /// </summary>
    public string TickerEndpoint { get; set; } = "/api/v5/market/ticker";

    /// <summary>
    /// The timeout for API requests in seconds
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// The API key for OKEx (if required)
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// The API secret for OKEx (if required)
    /// </summary>
    public string? ApiSecret { get; set; }

    /// <summary>
    /// The passphrase for OKEx API (if required)
    /// </summary>
    public string? Passphrase { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="OKExOptions"/> class
    /// </summary>
    public OKExOptions()
    {
        // Try to get API key, secret, and passphrase from environment variables
        ApiKey = Environment.GetEnvironmentVariable("OKEX_API_KEY");
        ApiSecret = Environment.GetEnvironmentVariable("OKEX_API_SECRET");
        Passphrase = Environment.GetEnvironmentVariable("OKEX_PASSPHRASE");
    }
}
