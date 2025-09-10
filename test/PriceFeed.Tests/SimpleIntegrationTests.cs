using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using PriceFeed.Core.Interfaces;
using PriceFeed.Core.Models;
using PriceFeed.Core.Options;
using PriceFeed.Infrastructure.Services;
using PriceFeed.Tests.TestUtilities;
using Xunit;
using Xunit.Abstractions;

namespace PriceFeed.Tests;

/// <summary>
/// Simplified integration tests for the complete price feed workflow
/// Tests end-to-end functionality without Neo Express dependencies
/// </summary>
[Collection("Integration")]
public class SimpleIntegrationTests : IDisposable
{
    private readonly ITestOutputHelper _output;

    public SimpleIntegrationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    public void Dispose()
    {
        // Cleanup if needed
    }

    [CITestHelper.IntegrationFact, Trait("Category", "SlowTest")] // Integration test
    public async Task CompleteWorkflow_FetchAggregateAndProcess_ShouldSucceed()
    {
        _output.WriteLine("Testing complete workflow: fetch -> aggregate -> process");

        // Setup test environment
        var host = CreateTestHost();
        var services = host.Services;

        // Step 1: Fetch prices from data sources
        var dataSourceAdapters = services.GetRequiredService<IEnumerable<IDataSourceAdapter>>();
        var symbols = new[] { "BTCUSDT", "ETHUSDT" };

        var allPriceData = new List<PriceData>();
        foreach (var adapter in dataSourceAdapters.Where(a => a.IsEnabled()))
        {
            _output.WriteLine($"Fetching prices from {adapter.SourceName}...");
            var prices = await adapter.GetPriceDataBatchAsync(symbols);
            allPriceData.AddRange(prices);
            _output.WriteLine($"Retrieved {prices.Count()} prices from {adapter.SourceName}");
        }

        Assert.NotEmpty(allPriceData);
        Assert.True(allPriceData.Count >= symbols.Length); // At least one source per symbol

        // Step 2: Aggregate prices
        var aggregationService = services.GetRequiredService<IPriceAggregationService>();

        // Group price data by symbol
        var priceDataBySymbol = allPriceData.GroupBy(p => p.Symbol)
            .ToDictionary(g => g.Key, g => g.AsEnumerable());

        var aggregatedPrices = await aggregationService.AggregateBatchAsync(priceDataBySymbol);
        var pricesList = aggregatedPrices.ToList();
        Assert.Equal(symbols.Length, pricesList.Count);
        foreach (var price in pricesList)
        {
            Assert.True(price.Price > 0);
            Assert.True(price.ConfidenceScore > 0);
            Assert.NotEmpty(price.SourceData);
            _output.WriteLine($"Aggregated {price.Symbol}: ${price.Price} (confidence: {price.ConfidenceScore}%)");
        }

        // Step 3: Create and process batch
        var priceBatch = new PriceBatch
        {
            BatchId = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow,
            Prices = pricesList
        };

        var batchProcessor = services.GetRequiredService<IBatchProcessingService>();
        var result = await batchProcessor.ProcessBatchAsync(priceBatch);

        Assert.True(result);
        _output.WriteLine($"Batch {priceBatch.BatchId} processed successfully");

        // Step 4: Verify batch status
        var batchStatus = await batchProcessor.GetBatchStatusAsync(priceBatch.BatchId);
        Assert.NotNull(batchStatus);
        Assert.Equal(priceBatch.BatchId, batchStatus.BatchId);
        _output.WriteLine($"Batch status: {batchStatus.Status}");

        _output.WriteLine("Complete workflow test passed");
    }

    [CITestHelper.IntegrationFact, Trait("Category", "SlowTest")] // Integration test with external calls
    public async Task DataSourceAdapters_ShouldReturnValidPrices()
    {
        _output.WriteLine("Testing data source adapters individually");

        var host = CreateTestHost();
        var adapters = host.Services.GetRequiredService<IEnumerable<IDataSourceAdapter>>();
        var symbols = new[] { "BTCUSDT", "ETHUSDT" };

        foreach (var adapter in adapters.Where(a => a.IsEnabled()))
        {
            _output.WriteLine($"Testing {adapter.SourceName}...");

            // Test supported symbols
            var supportedSymbols = await adapter.GetSupportedSymbolsAsync();
            Assert.NotEmpty(supportedSymbols);
            _output.WriteLine($"{adapter.SourceName} supports {supportedSymbols.Count()} symbols");

            // Test individual price fetching
            var firstSymbol = symbols.First();
            var singlePrice = await adapter.GetPriceDataAsync(firstSymbol);
            Assert.NotNull(singlePrice);
            Assert.Equal(firstSymbol, singlePrice.Symbol);
            Assert.True(singlePrice.Price > 0);
            Assert.Equal(adapter.SourceName, singlePrice.Source);
            _output.WriteLine($"{adapter.SourceName} - {firstSymbol}: ${singlePrice.Price}");

            // Test batch price fetching
            var batchPrices = await adapter.GetPriceDataBatchAsync(symbols);
            Assert.NotEmpty(batchPrices);
            Assert.True(batchPrices.Count() >= symbols.Length);

            foreach (var price in batchPrices)
            {
                Assert.Contains(price.Symbol, symbols);
                Assert.True(price.Price > 0);
                Assert.Equal(adapter.SourceName, price.Source);
                Assert.True(price.Timestamp <= DateTime.UtcNow);
                _output.WriteLine($"{adapter.SourceName} - {price.Symbol}: ${price.Price}");
            }
        }

        _output.WriteLine("All data source adapters tested successfully");
    }

    [Fact]
    public async Task PriceAggregation_ShouldCalculateCorrectly()
    {
        _output.WriteLine("Testing price aggregation logic");

        var testPrices = new List<PriceData>
        {
            new() { Symbol = "BTCUSDT", Price = 50000m, Source = "Source1", Timestamp = DateTime.UtcNow },
            new() { Symbol = "BTCUSDT", Price = 50100m, Source = "Source2", Timestamp = DateTime.UtcNow },
            new() { Symbol = "BTCUSDT", Price = 49900m, Source = "Source3", Timestamp = DateTime.UtcNow },
            new() { Symbol = "ETHUSDT", Price = 3000m, Source = "Source1", Timestamp = DateTime.UtcNow },
            new() { Symbol = "ETHUSDT", Price = 3050m, Source = "Source2", Timestamp = DateTime.UtcNow }
        };

        var aggregationService = new PriceAggregationService(
            Mock.Of<ILogger<PriceAggregationService>>());

        // Group test prices by symbol
        var priceDataBySymbol = testPrices.GroupBy(p => p.Symbol)
            .ToDictionary(g => g.Key, g => g.AsEnumerable());

        var aggregatedPrices = await aggregationService.AggregateBatchAsync(priceDataBySymbol);
        var pricesList = aggregatedPrices.ToList();

        Assert.Equal(2, pricesList.Count);

        var btcPrice = pricesList.First(p => p.Symbol == "BTCUSDT");
        var ethPrice = pricesList.First(p => p.Symbol == "ETHUSDT");

        // BTC average: (50000 + 50100 + 49900) / 3 = 50000
        Assert.Equal(50000m, btcPrice.Price);
        Assert.Equal(3, btcPrice.SourceData.Count);
        Assert.True(btcPrice.ConfidenceScore > 0);

        // ETH average: (3000 + 3050) / 2 = 3025  
        Assert.Equal(3025m, ethPrice.Price);
        Assert.Equal(2, ethPrice.SourceData.Count);
        Assert.True(ethPrice.ConfidenceScore > 0);

        _output.WriteLine($"BTC aggregated: ${btcPrice.Price} from {btcPrice.SourceData.Count} sources (confidence: {btcPrice.ConfidenceScore})");
        _output.WriteLine($"ETH aggregated: ${ethPrice.Price} from {ethPrice.SourceData.Count} sources (confidence: {ethPrice.ConfidenceScore})");
    }

    [Fact]
    public async Task BatchProcessing_WithLargeBatch_ShouldHandle()
    {
        _output.WriteLine("Testing batch processing with large dataset");

        var host = CreateTestHost();
        var batchProcessor = host.Services.GetRequiredService<IBatchProcessingService>();

        // Create a large batch
        var largeBatch = new PriceBatch
        {
            BatchId = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow,
            Prices = Enumerable.Range(1, 25).Select(i => new AggregatedPriceData
            {
                Symbol = $"TOKEN{i}USDT",
                Price = 100m + i,
                Timestamp = DateTime.UtcNow,
                ConfidenceScore = 95,
                SourceData = new List<PriceData>
                {
                    new() { Symbol = $"TOKEN{i}USDT", Price = 100m + i, Source = "TestSource", Timestamp = DateTime.UtcNow }
                }
            }).ToList()
        };

        var startTime = DateTime.UtcNow;
        var result = await batchProcessor.ProcessBatchAsync(largeBatch);
        var processingTime = DateTime.UtcNow - startTime;

        Assert.True(result);
        Assert.True(processingTime.TotalSeconds < 30); // Should complete within 30 seconds

        _output.WriteLine($"Large batch ({largeBatch.Prices.Count} prices) processed in {processingTime.TotalSeconds:F2}s");

        // Verify batch status
        var status = await batchProcessor.GetBatchStatusAsync(largeBatch.BatchId);
        Assert.NotNull(status);
        Assert.Equal(largeBatch.BatchId, status.BatchId);
        _output.WriteLine($"Batch status: {status.Status}");
    }

    [Fact]
    public async Task ErrorHandling_WithInvalidData_ShouldBeGraceful()
    {
        _output.WriteLine("Testing error handling with invalid data");

        var host = CreateTestHost();
        var services = host.Services;

        // Test aggregation with empty data
        var aggregationService = services.GetRequiredService<IPriceAggregationService>();
        var emptyResult = await aggregationService.AggregateBatchAsync(new Dictionary<string, IEnumerable<PriceData>>());
        Assert.NotNull(emptyResult);
        Assert.Empty(emptyResult.ToList());

        // Test batch processing with empty batch
        var batchProcessor = services.GetRequiredService<IBatchProcessingService>();
        var emptyBatch = new PriceBatch
        {
            BatchId = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow,
            Prices = new List<AggregatedPriceData>()
        };

        var result = await batchProcessor.ProcessBatchAsync(emptyBatch);
        Assert.True(result); // Empty batch should be handled gracefully

        // Test data source with error-prone adapter
        var errorAdapter = new ErrorProneTestAdapter();
        try
        {
            await errorAdapter.GetPriceDataBatchAsync(new[] { "INVALID" });
            Assert.Fail("Should have thrown exception");
        }
        catch (Exception ex)
        {
            Assert.Contains("Error", ex.Message);
            _output.WriteLine($"Expected error handled: {ex.Message}");
        }

        _output.WriteLine("Error handling tests completed");
    }

    private IHost CreateTestHost()
    {
        return Host.CreateDefaultBuilder()
            .ConfigureServices((context, services) =>
            {
                // Configure options
                services.Configure<PriceFeedOptions>(options =>
                {
                    options.Symbols = new List<string> { "BTCUSDT", "ETHUSDT", "ADAUSDT" };
                    options.SymbolMappings = CreateTestSymbolMappings();
                });

                services.Configure<BatchProcessingOptions>(options =>
                {
                    options.RpcEndpoint = "http://localhost:10332";
                    options.ContractScriptHash = "0x1234567890abcdef1234567890abcdef12345678";
                    options.TeeAccountAddress = "NTestAccount1";
                    options.TeeAccountPrivateKey = GenerateTestWIF();
                    options.MasterAccountAddress = "NTestAccount2";
                    options.MasterAccountPrivateKey = GenerateTestWIF();
                    options.MaxBatchSize = 10;
                });

                // Register services
                services.AddHttpClient();
                services.AddSingleton<IPriceAggregationService, PriceAggregationService>();
                services.AddSingleton<IBatchProcessingService>(provider =>
                    new MockBatchProcessingService(_output));
                services.AddSingleton<IAttestationService>(Mock.Of<IAttestationService>());

                // Register test data source adapters
                services.AddSingleton<IDataSourceAdapter>(new TestDataSourceAdapter("Binance", _output));
                services.AddSingleton<IDataSourceAdapter>(new TestDataSourceAdapter("Coinbase", _output));
                services.AddSingleton<IDataSourceAdapter>(new TestDataSourceAdapter("OKEx", _output));

                services.AddSingleton<IEnumerable<IDataSourceAdapter>>(provider =>
                    provider.GetServices<IDataSourceAdapter>());
            })
            .Build();
    }

    private SymbolMappingOptions CreateTestSymbolMappings()
    {
        return new SymbolMappingOptions
        {
            Mappings = new Dictionary<string, Dictionary<string, string>>
            {
                { "BTCUSDT", new() { { "Binance", "BTCUSDT" }, { "Coinbase", "BTC-USD" }, { "OKEx", "BTC-USDT" } } },
                { "ETHUSDT", new() { { "Binance", "ETHUSDT" }, { "Coinbase", "ETH-USD" }, { "OKEx", "ETH-USDT" } } },
                { "ADAUSDT", new() { { "Binance", "ADAUSDT" }, { "Coinbase", "ADA-USD" }, { "OKEx", "ADA-USDT" } } }
            }
        };
    }

    private string GenerateTestWIF()
    {
        return "KxDgvEKzgSBPPfuVfw67oPQBSjidEiqTHURKSDL1R7yGaGYAeYnr";
    }
}

/// <summary>
/// Test data source adapter for integration testing
/// </summary>
public class TestDataSourceAdapter : IDataSourceAdapter
{
    private readonly Random _random = new();
    private readonly ITestOutputHelper _output;

    public string SourceName { get; }

    public TestDataSourceAdapter(string sourceName, ITestOutputHelper output)
    {
        SourceName = sourceName;
        _output = output;
    }

    public bool IsEnabled() => true;

    public Task<IEnumerable<string>> GetSupportedSymbolsAsync()
    {
        var symbols = new[] { "BTCUSDT", "ETHUSDT", "ADAUSDT", "BNBUSDT", "XRPUSDT" };
        return Task.FromResult(symbols.AsEnumerable());
    }

    public Task<PriceData> GetPriceDataAsync(string symbol)
    {
        var priceData = new PriceData
        {
            Symbol = symbol,
            Source = SourceName,
            Price = GenerateRealisticPrice(symbol),
            Volume = _random.Next(1000, 50000),
            Timestamp = DateTime.UtcNow
        };
        return Task.FromResult(priceData);
    }

    public async Task<IEnumerable<PriceData>> GetPriceDataBatchAsync(IEnumerable<string> symbols)
    {
        await Task.Delay(_random.Next(100, 300)); // Simulate network delay

        return symbols.Select(symbol => new PriceData
        {
            Symbol = symbol,
            Source = SourceName,
            Price = GenerateRealisticPrice(symbol),
            Volume = _random.Next(1000, 50000),
            Timestamp = DateTime.UtcNow
        });
    }

    private decimal GenerateRealisticPrice(string symbol)
    {
        var basePrice = symbol switch
        {
            "BTCUSDT" => 50000m,
            "ETHUSDT" => 3000m,
            "ADAUSDT" => 0.5m,
            "BNBUSDT" => 300m,
            "XRPUSDT" => 0.6m,
            _ => 100m
        };

        // Add source-specific variation
        var variation = SourceName switch
        {
            "Binance" => (decimal)(_random.NextDouble() - 0.5) * 0.01m, // ±0.5%
            "Coinbase" => (decimal)(_random.NextDouble() - 0.5) * 0.015m, // ±0.75%
            "OKEx" => (decimal)(_random.NextDouble() - 0.5) * 0.012m, // ±0.6%
            _ => 0m
        };

        return basePrice * (1 + variation);
    }
}

/// <summary>
/// Error-prone adapter for testing error handling
/// </summary>
public class ErrorProneTestAdapter : IDataSourceAdapter
{
    public string SourceName => "ErrorProne";

    public bool IsEnabled() => true;

    public Task<IEnumerable<string>> GetSupportedSymbolsAsync()
    {
        throw new Exception("Error fetching supported symbols");
    }

    public Task<PriceData> GetPriceDataAsync(string symbol)
    {
        throw new Exception("Error fetching price data");
    }

    public Task<IEnumerable<PriceData>> GetPriceDataBatchAsync(IEnumerable<string> symbols)
    {
        throw new Exception("Error fetching batch price data");
    }
}

/// <summary>
/// Mock batch processing service for testing
/// </summary>
public class MockBatchProcessingService : IBatchProcessingService
{
    private readonly ITestOutputHelper _output;
    private readonly Dictionary<Guid, BatchStatusInfo> _batchStatuses = new();

    public MockBatchProcessingService(ITestOutputHelper output)
    {
        _output = output;
    }

    public async Task<bool> ProcessBatchAsync(PriceBatch batch)
    {
        await Task.Delay(100); // Simulate processing time

        _output.WriteLine($"Mock batch processing: {batch.Prices.Count} prices in batch {batch.BatchId}");

        // Store batch status
        _batchStatuses[batch.BatchId] = new BatchStatusInfo
        {
            BatchId = batch.BatchId,
            Status = BatchStatus.Confirmed,
            TransactionHash = "0x" + Guid.NewGuid().ToString("N"),
            Timestamp = DateTime.UtcNow,
            ProcessedCount = batch.Prices.Count,
            TotalCount = batch.Prices.Count
        };

        return true;
    }

    public Task<BatchStatusInfo> GetBatchStatusAsync(Guid batchId)
    {
        if (_batchStatuses.TryGetValue(batchId, out var status))
        {
            return Task.FromResult(status);
        }

        return Task.FromResult(new BatchStatusInfo
        {
            BatchId = batchId,
            Status = BatchStatus.Unknown,
            Timestamp = DateTime.UtcNow
        });
    }
}
