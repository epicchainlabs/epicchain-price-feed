using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PriceFeed.Core.Interfaces;
using PriceFeed.Core.Models;
using PriceFeed.Core.Options;
using PriceFeed.Infrastructure.Services;

namespace PriceFeed.Tests
{
    /// <summary>
    /// Mock implementation of the PriceFeedJob class for testing
    /// </summary>
    public class PriceFeedJob
    {
        private readonly ILogger<PriceFeedJob> _logger;
        private readonly PriceFeedOptions _options;
        private readonly IEnumerable<IDataSourceAdapter> _dataSourceAdapters;
        private readonly IPriceAggregationService _aggregationService;
        private readonly IBatchProcessingService _batchProcessingService;
        private readonly IAttestationService _attestationService;
        private readonly int _maxRetries = 3;

        /// <summary>
        /// Initializes a new instance of the <see cref="PriceFeedJob"/> class
        /// </summary>
        public PriceFeedJob(
            ILogger<PriceFeedJob> logger,
            IOptions<PriceFeedOptions> options,
            IEnumerable<IDataSourceAdapter> dataSourceAdapters,
            IPriceAggregationService aggregationService,
            IBatchProcessingService batchProcessingService,
            IAttestationService attestationService)
        {
            _logger = logger;
            _options = options.Value;
            _dataSourceAdapters = dataSourceAdapters;
            _aggregationService = aggregationService;
            _batchProcessingService = batchProcessingService;
            _attestationService = attestationService;
        }

        /// <summary>
        /// Runs the price feed job
        /// </summary>
        public async Task RunAsync()
        {
            try
            {
                _logger.LogInformation("Starting price feed job");

                // Collect price data from all enabled sources
                var priceDataBySymbol = new Dictionary<string, List<PriceData>>();
                foreach (var symbol in _options.Symbols)
                {
                    priceDataBySymbol[symbol] = new List<PriceData>();
                }

                // Filter out disabled data sources
                var enabledAdapters = _dataSourceAdapters.Where(adapter => adapter.IsEnabled()).ToList();

                if (!enabledAdapters.Any())
                {
                    _logger.LogWarning("No enabled data sources found. Please configure at least one data source API key.");
                    throw new InvalidOperationException("No enabled data sources found. Please configure at least one data source API key.");
                }

                _logger.LogInformation("Using {Count} enabled data sources: {Sources}",
                    enabledAdapters.Count,
                    string.Join(", ", enabledAdapters.Select(a => a.SourceName)));

                foreach (var adapter in enabledAdapters)
                {
                    try
                    {
                        var priceData = await adapter.GetPriceDataBatchAsync(_options.Symbols);
                        foreach (var data in priceData)
                        {
                            if (priceDataBySymbol.ContainsKey(data.Symbol))
                            {
                                priceDataBySymbol[data.Symbol].Add(data);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error getting price data from {Source}", adapter.SourceName);
                    }
                }

                // Check if we have any price data
                if (priceDataBySymbol.All(p => !p.Value.Any()))
                {
                    throw new InvalidOperationException("Failed to collect price data from any source");
                }

                // Aggregate price data
                var aggregatedPrices = await _aggregationService.AggregateBatchAsync(priceDataBySymbol.ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value.AsEnumerable()));

                // Create a batch
                var batch = new PriceBatch
                {
                    Prices = aggregatedPrices.ToList(),
                    Timestamp = DateTime.UtcNow
                };

                // Process the batch with retries
                bool success = false;
                int retryCount = 0;
                while (!success && retryCount < _maxRetries)
                {
                    success = await _batchProcessingService.ProcessBatchAsync(batch);
                    if (!success)
                    {
                        retryCount++;
                        if (retryCount < _maxRetries)
                        {
                            _logger.LogWarning("Batch processing failed, retrying ({RetryCount}/{MaxRetries})", retryCount, _maxRetries);
                            await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, retryCount))); // Exponential backoff
                        }
                    }
                }

                if (!success)
                {
                    _logger.LogError("Failed to process batch after {MaxRetries} retries", _maxRetries);
                }
                else
                {
                    _logger.LogInformation("Successfully processed batch with {Count} prices", batch.Prices.Count);
                }

                // Clean up old attestations
                await _attestationService.CleanupOldAttestationsAsync(7); // Keep attestations for 7 days

                _logger.LogInformation("Price feed job completed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error running price feed job");
                throw;
            }
        }
    }
}
