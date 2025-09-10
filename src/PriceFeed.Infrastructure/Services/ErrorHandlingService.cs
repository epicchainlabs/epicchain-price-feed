using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace PriceFeed.Infrastructure.Services;

/// <summary>
/// Service for centralized error handling and monitoring
/// </summary>
public class ErrorHandlingService : IErrorHandlingService
{
    private readonly ILogger<ErrorHandlingService> _logger;
    private readonly ConcurrentDictionary<string, ErrorMetrics> _errorMetrics = new();
    private readonly TimeSpan _metricsWindow = TimeSpan.FromMinutes(5);

    public ErrorHandlingService(ILogger<ErrorHandlingService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Records an error and determines if the error threshold has been exceeded
    /// </summary>
    public bool RecordError(string operation, Exception exception)
    {
        var metrics = _errorMetrics.GetOrAdd(operation, _ => new ErrorMetrics());
        metrics.RecordError();

        _logger.LogError(exception, "Error in operation {Operation}. Error count in last {Window} minutes: {ErrorCount}",
            operation, _metricsWindow.TotalMinutes, metrics.GetRecentErrorCount(_metricsWindow));

        return !metrics.IsThresholdExceeded(_metricsWindow);
    }

    /// <summary>
    /// Gets the current error rate for an operation
    /// </summary>
    public double GetErrorRate(string operation)
    {
        if (_errorMetrics.TryGetValue(operation, out var metrics))
        {
            return metrics.GetErrorRate(_metricsWindow);
        }
        return 0;
    }

    /// <summary>
    /// Resets error metrics for an operation
    /// </summary>
    public void ResetMetrics(string operation)
    {
        if (_errorMetrics.TryGetValue(operation, out var metrics))
        {
            metrics.Reset();
        }
    }

    private class ErrorMetrics
    {
        private readonly ConcurrentQueue<DateTime> _errorTimestamps = new();
        private readonly object _lock = new();
        private int _totalRequests;
        private const int MaxErrorsPerWindow = 10;
        private const double MaxErrorRate = 0.5; // 50%

        public void RecordError()
        {
            lock (_lock)
            {
                _errorTimestamps.Enqueue(DateTime.UtcNow);
                _totalRequests++;

                // Clean up old timestamps
                var cutoff = DateTime.UtcNow.Subtract(TimeSpan.FromMinutes(10));
                while (_errorTimestamps.TryPeek(out var timestamp) && timestamp < cutoff)
                {
                    _errorTimestamps.TryDequeue(out _);
                }
            }
        }

        public int GetRecentErrorCount(TimeSpan window)
        {
            lock (_lock)
            {
                var cutoff = DateTime.UtcNow.Subtract(window);
                return _errorTimestamps.Count(t => t >= cutoff);
            }
        }

        public double GetErrorRate(TimeSpan window)
        {
            lock (_lock)
            {
                if (_totalRequests == 0) return 0;
                var recentErrors = GetRecentErrorCount(window);
                return (double)recentErrors / _totalRequests;
            }
        }

        public bool IsThresholdExceeded(TimeSpan window)
        {
            lock (_lock)
            {
                var recentErrors = GetRecentErrorCount(window);
                var errorRate = GetErrorRate(window);

                return recentErrors > MaxErrorsPerWindow || errorRate > MaxErrorRate;
            }
        }

        public void Reset()
        {
            lock (_lock)
            {
                _errorTimestamps.Clear();
                _totalRequests = 0;
            }
        }
    }
}

public interface IErrorHandlingService
{
    bool RecordError(string operation, Exception exception);
    double GetErrorRate(string operation);
    void ResetMetrics(string operation);
}
