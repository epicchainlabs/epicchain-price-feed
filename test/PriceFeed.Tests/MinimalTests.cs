using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using PriceFeed.Core.Models;
using PriceFeed.Infrastructure.Services;
using Xunit;

namespace PriceFeed.Tests
{
    public class MinimalTests
    {
        [Fact]
        public void SimpleTest_ShouldPass()
        {
            // Arrange
            var expected = 2;

            // Act
            var actual = 1 + 1;

            // Assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void PriceData_ShouldBeCreatable()
        {
            // Arrange & Act
            var priceData = new PriceData
            {
                Symbol = "BTCUSDT",
                Price = 50000,
                Source = "Binance",
                Timestamp = DateTime.UtcNow
            };

            // Assert
            Assert.Equal("BTCUSDT", priceData.Symbol);
            Assert.Equal(50000, priceData.Price);
            Assert.Equal("Binance", priceData.Source);
        }
    }
}
