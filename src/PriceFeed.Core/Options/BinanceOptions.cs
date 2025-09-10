namespace PriceFeed.Core.Options;

/// <summary>
/// Configuration options for the Binance data source adapter
/// </summary>
public class BinanceOptions
{
    /// <summary>
    /// The base URL for the Binance API
    /// </summary>
    public string BaseUrl { get; set; } = "https://api.binance.com";

    /// <summary>
    /// The API endpoint for fetching ticker price data
    /// </summary>
    public string TickerPriceEndpoint { get; set; } = "/api/v3/ticker/price";

    /// <summary>
    /// The API endpoint for fetching 24-hour statistics
    /// </summary>
    public string Ticker24hEndpoint { get; set; } = "/api/v3/ticker/24hr";

    /// <summary>
    /// The API endpoint for fetching exchange information
    /// </summary>
    public string ExchangeInfoEndpoint { get; set; } = "/api/v3/exchangeInfo";

    /// <summary>
    /// The timeout for API requests in seconds
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// The API key for Binance
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// The API secret for Binance
    /// </summary>
    public string? ApiSecret { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="BinanceOptions"/> class
    /// </summary>
    public BinanceOptions()
    {
        // Try to get API key and secret from environment variables
        ApiKey = Environment.GetEnvironmentVariable("BINANCE_API_KEY");
        ApiSecret = Environment.GetEnvironmentVariable("BINANCE_API_SECRET");
    }
}
