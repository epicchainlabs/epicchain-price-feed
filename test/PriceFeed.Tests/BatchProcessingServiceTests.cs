using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using Newtonsoft.Json;
using PriceFeed.Core.Interfaces;
using PriceFeed.Core.Models;
using PriceFeed.Core.Options;
using PriceFeed.Infrastructure.Services;
using Xunit;

namespace PriceFeed.Tests
{
    public class BatchProcessingServiceTests
    {
        private readonly Mock<ILogger<BatchProcessingService>> _loggerMock;
        private readonly Mock<IOptions<BatchProcessingOptions>> _optionsMock;
        private readonly Mock<HttpMessageHandler> _httpMessageHandlerMock;
        private readonly HttpClient _httpClient;
        private readonly Mock<IHttpClientFactory> _httpClientFactoryMock;
        private readonly Mock<IAttestationService> _attestationServiceMock;

        public BatchProcessingServiceTests()
        {
            _loggerMock = new Mock<ILogger<BatchProcessingService>>();
            _optionsMock = new Mock<IOptions<BatchProcessingOptions>>();
            _httpMessageHandlerMock = new Mock<HttpMessageHandler>();
            _httpClient = new HttpClient(_httpMessageHandlerMock.Object);

            _httpClientFactoryMock = new Mock<IHttpClientFactory>();
            _httpClientFactoryMock.Setup(f => f.CreateClient("Neo")).Returns(_httpClient);

            _attestationServiceMock = new Mock<IAttestationService>();

            _optionsMock.Setup(o => o.Value).Returns(new BatchProcessingOptions
            {
                RpcEndpoint = "https://example.com/rpc",
                ContractScriptHash = "0x1234567890abcdef",
                TeeAccountAddress = "NeoAddress123",
                TeeAccountPrivateKey = "KxDgvEKzgSBPPfuVfw67oPQBSjidEiqTHURKSDL1R7yGaGYAeYnr",
                MasterAccountAddress = "NeoAddress456",
                MasterAccountPrivateKey = "KxDgvEKzgSBPPfuVfw67oPQBSjidEiqTHURKSDL1R7yGaGYAeYnr",
                MaxBatchSize = 50
            });
        }

        [Fact]
        public async Task ProcessBatchAsync_WithValidBatch_ShouldReturnTrue()
        {
            // Arrange
            var batch = new PriceBatch
            {
                Prices = new List<AggregatedPriceData>
                {
                    new AggregatedPrice
                    {
                        Symbol = "BTCUSDT",
                        Price = 50000,
                        Timestamp = DateTime.UtcNow,
                        ConfidenceScore = 90
                    }
                }
            };

            // Mock HTTP response
            // The response will be always successfull no matter what the input is.
            var responseContent = JsonConvert.SerializeObject(new
            {
                jsonrpc = "2.0",
                id = 1,
                result = new
                {
                    hash = "0xtransactionhash123",
                    script = "script",
                    state = "HALT",
                    gasconsumed = "1000",
                    exception = (string?)null
                }
            });

            _httpMessageHandlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(responseContent)
                });

            var service = new BatchProcessingService(_loggerMock.Object, _optionsMock.Object, _httpClientFactoryMock.Object, _attestationServiceMock.Object);

            // Act
            var result = await service.ProcessBatchAsync(batch);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task ProcessBatchAsync_WithHttpError_ShouldReturnFalse()
        {
            // Arrange
            var batch = new PriceBatch
            {
                Prices = new List<AggregatedPriceData>
                {
                    new AggregatedPrice
                    {
                        Symbol = "BTCUSDT",
                        Price = 50000,
                        Timestamp = DateTime.UtcNow,
                        ConfidenceScore = 90
                    }
                }
            };

            _httpMessageHandlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.InternalServerError
                });

            var service = new BatchProcessingService(_loggerMock.Object, _optionsMock.Object, _httpClientFactoryMock.Object, _attestationServiceMock.Object);

            // Act
            var result = await service.ProcessBatchAsync(batch);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task ProcessBatchAsync_WithLargeBatch_ShouldSplitIntoSubBatches()
        {
            // Arrange
            var prices = new List<AggregatedPriceData>();
            for (int i = 0; i < 100; i++)
            {
                prices.Add(new AggregatedPrice
                {
                    Symbol = $"SYMBOL{i}",
                    Price = 1000 + i,
                    Timestamp = DateTime.UtcNow,
                    ConfidenceScore = 90
                });
            }

            var batch = new PriceBatch
            {
                Prices = prices
            };

            // Mock HTTP response
            var responseContent = JsonConvert.SerializeObject(new
            {
                jsonrpc = "2.0",
                id = 1,
                result = new
                {
                    hash = "0xtransactionhash123",
                    script = "script",
                    state = "HALT",
                    gasconsumed = "1000",
                    exception = (string?)null
                }
            });

            _httpMessageHandlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(responseContent)
                });

            var service = new BatchProcessingService(_loggerMock.Object, _optionsMock.Object, _httpClientFactoryMock.Object, _attestationServiceMock.Object);

            // Act
            var result = await service.ProcessBatchAsync(batch);

            // Assert
            Assert.True(result);

            // Verify that SendAsync was called multiple times (for each sub-batch)
            _httpMessageHandlerMock
                .Protected()
                .Verify(
                    "SendAsync",
                    Times.AtLeast(2), // At least 2 sub-batches (100 items / 50 max batch size)
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>());
        }

        [Fact]
        public async Task ProcessBatchAsync_WithRpcErrorResponse_ShouldReturnFalse()
        {
            // Arrange
            var batch = new PriceBatch
            {
                Prices = new List<AggregatedPriceData>
                {
                    new AggregatedPrice
                    {
                        Symbol = "BTCUSDT",
                        Price = 50000,
                        Timestamp = DateTime.UtcNow,
                        ConfidenceScore = 90
                    }
                }
            };

            // Mock HTTP response with RPC error
            var responseContent = JsonConvert.SerializeObject(new
            {
                jsonrpc = "2.0",
                id = 1,
                error = new
                {
                    code = -32602,
                    message = "Invalid params"
                }
            });

            _httpMessageHandlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(responseContent)
                });

            var service = new BatchProcessingService(_loggerMock.Object, _optionsMock.Object, _httpClientFactoryMock.Object, _attestationServiceMock.Object);

            // Act
            var result = await service.ProcessBatchAsync(batch);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task ProcessBatchAsync_WithTransactionException_ShouldReturnFalse()
        {
            // Arrange
            var batch = new PriceBatch
            {
                Prices = new List<AggregatedPriceData>
                {
                    new AggregatedPrice
                    {
                        Symbol = "BTCUSDT",
                        Price = 50000,
                        Timestamp = DateTime.UtcNow,
                        ConfidenceScore = 90
                    }
                }
            };

            // Mock HTTP response with transaction exception
            var responseContent = JsonConvert.SerializeObject(new
            {
                jsonrpc = "2.0",
                id = 1,
                result = new
                {
                    hash = "0xtransactionhash123",
                    script = "script",
                    state = "FAULT", // Transaction failed
                    gasconsumed = "1000",
                    exception = "Contract execution failed: Insufficient funds"
                }
            });

            _httpMessageHandlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(responseContent)
                });

            var service = new BatchProcessingService(_loggerMock.Object, _optionsMock.Object, _httpClientFactoryMock.Object, _attestationServiceMock.Object);

            // Act
            var result = await service.ProcessBatchAsync(batch);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task ProcessBatchAsync_WithEmptyBatch_ShouldThrowException()
        {
            // Arrange
            var batch = new PriceBatch
            {
                Prices = new List<AggregatedPriceData>()
            };

            var service = new BatchProcessingService(_loggerMock.Object, _optionsMock.Object, _httpClientFactoryMock.Object, _attestationServiceMock.Object);

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(async () => await service.ProcessBatchAsync(batch));

            // Verify that SendAsync was not called
            _httpMessageHandlerMock
                .Protected()
                .Verify(
                    "SendAsync",
                    Times.Never(),
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>());
        }
    }
}
