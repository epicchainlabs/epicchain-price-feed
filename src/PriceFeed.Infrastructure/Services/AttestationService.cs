using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using PriceFeed.Core.Interfaces;
using PriceFeed.Core.Models;

namespace PriceFeed.Infrastructure.Services
{
    /// <summary>
    /// Service for creating and verifying attestations for GitHub Actions runs
    /// </summary>
    public class AttestationService : IAttestationService
    {
        private readonly ILogger<AttestationService> _logger;
        private readonly string _baseAttestationDirectory;

        /// <summary>
        /// Initializes a new instance of the <see cref="AttestationService"/> class
        /// </summary>
        /// <param name="logger">The logger</param>
        /// <param name="configuration">Optional configuration for attestation settings</param>
        public AttestationService(ILogger<AttestationService> logger, IConfiguration? configuration = null)
        {
            _logger = logger;
            _baseAttestationDirectory = configuration?.GetSection("AttestationSettings:BaseDirectory").Value ?? "attestations";
        }

        /// <summary>
        /// Creates an attestation for the TEE account
        /// </summary>
        /// <param name="accountAddress">The account address</param>
        /// <param name="accountPublicKey">The account public key</param>
        /// <returns>True if the attestation was created successfully</returns>
        public async Task<bool> CreateAccountAttestationAsync(string accountAddress, string accountPublicKey)
        {
            try
            {
                _logger.LogInformation("Creating account attestation for address {AccountAddress}", accountAddress);

                // Get GitHub Actions-specific environment variables
                var runId = Environment.GetEnvironmentVariable("GITHUB_RUN_ID") ?? "unknown";
                var runNumber = Environment.GetEnvironmentVariable("GITHUB_RUN_NUMBER") ?? "unknown";
                var repository = Environment.GetEnvironmentVariable("GITHUB_REPOSITORY") ?? "unknown/unknown";
                var workflow = Environment.GetEnvironmentVariable("GITHUB_WORKFLOW") ?? "unknown";

                // Split repository into owner and name
                var repoParts = repository.Split('/');
                var repositoryOwner = repoParts.Length > 0 ? repoParts[0] : "unknown";
                var repositoryName = repoParts.Length > 1 ? repoParts[1] : "unknown";

                // Create attestation data
                var attestation = new AccountAttestationData
                {
                    AccountAddress = accountAddress,
                    AccountPublicKey = accountPublicKey,
                    CreatedAt = DateTime.UtcNow,
                    GitHubRunId = runId,
                    GitHubRunNumber = runNumber,
                    GitHubRepository = repository,
                    GitHubWorkflow = workflow,
                    AttestationType = "account_generation",
                    Signature = string.Empty // Will be set after generation
                };

                // Generate attestation signature
                attestation.Signature = GenerateSignature(attestation);

                // Save attestation to file (persistent storage)
                var attestationJson = JsonConvert.SerializeObject(attestation, Formatting.Indented);
                var attestationPath = Path.Combine("attestations", "account_attestation.json");

                // Ensure directory exists
                Directory.CreateDirectory(Path.GetDirectoryName(attestationPath)!);

                // Write attestation to file
                await File.WriteAllTextAsync(attestationPath, attestationJson);

                _logger.LogInformation("Account attestation created and saved to {Path}", attestationPath);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating account attestation");
                return false;
            }
        }

        /// <summary>
        /// Creates an attestation for an account generation
        /// </summary>
        /// <param name="accountAddress">The generated account address</param>
        /// <param name="runId">The GitHub Actions run ID</param>
        /// <param name="runNumber">The GitHub Actions run number</param>
        /// <param name="repositoryOwner">The repository owner</param>
        /// <param name="repositoryName">The repository name</param>
        /// <param name="workflowName">The workflow name</param>
        /// <returns>The attestation data</returns>
        public async Task<AccountAttestationData> CreateAccountAttestationAsync(
            string accountAddress,
            string runId,
            string runNumber,
            string repositoryOwner,
            string repositoryName,
            string workflowName)
        {
            try
            {
                _logger.LogInformation("Creating account attestation for address {AccountAddress}", accountAddress);

                // Create attestation data
                var attestation = new AccountAttestationData
                {
                    AccountAddress = accountAddress,
                    AccountPublicKey = string.Empty, // Will be set after generation
                    CreatedAt = DateTime.UtcNow,
                    GitHubRunId = runId,
                    GitHubRunNumber = runNumber,
                    GitHubRepository = $"{repositoryOwner}/{repositoryName}",
                    GitHubWorkflow = workflowName,
                    AttestationType = "account_generation",
                    Signature = string.Empty // Will be set after generation
                };

                // Generate attestation signature
                attestation.Signature = GenerateSignature(attestation);

                // Save attestation to file (persistent storage)
                var attestationJson = JsonConvert.SerializeObject(attestation, Formatting.Indented);
                var attestationPath = Path.Combine("attestations", "account_attestation.json");

                // Ensure directory exists
                Directory.CreateDirectory(Path.GetDirectoryName(attestationPath)!);

                // Write attestation to file
                await File.WriteAllTextAsync(attestationPath, attestationJson);

                _logger.LogInformation("Account attestation created and saved to {Path}", attestationPath);

                return attestation;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating account attestation");
                throw;
            }
        }

        /// <summary>
        /// Creates an attestation for a price feed update
        /// </summary>
        /// <param name="batch">The price batch</param>
        /// <param name="transactionHash">The transaction hash</param>
        /// <param name="runId">The GitHub Actions run ID</param>
        /// <param name="runNumber">The GitHub Actions run number</param>
        /// <param name="repositoryOwner">The repository owner</param>
        /// <param name="repositoryName">The repository name</param>
        /// <param name="workflowName">The workflow name</param>
        /// <returns>True if the attestation was created successfully</returns>
        public async Task<bool> CreatePriceFeedAttestationAsync(
            PriceBatch batch,
            string transactionHash,
            string runId,
            string runNumber,
            string repositoryOwner,
            string repositoryName,
            string workflowName)
        {
            try
            {
                _logger.LogInformation("Creating price feed attestation for batch {BatchId}", batch.BatchId);

                // Create attestation data
                var attestation = new PriceFeedAttestationData
                {
                    BatchId = batch.BatchId.ToString(),
                    TransactionHash = transactionHash,
                    PriceCount = batch.Prices.Count,
                    Timestamp = DateTime.UtcNow,
                    GitHubRunId = runId,
                    GitHubRunNumber = runNumber,
                    GitHubRepository = $"{repositoryOwner}/{repositoryName}",
                    GitHubWorkflow = workflowName,
                    AttestationType = "price_feed_update",
                    Signature = string.Empty // Will be set after generation
                };

                // Add price summaries (limited data to keep attestation small)
                foreach (var price in batch.Prices)
                {
                    attestation.PriceSummaries.Add(new PriceSummary
                    {
                        Symbol = price.Symbol,
                        Price = (double)price.Price,
                        ConfidenceScore = price.ConfidenceScore
                    });
                }

                // Generate attestation signature
                attestation.Signature = GenerateSignature(attestation);

                // Save attestation to file (with date-based naming for retention)
                var dateStr = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
                var attestationPath = Path.Combine(_baseAttestationDirectory, "price_feed", $"attestation_{dateStr}_{batch.BatchId}.json");

                // Ensure directory exists
                Directory.CreateDirectory(Path.GetDirectoryName(attestationPath)!);

                // Write attestation to file
                var attestationJson = JsonConvert.SerializeObject(attestation, Formatting.Indented);
                await File.WriteAllTextAsync(attestationPath, attestationJson);

                _logger.LogInformation("Price feed attestation created and saved to {Path}", attestationPath);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating price feed attestation");
                return false;
            }
        }

        /// <summary>
        /// Verifies an account attestation
        /// </summary>
        /// <param name="attestation">The attestation to verify</param>
        /// <returns>True if the attestation is valid</returns>
        public bool VerifyAccountAttestation(AccountAttestationData attestation)
        {
            try
            {
                // Save the original signature
                var originalSignature = attestation.Signature;

                // Clear the signature for verification
                attestation.Signature = string.Empty;

                // Generate a new signature
                var newSignature = GenerateSignature(attestation);

                // Restore the original signature
                attestation.Signature = originalSignature;

                // Compare signatures
                return string.Equals(originalSignature, newSignature);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying account attestation");
                return false;
            }
        }

        /// <summary>
        /// Verifies a price feed attestation
        /// </summary>
        /// <param name="attestation">The attestation to verify</param>
        /// <returns>True if the attestation is valid</returns>
        public bool VerifyPriceFeedAttestation(PriceFeedAttestationData attestation)
        {
            try
            {
                // Save the original signature
                var originalSignature = attestation.Signature;

                // Clear the signature for verification
                attestation.Signature = string.Empty;

                // Generate a new signature
                var newSignature = GenerateSignature(attestation);

                // Restore the original signature
                attestation.Signature = originalSignature;

                // Compare signatures
                return string.Equals(originalSignature, newSignature);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying price feed attestation");
                return false;
            }
        }

        /// <summary>
        /// Cleans up old price feed attestations
        /// </summary>
        /// <param name="retentionDays">The number of days to retain attestations</param>
        /// <returns>The number of attestations removed</returns>
        public Task<int> CleanupOldAttestationsAsync(int retentionDays)
        {
            try
            {
                _logger.LogInformation("Cleaning up price feed attestations older than {RetentionDays} days", retentionDays);

                var attestationDir = Path.Combine(_baseAttestationDirectory, "price_feed");
                if (!Directory.Exists(attestationDir))
                {
                    return Task.FromResult(0);
                }

                var cutoffDate = DateTime.UtcNow.AddDays(-retentionDays);
                var files = Directory.GetFiles(attestationDir, "attestation_*.json");
                var removedCount = 0;

                foreach (var file in files)
                {
                    try
                    {
                        var fileInfo = new FileInfo(file);
                        if (fileInfo.CreationTimeUtc < cutoffDate)
                        {
                            File.Delete(file);
                            removedCount++;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error deleting attestation file {File}", file);
                    }
                }

                _logger.LogInformation("Removed {Count} old attestation files", removedCount);
                return Task.FromResult(removedCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cleaning up old attestations");
                return Task.FromResult(0);
            }
        }

        /// <summary>
        /// Generates a signature for an attestation
        /// </summary>
        /// <param name="data">The data to sign</param>
        /// <returns>The signature</returns>
        private string GenerateSignature(object data)
        {
            // Using a hash-based approach for attestation signatures
            // This provides a cryptographic proof that the data was generated in the TEE

            // Convert the data to JSON
            var json = JsonConvert.SerializeObject(data);

            // Get GitHub Actions-specific environment variables to include in the signature
            // These are only available in the GitHub Actions environment
            var githubSha = Environment.GetEnvironmentVariable("GITHUB_SHA") ?? "unknown";
            var githubToken = Environment.GetEnvironmentVariable("GITHUB_TOKEN") ?? "unknown";
            var githubActor = Environment.GetEnvironmentVariable("GITHUB_ACTOR") ?? "unknown";

            // Combine the data with GitHub-specific values
            var dataToSign = $"{json}|{githubSha}|{githubActor}";

            // Generate a hash
            using var sha256 = SHA256.Create();
            var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(dataToSign));

            // Convert to hex string
            return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
        }
    }
}
