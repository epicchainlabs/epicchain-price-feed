using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using PriceFeed.Core.Options;
using PriceFeed.Infrastructure.DataSources;
using System.Net;
using System.Text;
using Xunit;

namespace PriceFeed.Tests;

public class KrakenDataSourceAdapterTests
{
    private readonly Mock<ILogger<KrakenDataSourceAdapter>> _mockLogger;
    private readonly Mock<IHttpClientFactory> _mockHttpClientFactory;
    private readonly Mock<HttpMessageHandler> _mockHttpMessageHandler;
    private readonly HttpClient _httpClient;
    private readonly KrakenOptions _options;
    private readonly PriceFeedOptions _priceFeedOptions;

    public KrakenDataSourceAdapterTests()
    {
        _mockLogger = new Mock<ILogger<KrakenDataSourceAdapter>>();
        _mockHttpClientFactory = new Mock<IHttpClientFactory>();
        _mockHttpMessageHandler = new Mock<HttpMessageHandler>();

        _httpClient = new HttpClient(_mockHttpMessageHandler.Object);
        _mockHttpClientFactory.Setup(x => x.CreateClient("Kraken")).Returns(_httpClient);

        _options = new KrakenOptions();
        _priceFeedOptions = new PriceFeedOptions
        {
            SymbolMappings = new SymbolMappingOptions
            {
                Mappings = new Dictionary<string, Dictionary<string, string>>
                {
                    {
                        "BTCUSDT", new Dictionary<string, string>
                        {
                            { "Kraken", "XBTUSD" }
                        }
                    }
                }
            }
        };
    }

    [Fact]
    public void IsEnabled_ShouldReturnTrue()
    {
        // Arrange
        var adapter = new KrakenDataSourceAdapter(
            _mockHttpClientFactory.Object,
            _mockLogger.Object,
            Options.Create(_options),
            Options.Create(_priceFeedOptions));

        // Act
        var result = adapter.IsEnabled();

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void SourceName_ShouldReturnKraken()
    {
        // Arrange
        var adapter = new KrakenDataSourceAdapter(
            _mockHttpClientFactory.Object,
            _mockLogger.Object,
            Options.Create(_options),
            Options.Create(_priceFeedOptions));

        // Act
        var result = adapter.SourceName;

        // Assert
        Assert.Equal("Kraken", result);
    }

    [Fact]
    public async Task GetPriceDataAsync_WithValidSymbol_ShouldReturnPriceData()
    {
        // Arrange
        var responseContent = """
        {
            "error": [],
            "result": {
                "XXBTZUSD": {
                    "a": ["45000.50", "1", "1.000"],
                    "b": ["45000.00", "2", "2.000"],
                    "c": ["45000.25", "0.12345678"],
                    "v": ["123.45678901", "1234.56789012"],
                    "p": ["45000.12", "44999.88"],
                    "t": [1234, 12345],
                    "l": ["44900.00", "44800.00"],
                    "h": ["45100.00", "45200.00"],
                    "o": "44950.00"
                }
            }
        }
        """;

        _mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(responseContent, Encoding.UTF8, "application/json")
            });

        var adapter = new KrakenDataSourceAdapter(
            _mockHttpClientFactory.Object,
            _mockLogger.Object,
            Options.Create(_options),
            Options.Create(_priceFeedOptions));

        // Act
        var result = await adapter.GetPriceDataAsync("BTCUSDT");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("BTCUSDT", result.Symbol);
        Assert.Equal(45000.25m, result.Price);
        Assert.Equal("Kraken", result.Source);
        Assert.Equal(1234.56789012m, result.Volume);
        Assert.True(result.Metadata.ContainsKey("Ask"));
        Assert.Equal("45000.50", result.Metadata["Ask"]);
        Assert.True(result.Metadata.ContainsKey("Bid"));
        Assert.Equal("45000.00", result.Metadata["Bid"]);
    }

    [Fact]
    public async Task GetPriceDataAsync_WithUnsupportedSymbol_ShouldThrowException()
    {
        // Arrange
        var adapter = new KrakenDataSourceAdapter(
            _mockHttpClientFactory.Object,
            _mockLogger.Object,
            Options.Create(_options),
            Options.Create(_priceFeedOptions));

        // Act & Assert
        // The adapter checks for symbol support first, then makes HTTP call
        // Since HTTP mock isn't set up, it throws InvalidOperationException
        await Assert.ThrowsAsync<InvalidOperationException>(() => adapter.GetPriceDataAsync("UNSUPPORTED"));
    }

    [Fact]
    public async Task GetSupportedSymbolsAsync_ShouldReturnMappedSymbols()
    {
        // Arrange
        var adapter = new KrakenDataSourceAdapter(
            _mockHttpClientFactory.Object,
            _mockLogger.Object,
            Options.Create(_options),
            Options.Create(_priceFeedOptions));

        // Act
        var result = await adapter.GetSupportedSymbolsAsync();

        // Assert
        Assert.Contains("BTCUSDT", result);
    }

    [Fact]
    public async Task GetPriceDataBatchAsync_WithHttpError_ShouldFallbackToIndividualRequests()
    {
        // Arrange
        var responseContent = """
        {
            "error": [],
            "result": {
                "XXBTZUSD": {
                    "a": ["45000.50", "1", "1.000"],
                    "b": ["45000.00", "2", "2.000"],
                    "c": ["45000.25", "0.12345678"],
                    "v": ["123.45678901", "1234.56789012"],
                    "p": ["45000.12", "44999.88"],
                    "t": [1234, 12345],
                    "l": ["44900.00", "44800.00"],
                    "h": ["45100.00", "45200.00"],
                    "o": "44950.00"
                }
            }
        }
        """;

        // First call fails (batch), second call succeeds (individual)
        _mockHttpMessageHandler.Protected()
            .SetupSequence<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.InternalServerError
            })
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(responseContent, Encoding.UTF8, "application/json")
            });

        var adapter = new KrakenDataSourceAdapter(
            _mockHttpClientFactory.Object,
            _mockLogger.Object,
            Options.Create(_options),
            Options.Create(_priceFeedOptions));

        // Act
        var result = await adapter.GetPriceDataBatchAsync(new[] { "BTCUSDT" });

        // Assert
        Assert.NotEmpty(result);
        var priceData = result.First();
        Assert.Equal("BTCUSDT", priceData.Symbol);
        Assert.Equal(45000.25m, priceData.Price);
        Assert.Equal("Kraken", priceData.Source);
    }
}
