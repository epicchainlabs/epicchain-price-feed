using System;
using Microsoft.Extensions.Options;
using PriceFeed.Core.Options;
using Xunit;

namespace PriceFeed.Tests
{
    public class PriceFeedOptionsTests
    {
        [Fact]
        public void ApplyEnvironmentOverrides_WithValidSymbols_ShouldParseCorrectly()
        {
            // Arrange
            Environment.SetEnvironmentVariable("SYMBOLS", "BTCUSDT,ETHBTC,NEOBTC");

            // Act
            var options = new PriceFeedOptions();
            options.ApplyEnvironmentOverrides();

            // Assert
            Assert.Equal(3, options.Symbols.Count);
            Assert.Contains("BTCUSDT", options.Symbols);
            Assert.Contains("ETHBTC", options.Symbols);
            Assert.Contains("NEOBTC", options.Symbols);

            // Cleanup
            Environment.SetEnvironmentVariable("SYMBOLS", null);
        }

        [Fact]
        public void ApplyEnvironmentOverrides_WithInvalidSymbols_ShouldFilterThem()
        {
            // Arrange
            Environment.SetEnvironmentVariable("SYMBOLS", "BTCUSDT,ETH-BTC,123,AB");

            // Act
            var options = new PriceFeedOptions();
            options.ApplyEnvironmentOverrides();

            // Assert - sanitized symbols
            Assert.Contains("BTCUSDT", options.Symbols);
            Assert.Contains("123", options.Symbols); // Valid after sanitization
            Assert.Contains("ETHBTC", options.Symbols); // ETH-BTC becomes ETHBTC
            Assert.DoesNotContain("AB", options.Symbols); // Too short after sanitization

            // Cleanup
            Environment.SetEnvironmentVariable("SYMBOLS", null);
        }

        [Fact]
        public void ApplyEnvironmentOverrides_WithNoSymbols_ShouldUseDefaults()
        {
            // Arrange
            Environment.SetEnvironmentVariable("SYMBOLS", null);

            // Act
            var options = new PriceFeedOptions();
            options.ApplyEnvironmentOverrides();

            // Assert - default symbols from ApplyEnvironmentOverrides
            Assert.Equal(3, options.Symbols.Count);
            Assert.Contains("BTCUSDT", options.Symbols);
            Assert.Contains("ETHUSDT", options.Symbols);
            Assert.Contains("NEOUSDT", options.Symbols);
        }

        [Fact]
        public void ApplyEnvironmentOverrides_WithDuplicateSymbols_ShouldRemoveDuplicates()
        {
            // Arrange
            Environment.SetEnvironmentVariable("SYMBOLS", "BTCUSDT,btcusdt,ETHBTC");

            // Act
            var options = new PriceFeedOptions();
            options.ApplyEnvironmentOverrides();

            // Assert
            Assert.Equal(2, options.Symbols.Count);
            Assert.Contains("BTCUSDT", options.Symbols);
            Assert.Contains("ETHBTC", options.Symbols);

            // Cleanup
            Environment.SetEnvironmentVariable("SYMBOLS", null);
        }
    }
}
