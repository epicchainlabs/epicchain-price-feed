using System.Collections.Generic;
using PriceFeed.Core.Options;
using Xunit;

namespace PriceFeed.Tests
{
    public class SymbolMappingOptionsTests
    {
        [Fact]
        public void GetSourceSymbol_WithExistingMapping_ShouldReturnMappedSymbol()
        {
            // Arrange
            var options = new SymbolMappingOptions
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
                    }
                }
            };

            // Act
            var binanceSymbol = options.GetSourceSymbol("BTCUSDT", "Binance");
            var okexSymbol = options.GetSourceSymbol("BTCUSDT", "OKEx");
            var coinbaseSymbol = options.GetSourceSymbol("BTCUSDT", "Coinbase");
            var cmcSymbol = options.GetSourceSymbol("BTCUSDT", "CoinMarketCap");

            // Assert
            Assert.Equal("BTCUSDT", binanceSymbol);
            Assert.Equal("BTC-USDT", okexSymbol);
            Assert.Equal("BTC-USD", coinbaseSymbol);
            Assert.Equal("BTC", cmcSymbol);
        }

        [Fact]
        public void GetSourceSymbol_WithNonExistingMapping_ShouldReturnStandardSymbol()
        {
            // Arrange
            var options = new SymbolMappingOptions
            {
                Mappings = new Dictionary<string, Dictionary<string, string>>
                {
                    {
                        "BTCUSDT", new Dictionary<string, string>
                        {
                            { "Binance", "BTCUSDT" }
                        }
                    }
                }
            };

            // Act
            var okexSymbol = options.GetSourceSymbol("BTCUSDT", "OKEx");
            var nonExistingSymbol = options.GetSourceSymbol("ETHUSDT", "Binance");

            // Assert
            Assert.Equal("BTCUSDT", okexSymbol); // Falls back to standard symbol
            Assert.Equal("ETHUSDT", nonExistingSymbol); // Falls back to standard symbol
        }

        [Fact]
        public void IsSymbolSupportedBySource_WithExplicitMapping_ShouldReturnTrue()
        {
            // Arrange
            var options = new SymbolMappingOptions
            {
                Mappings = new Dictionary<string, Dictionary<string, string>>
                {
                    {
                        "BTCUSDT", new Dictionary<string, string>
                        {
                            { "Binance", "BTCUSDT" },
                            { "OKEx", "BTC-USDT" }
                        }
                    }
                }
            };

            // Act
            var isBinanceSupported = options.IsSymbolSupportedBySource("BTCUSDT", "Binance");
            var isOKExSupported = options.IsSymbolSupportedBySource("BTCUSDT", "OKEx");

            // Assert
            Assert.True(isBinanceSupported);
            Assert.True(isOKExSupported);
        }

        [Fact]
        public void IsSymbolSupportedBySource_WithEmptyMapping_ShouldReturnFalse()
        {
            // Arrange
            var options = new SymbolMappingOptions
            {
                Mappings = new Dictionary<string, Dictionary<string, string>>
                {
                    {
                        "BTCUSDT", new Dictionary<string, string>
                        {
                            { "Binance", "BTCUSDT" },
                            { "OKEx", "" } // Empty string indicates not supported
                        }
                    }
                }
            };

            // Act
            var isBinanceSupported = options.IsSymbolSupportedBySource("BTCUSDT", "Binance");
            var isOKExSupported = options.IsSymbolSupportedBySource("BTCUSDT", "OKEx");

            // Assert
            Assert.True(isBinanceSupported);
            Assert.False(isOKExSupported);
        }

        [Fact]
        public void IsSymbolSupportedBySource_WithNoMapping_ShouldReturnTrue()
        {
            // Arrange
            var options = new SymbolMappingOptions
            {
                Mappings = new Dictionary<string, Dictionary<string, string>>
                {
                    {
                        "BTCUSDT", new Dictionary<string, string>
                        {
                            { "Binance", "BTCUSDT" }
                        }
                    }
                }
            };

            // Act
            var isCoinbaseSupported = options.IsSymbolSupportedBySource("BTCUSDT", "Coinbase");

            // Assert
            Assert.True(isCoinbaseSupported); // By default, assume supported if no explicit mapping
        }

        [Fact]
        public void GetAllStandardSymbols_ShouldReturnAllMappedSymbols()
        {
            // Arrange
            var options = new SymbolMappingOptions
            {
                Mappings = new Dictionary<string, Dictionary<string, string>>
                {
                    {
                        "BTCUSDT", new Dictionary<string, string>()
                    },
                    {
                        "ETHUSDT", new Dictionary<string, string>()
                    }
                }
            };

            // Act
            var symbols = options.GetAllStandardSymbols();

            // Assert
            Assert.Equal(2, symbols.Count);
            Assert.Contains("BTCUSDT", symbols);
            Assert.Contains("ETHUSDT", symbols);
        }

        [Fact]
        public void GetSymbolsForDataSource_ShouldReturnSupportedSymbols()
        {
            // Arrange
            var options = new SymbolMappingOptions
            {
                Mappings = new Dictionary<string, Dictionary<string, string>>
                {
                    {
                        "BTCUSDT", new Dictionary<string, string>
                        {
                            { "Binance", "BTCUSDT" },
                            { "OKEx", "BTC-USDT" }
                        }
                    },
                    {
                        "ETHUSDT", new Dictionary<string, string>
                        {
                            { "Binance", "ETHUSDT" }
                        }
                    },
                    {
                        "NEOUSDT", new Dictionary<string, string>
                        {
                            { "OKEx", "NEO-USDT" }
                        }
                    }
                }
            };

            // Act
            var binanceSymbols = options.GetSymbolsForDataSource("Binance");
            var okexSymbols = options.GetSymbolsForDataSource("OKEx");

            // Assert
            Assert.Equal(3, binanceSymbols.Count); // BTCUSDT, ETHUSDT, NEOUSDT (default behavior)
            Assert.Equal(3, okexSymbols.Count); // BTCUSDT, ETHUSDT (default behavior), NEOUSDT
        }
    }
}
