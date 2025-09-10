using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using PriceFeed.Core.Options;
using PriceFeed.Infrastructure.HealthChecks;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace PriceFeed.Tests.Infrastructure.HealthChecks;

public class NeoRpcHealthCheckTests
{
    private readonly Mock<ILogger<NeoRpcHealthCheck>> _mockLogger;
    private readonly Mock<IOptions<BatchProcessingOptions>> _mockOptions;
    private readonly Mock<HttpMessageHandler> _mockHttpMessageHandler;
    private readonly Mock<IHttpClientFactory> _mockHttpClientFactory;
    private readonly HttpClient _httpClient;
    private readonly NeoRpcHealthCheck _healthCheck;

    public NeoRpcHealthCheckTests()
    {
        _mockLogger = new Mock<ILogger<NeoRpcHealthCheck>>();
        _mockOptions = new Mock<IOptions<BatchProcessingOptions>>();
        _mockOptions.Setup(o => o.Value).Returns(new BatchProcessingOptions
        {
            RpcEndpoint = "https://test.neo.org"
        });

        _mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_mockHttpMessageHandler.Object);
        _mockHttpClientFactory = new Mock<IHttpClientFactory>();
        _mockHttpClientFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(_httpClient);
        _healthCheck = new NeoRpcHealthCheck(_mockHttpClientFactory.Object, _mockOptions.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task CheckHealthAsync_SuccessfulResponse_ReturnsHealthy()
    {
        // Arrange
        var responseJson = @"{""jsonrpc"":""2.0"",""result"":123456,""id"":1}";
        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(responseJson)
            });

        // Act
        var result = await _healthCheck.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

        // Assert
        Assert.Equal(HealthStatus.Healthy, result.Status);
        Assert.Contains("Neo RPC endpoint is responsive", result.Description);
        Assert.True(result.Data.ContainsKey("endpoint"));
        Assert.True(result.Data.ContainsKey("status_code"));
    }

    [Fact]
    public async Task CheckHealthAsync_HttpRequestException_ReturnsUnhealthy()
    {
        // Arrange
        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Connection failed"));

        // Act
        var result = await _healthCheck.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

        // Assert
        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Contains("Failed to connect to Neo RPC endpoint", result.Description);
        Assert.True(result.Data.ContainsKey("error"));
    }

    [Fact]
    public async Task CheckHealthAsync_TaskCanceledException_ReturnsUnhealthy()
    {
        // Arrange
        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new TaskCanceledException("Request timeout"));

        // Act
        var result = await _healthCheck.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

        // Assert
        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Contains("Neo RPC endpoint request timed out", result.Description);
    }

    [Fact]
    public async Task CheckHealthAsync_NonSuccessStatusCode_ReturnsUnhealthy()
    {
        // Arrange
        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.ServiceUnavailable,
                Content = new StringContent("Service Unavailable")
            });

        // Act
        var result = await _healthCheck.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

        // Assert
        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Contains("Neo RPC endpoint returned", result.Description);
        Assert.True(result.Data.ContainsKey("status_code"));
        Assert.Equal(503, result.Data["status_code"]);
    }

    [Fact]
    public async Task CheckHealthAsync_InvalidJsonResponse_ReturnsUnhealthy()
    {
        // Arrange
        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("invalid json")
            });

        // Act
        var result = await _healthCheck.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

        // Assert
        Assert.Equal(HealthStatus.Degraded, result.Status);
        Assert.Contains("Neo RPC endpoint returned unexpected response", result.Description);
        Assert.True(result.Data.ContainsKey("response"));
    }

    [Fact]
    public async Task CheckHealthAsync_ErrorInResponse_ReturnsUnhealthy()
    {
        // Arrange
        var responseJson = @"{""jsonrpc"":""2.0"",""error"":{""code"":-32601,""message"":""Method not found""},""id"":1}";
        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(responseJson)
            });

        // Act
        var result = await _healthCheck.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

        // Assert
        Assert.Equal(HealthStatus.Degraded, result.Status);
        Assert.Contains("Neo RPC endpoint returned unexpected response", result.Description);
        Assert.True(result.Data.ContainsKey("response"));
    }

    [Fact]
    public async Task CheckHealthAsync_ValidatesRequestContent()
    {
        // Arrange
        HttpRequestMessage? capturedRequest = null;
        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, ct) => capturedRequest = req)
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(@"{""jsonrpc"":""2.0"",""result"":123456,""id"":1}")
            });

        // Act
        await _healthCheck.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

        // Assert
        Assert.NotNull(capturedRequest);
        Assert.Equal(HttpMethod.Post, capturedRequest.Method);
        Assert.Equal("https://test.neo.org/", capturedRequest.RequestUri?.ToString());

        var content = await capturedRequest.Content!.ReadAsStringAsync();
        Assert.Contains("getblockcount", content);
        Assert.Contains("\"jsonrpc\":\"2.0\"", content);
    }
}
