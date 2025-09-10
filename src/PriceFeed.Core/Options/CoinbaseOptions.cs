namespace PriceFeed.Core.Options;

/// <summary>
/// Configuration options for the Coinbase data source adapter
/// </summary>
public class CoinbaseOptions
{
    /// <summary>
    /// The base URL for the Coinbase API
    /// </summary>
    public string BaseUrl { get; set; } = "https://api.coinbase.com";

    /// <summary>
    /// The API endpoint for fetching exchange rates
    /// </summary>
    public string ExchangeRatesEndpoint { get; set; } = "/v2/exchange-rates";

    /// <summary>
    /// The API endpoint for fetching spot prices
    /// </summary>
    public string SpotPriceEndpoint { get; set; } = "/v2/prices";

    /// <summary>
    /// The timeout for API requests in seconds
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// The API key for Coinbase (if required)
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// The API secret for Coinbase (if required)
    /// </summary>
    public string? ApiSecret { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="CoinbaseOptions"/> class
    /// </summary>
    public CoinbaseOptions()
    {
        // Try to get API key and secret from environment variables
        ApiKey = Environment.GetEnvironmentVariable("COINBASE_API_KEY");
        ApiSecret = Environment.GetEnvironmentVariable("COINBASE_API_SECRET");
    }
}
