using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PriceFeed.Core.Options;
using PriceFeed.Infrastructure.DataSources;
using System.Net.Http;
using Xunit;

namespace PriceFeed.Tests;

public class KrakenDataSourceAdapterRealApiTests
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly KrakenOptions _options;
    private readonly PriceFeedOptions _priceFeedOptions;

    public KrakenDataSourceAdapterRealApiTests()
    {
        _httpClientFactory = new HttpClientFactory();
        _options = new KrakenOptions
        {
            BaseUrl = "https://api.kraken.com",
            TickerEndpoint = "/0/public/Ticker",
            AssetPairsEndpoint = "/0/public/AssetPairs",
            TimeoutSeconds = 30
        };

        _priceFeedOptions = new PriceFeedOptions
        {
            SymbolMappings = new SymbolMappingOptions
            {
                Mappings = new Dictionary<string, Dictionary<string, string>>
                {
                    {
                        "BTCUSDT", new Dictionary<string, string>
                        {
                            { "Kraken", "XXBTZUSD" }
                        }
                    },
                    {
                        "ETHUSDT", new Dictionary<string, string>
                        {
                            { "Kraken", "XETHZUSD" }
                        }
                    }
                }
            }
        };
    }

    [Fact]
    public async Task GetPriceDataAsync_WithRealApi_ShouldReturnValidPriceData()
    {
        // Arrange
        var adapter = new KrakenDataSourceAdapter(
            _httpClientFactory,
            NullLogger<KrakenDataSourceAdapter>.Instance,
            Options.Create(_options),
            Options.Create(_priceFeedOptions));

        // Act
        var result = await adapter.GetPriceDataAsync("BTCUSDT");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("BTCUSDT", result.Symbol);
        Assert.True(result.Price > 0, "Price should be greater than 0");
        Assert.Equal("Kraken", result.Source);
        Assert.True(result.Volume > 0, "Volume should be greater than 0");
    }

    [Fact]
    public async Task GetSupportedSymbolsAsync_WithRealApi_ShouldReturnSupportedSymbols()
    {
        // Arrange
        var adapter = new KrakenDataSourceAdapter(
            _httpClientFactory,
            NullLogger<KrakenDataSourceAdapter>.Instance,
            Options.Create(_options),
            Options.Create(_priceFeedOptions));

        // Act
        var result = await adapter.GetSupportedSymbolsAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Contains("BTCUSDT", result);
        Assert.Contains("ETHUSDT", result);
    }

    [Fact]
    public async Task GetPriceDataAsync_WithUnsupportedSymbol_ShouldThrowException()
    {
        // Arrange
        var adapter = new KrakenDataSourceAdapter(
            _httpClientFactory,
            NullLogger<KrakenDataSourceAdapter>.Instance,
            Options.Create(_options),
            Options.Create(_priceFeedOptions));

        // Act & Assert
        await Assert.ThrowsAsync<Exception>(() => adapter.GetPriceDataAsync("UNSUPPORTED"));
    }

    private class HttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name)
        {
            return new HttpClient();
        }
    }
}
