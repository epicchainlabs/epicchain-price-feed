using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using PriceFeed.Core.Models;
using PriceFeed.Infrastructure.Services;
using Xunit;

namespace PriceFeed.Tests
{
    public class PriceAggregationServiceTests
    {
        private readonly Mock<ILogger<PriceAggregationService>> _loggerMock;
        private readonly PriceAggregationService _service;

        public PriceAggregationServiceTests()
        {
            _loggerMock = new Mock<ILogger<PriceAggregationService>>();
            _service = new PriceAggregationService(_loggerMock.Object);
        }

        [Fact]
        public async Task AggregateBatchAsync_WithValidData_ShouldAggregateCorrectly()
        {
            // Arrange
            var priceDataBySymbol = new Dictionary<string, IEnumerable<PriceData>>
            {
                ["BTCUSDT"] = new List<PriceData>
                {
                    new PriceData { Symbol = "BTCUSDT", Price = 50000, Source = "Binance", Timestamp = DateTime.UtcNow },
                    new PriceData { Symbol = "BTCUSDT", Price = 50100, Source = "Coinbase", Timestamp = DateTime.UtcNow },
                    new PriceData { Symbol = "BTCUSDT", Price = 50200, Source = "CoinMarketCap", Timestamp = DateTime.UtcNow }
                },
                ["ETHBTC"] = new List<PriceData>
                {
                    new PriceData { Symbol = "ETHBTC", Price = 0.07m, Source = "Binance", Timestamp = DateTime.UtcNow },
                    new PriceData { Symbol = "ETHBTC", Price = 0.071m, Source = "Coinbase", Timestamp = DateTime.UtcNow }
                }
            };

            // Act
            var result = await _service.AggregateBatchAsync(priceDataBySymbol);

            // Assert
            Assert.Equal(2, result.Count());

            var btcPrice = result.First(p => p.Symbol == "BTCUSDT");
            Assert.Equal(50100, btcPrice.Price); // Should be the median of 50000, 50100, 50200
            Assert.Equal(100, btcPrice.ConfidenceScore); // High confidence with 3 sources

            var ethPrice = result.First(p => p.Symbol == "ETHBTC");
            Assert.Equal(0.0705m, ethPrice.Price); // Should be the average of 0.07 and 0.071
            Assert.Equal(80, ethPrice.ConfidenceScore); // Medium confidence with 2 sources
        }

        [Fact]
        public async Task AggregateBatchAsync_WithOutliers_ShouldFilterThem()
        {
            // Arrange
            var priceDataBySymbol = new Dictionary<string, IEnumerable<PriceData>>
            {
                ["BTCUSDT"] = new List<PriceData>
                {
                    new PriceData { Symbol = "BTCUSDT", Price = 50000, Source = "Binance", Timestamp = DateTime.UtcNow },
                    new PriceData { Symbol = "BTCUSDT", Price = 50100, Source = "Coinbase", Timestamp = DateTime.UtcNow },
                    new PriceData { Symbol = "BTCUSDT", Price = 60000, Source = "CoinMarketCap", Timestamp = DateTime.UtcNow } // Outlier
                }
            };

            // Act
            var result = await _service.AggregateBatchAsync(priceDataBySymbol);

            // Assert
            Assert.Single(result);

            var btcPrice = result.First();
            Assert.Equal("BTCUSDT", btcPrice.Symbol);
            Assert.Equal(50050, btcPrice.Price); // Should be the average of 50000 and 50100, excluding the outlier
            Assert.Equal(80, btcPrice.ConfidenceScore); // Medium confidence with 2 sources after filtering
        }

        [Fact]
        public async Task AggregateBatchAsync_WithSingleSource_ShouldHaveLowerConfidence()
        {
            // Arrange
            var priceDataBySymbol = new Dictionary<string, IEnumerable<PriceData>>
            {
                ["BTCUSDT"] = new List<PriceData>
                {
                    new PriceData { Symbol = "BTCUSDT", Price = 50000, Source = "Binance", Timestamp = DateTime.UtcNow }
                }
            };

            // Act
            var result = await _service.AggregateBatchAsync(priceDataBySymbol);

            // Assert
            Assert.Single(result);

            var btcPrice = result.First();
            Assert.Equal("BTCUSDT", btcPrice.Symbol);
            Assert.Equal(50000, btcPrice.Price);
            Assert.Equal(60, btcPrice.ConfidenceScore); // Lower confidence with only 1 source
        }

        [Fact]
        public async Task AggregateBatchAsync_WithEmptyData_ShouldReturnEmptyResult()
        {
            // Arrange
            var priceDataBySymbol = new Dictionary<string, IEnumerable<PriceData>>();

            // Act
            var result = await _service.AggregateBatchAsync(priceDataBySymbol);

            // Assert
            Assert.Empty(result);
        }

        [Fact]
        public async Task AggregateBatchAsync_WithExtremeOutliers_ShouldFilterAllAndUseMedian()
        {
            // Arrange
            var priceDataBySymbol = new Dictionary<string, IEnumerable<PriceData>>
            {
                ["BTCUSDT"] = new List<PriceData>
                {
                    new PriceData { Symbol = "BTCUSDT", Price = 50000, Source = "Binance", Timestamp = DateTime.UtcNow },
                    new PriceData { Symbol = "BTCUSDT", Price = 5000, Source = "Coinbase", Timestamp = DateTime.UtcNow }, // 10x lower outlier
                    new PriceData { Symbol = "BTCUSDT", Price = 500000, Source = "CoinMarketCap", Timestamp = DateTime.UtcNow }, // 10x higher outlier
                    new PriceData { Symbol = "BTCUSDT", Price = 49500, Source = "OKEx", Timestamp = DateTime.UtcNow },
                    new PriceData { Symbol = "BTCUSDT", Price = 50500, Source = "Huobi", Timestamp = DateTime.UtcNow }
                }
            };

            // Act
            var result = await _service.AggregateBatchAsync(priceDataBySymbol);

            // Assert
            Assert.Single(result);

            var btcPrice = result.First();
            Assert.Equal("BTCUSDT", btcPrice.Symbol);

            // Should be the median of the non-outlier values (50000)
            // or close to the average of the three middle values (49500, 50000, 50500) = 50000
            Assert.Equal(50000, btcPrice.Price);

            // High confidence with 3 sources after filtering extreme outliers
            Assert.Equal(100, btcPrice.ConfidenceScore);
        }

        [Fact]
        public async Task AggregateBatchAsync_WithAllSourcesAgreeing_ShouldHaveHighestConfidence()
        {
            // Arrange
            var priceDataBySymbol = new Dictionary<string, IEnumerable<PriceData>>
            {
                ["BTCUSDT"] = new List<PriceData>
                {
                    new PriceData { Symbol = "BTCUSDT", Price = 50000, Source = "Binance", Timestamp = DateTime.UtcNow },
                    new PriceData { Symbol = "BTCUSDT", Price = 50001, Source = "Coinbase", Timestamp = DateTime.UtcNow },
                    new PriceData { Symbol = "BTCUSDT", Price = 49999, Source = "CoinMarketCap", Timestamp = DateTime.UtcNow },
                    new PriceData { Symbol = "BTCUSDT", Price = 50002, Source = "OKEx", Timestamp = DateTime.UtcNow }
                }
            };

            // Act
            var result = await _service.AggregateBatchAsync(priceDataBySymbol);

            // Assert
            Assert.Single(result);

            var btcPrice = result.First();
            Assert.Equal("BTCUSDT", btcPrice.Symbol);

            // Should be very close to 50000 (the median/average of the values)
            Assert.Equal(50000.5m, btcPrice.Price);

            // Highest confidence with 4 sources all in agreement
            Assert.Equal(100, btcPrice.ConfidenceScore);
        }
    }
}
