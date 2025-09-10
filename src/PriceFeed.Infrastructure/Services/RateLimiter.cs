using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace PriceFeed.Infrastructure.Services
{
    /// <summary>
    /// Provides rate limiting functionality for API calls
    /// </summary>
    public class RateLimiter
    {
        private readonly ConcurrentDictionary<string, SemaphoreSlim> _semaphores = new();
        private readonly ConcurrentDictionary<string, DateTime> _lastRequestTimes = new();
        private readonly ConcurrentDictionary<string, TimeSpan> _requestIntervals = new();

        /// <summary>
        /// Initializes a new instance of the <see cref="RateLimiter"/> class
        /// </summary>
        public RateLimiter()
        {
        }

        /// <summary>
        /// Configures rate limiting for a specific API
        /// </summary>
        /// <param name="apiName">The name of the API</param>
        /// <param name="requestsPerSecond">The maximum number of requests per second</param>
        public void Configure(string apiName, double requestsPerSecond)
        {
            if (requestsPerSecond <= 0)
                throw new ArgumentException("Requests per second must be greater than zero", nameof(requestsPerSecond));

            // Calculate the interval between requests
            TimeSpan interval = TimeSpan.FromSeconds(1.0 / requestsPerSecond);

            // Create or update the semaphore and interval
            _semaphores.AddOrUpdate(apiName, _ => new SemaphoreSlim(1, 1), (_, existing) => existing);
            _requestIntervals.AddOrUpdate(apiName, interval, (_, _) => interval);
            _lastRequestTimes.TryAdd(apiName, DateTime.MinValue);
        }

        /// <summary>
        /// Waits for rate limit to allow a request
        /// </summary>
        /// <param name="apiName">The name of the API</param>
        /// <param name="cancellationToken">A cancellation token</param>
        /// <returns>A task that completes when the request can be made</returns>
        public async Task WaitAsync(string apiName, CancellationToken cancellationToken = default)
        {
            // Get or create the semaphore for this API
            var semaphore = _semaphores.GetOrAdd(apiName, _ => new SemaphoreSlim(1, 1));

            // Wait for the semaphore
            await semaphore.WaitAsync(cancellationToken);

            try
            {
                // Get the last request time and interval
                if (_lastRequestTimes.TryGetValue(apiName, out var lastRequestTime) &&
                    _requestIntervals.TryGetValue(apiName, out var interval))
                {
                    // Calculate the time to wait
                    var timeSinceLastRequest = DateTime.UtcNow - lastRequestTime;
                    var timeToWait = interval - timeSinceLastRequest;

                    // Wait if necessary
                    if (timeToWait > TimeSpan.Zero)
                    {
                        await Task.Delay(timeToWait, cancellationToken);
                    }
                }

                // Update the last request time
                _lastRequestTimes[apiName] = DateTime.UtcNow;
            }
            finally
            {
                // Release the semaphore
                semaphore.Release();
            }
        }
    }
}
