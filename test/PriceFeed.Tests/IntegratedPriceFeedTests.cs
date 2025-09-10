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
using PriceFeed.Core.Interfaces;
using PriceFeed.Core.Models;
using PriceFeed.Core.Options;
using PriceFeed.Infrastructure.DataSources;
using PriceFeed.Infrastructure.Services;
using Xunit;



namespace PriceFeed.Tests
{
    public class IntegratedPriceFeedTests
    {
        private readonly Mock<ILogger<PriceFeedJob>> _loggerMock;
        private readonly Mock<IOptions<PriceFeedOptions>> _priceFeedOptionsMock;
        private readonly Mock<IOptions<BinanceOptions>> _binanceOptionsMock;
        private readonly Mock<IOptions<CoinMarketCapOptions>> _coinMarketCapOptionsMock;
        private readonly Mock<IOptions<CoinbaseOptions>> _coinbaseOptionsMock;
        private readonly Mock<IOptions<OKExOptions>> _okexOptionsMock;
        private readonly Mock<IOptions<BatchProcessingOptions>> _batchProcessingOptionsMock;
        private readonly Mock<HttpMessageHandler> _httpMessageHandlerMock;
        private readonly Mock<IHttpClientFactory> _httpClientFactoryMock;
        private readonly SymbolMappingOptions _symbolMappings;
        private readonly List<IDataSourceAdapter> _dataSourceAdapters;
        private readonly IPriceAggregationService _aggregationService;
        private readonly Mock<IBatchProcessingService> _batchProcessingServiceMock;
        private readonly Mock<IAttestationService> _attestationServiceMock;
        private readonly PriceFeedJob _priceFeedJob;

        public IntegratedPriceFeedTests()
        {
            _loggerMock = new Mock<ILogger<PriceFeedJob>>();

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
                    }
                }
            };

            _priceFeedOptionsMock = new Mock<IOptions<PriceFeedOptions>>();
            _priceFeedOptionsMock.Setup(o => o.Value).Returns(new PriceFeedOptions
            {
                Symbols = new List<string> { "BTCUSDT", "ETHUSDT" },
                SymbolMappings = _symbolMappings
            });

            // Setup options for data sources
            _binanceOptionsMock = new Mock<IOptions<BinanceOptions>>();
            _binanceOptionsMock.Setup(o => o.Value).Returns(new BinanceOptions
            {
                BaseUrl = "https://api.binance.com",
                ApiKey = "test_api_key",
                ApiSecret = "test_api_secret",
                TimeoutSeconds = 30,
                TickerPriceEndpoint = "/api/v3/ticker/price",
                Ticker24hEndpoint = "/api/v3/ticker/24hr",
                ExchangeInfoEndpoint = "/api/v3/exchangeInfo"
            });

            _coinMarketCapOptionsMock = new Mock<IOptions<CoinMarketCapOptions>>();
            _coinMarketCapOptionsMock.Setup(o => o.Value).Returns(new CoinMarketCapOptions
            {
                BaseUrl = "https://pro-api.coinmarketcap.com",
                ApiKey = "test_api_key",
                LatestQuotesEndpoint = "/v1/cryptocurrency/quotes/latest",
                TimeoutSeconds = 30
            });

            _coinbaseOptionsMock = new Mock<IOptions<CoinbaseOptions>>();
            _coinbaseOptionsMock.Setup(o => o.Value).Returns(new CoinbaseOptions
            {
                BaseUrl = "https://api.coinbase.com",
                ApiKey = "test_api_key",
                ApiSecret = "test_api_secret",
                TimeoutSeconds = 30,
                ExchangeRatesEndpoint = "/v2/exchange-rates",
                SpotPriceEndpoint = "/v2/prices"
            });

            _okexOptionsMock = new Mock<IOptions<OKExOptions>>();
            _okexOptionsMock.Setup(o => o.Value).Returns(new OKExOptions
            {
                BaseUrl = "https://www.okex.com",
                ApiKey = "test_api_key",
                ApiSecret = "test_api_secret",
                Passphrase = "test_passphrase",
                TimeoutSeconds = 30,
                TickerEndpoint = "/api/v5/market/ticker"
            });

            _batchProcessingOptionsMock = new Mock<IOptions<BatchProcessingOptions>>();
            _batchProcessingOptionsMock.Setup(o => o.Value).Returns(new BatchProcessingOptions
            {
                RpcEndpoint = "http://localhost:10332",
                ContractScriptHash = "0xd2a4cff31913016155e38e474a2c06d08be276cf",
                WalletWif = "KxDgvEKzgSBPPfuVfw67oPQBSjidEiqTHURKSDL1R7yGaGYAeYnr",
                MaxBatchSize = 50
            });

            // Setup HTTP client factory
            _httpMessageHandlerMock = new Mock<HttpMessageHandler>();
            _httpClientFactoryMock = new Mock<IHttpClientFactory>();

            // Setup HTTP clients for each data source
            var binanceHttpClient = new HttpClient(_httpMessageHandlerMock.Object)
            {
                BaseAddress = new Uri("https://api.binance.com")
            };

            var coinMarketCapHttpClient = new HttpClient(_httpMessageHandlerMock.Object)
            {
                BaseAddress = new Uri("https://pro-api.coinmarketcap.com")
            };

            var coinbaseHttpClient = new HttpClient(_httpMessageHandlerMock.Object)
            {
                BaseAddress = new Uri("https://api.coinbase.com")
            };

            var okexHttpClient = new HttpClient(_httpMessageHandlerMock.Object)
            {
                BaseAddress = new Uri("https://www.okex.com")
            };

            _httpClientFactoryMock.Setup(f => f.CreateClient("Binance")).Returns(binanceHttpClient);
            _httpClientFactoryMock.Setup(f => f.CreateClient("CoinMarketCap")).Returns(coinMarketCapHttpClient);
            _httpClientFactoryMock.Setup(f => f.CreateClient("Coinbase")).Returns(coinbaseHttpClient);
            _httpClientFactoryMock.Setup(f => f.CreateClient("OKEx")).Returns(okexHttpClient);

            // Setup data source adapters
            var binanceLoggerMock = new Mock<ILogger<BinanceDataSourceAdapter>>();
            var coinMarketCapLoggerMock = new Mock<ILogger<CoinMarketCapDataSourceAdapter>>();
            var coinbaseLoggerMock = new Mock<ILogger<CoinbaseDataSourceAdapter>>();
            var okexLoggerMock = new Mock<ILogger<OKExDataSourceAdapter>>();

            var binanceAdapter = new BinanceDataSourceAdapter(
                _httpClientFactoryMock.Object,
                binanceLoggerMock.Object,
                _binanceOptionsMock.Object,
                _priceFeedOptionsMock.Object);

            var coinMarketCapAdapter = new CoinMarketCapDataSourceAdapter(
                _httpClientFactoryMock.Object,
                coinMarketCapLoggerMock.Object,
                _coinMarketCapOptionsMock.Object,
                _priceFeedOptionsMock.Object);

            var coinbaseAdapter = new CoinbaseDataSourceAdapter(
                _httpClientFactoryMock.Object,
                coinbaseLoggerMock.Object,
                _coinbaseOptionsMock.Object,
                _priceFeedOptionsMock.Object);

            var okexAdapter = new OKExDataSourceAdapter(
                _httpClientFactoryMock.Object,
                okexLoggerMock.Object,
                _okexOptionsMock.Object,
                _priceFeedOptionsMock.Object);

            // Create mock adapters that are always enabled
            var binanceMock = new Mock<IDataSourceAdapter>();
            binanceMock.Setup(a => a.SourceName).Returns("Binance");
            binanceMock.Setup(a => a.IsEnabled()).Returns(true);
            binanceMock.Setup(a => a.GetPriceDataBatchAsync(It.IsAny<IEnumerable<string>>()))
                .Returns<IEnumerable<string>>(symbols => binanceAdapter.GetPriceDataBatchAsync(symbols));

            var coinMarketCapMock = new Mock<IDataSourceAdapter>();
            coinMarketCapMock.Setup(a => a.SourceName).Returns("CoinMarketCap");
            coinMarketCapMock.Setup(a => a.IsEnabled()).Returns(true);
            coinMarketCapMock.Setup(a => a.GetPriceDataBatchAsync(It.IsAny<IEnumerable<string>>()))
                .Returns<IEnumerable<string>>(symbols => coinMarketCapAdapter.GetPriceDataBatchAsync(symbols));

            var coinbaseMock = new Mock<IDataSourceAdapter>();
            coinbaseMock.Setup(a => a.SourceName).Returns("Coinbase");
            coinbaseMock.Setup(a => a.IsEnabled()).Returns(true);
            coinbaseMock.Setup(a => a.GetPriceDataBatchAsync(It.IsAny<IEnumerable<string>>()))
                .Returns<IEnumerable<string>>(symbols => coinbaseAdapter.GetPriceDataBatchAsync(symbols));

            var okexMock = new Mock<IDataSourceAdapter>();
            okexMock.Setup(a => a.SourceName).Returns("OKEx");
            okexMock.Setup(a => a.IsEnabled()).Returns(true);
            okexMock.Setup(a => a.GetPriceDataBatchAsync(It.IsAny<IEnumerable<string>>()))
                .Returns<IEnumerable<string>>(symbols => okexAdapter.GetPriceDataBatchAsync(symbols));

            _dataSourceAdapters = new List<IDataSourceAdapter>
            {
                binanceMock.Object,
                coinMarketCapMock.Object,
                coinbaseMock.Object,
                okexMock.Object
            };

            // Setup price aggregation service
            var aggregationServiceLoggerMock = new Mock<ILogger<PriceAggregationService>>();
            _aggregationService = new PriceAggregationService(aggregationServiceLoggerMock.Object);

            // Setup batch processing service
            _batchProcessingServiceMock = new Mock<IBatchProcessingService>();
            _batchProcessingServiceMock
                .Setup(s => s.ProcessBatchAsync(Moq.It.IsAny<PriceBatch>()))
                .ReturnsAsync(true);

            // Setup attestation service
            _attestationServiceMock = new Mock<IAttestationService>();

            // Create the PriceFeedJob
            _priceFeedJob = new PriceFeedJob(
                _loggerMock.Object,
                _priceFeedOptionsMock.Object,
                _dataSourceAdapters,
                _aggregationService,
                _batchProcessingServiceMock.Object,
                _attestationServiceMock.Object);
        }

        [Fact]
        public async Task RunAsync_WithValidResponses_ShouldProcessBatch()
        {
            // Arrange
            PriceBatch? capturedBatch = null;
            _batchProcessingServiceMock
                .Setup(s => s.ProcessBatchAsync(It.IsAny<PriceBatch>()))
                .Callback<PriceBatch>(batch => capturedBatch = batch)
                .ReturnsAsync(true);

            // Mock Binance responses
            MockBinanceResponses();

            // Mock CoinMarketCap responses
            MockCoinMarketCapResponses();

            // Mock Coinbase responses
            MockCoinbaseResponses();

            // Mock OKEx responses
            MockOKExResponses();

            // Act
            await _priceFeedJob.RunAsync();

            // Assert
            _batchProcessingServiceMock.Verify(
                s => s.ProcessBatchAsync(It.IsAny<PriceBatch>()),
                Times.Once);

            // Verify the batch contains the expected data
            Assert.NotNull(capturedBatch);
            Assert.Equal(2, capturedBatch.Prices.Count); // Should have BTC and ETH

            // Verify BTC price
            var btcPrice = capturedBatch.Prices.FirstOrDefault(p => p.Symbol == "BTCUSDT");
            Assert.NotNull(btcPrice);
            // The aggregated price should be close to the average of all sources
            // (50000 + 50500 + 50200 + 50300) / 4 = 50250
            Assert.True(Math.Abs(btcPrice.Price - 50250m) < 1m, $"Expected price around 50250, but got {btcPrice.Price}");

            // Verify ETH price
            var ethPrice = capturedBatch.Prices.FirstOrDefault(p => p.Symbol == "ETHUSDT");
            Assert.NotNull(ethPrice);
            // The aggregated price should be close to the average of all sources
            // (3000 + 3050 + 3020 + 3030) / 4 = 3025
            Assert.True(Math.Abs(ethPrice.Price - 3025m) < 1m, $"Expected price around 3025, but got {ethPrice.Price}");
        }

        private void MockBinanceResponses()
        {
            // Mock Binance ticker price responses
            var btcTickerPriceResponse = new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(JsonConvert.SerializeObject(new
                {
                    symbol = "BTCUSDT",
                    price = "50000.00"
                }))
            };

            var ethTickerPriceResponse = new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(JsonConvert.SerializeObject(new
                {
                    symbol = "ETHUSDT",
                    price = "3000.00"
                }))
            };

            // Mock Binance 24h ticker responses
            var btcTicker24hResponse = new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(JsonConvert.SerializeObject(new
                {
                    symbol = "BTCUSDT",
                    volume = "1000.00",
                    quoteVolume = "50000000.00",
                    priceChangePercent = "2.5"
                }))
            };

            var ethTicker24hResponse = new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(JsonConvert.SerializeObject(new
                {
                    symbol = "ETHUSDT",
                    volume = "5000.00",
                    quoteVolume = "15000000.00",
                    priceChangePercent = "1.5"
                }))
            };

            _httpMessageHandlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req =>
                        req.RequestUri!.ToString().Contains("api.binance.com") &&
                        req.RequestUri!.ToString().Contains("ticker/price") &&
                        req.RequestUri!.ToString().Contains("BTCUSDT")),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(btcTickerPriceResponse);

            _httpMessageHandlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req =>
                        req.RequestUri!.ToString().Contains("api.binance.com") &&
                        req.RequestUri!.ToString().Contains("ticker/price") &&
                        req.RequestUri!.ToString().Contains("ETHUSDT")),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(ethTickerPriceResponse);

            _httpMessageHandlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req =>
                        req.RequestUri!.ToString().Contains("api.binance.com") &&
                        req.RequestUri!.ToString().Contains("ticker/24hr") &&
                        req.RequestUri!.ToString().Contains("BTCUSDT")),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(btcTicker24hResponse);

            _httpMessageHandlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req =>
                        req.RequestUri!.ToString().Contains("api.binance.com") &&
                        req.RequestUri!.ToString().Contains("ticker/24hr") &&
                        req.RequestUri!.ToString().Contains("ETHUSDT")),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(ethTicker24hResponse);
        }

        private void MockCoinMarketCapResponses()
        {
            // Mock CoinMarketCap responses
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
                                            price = 50500.00,
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
                                            price = 3050.00,
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
                    ItExpr.Is<HttpRequestMessage>(req =>
                        req.RequestUri!.ToString().Contains("coinmarketcap.com") &&
                        req.RequestUri!.ToString().Contains("BTC")),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(btcResponse);

            _httpMessageHandlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req =>
                        req.RequestUri!.ToString().Contains("coinmarketcap.com") &&
                        req.RequestUri!.ToString().Contains("ETH")),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(ethResponse);
        }

        private void MockCoinbaseResponses()
        {
            // Mock Coinbase exchange rates responses (new endpoint)
            var btcExchangeRatesResponse = new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(JsonConvert.SerializeObject(new
                {
                    data = new
                    {
                        currency = "BTC",
                        rates = new Dictionary<string, string>
                        {
                            { "USD", "0.0000199203" } // 1/50200 = 0.0000199203 (rate for BTC to USD)
                        }
                    }
                }))
            };

            var ethExchangeRatesResponse = new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(JsonConvert.SerializeObject(new
                {
                    data = new
                    {
                        currency = "ETH",
                        rates = new Dictionary<string, string>
                        {
                            { "USD", "0.000331126" } // 1/3020 = 0.000331126 (rate for ETH to USD)
                        }
                    }
                }))
            };

            _httpMessageHandlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req =>
                        req.RequestUri!.ToString().Contains("coinbase.com") &&
                        req.RequestUri!.ToString().Contains("exchange-rates") &&
                        req.RequestUri!.ToString().Contains("currency=BTC")),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(btcExchangeRatesResponse);

            _httpMessageHandlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req =>
                        req.RequestUri!.ToString().Contains("coinbase.com") &&
                        req.RequestUri!.ToString().Contains("exchange-rates") &&
                        req.RequestUri!.ToString().Contains("currency=ETH")),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(ethExchangeRatesResponse);
        }

        private void MockOKExResponses()
        {
            // Mock OKEx responses
            var btcResponse = new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(JsonConvert.SerializeObject(new
                {
                    code = "0",
                    msg = "",
                    data = new[]
                    {
                        new
                        {
                            instId = "BTC-USDT",
                            last = "50300.00",
                            vol24h = "1200.00",
                            volCcy24h = "60000000.00",
                            open24h = "49000.00",
                            high24h = "51000.00",
                            low24h = "48500.00"
                        }
                    }
                }))
            };

            var ethResponse = new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(JsonConvert.SerializeObject(new
                {
                    code = "0",
                    msg = "",
                    data = new[]
                    {
                        new
                        {
                            instId = "ETH-USDT",
                            last = "3030.00",
                            vol24h = "5200.00",
                            volCcy24h = "15600000.00",
                            open24h = "2980.00",
                            high24h = "3050.00",
                            low24h = "2950.00"
                        }
                    }
                }))
            };

            _httpMessageHandlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req =>
                        req.RequestUri!.ToString().Contains("okex.com") &&
                        req.RequestUri!.ToString().Contains("BTC-USDT")),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(btcResponse);

            _httpMessageHandlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req =>
                        req.RequestUri!.ToString().Contains("okex.com") &&
                        req.RequestUri!.ToString().Contains("ETH-USDT")),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(ethResponse);
        }
    }
}
