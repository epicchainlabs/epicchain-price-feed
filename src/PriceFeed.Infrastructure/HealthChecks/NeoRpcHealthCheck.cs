using System.Net.Http.Json;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PriceFeed.Core.Options;

namespace PriceFeed.Infrastructure.HealthChecks;

/// <summary>
/// Health check for Neo RPC endpoint connectivity
/// </summary>
public class NeoRpcHealthCheck : IHealthCheck
{
    private readonly HttpClient _httpClient;
    private readonly BatchProcessingOptions _options;
    private readonly ILogger<NeoRpcHealthCheck> _logger;

    public NeoRpcHealthCheck(
        IHttpClientFactory httpClientFactory,
        IOptions<BatchProcessingOptions> options,
        ILogger<NeoRpcHealthCheck> logger)
    {
        _httpClient = httpClientFactory.CreateClient("Neo");
        _options = options.Value;
        _logger = logger;

        if (!string.IsNullOrEmpty(_options.RpcEndpoint))
        {
            _httpClient.BaseAddress = new Uri(_options.RpcEndpoint);
        }
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrEmpty(_options.RpcEndpoint))
            {
                return HealthCheckResult.Unhealthy("Neo RPC endpoint not configured");
            }

            // Send a simple RPC request to check connectivity
            var request = new
            {
                jsonrpc = "2.0",
                method = "getblockcount",
                @params = Array.Empty<object>(),
                id = 1
            };

            var response = await _httpClient.PostAsJsonAsync("", request, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync(cancellationToken);

                if (content.Contains("\"result\""))
                {
                    return HealthCheckResult.Healthy("Neo RPC endpoint is responsive", new Dictionary<string, object>
                    {
                        { "endpoint", _options.RpcEndpoint },
                        { "status_code", (int)response.StatusCode }
                    });
                }
                else
                {
                    return HealthCheckResult.Degraded("Neo RPC endpoint returned unexpected response", null, new Dictionary<string, object>
                    {
                        { "endpoint", _options.RpcEndpoint },
                        { "response", content.Substring(0, Math.Min(200, content.Length)) }
                    });
                }
            }
            else
            {
                return HealthCheckResult.Unhealthy($"Neo RPC endpoint returned {response.StatusCode}", null, new Dictionary<string, object>
                {
                    { "endpoint", _options.RpcEndpoint },
                    { "status_code", (int)response.StatusCode }
                });
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error during Neo RPC health check");
            return HealthCheckResult.Unhealthy("Failed to connect to Neo RPC endpoint", ex, new Dictionary<string, object>
            {
                { "endpoint", _options.RpcEndpoint ?? "not configured" },
                { "error", ex.Message }
            });
        }
        catch (TaskCanceledException)
        {
            return HealthCheckResult.Unhealthy("Neo RPC endpoint request timed out", null, new Dictionary<string, object>
            {
                { "endpoint", _options.RpcEndpoint ?? "not configured" }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during Neo RPC health check");
            return HealthCheckResult.Unhealthy("Health check failed", ex);
        }
    }
}
