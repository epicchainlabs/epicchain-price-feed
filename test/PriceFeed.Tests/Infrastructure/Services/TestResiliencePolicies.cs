using System.Net;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.CircuitBreaker;
using Polly.Extensions.Http;
using Polly.Timeout;

namespace PriceFeed.Tests.Infrastructure.Services;

/// <summary>
/// Test-optimized resilience policies with shorter delays for faster test execution
/// </summary>
public static class TestResiliencePolicies
{
    /// <summary>
    /// Creates a retry policy with minimal delays for testing
    /// </summary>
    public static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy(ILogger logger, string sourceName)
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .OrResult(msg => !msg.IsSuccessStatusCode)
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: retryAttempt => TimeSpan.FromMilliseconds(100 * retryAttempt), // Much shorter delays
                onRetry: (outcome, timespan, retryCount, context) =>
                {
                    var reason = outcome.Result?.StatusCode.ToString() ?? outcome.Exception?.Message ?? "Unknown";
                    logger.LogWarning(
                        "Retry {RetryCount} for {Source} after {Delay}ms. Reason: {Reason}",
                        retryCount, sourceName, timespan.TotalMilliseconds, reason);
                });
    }

    /// <summary>
    /// Creates a circuit breaker policy with shorter break duration for testing
    /// </summary>
    public static IAsyncPolicy<HttpResponseMessage> GetCircuitBreakerPolicy(ILogger logger, string sourceName)
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .OrResult(msg => (int)msg.StatusCode >= 500)
            .CircuitBreakerAsync(
                handledEventsAllowedBeforeBreaking: 5,
                durationOfBreak: TimeSpan.FromMilliseconds(500), // Much shorter break
                onBreak: (result, duration) =>
                {
                    logger.LogError(
                        "Circuit breaker opened for {Source}. Duration: {Duration}ms",
                        sourceName, duration.TotalMilliseconds);
                },
                onReset: () =>
                {
                    logger.LogInformation("Circuit breaker reset for {Source}", sourceName);
                },
                onHalfOpen: () =>
                {
                    logger.LogInformation("Circuit breaker half-open for {Source}", sourceName);
                });
    }

    /// <summary>
    /// Creates a timeout policy for testing
    /// </summary>
    public static IAsyncPolicy<HttpResponseMessage> GetTimeoutPolicy(int timeoutSeconds = 10)
    {
        return Policy.TimeoutAsync<HttpResponseMessage>(timeoutSeconds);
    }

    /// <summary>
    /// Combines all resilience policies with test-friendly timings
    /// </summary>
    public static IAsyncPolicy<HttpResponseMessage> GetCombinedPolicy(ILogger logger, string sourceName, int timeoutSeconds = 10)
    {
        return Policy.WrapAsync(
            GetRetryPolicy(logger, sourceName),
            GetCircuitBreakerPolicy(logger, sourceName),
            GetTimeoutPolicy(timeoutSeconds)
        );
    }
}
