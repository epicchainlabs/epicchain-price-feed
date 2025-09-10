using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;
using PriceFeed.Core.Interfaces;
using PriceFeed.Core.Models;
using PriceFeed.Infrastructure.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace PriceFeed.Tests.Infrastructure.Services;

public class CachedPriceServiceTests
{
    private readonly Mock<IDataSourceAdapter> _mockInnerAdapter;
    private readonly Mock<ILogger<CachedPriceService>> _mockLogger;
    private readonly IMemoryCache _cache;
    private readonly CachedPriceService _cachedService;

    public CachedPriceServiceTests()
    {
        _mockInnerAdapter = new Mock<IDataSourceAdapter>();
        _mockInnerAdapter.Setup(a => a.SourceName).Returns("TestSource");
        _mockLogger = new Mock<ILogger<CachedPriceService>>();
        _cache = new MemoryCache(new MemoryCacheOptions());
        _cachedService = new CachedPriceService(
            _mockInnerAdapter.Object,
            _cache,
            _mockLogger.Object,
            TimeSpan.FromSeconds(30));
    }

    [Fact]
    public void SourceName_ReturnsInnerAdapterSourceName()
    {
        Assert.Equal("TestSource", _cachedService.SourceName);
    }

    [Fact]
    public void IsEnabled_ReturnsInnerAdapterIsEnabled()
    {
        _mockInnerAdapter.Setup(a => a.IsEnabled()).Returns(true);
        Assert.True(_cachedService.IsEnabled());

        _mockInnerAdapter.Setup(a => a.IsEnabled()).Returns(false);
        Assert.False(_cachedService.IsEnabled());
    }

    [Fact]
    public async Task GetPriceDataAsync_CacheMiss_FetchesFromInnerAdapter()
    {
        // Arrange
        var expectedPrice = new PriceData { Symbol = "BTC", Price = 50000 };
        _mockInnerAdapter.Setup(a => a.GetPriceDataAsync("BTC"))
            .ReturnsAsync(expectedPrice);

        // Act
        var result = await _cachedService.GetPriceDataAsync("BTC");

        // Assert
        Assert.Equal(expectedPrice, result);
        _mockInnerAdapter.Verify(a => a.GetPriceDataAsync("BTC"), Times.Once);
    }

    [Fact]
    public async Task GetPriceDataAsync_CacheHit_DoesNotFetchFromInnerAdapter()
    {
        // Arrange
        var expectedPrice = new PriceData { Symbol = "BTC", Price = 50000 };
        _cache.Set("TestSource:price:BTC", expectedPrice);

        // Act
        var result = await _cachedService.GetPriceDataAsync("BTC");

        // Assert
        Assert.Equal(expectedPrice, result);
        _mockInnerAdapter.Verify(a => a.GetPriceDataAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task GetPriceDataAsync_SuccessfulFetch_CachesResult()
    {
        // Arrange
        var expectedPrice = new PriceData { Symbol = "BTC", Price = 50000 };
        _mockInnerAdapter.Setup(a => a.GetPriceDataAsync("BTC"))
            .ReturnsAsync(expectedPrice);

        // Act
        await _cachedService.GetPriceDataAsync("BTC");

        // Assert
        Assert.True(_cache.TryGetValue("TestSource:price:BTC", out PriceData? cachedValue));
        Assert.Equal(expectedPrice, cachedValue);
    }

    [Fact]
    public async Task GetPriceDataAsync_InvalidPrice_DoesNotCache()
    {
        // Arrange
        var invalidPrice = new PriceData { Symbol = "BTC", Price = 0 };
        _mockInnerAdapter.Setup(a => a.GetPriceDataAsync("BTC"))
            .ReturnsAsync(invalidPrice);

        // Act
        await _cachedService.GetPriceDataAsync("BTC");

        // Assert
        Assert.False(_cache.TryGetValue("TestSource:price:BTC", out _));
    }


    [Fact]
    public async Task GetPriceDataAsync_ExceptionWithoutStaleData_ThrowsException()
    {
        // Arrange
        _mockInnerAdapter.Setup(a => a.GetPriceDataAsync("BTC"))
            .ThrowsAsync(new Exception("Service unavailable"));

        // Act & Assert
        await Assert.ThrowsAsync<Exception>(() => _cachedService.GetPriceDataAsync("BTC"));
    }

    [Fact]
    public async Task GetSupportedSymbolsAsync_CachesForLongerDuration()
    {
        // Arrange
        var symbols = new[] { "BTC", "ETH", "NEO" };
        _mockInnerAdapter.Setup(a => a.GetSupportedSymbolsAsync())
            .ReturnsAsync(symbols);

        // Act
        var result1 = await _cachedService.GetSupportedSymbolsAsync();
        var result2 = await _cachedService.GetSupportedSymbolsAsync();

        // Assert
        Assert.Equal(symbols, result1);
        Assert.Equal(symbols, result2);
        _mockInnerAdapter.Verify(a => a.GetSupportedSymbolsAsync(), Times.Once);
    }

    [Fact]
    public async Task GetPriceDataBatchAsync_MixOfCachedAndFresh_OptimizesFetching()
    {
        // Arrange
        var cachedPrice = new PriceData { Symbol = "BTC", Price = 50000 };
        _cache.Set("TestSource:price:BTC", cachedPrice);

        var freshPrices = new[]
        {
            new PriceData { Symbol = "ETH", Price = 3000 },
            new PriceData { Symbol = "NEO", Price = 50 }
        };
        _mockInnerAdapter.Setup(a => a.GetPriceDataBatchAsync(It.Is<IEnumerable<string>>(
                s => s.Count() == 2 && s.Contains("ETH") && s.Contains("NEO"))))
            .ReturnsAsync(freshPrices);

        // Act
        var result = await _cachedService.GetPriceDataBatchAsync(new[] { "BTC", "ETH", "NEO" });

        // Assert
        Assert.Equal(3, result.Count());
        Assert.Contains(result, p => p.Symbol == "BTC" && p.Price == 50000);
        Assert.Contains(result, p => p.Symbol == "ETH" && p.Price == 3000);
        Assert.Contains(result, p => p.Symbol == "NEO" && p.Price == 50);
        _mockInnerAdapter.Verify(a => a.GetPriceDataBatchAsync(It.IsAny<IEnumerable<string>>()), Times.Once);
    }

    [Fact]
    public async Task GetPriceDataBatchAsync_AllCached_DoesNotFetchFromInnerAdapter()
    {
        // Arrange
        _cache.Set("TestSource:price:BTC", new PriceData { Symbol = "BTC", Price = 50000 });
        _cache.Set("TestSource:price:ETH", new PriceData { Symbol = "ETH", Price = 3000 });

        // Act
        var result = await _cachedService.GetPriceDataBatchAsync(new[] { "BTC", "ETH" });

        // Assert
        Assert.Equal(2, result.Count());
        _mockInnerAdapter.Verify(a => a.GetPriceDataBatchAsync(It.IsAny<IEnumerable<string>>()), Times.Never);
    }

    [Fact]
    public async Task GetPriceDataBatchAsync_BatchFetchFailsWithNoCache_ThrowsException()
    {
        // Arrange
        _mockInnerAdapter.Setup(a => a.GetPriceDataBatchAsync(It.IsAny<IEnumerable<string>>()))
            .ThrowsAsync(new Exception("Service unavailable"));

        // Act & Assert
        await Assert.ThrowsAsync<Exception>(() =>
            _cachedService.GetPriceDataBatchAsync(new[] { "BTC", "ETH", "NEO" }));
    }

    [Fact]
    public async Task GetPriceDataBatchAsync_NullPriceData_NotAddedToResults()
    {
        // Arrange
        var prices = new[]
        {
            new PriceData { Symbol = "BTC", Price = 50000 },
            null,
            new PriceData { Symbol = "ETH", Price = 3000 }
        };
        _mockInnerAdapter.Setup(a => a.GetPriceDataBatchAsync(It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync(prices!);

        // Act
        var result = await _cachedService.GetPriceDataBatchAsync(new[] { "BTC", "ETH", "NEO" });

        // Assert
        Assert.Equal(2, result.Count());
        Assert.All(result, p => Assert.NotNull(p));
    }
}
