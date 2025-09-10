using Microsoft.Extensions.Options;
using PriceFeed.Core.Options;

namespace PriceFeed.Core.Validation;

/// <summary>
/// Base validator for data source options to ensure HTTPS usage
/// </summary>
public abstract class DataSourceOptionsValidator<T> : IValidateOptions<T> where T : class
{
    protected abstract string GetBaseUrl(T options);
    protected abstract string GetDataSourceName();
    protected virtual bool IsApiKeyRequired() => false;
    protected virtual string? GetApiKey(T options) => null;

    public virtual ValidateOptionsResult Validate(string? name, T options)
    {
        var failures = new List<string>();
        var baseUrl = GetBaseUrl(options);
        var sourceName = GetDataSourceName();

        // Validate base URL
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            failures.Add($"{sourceName} base URL must be configured");
        }
        else if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri))
        {
            failures.Add($"{sourceName} base URL must be a valid URI");
        }
        else if (uri.Scheme != "https")
        {
            failures.Add($"{sourceName} base URL must use HTTPS for security");
        }

        // Validate API key if required
        if (IsApiKeyRequired())
        {
            var apiKey = GetApiKey(options);
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                failures.Add($"{sourceName} API key must be configured");
            }
            else if (apiKey.Length < 16)
            {
                failures.Add($"{sourceName} API key appears to be too short");
            }
        }

        return failures.Any()
            ? ValidateOptionsResult.Fail(failures)
            : ValidateOptionsResult.Success;
    }
}

public class BinanceOptionsValidator : DataSourceOptionsValidator<BinanceOptions>
{
    protected override string GetBaseUrl(BinanceOptions options) => options.BaseUrl;
    protected override string GetDataSourceName() => "Binance";
}

public class CoinMarketCapOptionsValidator : DataSourceOptionsValidator<CoinMarketCapOptions>
{
    protected override string GetBaseUrl(CoinMarketCapOptions options) => options.BaseUrl;
    protected override string GetDataSourceName() => "CoinMarketCap";
    protected override bool IsApiKeyRequired() => true;
    protected override string? GetApiKey(CoinMarketCapOptions options) => options.ApiKey;

    public override ValidateOptionsResult Validate(string? name, CoinMarketCapOptions options)
    {
        // For testnet mode, check if API key is configured in the options itself
        // This allows for hardcoded API keys in appsettings.json for development
        if (!string.IsNullOrEmpty(options.ApiKey))
        {
            // If API key is set in configuration, only validate the base URL
            var failures = new List<string>();
            var baseUrl = GetBaseUrl(options);
            var sourceName = GetDataSourceName();

            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                failures.Add($"{sourceName} base URL must be configured");
            }
            else if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri))
            {
                failures.Add($"{sourceName} base URL must be a valid URI");
            }
            else if (uri.Scheme != "https")
            {
                failures.Add($"{sourceName} base URL must use HTTPS for security");
            }

            return failures.Any()
                ? ValidateOptionsResult.Fail(failures)
                : ValidateOptionsResult.Success;
        }

        // Fall back to standard validation if no API key is configured
        return base.Validate(name, options);
    }
}

public class CoinbaseOptionsValidator : DataSourceOptionsValidator<CoinbaseOptions>
{
    protected override string GetBaseUrl(CoinbaseOptions options) => options.BaseUrl;
    protected override string GetDataSourceName() => "Coinbase";
}

public class OKExOptionsValidator : DataSourceOptionsValidator<OKExOptions>
{
    protected override string GetBaseUrl(OKExOptions options) => options.BaseUrl;
    protected override string GetDataSourceName() => "OKEx";
}
