namespace PriceFeed.Core.Options;

/// <summary>
/// Configuration options for the Kraken data source adapter
/// </summary>
public class KrakenOptions
{
    /// <summary>
    /// The base URL for the Kraken API
    /// </summary>
    public string BaseUrl { get; set; } = "https://api.kraken.com";

    /// <summary>
    /// The API endpoint for fetching ticker data
    /// </summary>
    public string TickerEndpoint { get; set; } = "/0/public/Ticker";

    /// <summary>
    /// The API endpoint for fetching asset pairs
    /// </summary>
    public string AssetPairsEndpoint { get; set; } = "/0/public/AssetPairs";

    /// <summary>
    /// The timeout for API requests in seconds
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// The API key for Kraken (optional for public endpoints)
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// The API secret for Kraken (optional for public endpoints)
    /// </summary>
    public string? ApiSecret { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="KrakenOptions"/> class
    /// </summary>
    public KrakenOptions()
    {
        // Try to get API key and secret from environment variables (optional for public endpoints)
        ApiKey = Environment.GetEnvironmentVariable("KRAKEN_API_KEY");
        ApiSecret = Environment.GetEnvironmentVariable("KRAKEN_API_SECRET");
    }
}
