using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using PriceFeed.Core.Interfaces;

namespace PriceFeed.Infrastructure.HealthChecks;

/// <summary>
/// Health check for verifying data source availability
/// </summary>
public class DataSourceHealthCheck : IHealthCheck
{
    private readonly IEnumerable<IDataSourceAdapter> _dataSourceAdapters;
    private readonly ILogger<DataSourceHealthCheck> _logger;

    public DataSourceHealthCheck(
        IEnumerable<IDataSourceAdapter> dataSourceAdapters,
        ILogger<DataSourceHealthCheck> logger)
    {
        _dataSourceAdapters = dataSourceAdapters;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var enabledAdapters = _dataSourceAdapters.Where(a => a.IsEnabled()).ToList();

            if (!enabledAdapters.Any())
            {
                return HealthCheckResult.Unhealthy("No enabled data sources");
            }

            var healthyCount = 0;
            var unhealthyReasons = new List<string>();

            foreach (var adapter in enabledAdapters)
            {
                try
                {
                    // Try to fetch a single symbol to test connectivity
                    var testSymbol = "BTCUSDT";
                    var priceData = await adapter.GetPriceDataAsync(testSymbol);

                    if (priceData != null && priceData.Price > 0)
                    {
                        healthyCount++;
                    }
                    else
                    {
                        unhealthyReasons.Add($"{adapter.SourceName}: Invalid price data");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Health check failed for {Source}", adapter.SourceName);
                    unhealthyReasons.Add($"{adapter.SourceName}: {ex.Message}");
                }
            }

            var healthPercentage = (double)healthyCount / enabledAdapters.Count * 100;
            var data = new Dictionary<string, object>
            {
                { "healthy_sources", healthyCount },
                { "total_sources", enabledAdapters.Count },
                { "health_percentage", healthPercentage }
            };

            if (healthPercentage >= 75)
            {
                return HealthCheckResult.Healthy($"{healthyCount}/{enabledAdapters.Count} data sources healthy", data);
            }
            else if (healthPercentage >= 50)
            {
                return HealthCheckResult.Degraded($"Only {healthyCount}/{enabledAdapters.Count} data sources healthy: {string.Join("; ", unhealthyReasons)}", null, data);
            }
            else
            {
                return HealthCheckResult.Unhealthy($"Only {healthyCount}/{enabledAdapters.Count} data sources healthy: {string.Join("; ", unhealthyReasons)}", null, data);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during data source health check");
            return HealthCheckResult.Unhealthy("Health check failed", ex);
        }
    }
}
