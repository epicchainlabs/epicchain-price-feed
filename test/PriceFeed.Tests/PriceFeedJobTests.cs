using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using PriceFeed.Core.Interfaces;
using PriceFeed.Core.Models;
using PriceFeed.Core.Options;
using PriceFeed.Infrastructure.Services;
using Xunit;

namespace PriceFeed.Tests
{
    public class PriceFeedJobTests
    {
        private readonly Mock<ILogger<PriceFeedJob>> _loggerMock;
        private readonly Mock<IOptions<PriceFeedOptions>> _optionsMock;
        private readonly Mock<IDataSourceAdapter> _dataSourceAdapterMock;
        private readonly Mock<IPriceAggregationService> _aggregationServiceMock;
        private readonly Mock<IBatchProcessingService> _batchProcessingServiceMock;
        private readonly Mock<IAttestationService> _attestationServiceMock;
        private readonly PriceFeedJob _job;

        public PriceFeedJobTests()
        {
            _loggerMock = new Mock<ILogger<PriceFeedJob>>();

            _optionsMock = new Mock<IOptions<PriceFeedOptions>>();
            _optionsMock.Setup(o => o.Value).Returns(new PriceFeedOptions
            {
                Symbols = new List<string> { "BTCUSDT", "ETHUSDT" }
            });

            _dataSourceAdapterMock = new Mock<IDataSourceAdapter>();
            _dataSourceAdapterMock.Setup(a => a.SourceName).Returns("TestSource");
            _dataSourceAdapterMock.Setup(a => a.IsEnabled()).Returns(true);

            _aggregationServiceMock = new Mock<IPriceAggregationService>();
            _batchProcessingServiceMock = new Mock<IBatchProcessingService>();
            _attestationServiceMock = new Mock<IAttestationService>();

            _job = new PriceFeedJob(
                _loggerMock.Object,
                _optionsMock.Object,
                new[] { _dataSourceAdapterMock.Object },
                _aggregationServiceMock.Object,
                _batchProcessingServiceMock.Object,
                _attestationServiceMock.Object);
        }

        [Fact]
        public async Task RunAsync_ShouldProcessBatch()
        {
            // Arrange

            var priceData = new List<PriceData>
            {
                new PriceData
                {
                    Symbol = "BTCUSDT",
                    Price = 50000,
                    Source = "TestSource",
                    Timestamp = DateTime.UtcNow
                },
                new PriceData
                {
                    Symbol = "ETHUSDT",
                    Price = 3000,
                    Source = "TestSource",
                    Timestamp = DateTime.UtcNow
                }
            };

            _dataSourceAdapterMock
                .Setup(a => a.GetPriceDataBatchAsync(It.IsAny<IEnumerable<string>>()))
                .ReturnsAsync(priceData);

            var aggregatedPrices = new List<AggregatedPrice>
            {
                new AggregatedPrice
                {
                    Symbol = "BTCUSDT",
                    Price = 50000,
                    Timestamp = DateTime.UtcNow,
                    ConfidenceScore = 90
                },
                new AggregatedPrice
                {
                    Symbol = "ETHUSDT",
                    Price = 3000,
                    Timestamp = DateTime.UtcNow,
                    ConfidenceScore = 90
                }
            };

            _aggregationServiceMock
                .Setup(a => a.AggregateBatchAsync(It.IsAny<Dictionary<string, IEnumerable<PriceData>>>()))
                .ReturnsAsync(aggregatedPrices);

            _batchProcessingServiceMock
                .Setup(b => b.ProcessBatchAsync(It.IsAny<PriceBatch>()))
                .ReturnsAsync(true);

            _attestationServiceMock
                .Setup(a => a.CleanupOldAttestationsAsync(It.IsAny<int>()))
                .ReturnsAsync(0);

            // Act
            await _job.RunAsync();

            // Assert
            _dataSourceAdapterMock.Verify(a => a.GetPriceDataBatchAsync(It.IsAny<IEnumerable<string>>()), Times.Once);
            _aggregationServiceMock.Verify(a => a.AggregateBatchAsync(It.IsAny<Dictionary<string, IEnumerable<PriceData>>>()), Times.Once);
            _batchProcessingServiceMock.Verify(b => b.ProcessBatchAsync(It.Is<PriceBatch>(batch =>
                batch.Prices.Count == 2 &&
                batch.Prices.Any(p => p.Symbol == "BTCUSDT") &&
                batch.Prices.Any(p => p.Symbol == "ETHUSDT"))), Times.Once);
            _attestationServiceMock.Verify(a => a.CleanupOldAttestationsAsync(It.IsAny<int>()), Times.Once);
        }



        [Fact]
        public async Task RunAsync_WithNoDataFromSources_ShouldThrowException()
        {
            // Arrange

            _dataSourceAdapterMock
                .Setup(a => a.GetPriceDataBatchAsync(It.IsAny<IEnumerable<string>>()))
                .ReturnsAsync(new List<PriceData>());

            // Act & Assert
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => _job.RunAsync());
            Assert.Contains("Failed to collect price data", exception.Message);
        }

        [Fact(Timeout = 30000), Trait("Category", "SlowTest")] // 30 second timeout
        public async Task RunAsync_WithBatchProcessingFailure_ShouldRetry()
        {
            // Arrange

            var priceData = new List<PriceData>
            {
                new PriceData
                {
                    Symbol = "BTCUSDT",
                    Price = 50000,
                    Source = "TestSource",
                    Timestamp = DateTime.UtcNow
                }
            };

            _dataSourceAdapterMock
                .Setup(a => a.GetPriceDataBatchAsync(It.IsAny<IEnumerable<string>>()))
                .ReturnsAsync(priceData);

            var aggregatedPrices = new List<AggregatedPrice>
            {
                new AggregatedPrice
                {
                    Symbol = "BTCUSDT",
                    Price = 50000,
                    Timestamp = DateTime.UtcNow,
                    ConfidenceScore = 90
                }
            };

            _aggregationServiceMock
                .Setup(a => a.AggregateBatchAsync(It.IsAny<Dictionary<string, IEnumerable<PriceData>>>()))
                .ReturnsAsync(aggregatedPrices);

            // First attempt fails, second succeeds
            _batchProcessingServiceMock
                .SetupSequence(b => b.ProcessBatchAsync(It.IsAny<PriceBatch>()))
                .ReturnsAsync(false)
                .ReturnsAsync(true);

            _attestationServiceMock
                .Setup(a => a.CleanupOldAttestationsAsync(It.IsAny<int>()))
                .ReturnsAsync(0);

            // Act
            await _job.RunAsync();

            // Assert
            _batchProcessingServiceMock.Verify(b => b.ProcessBatchAsync(It.IsAny<PriceBatch>()), Times.Exactly(2));
        }
        [Fact]
        public async Task RunAsync_WithMultipleDataSources_ShouldAggregateCorrectly()
        {
            // Arrange

            // Create multiple data source adapters
            var dataSourceAdapter1 = new Mock<IDataSourceAdapter>();
            dataSourceAdapter1.Setup(a => a.SourceName).Returns("Source1");
            dataSourceAdapter1.Setup(a => a.IsEnabled()).Returns(true);

            var dataSourceAdapter2 = new Mock<IDataSourceAdapter>();
            dataSourceAdapter2.Setup(a => a.SourceName).Returns("Source2");
            dataSourceAdapter2.Setup(a => a.IsEnabled()).Returns(true);

            // Setup price data from multiple sources
            var priceDataSource1 = new List<PriceData>
            {
                new PriceData
                {
                    Symbol = "BTCUSDT",
                    Price = 50000,
                    Source = "Source1",
                    Timestamp = DateTime.UtcNow
                }
            };

            var priceDataSource2 = new List<PriceData>
            {
                new PriceData
                {
                    Symbol = "BTCUSDT",
                    Price = 50100,
                    Source = "Source2",
                    Timestamp = DateTime.UtcNow
                }
            };

            dataSourceAdapter1
                .Setup(a => a.GetPriceDataBatchAsync(It.IsAny<IEnumerable<string>>()))
                .ReturnsAsync(priceDataSource1);

            dataSourceAdapter2
                .Setup(a => a.GetPriceDataBatchAsync(It.IsAny<IEnumerable<string>>()))
                .ReturnsAsync(priceDataSource2);

            // Create job with multiple data sources
            var job = new PriceFeedJob(
                _loggerMock.Object,
                _optionsMock.Object,
                new[] { dataSourceAdapter1.Object, dataSourceAdapter2.Object },
                _aggregationServiceMock.Object,
                _batchProcessingServiceMock.Object,
                _attestationServiceMock.Object);

            // Setup aggregation service to verify it receives data from both sources
            _aggregationServiceMock
                .Setup(a => a.AggregateBatchAsync(It.Is<Dictionary<string, IEnumerable<PriceData>>>(
                    dict => dict["BTCUSDT"].Count() == 2)))
                .ReturnsAsync(new List<AggregatedPrice>
                {
                    new AggregatedPrice
                    {
                        Symbol = "BTCUSDT",
                        Price = 50050, // Average of the two prices
                        Timestamp = DateTime.UtcNow,
                        ConfidenceScore = 95 // Higher confidence with multiple sources
                    }
                });

            _batchProcessingServiceMock
                .Setup(b => b.ProcessBatchAsync(It.IsAny<PriceBatch>()))
                .ReturnsAsync(true);

            // Act
            await job.RunAsync();

            // Assert
            dataSourceAdapter1.Verify(a => a.GetPriceDataBatchAsync(It.IsAny<IEnumerable<string>>()), Times.Once);
            dataSourceAdapter2.Verify(a => a.GetPriceDataBatchAsync(It.IsAny<IEnumerable<string>>()), Times.Once);
            _aggregationServiceMock.Verify(a => a.AggregateBatchAsync(It.Is<Dictionary<string, IEnumerable<PriceData>>>(
                dict => dict["BTCUSDT"].Count() == 2)), Times.Once);
        }

        [Fact]
        public async Task RunAsync_WithAllDataSourcesDisabled_ShouldThrowException()
        {
            // Arrange
            var disabledDataSource1 = new Mock<IDataSourceAdapter>();
            disabledDataSource1.Setup(a => a.SourceName).Returns("DisabledSource1");
            disabledDataSource1.Setup(a => a.IsEnabled()).Returns(false);

            var disabledDataSource2 = new Mock<IDataSourceAdapter>();
            disabledDataSource2.Setup(a => a.SourceName).Returns("DisabledSource2");
            disabledDataSource2.Setup(a => a.IsEnabled()).Returns(false);

            // Create job with only disabled data sources
            var job = new PriceFeedJob(
                _loggerMock.Object,
                _optionsMock.Object,
                new[] { disabledDataSource1.Object, disabledDataSource2.Object },
                _aggregationServiceMock.Object,
                _batchProcessingServiceMock.Object,
                _attestationServiceMock.Object);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => job.RunAsync());
            Assert.Contains("No enabled data sources found", exception.Message);

            // Verify that no data sources were called
            disabledDataSource1.Verify(a => a.GetPriceDataBatchAsync(It.IsAny<IEnumerable<string>>()), Times.Never);
            disabledDataSource2.Verify(a => a.GetPriceDataBatchAsync(It.IsAny<IEnumerable<string>>()), Times.Never);
        }

        [Fact(Timeout = 30000), Trait("Category", "SlowTest")] // 30 second timeout
        public async Task RunAsync_WithDisabledDataSources_ShouldSkipThem()
        {
            // Arrange
            var enabledDataSourceMock = new Mock<IDataSourceAdapter>();
            enabledDataSourceMock.Setup(a => a.SourceName).Returns("EnabledSource");
            enabledDataSourceMock.Setup(a => a.IsEnabled()).Returns(true);

            var disabledDataSourceMock = new Mock<IDataSourceAdapter>();
            disabledDataSourceMock.Setup(a => a.SourceName).Returns("DisabledSource");
            disabledDataSourceMock.Setup(a => a.IsEnabled()).Returns(false);

            // Setup price data
            var priceData = new List<PriceData>
            {
                new PriceData
                {
                    Symbol = "BTCUSDT",
                    Price = 50000m,
                    Source = "EnabledSource",
                    Timestamp = DateTime.UtcNow
                }
            };

            enabledDataSourceMock
                .Setup(a => a.GetPriceDataBatchAsync(It.IsAny<IEnumerable<string>>()))
                .ReturnsAsync(priceData);

            // Create job with both enabled and disabled data sources
            var job = new PriceFeedJob(
                _loggerMock.Object,
                _optionsMock.Object,
                new[] { enabledDataSourceMock.Object, disabledDataSourceMock.Object },
                _aggregationServiceMock.Object,
                _batchProcessingServiceMock.Object,
                _attestationServiceMock.Object);

            // Setup aggregation service to return aggregated prices
            var aggregatedPrices = new List<AggregatedPriceData>
            {
                new AggregatedPriceData
                {
                    Symbol = "BTCUSDT",
                    Price = 50000m,
                    Timestamp = DateTime.UtcNow,
                    ConfidenceScore = 80
                }
            };

            _aggregationServiceMock
                .Setup(a => a.AggregateBatchAsync(It.IsAny<Dictionary<string, IEnumerable<PriceData>>>()))
                .ReturnsAsync(aggregatedPrices);

            // Act
            await job.RunAsync();

            // Assert
            enabledDataSourceMock.Verify(a => a.GetPriceDataBatchAsync(It.IsAny<IEnumerable<string>>()), Times.Once);
            disabledDataSourceMock.Verify(a => a.GetPriceDataBatchAsync(It.IsAny<IEnumerable<string>>()), Times.Never);

            // Create a new mock for the batch processing service to avoid interference from other tests
            var batchProcessingServiceMock = new Mock<IBatchProcessingService>();
            batchProcessingServiceMock
                .Setup(b => b.ProcessBatchAsync(It.IsAny<PriceBatch>()))
                .ReturnsAsync(true);

            var jobWithNewMock = new PriceFeedJob(
                _loggerMock.Object,
                _optionsMock.Object,
                new[] { enabledDataSourceMock.Object, disabledDataSourceMock.Object },
                _aggregationServiceMock.Object,
                batchProcessingServiceMock.Object,
                _attestationServiceMock.Object);

            // Run the job again with the new mock
            await jobWithNewMock.RunAsync();

            // Verify the new mock was called exactly once
            batchProcessingServiceMock.Verify(b => b.ProcessBatchAsync(It.Is<PriceBatch>(batch =>
                batch.Prices.Count == 1 &&
                batch.Prices.Any(p => p.Symbol == "BTCUSDT"))), Times.Once);
        }

        [Fact]
        public async Task RunAsync_WithDataSourceException_ShouldContinueWithOtherSources()
        {
            // Arrange

            // Create multiple data source adapters
            var dataSourceAdapter1 = new Mock<IDataSourceAdapter>();
            dataSourceAdapter1.Setup(a => a.SourceName).Returns("Source1");
            dataSourceAdapter1.Setup(a => a.IsEnabled()).Returns(true);

            var dataSourceAdapter2 = new Mock<IDataSourceAdapter>();
            dataSourceAdapter2.Setup(a => a.SourceName).Returns("Source2");
            dataSourceAdapter2.Setup(a => a.IsEnabled()).Returns(true);

            // First source throws exception
            dataSourceAdapter1
                .Setup(a => a.GetPriceDataBatchAsync(It.IsAny<IEnumerable<string>>()))
                .ThrowsAsync(new Exception("API error"));

            // Second source returns valid data
            var priceDataSource2 = new List<PriceData>
            {
                new PriceData
                {
                    Symbol = "BTCUSDT",
                    Price = 50100,
                    Source = "Source2",
                    Timestamp = DateTime.UtcNow
                }
            };

            dataSourceAdapter2
                .Setup(a => a.GetPriceDataBatchAsync(It.IsAny<IEnumerable<string>>()))
                .ReturnsAsync(priceDataSource2);

            // Create job with multiple data sources
            var job = new PriceFeedJob(
                _loggerMock.Object,
                _optionsMock.Object,
                new[] { dataSourceAdapter1.Object, dataSourceAdapter2.Object },
                _aggregationServiceMock.Object,
                _batchProcessingServiceMock.Object,
                _attestationServiceMock.Object);

            // Setup aggregation service
            _aggregationServiceMock
                .Setup(a => a.AggregateBatchAsync(It.IsAny<Dictionary<string, IEnumerable<PriceData>>>()))
                .ReturnsAsync(new List<AggregatedPrice>
                {
                    new AggregatedPrice
                    {
                        Symbol = "BTCUSDT",
                        Price = 50100,
                        Timestamp = DateTime.UtcNow,
                        ConfidenceScore = 70 // Lower confidence with single source
                    }
                });

            _batchProcessingServiceMock
                .Setup(b => b.ProcessBatchAsync(It.IsAny<PriceBatch>()))
                .ReturnsAsync(true);

            // Act
            await job.RunAsync();

            // Assert
            dataSourceAdapter1.Verify(a => a.GetPriceDataBatchAsync(It.IsAny<IEnumerable<string>>()), Times.Once);
            dataSourceAdapter2.Verify(a => a.GetPriceDataBatchAsync(It.IsAny<IEnumerable<string>>()), Times.Once);
            _aggregationServiceMock.Verify(a => a.AggregateBatchAsync(It.Is<Dictionary<string, IEnumerable<PriceData>>>(
                dict => dict.ContainsKey("BTCUSDT") && dict["BTCUSDT"].Count() == 1)), Times.Once);
        }
    }
}
