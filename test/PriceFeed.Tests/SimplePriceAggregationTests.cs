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
    public class SimplePriceAggregationTests
    {
        private readonly Mock<ILogger<PriceAggregationService>> _loggerMock;
        private readonly PriceAggregationService _service;

        public SimplePriceAggregationTests()
        {
            _loggerMock = new Mock<ILogger<PriceAggregationService>>();
            _service = new PriceAggregationService(_loggerMock.Object);
        }

        [Fact]
        public async Task AggregateBatchAsync_WithMultipleSources_ShouldReturnAggregatedPrices()
        {
            // Arrange
            var priceDataBySymbol = new Dictionary<string, IEnumerable<PriceData>>
            {
                ["BTCUSDT"] = new List<PriceData>
                {
                    new PriceData { Symbol = "BTCUSDT", Price = 50000, Source = "Binance", Timestamp = DateTime.UtcNow },
                    new PriceData { Symbol = "BTCUSDT", Price = 50100, Source = "Coinbase", Timestamp = DateTime.UtcNow },
                    new PriceData { Symbol = "BTCUSDT", Price = 49900, Source = "OKEx", Timestamp = DateTime.UtcNow }
                },
                ["ETHUSDT"] = new List<PriceData>
                {
                    new PriceData { Symbol = "ETHUSDT", Price = 3000, Source = "Binance", Timestamp = DateTime.UtcNow },
                    new PriceData { Symbol = "ETHUSDT", Price = 3010, Source = "Coinbase", Timestamp = DateTime.UtcNow },
                    new PriceData { Symbol = "ETHUSDT", Price = 2990, Source = "OKEx", Timestamp = DateTime.UtcNow }
                }
            };

            // Act
            var result = await _service.AggregateBatchAsync(priceDataBySymbol);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.Count());

            var btcPrice = result.FirstOrDefault(p => p.Symbol == "BTCUSDT");
            Assert.NotNull(btcPrice);
            Assert.Equal(50000, btcPrice.Price);
            Assert.Equal(100, btcPrice.ConfidenceScore);

            var ethPrice = result.FirstOrDefault(p => p.Symbol == "ETHUSDT");
            Assert.NotNull(ethPrice);
            Assert.Equal(3000, ethPrice.Price);
            Assert.Equal(100, ethPrice.ConfidenceScore);
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
            Assert.NotNull(result);
            Assert.Single(result);

            var btcPrice = result.First();
            Assert.Equal("BTCUSDT", btcPrice.Symbol);
            Assert.Equal(50000, btcPrice.Price);
            Assert.Equal(60, btcPrice.ConfidenceScore); // Lower confidence with single source
        }

        [Fact]
        public async Task AggregateBatchAsync_WithEmptyData_ShouldReturnEmptyList()
        {
            // Arrange
            var priceDataBySymbol = new Dictionary<string, IEnumerable<PriceData>>();

            // Act
            var result = await _service.AggregateBatchAsync(priceDataBySymbol);

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result);
        }
    }
}
