using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PriceFeed.Core.Options;
using PriceFeed.Infrastructure.DataSources;
using System.Net.Http;
using Xunit;

namespace PriceFeed.Tests;

public class CoinGeckoDataSourceAdapterRealApiTests
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly CoinGeckoOptions _options;
    private readonly PriceFeedOptions _priceFeedOptions;

    public CoinGeckoDataSourceAdapterRealApiTests()
    {
        _httpClientFactory = new HttpClientFactory();
        _options = new CoinGeckoOptions
        {
            BaseUrl = "https://api.coingecko.com",
            SimplePriceEndpoint = "/api/v3/simple/price",
            CoinListEndpoint = "/api/v3/coins/list",
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
                            { "CoinGecko", "bitcoin" }
                        }
                    },
                    {
                        "ETHUSDT", new Dictionary<string, string>
                        {
                            { "CoinGecko", "ethereum" }
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
        var adapter = new CoinGeckoDataSourceAdapter(
            _httpClientFactory,
            NullLogger<CoinGeckoDataSourceAdapter>.Instance,
            Options.Create(_options),
            Options.Create(_priceFeedOptions));

        // Act
        var result = await adapter.GetPriceDataAsync("BTCUSDT");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("BTCUSDT", result.Symbol);
        Assert.True(result.Price > 0, "Price should be greater than 0");
        Assert.Equal("CoinGecko", result.Source);
        Assert.True(result.Volume > 0, "Volume should be greater than 0");
        Assert.True(result.Metadata.ContainsKey("PriceChange24h"), "Metadata should contain 24h price change");
    }

    [Fact]
    public async Task GetSupportedSymbolsAsync_WithRealApi_ShouldReturnSupportedSymbols()
    {
        // Arrange
        var adapter = new CoinGeckoDataSourceAdapter(
            _httpClientFactory,
            NullLogger<CoinGeckoDataSourceAdapter>.Instance,
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
        var adapter = new CoinGeckoDataSourceAdapter(
            _httpClientFactory,
            NullLogger<CoinGeckoDataSourceAdapter>.Instance,
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
