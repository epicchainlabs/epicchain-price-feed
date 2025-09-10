using System.Net;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.CircuitBreaker;
using Polly.Extensions.Http;
using Polly.Timeout;

namespace PriceFeed.Infrastructure.Services;

/// <summary>
/// Provides resilience policies for HTTP operations using Polly
/// </summary>
public static class ResiliencePolicies
{
    /// <summary>
    /// Creates a retry policy with exponential backoff and jitter
    /// </summary>
    public static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy(ILogger logger, string sourceName)
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .OrResult(msg => !msg.IsSuccessStatusCode)
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt))
                    + TimeSpan.FromMilliseconds(Random.Shared.Next(0, 1000)),
                onRetry: (outcome, timespan, retryCount, context) =>
                {
                    var reason = outcome.Result?.StatusCode.ToString() ?? outcome.Exception?.Message ?? "Unknown";
                    logger.LogWarning(
                        "Retry {RetryCount} for {Source} after {Delay}ms. Reason: {Reason}",
                        retryCount, sourceName, timespan.TotalMilliseconds, reason);
                });
    }

    /// <summary>
    /// Creates a circuit breaker policy to prevent cascading failures
    /// </summary>
    public static IAsyncPolicy<HttpResponseMessage> GetCircuitBreakerPolicy(ILogger logger, string sourceName)
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .OrResult(msg => (int)msg.StatusCode >= 500)
            .CircuitBreakerAsync(
                handledEventsAllowedBeforeBreaking: 5,
                durationOfBreak: TimeSpan.FromSeconds(30),
                onBreak: (result, duration) =>
                {
                    logger.LogError(
                        "Circuit breaker opened for {Source}. Duration: {Duration}s",
                        sourceName, duration.TotalSeconds);
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
    /// Creates a timeout policy to prevent hanging requests
    /// </summary>
    public static IAsyncPolicy<HttpResponseMessage> GetTimeoutPolicy(int timeoutSeconds = 10)
    {
        return Policy.TimeoutAsync<HttpResponseMessage>(timeoutSeconds);
    }

    /// <summary>
    /// Combines all resilience policies in the correct order
    /// </summary>
    public static IAsyncPolicy<HttpResponseMessage> GetCombinedPolicy(ILogger logger, string sourceName, int timeoutSeconds = 10)
    {
        // Order matters: Retry (outer) -> CircuitBreaker -> Timeout (inner)
        return Policy.WrapAsync(
            GetRetryPolicy(logger, sourceName),
            GetCircuitBreakerPolicy(logger, sourceName),
            GetTimeoutPolicy(timeoutSeconds)
        );
    }

    /// <summary>
    /// Creates a fallback policy with cached data
    /// </summary>
    public static IAsyncPolicy<T> GetFallbackPolicy<T>(
        ILogger logger,
        string operation,
        Func<CancellationToken, Task<T>> fallbackAction)
    {
        return Policy<T>
            .Handle<Exception>()
            .FallbackAsync(
                fallbackAction: async (ct) => await fallbackAction(ct),
                onFallbackAsync: async (delegateResult) =>
                {
                    logger.LogWarning(
                        "Fallback triggered for {Operation}. Exception: {Exception}",
                        operation, delegateResult.Exception?.Message ?? "Unknown");
                    await Task.CompletedTask;
                });
    }

    /// <summary>
    /// Creates a bulkhead isolation policy to limit concurrent operations
    /// </summary>
    public static IAsyncPolicy GetBulkheadPolicy(int maxParallelization, int maxQueuingActions)
    {
        return Policy
            .BulkheadAsync(maxParallelization, maxQueuingActions);
    }
}
