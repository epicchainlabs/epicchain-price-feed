using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using PriceFeed.Core.Interfaces;
using PriceFeed.Core.Models;
using PriceFeed.Infrastructure.HealthChecks;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace PriceFeed.Tests.Infrastructure.HealthChecks;

public class DataSourceHealthCheckTests
{
    private readonly Mock<ILogger<DataSourceHealthCheck>> _mockLogger;
    private readonly List<IDataSourceAdapter> _adapters;
    private readonly DataSourceHealthCheck _healthCheck;

    public DataSourceHealthCheckTests()
    {
        _mockLogger = new Mock<ILogger<DataSourceHealthCheck>>();
        _adapters = new List<IDataSourceAdapter>();
        _healthCheck = new DataSourceHealthCheck(_adapters, _mockLogger.Object);
    }

    [Fact]
    public async Task CheckHealthAsync_AllAdaptersWorking_ReturnsHealthy()
    {
        // Arrange
        var mockAdapter1 = new Mock<IDataSourceAdapter>();
        mockAdapter1.Setup(a => a.SourceName).Returns("Source1");
        mockAdapter1.Setup(a => a.IsEnabled()).Returns(true);
        mockAdapter1.Setup(a => a.GetPriceDataAsync(It.IsAny<string>()))
            .ReturnsAsync(new PriceData { Symbol = "BTC", Price = 50000 });

        var mockAdapter2 = new Mock<IDataSourceAdapter>();
        mockAdapter2.Setup(a => a.SourceName).Returns("Source2");
        mockAdapter2.Setup(a => a.IsEnabled()).Returns(true);
        mockAdapter2.Setup(a => a.GetPriceDataAsync(It.IsAny<string>()))
            .ReturnsAsync(new PriceData { Symbol = "BTC", Price = 50100 });

        _adapters.Add(mockAdapter1.Object);
        _adapters.Add(mockAdapter2.Object);

        // Act
        var result = await _healthCheck.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

        // Assert
        Assert.Equal(HealthStatus.Healthy, result.Status);
        Assert.Contains("2/2 data sources healthy", result.Description);
    }

    [Fact]
    public async Task CheckHealthAsync_SomeAdaptersWorking_ReturnsDegraded()
    {
        // Arrange - 2 out of 4 = 50% which should be degraded
        var mockAdapter1 = new Mock<IDataSourceAdapter>();
        mockAdapter1.Setup(a => a.SourceName).Returns("Source1");
        mockAdapter1.Setup(a => a.IsEnabled()).Returns(true);
        mockAdapter1.Setup(a => a.GetPriceDataAsync(It.IsAny<string>()))
            .ReturnsAsync(new PriceData { Symbol = "BTC", Price = 50000 });

        var mockAdapter2 = new Mock<IDataSourceAdapter>();
        mockAdapter2.Setup(a => a.SourceName).Returns("Source2");
        mockAdapter2.Setup(a => a.IsEnabled()).Returns(true);
        mockAdapter2.Setup(a => a.GetPriceDataAsync(It.IsAny<string>()))
            .ReturnsAsync(new PriceData { Symbol = "BTC", Price = 50100 });

        var mockAdapter3 = new Mock<IDataSourceAdapter>();
        mockAdapter3.Setup(a => a.SourceName).Returns("Source3");
        mockAdapter3.Setup(a => a.IsEnabled()).Returns(true);
        mockAdapter3.Setup(a => a.GetPriceDataAsync(It.IsAny<string>()))
            .ThrowsAsync(new System.Exception("Connection failed"));

        var mockAdapter4 = new Mock<IDataSourceAdapter>();
        mockAdapter4.Setup(a => a.SourceName).Returns("Source4");
        mockAdapter4.Setup(a => a.IsEnabled()).Returns(true);
        mockAdapter4.Setup(a => a.GetPriceDataAsync(It.IsAny<string>()))
            .ThrowsAsync(new System.Exception("Timeout"));

        _adapters.Add(mockAdapter1.Object);
        _adapters.Add(mockAdapter2.Object);
        _adapters.Add(mockAdapter3.Object);
        _adapters.Add(mockAdapter4.Object);

        // Act
        var result = await _healthCheck.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

        // Assert
        Assert.Equal(HealthStatus.Degraded, result.Status);
        Assert.Contains("2/4 data sources healthy", result.Description);
    }

    [Fact]
    public async Task CheckHealthAsync_MostAdaptersDown_ReturnsUnhealthy()
    {
        // Arrange
        var mockAdapter1 = new Mock<IDataSourceAdapter>();
        mockAdapter1.Setup(a => a.SourceName).Returns("Source1");
        mockAdapter1.Setup(a => a.IsEnabled()).Returns(true);
        mockAdapter1.Setup(a => a.GetPriceDataAsync(It.IsAny<string>()))
            .ReturnsAsync(new PriceData { Symbol = "BTC", Price = 50000 });

        var mockAdapter2 = new Mock<IDataSourceAdapter>();
        mockAdapter2.Setup(a => a.SourceName).Returns("Source2");
        mockAdapter2.Setup(a => a.IsEnabled()).Returns(true);
        mockAdapter2.Setup(a => a.GetPriceDataAsync(It.IsAny<string>()))
            .ThrowsAsync(new System.Exception("Connection failed"));

        var mockAdapter3 = new Mock<IDataSourceAdapter>();
        mockAdapter3.Setup(a => a.SourceName).Returns("Source3");
        mockAdapter3.Setup(a => a.IsEnabled()).Returns(true);
        mockAdapter3.Setup(a => a.GetPriceDataAsync(It.IsAny<string>()))
            .ThrowsAsync(new System.Exception("Timeout"));

        var mockAdapter4 = new Mock<IDataSourceAdapter>();
        mockAdapter4.Setup(a => a.SourceName).Returns("Source4");
        mockAdapter4.Setup(a => a.IsEnabled()).Returns(true);
        mockAdapter4.Setup(a => a.GetPriceDataAsync(It.IsAny<string>()))
            .ThrowsAsync(new System.Exception("Service unavailable"));

        _adapters.Add(mockAdapter1.Object);
        _adapters.Add(mockAdapter2.Object);
        _adapters.Add(mockAdapter3.Object);
        _adapters.Add(mockAdapter4.Object);

        // Act
        var result = await _healthCheck.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

        // Assert
        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Contains("1/4 data sources healthy", result.Description);
    }

    [Fact]
    public async Task CheckHealthAsync_NoEnabledAdapters_ReturnsUnhealthy()
    {
        // Arrange
        var mockAdapter1 = new Mock<IDataSourceAdapter>();
        mockAdapter1.Setup(a => a.SourceName).Returns("Source1");
        mockAdapter1.Setup(a => a.IsEnabled()).Returns(false);

        var mockAdapter2 = new Mock<IDataSourceAdapter>();
        mockAdapter2.Setup(a => a.SourceName).Returns("Source2");
        mockAdapter2.Setup(a => a.IsEnabled()).Returns(false);

        _adapters.Add(mockAdapter1.Object);
        _adapters.Add(mockAdapter2.Object);

        // Act
        var result = await _healthCheck.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

        // Assert
        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Contains("No enabled data sources", result.Description);
    }

    [Fact]
    public async Task CheckHealthAsync_ReturnsDataInHealthCheckResult()
    {
        // Arrange
        var mockAdapter1 = new Mock<IDataSourceAdapter>();
        mockAdapter1.Setup(a => a.SourceName).Returns("Source1");
        mockAdapter1.Setup(a => a.IsEnabled()).Returns(true);
        mockAdapter1.Setup(a => a.GetPriceDataAsync(It.IsAny<string>()))
            .ReturnsAsync(new PriceData { Symbol = "BTC", Price = 50000 });

        var mockAdapter2 = new Mock<IDataSourceAdapter>();
        mockAdapter2.Setup(a => a.SourceName).Returns("Source2");
        mockAdapter2.Setup(a => a.IsEnabled()).Returns(true);
        mockAdapter2.Setup(a => a.GetPriceDataAsync(It.IsAny<string>()))
            .ThrowsAsync(new System.Exception("Connection failed"));

        _adapters.Add(mockAdapter1.Object);
        _adapters.Add(mockAdapter2.Object);

        // Act
        var result = await _healthCheck.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

        // Assert
        Assert.NotNull(result.Data);
        Assert.True(result.Data.ContainsKey("healthy_sources"));
        Assert.True(result.Data.ContainsKey("total_sources"));
        Assert.True(result.Data.ContainsKey("health_percentage"));
    }
}
