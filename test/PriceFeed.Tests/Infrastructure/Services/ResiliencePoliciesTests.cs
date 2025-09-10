using Microsoft.Extensions.Logging;
using Moq;
using Polly;
using Polly.CircuitBreaker;
using Polly.Timeout;
using Polly.Bulkhead;
using PriceFeed.Infrastructure.Services;
using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace PriceFeed.Tests.Infrastructure.Services;

public class ResiliencePoliciesTests
{
    private readonly Mock<ILogger> _mockLogger;

    public ResiliencePoliciesTests()
    {
        _mockLogger = new Mock<ILogger>();
    }

    [Fact(Timeout = 30000), Trait("Category", "SlowTest")] // 30 second timeout
    public async Task GetRetryPolicy_RetriesOnTransientErrors()
    {
        // Arrange
        var policy = ResiliencePolicies.GetRetryPolicy(_mockLogger.Object, "TestSource");
        var callCount = 0;

        // Act
        var result = await policy.ExecuteAsync(async () =>
        {
            callCount++;
            await Task.CompletedTask;
            if (callCount < 3)
            {
                return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
            }
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        // Assert
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        Assert.Equal(3, callCount);
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Retry")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task GetRetryPolicy_DoesNotRetryOnSuccess()
    {
        // Arrange
        var policy = ResiliencePolicies.GetRetryPolicy(_mockLogger.Object, "TestSource");
        var callCount = 0;

        // Act
        var result = await policy.ExecuteAsync(async () =>
        {
            callCount++;
            await Task.CompletedTask;
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        // Assert
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        Assert.Equal(1, callCount);
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
    }

    [Fact]
    public async Task GetCircuitBreakerPolicy_OpensAfterConsecutiveFailures()
    {
        // Arrange
        var policy = ResiliencePolicies.GetCircuitBreakerPolicy(_mockLogger.Object, "TestSource");
        var callCount = 0;

        // Act & Assert
        // Generate 5 failures to open the circuit
        for (int i = 0; i < 5; i++)
        {
            await policy.ExecuteAsync(async () =>
            {
                callCount++;
                await Task.CompletedTask;
                return new HttpResponseMessage(HttpStatusCode.InternalServerError);
            });
        }

        // Circuit should be open now
        await Assert.ThrowsAsync<BrokenCircuitException<HttpResponseMessage>>(async () =>
        {
            await policy.ExecuteAsync(async () =>
            {
                callCount++;
                await Task.CompletedTask;
                return new HttpResponseMessage(HttpStatusCode.OK);
            });
        });

        // Verify circuit was opened
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Circuit breaker opened")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);

        // Call count should be 5 (circuit opened after 5th failure)
        Assert.Equal(5, callCount);
    }

    [Fact]
    public async Task GetTimeoutPolicy_ThrowsOnTimeout()
    {
        // Arrange
        var policy = ResiliencePolicies.GetTimeoutPolicy(1); // 1 second timeout

        // Act & Assert
        await Assert.ThrowsAsync<TimeoutRejectedException>(async () =>
        {
            await policy.ExecuteAsync(async (ct) =>
            {
                await Task.Delay(TimeSpan.FromSeconds(2), ct);
                return new HttpResponseMessage(HttpStatusCode.OK);
            }, CancellationToken.None);
        });
    }

    [Fact]
    public async Task GetTimeoutPolicy_SucceedsWithinTimeout()
    {
        // Arrange
        var policy = ResiliencePolicies.GetTimeoutPolicy(2); // 2 second timeout

        // Act
        var result = await policy.ExecuteAsync(async (ct) =>
        {
            await Task.Delay(TimeSpan.FromMilliseconds(100), ct);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }, CancellationToken.None);

        // Assert
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
    }

    [Fact(Timeout = 30000), Trait("Category", "SlowTest")] // 30 second timeout
    public async Task GetCombinedPolicy_AppliesAllPoliciesInOrder()
    {
        // Arrange
        var policy = ResiliencePolicies.GetCombinedPolicy(_mockLogger.Object, "TestSource", 5);
        var callCount = 0;

        // Act
        var result = await policy.ExecuteAsync(async (ct) =>
        {
            callCount++;
            if (callCount == 1)
            {
                // First call fails with transient error (will be retried)
                return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
            }
            // Second call succeeds
            await Task.Delay(100, ct);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }, CancellationToken.None);

        // Assert
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        Assert.Equal(2, callCount); // One failure, one success
    }

    [Fact]
    public async Task GetFallbackPolicy_ExecutesFallbackOnException()
    {
        // Arrange
        var fallbackExecuted = false;
        var policy = ResiliencePolicies.GetFallbackPolicy<string>(
            _mockLogger.Object,
            "TestOperation",
            async (ct) =>
            {
                await Task.CompletedTask;
                fallbackExecuted = true;
                return "Fallback Value";
            });

        // Act
        var result = await policy.ExecuteAsync(async (ct) =>
        {
            await Task.CompletedTask;
            throw new Exception("Primary operation failed");
        }, CancellationToken.None);

        // Assert
        Assert.Equal("Fallback Value", result);
        Assert.True(fallbackExecuted);
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Fallback triggered")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task GetFallbackPolicy_DoesNotExecuteFallbackOnSuccess()
    {
        // Arrange
        var fallbackExecuted = false;
        var policy = ResiliencePolicies.GetFallbackPolicy<string>(
            _mockLogger.Object,
            "TestOperation",
            async (ct) =>
            {
                await Task.CompletedTask;
                fallbackExecuted = true;
                return "Fallback Value";
            });

        // Act
        var result = await policy.ExecuteAsync(async (ct) =>
        {
            await Task.CompletedTask;
            return "Primary Value";
        }, CancellationToken.None);

        // Assert
        Assert.Equal("Primary Value", result);
        Assert.False(fallbackExecuted);
    }

    [Fact(Timeout = 15000)] // 15 second timeout
    public async Task GetBulkheadPolicy_LimitsParallelExecution()
    {
        // Arrange
        var policy = ResiliencePolicies.GetBulkheadPolicy(2, 0); // Max 2 parallel, 0 queued for predictable behavior
        var executionCount = 0;
        var maxConcurrent = 0;
        var lockObj = new object();
        var successCount = 0;
        var rejectedCount = 0;

        // Act
        var tasks = new Task[4]; // Reduced from 5 to 4 to make it more predictable
        for (int i = 0; i < 4; i++)
        {
            tasks[i] = Task.Run(async () =>
            {
                try
                {
                    await policy.ExecuteAsync(async () =>
                    {
                        lock (lockObj)
                        {
                            executionCount++;
                            maxConcurrent = Math.Max(maxConcurrent, executionCount);
                        }

                        await Task.Delay(50); // Reduced delay to speed up test

                        lock (lockObj)
                        {
                            executionCount--;
                            successCount++;
                        }
                    });
                }
                catch (BulkheadRejectedException)
                {
                    // Expected for tasks that exceed the bulkhead capacity
                    Interlocked.Increment(ref rejectedCount);
                }
            });
        }

        // Add timeout to prevent infinite waiting
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await Task.WhenAll(tasks).WaitAsync(cts.Token);

        // Assert
        Assert.True(maxConcurrent <= 2, $"Max concurrent executions was {maxConcurrent}, expected <= 2");
        Assert.True(successCount + rejectedCount == 4, $"Expected 4 total operations, got {successCount + rejectedCount}");
        Assert.True(rejectedCount >= 2, $"Expected at least 2 rejections, got {rejectedCount}");
    }
}
