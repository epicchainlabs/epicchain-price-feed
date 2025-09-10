using System;
using System.Collections.Generic;
using System.Linq;
using PriceFeed.Core.Options;

namespace PriceFeed.Console
{
    /// <summary>
    /// Simple test class to verify that the symbol mappings work correctly
    /// </summary>
    public static class SymbolMappingTest
    {
        /// <summary>
        /// Runs a simple test to verify that the symbol mappings work correctly
        /// </summary>
        public static void RunTest()
        {
            System.Console.WriteLine("Running symbol mapping test...");

            // Create a sample symbol mappings configuration
            var symbolMappings = new SymbolMappingOptions
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
                        "NEOUSDT", new Dictionary<string, string>
                        {
                            { "Binance", "NEOUSDT" },
                            { "OKEx", "NEO-USDT" },
                            { "Coinbase", "" }, // Not supported by Coinbase
                            { "CoinMarketCap", "NEO" }
                        }
                    }
                }
            };

            // Test GetSourceSymbol
            System.Console.WriteLine("\nTesting GetSourceSymbol:");
            TestGetSourceSymbol(symbolMappings, "BTCUSDT", "Binance", "BTCUSDT");
            TestGetSourceSymbol(symbolMappings, "BTCUSDT", "OKEx", "BTC-USDT");
            TestGetSourceSymbol(symbolMappings, "BTCUSDT", "Coinbase", "BTC-USD");
            TestGetSourceSymbol(symbolMappings, "BTCUSDT", "CoinMarketCap", "BTC");
            TestGetSourceSymbol(symbolMappings, "BTCUSDT", "Unknown", "BTCUSDT"); // Should return standard symbol

            // Test IsSymbolSupportedBySource
            System.Console.WriteLine("\nTesting IsSymbolSupportedBySource:");
            TestIsSymbolSupportedBySource(symbolMappings, "BTCUSDT", "Binance", true);
            TestIsSymbolSupportedBySource(symbolMappings, "BTCUSDT", "OKEx", true);
            TestIsSymbolSupportedBySource(symbolMappings, "BTCUSDT", "Coinbase", true);
            TestIsSymbolSupportedBySource(symbolMappings, "BTCUSDT", "CoinMarketCap", true);
            TestIsSymbolSupportedBySource(symbolMappings, "BTCUSDT", "Unknown", true); // Should return true by default
            TestIsSymbolSupportedBySource(symbolMappings, "NEOUSDT", "Coinbase", false); // Explicitly not supported

            // Test GetSymbolsForDataSource
            System.Console.WriteLine("\nTesting GetSymbolsForDataSource:");
            TestGetSymbolsForDataSource(symbolMappings, "Binance", new[] { "BTCUSDT", "ETHUSDT", "NEOUSDT" });
            TestGetSymbolsForDataSource(symbolMappings, "OKEx", new[] { "BTCUSDT", "ETHUSDT", "NEOUSDT" });
            TestGetSymbolsForDataSource(symbolMappings, "Coinbase", new[] { "BTCUSDT", "ETHUSDT" }); // NEOUSDT is not supported
            TestGetSymbolsForDataSource(symbolMappings, "CoinMarketCap", new[] { "BTCUSDT", "ETHUSDT", "NEOUSDT" });

            System.Console.WriteLine("\nSymbol mapping test completed successfully!");
        }

        private static void TestGetSourceSymbol(SymbolMappingOptions symbolMappings, string standardSymbol, string dataSource, string expectedResult)
        {
            var result = symbolMappings.GetSourceSymbol(standardSymbol, dataSource);
            var success = result == expectedResult;

            System.Console.WriteLine($"  GetSourceSymbol({standardSymbol}, {dataSource}) = {result} ... {(success ? "PASS" : "FAIL")}");

            if (!success)
            {
                System.Console.WriteLine($"    Expected: {expectedResult}, Actual: {result}");
            }
        }

        private static void TestIsSymbolSupportedBySource(SymbolMappingOptions symbolMappings, string standardSymbol, string dataSource, bool expectedResult)
        {
            var result = symbolMappings.IsSymbolSupportedBySource(standardSymbol, dataSource);
            var success = result == expectedResult;

            System.Console.WriteLine($"  IsSymbolSupportedBySource({standardSymbol}, {dataSource}) = {result} ... {(success ? "PASS" : "FAIL")}");

            if (!success)
            {
                System.Console.WriteLine($"    Expected: {expectedResult}, Actual: {result}");
            }
        }

        private static void TestGetSymbolsForDataSource(SymbolMappingOptions symbolMappings, string dataSource, string[] expectedSymbols)
        {
            var result = symbolMappings.GetSymbolsForDataSource(dataSource);
            var success = result.Count == expectedSymbols.Length &&
                          expectedSymbols.All(s => result.Contains(s));

            System.Console.WriteLine($"  GetSymbolsForDataSource({dataSource}) = {string.Join(", ", result)} ... {(success ? "PASS" : "FAIL")}");

            if (!success)
            {
                System.Console.WriteLine($"    Expected: {string.Join(", ", expectedSymbols)}, Actual: {string.Join(", ", result)}");
            }
        }
    }
}
