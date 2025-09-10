using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PriceFeed.Core.Models;
using PriceFeed.Core.Interfaces;
using PriceFeed.Infrastructure.Services;
using PriceFeed.Core.Options;
using System;
using System.Threading.Tasks;
using Serilog;

namespace PriceFeed.Console
{
    public static class MockPriceFeedTest
    {
        public static async Task<int> RunTest()
        {
            Log.Information("Running mock price feed test...");

            // Create a test batch with mock data
            var batch = new PriceBatch
            {
                BatchId = Guid.NewGuid(),
                Prices = new List<AggregatedPriceData>
                {
                    new AggregatedPriceData
                    {
                        Symbol = "BTCUSDT",
                        Price = 50000.00m,
                        Timestamp = DateTime.UtcNow,
                        ConfidenceScore = 95,
                        SourceData = new List<PriceData>
                        {
                            new PriceData
                            {
                                Symbol = "BTCUSDT",
                                Price = 50000.00m,
                                Volume = 1000000.00m,
                                Timestamp = DateTime.UtcNow,
                                Source = "CoinGecko",
                                Metadata = new Dictionary<string, string>
                                {
                                    { "PriceChange24h", "2.5" }
                                }
                            },
                            new PriceData
                            {
                                Symbol = "BTCUSDT",
                                Price = 50000.50m,
                                Volume = 1200000.00m,
                                Timestamp = DateTime.UtcNow,
                                Source = "Kraken",
                                Metadata = new Dictionary<string, string>
                                {
                                    { "LastTradePrice", "50000.49" }
                                }
                            }
                        }
                    },
                    new AggregatedPriceData
                    {
                        Symbol = "ETHUSDT",
                        Price = 4000.00m,
                        Timestamp = DateTime.UtcNow,
                        ConfidenceScore = 95,
                        SourceData = new List<PriceData>
                        {
                            new PriceData
                            {
                                Symbol = "ETHUSDT",
                                Price = 4000.00m,
                                Volume = 800000.00m,
                                Timestamp = DateTime.UtcNow,
                                Source = "CoinGecko",
                                Metadata = new Dictionary<string, string>
                                {
                                    { "PriceChange24h", "1.5" }
                                }
                            },
                            new PriceData
                            {
                                Symbol = "ETHUSDT",
                                Price = 4000.50m,
                                Volume = 900000.00m,
                                Timestamp = DateTime.UtcNow,
                                Source = "Kraken",
                                Metadata = new Dictionary<string, string>
                                {
                                    { "LastTradePrice", "4000.49" }
                                }
                            }
                        }
                    }
                }
            };

            // Process the mock batch
            var mockHost = Host.CreateDefaultBuilder()
                .UseSerilog()
                .ConfigureServices((_, services) =>
                {
                    // Add required services
                    services.AddHttpClient();
                    services.AddMemoryCache();
                    services.AddScoped<IAttestationService, AttestationService>();

                    // Configure HTTP clients
                    services.AddHttpClient("CoinGecko", client =>
                    {
                        client.BaseAddress = new Uri("https://api.coingecko.com");
                        client.Timeout = TimeSpan.FromSeconds(30);
                    });

                    services.AddHttpClient("Kraken", client =>
                    {
                        client.BaseAddress = new Uri("https://api.kraken.com");
                        client.Timeout = TimeSpan.FromSeconds(30);
                    });

                    services.AddHttpClient("Neo", client =>
                    {
                        client.BaseAddress = new Uri("https://localhost:10332");
                        client.Timeout = TimeSpan.FromSeconds(30);
                    });

                    // Add batch processing service
                    services.AddScoped<IBatchProcessingService, BatchProcessingService>();

                    // Add required options
                    services.Configure<BatchProcessingOptions>(options =>
                    {
                        options.RpcEndpoint = "https://localhost:10332";
                        options.ContractScriptHash = "0x0000000000000000000000000000000000000000";
                        options.TeeAccountAddress = "NQPBWiGruFDpRrGNMDuGHLa7HkSZAZDk9x";
                        options.TeeAccountPrivateKey = "L1QqQJnpBwbsPGAuutuzPTac8piqvbR1HRjrY5qHup48TBCBFe4g";
                        options.MasterAccountAddress = "NZMyYadiW93JrpUxj7758BDppnND4KUu6X";
                        options.MasterAccountPrivateKey = "L2QqQJnpBwbsPGAuutuzPTac8piqvbR1HRjrY5qHup48TBCBFe4g";
                        options.MaxBatchSize = 50;
                        options.CheckAndTransferTeeAssets = true;
                    });
                })
                .Build();

            var batchProcessingService = mockHost.Services.GetRequiredService<IBatchProcessingService>();

            try
            {
                await batchProcessingService.ProcessBatchAsync(batch);
                Log.Information("Mock price feed test completed successfully");
                return 0;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error processing mock price feed batch");
                return 1;
            }
        }
    }
}
