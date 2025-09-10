using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using Newtonsoft.Json;
using PriceFeed.Core.Models;
using PriceFeed.Core.Options;
using PriceFeed.Infrastructure.DataSources;
using Xunit;



namespace PriceFeed.Tests
{
    public class CoinMarketCapDataSourceAdapterTests
    {
        private readonly Mock<ILogger<CoinMarketCapDataSourceAdapter>> _loggerMock;
        private readonly Mock<IOptions<CoinMarketCapOptions>> _optionsMock;
        private readonly Mock<IOptions<PriceFeedOptions>> _priceFeedOptionsMock;
        private readonly Mock<HttpMessageHandler> _httpMessageHandlerMock;
        private readonly HttpClient _httpClient;
        private readonly Mock<IHttpClientFactory> _httpClientFactoryMock;
        private readonly CoinMarketCapDataSourceAdapter _adapter;
        private readonly SymbolMappingOptions _symbolMappings;

        public CoinMarketCapDataSourceAdapterTests()
        {
            _loggerMock = new Mock<ILogger<CoinMarketCapDataSourceAdapter>>();

            _optionsMock = new Mock<IOptions<CoinMarketCapOptions>>();
            _optionsMock.Setup(o => o.Value).Returns(new CoinMarketCapOptions
            {
                BaseUrl = "https://pro-api.coinmarketcap.com",
                ApiKey = "test_api_key",
                LatestQuotesEndpoint = "/v1/cryptocurrency/quotes/latest",
                TimeoutSeconds = 30
            });

            // Setup symbol mappings
            _symbolMappings = new SymbolMappingOptions
            {
                Mappings = new Dictionary<string, Dictionary<string, string>>
                {
                    {
                        "BTCUSDT", new Dictionary<string, string>
                        {
                            { "Binance", "BTCUSDT" },
                            { "OKEx", "BTC-USDT" },
                            { "Coinbase", "BTC-USD" },
                            { "CoinMarketCap", "BTC" }
                        }
                    },
                    {
                        "ETHUSDT", new Dictionary<string, string>
                        {
                            { "Binance", "ETHUSDT" },
                            { "OKEx", "ETH-USDT" },
                            { "Coinbase", "ETH-USD" },
                            { "CoinMarketCap", "ETH" }
                        }
                    },
                    {
                        "NEOBTC", new Dictionary<string, string>
                        {
                            { "Binance", "NEOBTC" },
                            { "CoinMarketCap", "NEO" }
                        }
                    }
                }
            };

            _priceFeedOptionsMock = new Mock<IOptions<PriceFeedOptions>>();
            _priceFeedOptionsMock.Setup(o => o.Value).Returns(new PriceFeedOptions
            {
                Symbols = new List<string> { "BTCUSDT", "ETHUSDT", "NEOBTC" },
                SymbolMappings = _symbolMappings
            });

            _httpMessageHandlerMock = new Mock<HttpMessageHandler>();
            _httpClient = new HttpClient(_httpMessageHandlerMock.Object)
            {
                BaseAddress = new Uri("https://pro-api.coinmarketcap.com")
            };

            _httpClientFactoryMock = new Mock<IHttpClientFactory>();
            _httpClientFactoryMock.Setup(f => f.CreateClient("CoinMarketCap")).Returns(_httpClient);

            _adapter = new CoinMarketCapDataSourceAdapter(
                _httpClientFactoryMock.Object,
                _loggerMock.Object,
                _optionsMock.Object,
                _priceFeedOptionsMock.Object);
        }

        [Fact]
        public async Task GetPriceDataAsync_WithValidResponse_ShouldReturnPriceData()
        {
            // Arrange
            var symbol = "BTCUSDT";

            // Mock the HTTP response for BTC
            var btcResponse = new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(JsonConvert.SerializeObject(new
                {
                    data = new Dictionary<string, object>
                    {
                        {
                            "BTC", new
                            {
                                id = 1,
                                name = "Bitcoin",
                                symbol = "BTC",
                                quote = new Dictionary<string, object>
                                {
                                    {
                                        "USD", new
                                        {
                                            price = 50000.00,
                                            volume_24h = 30000000000.00,
                                            market_cap = 950000000000.00,
                                            percent_change_24h = 2.5
                                        }
                                    }
                                }
                            }
                        }
                    }
                }))
            };

            _httpMessageHandlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().Contains("quotes/latest") && req.RequestUri!.ToString().Contains("BTC")),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(btcResponse);

            // Act
            var result = await _adapter.GetPriceDataAsync(symbol);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(symbol, result.Symbol);
            Assert.Equal(50000m, result.Price);
            Assert.Equal("CoinMarketCap", result.Source);
            Assert.True(result.Metadata.ContainsKey("SourceSymbol"));
            Assert.Equal("BTC", result.Metadata["SourceSymbol"]);
        }

        [Fact]
        public async Task GetPriceDataBatchAsync_WithValidResponse_ShouldReturnPriceData()
        {
            // Arrange
            var symbols = new[] { "BTCUSDT", "ETHUSDT" };

            // Mock the HTTP response for BTC
            var btcResponse = new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(JsonConvert.SerializeObject(new
                {
                    data = new Dictionary<string, object>
                    {
                        {
                            "BTC", new
                            {
                                id = 1,
                                name = "Bitcoin",
                                symbol = "BTC",
                                quote = new Dictionary<string, object>
                                {
                                    {
                                        "USD", new
                                        {
                                            price = 50000.00,
                                            volume_24h = 30000000000.00,
                                            market_cap = 950000000000.00,
                                            percent_change_24h = 2.5
                                        }
                                    }
                                }
                            }
                        }
                    }
                }))
            };

            // Mock the HTTP response for ETH
            var ethResponse = new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(JsonConvert.SerializeObject(new
                {
                    data = new Dictionary<string, object>
                    {
                        {
                            "ETH", new
                            {
                                id = 1027,
                                name = "Ethereum",
                                symbol = "ETH",
                                quote = new Dictionary<string, object>
                                {
                                    {
                                        "USD", new
                                        {
                                            price = 3000.00,
                                            volume_24h = 15000000000.00,
                                            market_cap = 350000000000.00,
                                            percent_change_24h = 1.5
                                        }
                                    }
                                }
                            }
                        }
                    }
                }))
            };

            _httpMessageHandlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().Contains("quotes/latest") && req.RequestUri!.ToString().Contains("BTC")),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(btcResponse);

            _httpMessageHandlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().Contains("quotes/latest") && req.RequestUri!.ToString().Contains("ETH")),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(ethResponse);

            // Act
            var result = await _adapter.GetPriceDataBatchAsync(symbols);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.Count());

            var btcPrice = result.FirstOrDefault(p => p.Symbol == "BTCUSDT");
            Assert.NotNull(btcPrice);
            Assert.Equal(50000m, btcPrice.Price);
            Assert.Equal("CoinMarketCap", btcPrice.Source);
            Assert.True(btcPrice.Metadata.ContainsKey("SourceSymbol"));
            Assert.Equal("BTC", btcPrice.Metadata["SourceSymbol"]);

            var ethPrice = result.FirstOrDefault(p => p.Symbol == "ETHUSDT");
            Assert.NotNull(ethPrice);
            Assert.Equal(3000m, ethPrice.Price);
            Assert.Equal("CoinMarketCap", ethPrice.Source);
            Assert.True(ethPrice.Metadata.ContainsKey("SourceSymbol"));
            Assert.Equal("ETH", ethPrice.Metadata["SourceSymbol"]);
        }

        [Fact]
        public async Task GetPriceDataBatchAsync_WithUnsupportedSymbols_ShouldFilterThem()
        {
            // Arrange
            // Create a new adapter with a modified symbol mapping that only supports BTCUSDT
            var symbolMappings = new SymbolMappingOptions
            {
                Mappings = new Dictionary<string, Dictionary<string, string>>
                {
                    {
                        "BTCUSDT", new Dictionary<string, string>
                        {
                            { "CoinMarketCap", "BTC" }
                        }
                    },
                    {
                        "ETHUSDT", new Dictionary<string, string>
                        {
                            { "CoinMarketCap", "" } // Empty string means not supported
                        }
                    }
                }
            };

            var priceFeedOptionsMock = new Mock<IOptions<PriceFeedOptions>>();
            priceFeedOptionsMock.Setup(o => o.Value).Returns(new PriceFeedOptions
            {
                Symbols = new List<string> { "BTCUSDT", "ETHUSDT" },
                SymbolMappings = symbolMappings
            });

            var adapter = new CoinMarketCapDataSourceAdapter(
                _httpClientFactoryMock.Object,
                _loggerMock.Object,
                _optionsMock.Object,
                priceFeedOptionsMock.Object);

            // Mock the HTTP response for BTC
            var btcResponse = new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(JsonConvert.SerializeObject(new
                {
                    data = new Dictionary<string, object>
                    {
                        {
                            "BTC", new
                            {
                                id = 1,
                                name = "Bitcoin",
                                symbol = "BTC",
                                quote = new Dictionary<string, object>
                                {
                                    {
                                        "USD", new
                                        {
                                            price = 50000.00,
                                            volume_24h = 30000000000.00,
                                            market_cap = 950000000000.00,
                                            percent_change_24h = 2.5
                                        }
                                    }
                                }
                            }
                        }
                    }
                }))
            };

            _httpMessageHandlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().Contains("quotes/latest") && req.RequestUri!.ToString().Contains("BTC")),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(btcResponse);

            // Act
            var result = await adapter.GetPriceDataBatchAsync(new[] { "BTCUSDT", "ETHUSDT" });

            // Assert
            Assert.NotNull(result);
            Assert.Single(result); // Only BTCUSDT should be returned

            var btcPrice = result.FirstOrDefault();
            Assert.NotNull(btcPrice);
            Assert.Equal("BTCUSDT", btcPrice.Symbol);
            Assert.Equal(50000m, btcPrice.Price);
        }

        [Fact]
        public async Task GetSupportedSymbolsAsync_ShouldReturnMappedSymbols()
        {
            // Act
            var result = await _adapter.GetSupportedSymbolsAsync();

            // Assert
            Assert.NotNull(result);
            Assert.Equal(3, result.Count());
            Assert.Contains("BTCUSDT", result);
            Assert.Contains("ETHUSDT", result);
            Assert.Contains("NEOBTC", result);
        }
    }
}
