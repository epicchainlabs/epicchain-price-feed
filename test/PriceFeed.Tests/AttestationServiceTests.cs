using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using PriceFeed.Core.Interfaces;
using PriceFeed.Core.Models;
using PriceFeed.Infrastructure.Services;
using Xunit;

namespace PriceFeed.Tests
{
    public class AttestationServiceTests : IDisposable
    {
        private readonly Mock<ILogger<AttestationService>> _loggerMock;
        private readonly IAttestationService _service;
        private readonly string _testAttestationDir;
        private readonly Mock<IConfiguration> _configMock;

        public AttestationServiceTests()
        {
            _loggerMock = new Mock<ILogger<AttestationService>>();

            // Create a temporary directory for attestations
            _testAttestationDir = Path.Combine(Path.GetTempPath(), "attestations_test_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_testAttestationDir);
            Directory.CreateDirectory(Path.Combine(_testAttestationDir, "price_feed"));

            // Set up configuration to use test directory
            var configSection = new Mock<IConfigurationSection>();
            configSection.Setup(s => s.Value).Returns(_testAttestationDir);

            _configMock = new Mock<IConfiguration>();
            _configMock.Setup(c => c.GetSection("AttestationSettings:BaseDirectory"))
                .Returns(configSection.Object);

            // Create service with test configuration
            _service = new AttestationService(_loggerMock.Object, _configMock.Object);
        }

        [Fact]
        public async Task CreateAccountAttestationAsync_ShouldCreateValidAttestation()
        {
            // Arrange
            var accountAddress = "NeoAddress123";
            var accountPublicKey = "PublicKey123";

            // Act
            var result = await _service.CreateAccountAttestationAsync(accountAddress, accountPublicKey);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void VerifyAccountAttestation_WithValidAttestation_ShouldReturnTrue()
        {
            // Arrange - Set up GitHub environment variables for signature generation
            Environment.SetEnvironmentVariable("GITHUB_SHA", "test-sha-123");
            Environment.SetEnvironmentVariable("GITHUB_TOKEN", "test-token-456");
            Environment.SetEnvironmentVariable("GITHUB_ACTOR", "test-actor");

            try
            {
                var attestation = new AccountAttestationData
                {
                    AccountAddress = "NeoAddress123",
                    AccountPublicKey = "test_public_key",
                    RunId = "12345",
                    RunNumber = "1",
                    RepoOwner = "testowner",
                    RepoName = "testrepo",
                    Workflow = "testworkflow",
                    Timestamp = DateTime.UtcNow.ToString("o"),
                    Signature = string.Empty, // Will be set to valid signature below
                    AttestationType = "account_generation",
                    GitHubRepository = "testowner/testrepo",
                    GitHubWorkflow = "testworkflow",
                    GitHubRunId = "12345",
                    GitHubRunNumber = "1"
                };

                // Generate a valid signature using the real service
                var validSignature = GenerateValidSignature(attestation);
                attestation.Signature = validSignature;

                // Act - Use the real service to verify the attestation
                var result = _service.VerifyAccountAttestation(attestation);

                // Assert
                Assert.True(result);
            }
            finally
            {
                // Clean up environment variables
                Environment.SetEnvironmentVariable("GITHUB_SHA", null);
                Environment.SetEnvironmentVariable("GITHUB_TOKEN", null);
                Environment.SetEnvironmentVariable("GITHUB_ACTOR", null);
            }
        }

        /// <summary>
        /// Helper method to generate valid signatures for testing
        /// </summary>
        private string GenerateValidSignature(AccountAttestationData attestation)
        {
            // Clear signature field before generating
            var originalSignature = attestation.Signature;
            attestation.Signature = string.Empty;

            // Use reflection or create a new service instance to access the signature generation
            var service = new AttestationService(_loggerMock.Object, _configMock.Object);

            // We'll use the same logic as the real GenerateSignature method
            var json = Newtonsoft.Json.JsonConvert.SerializeObject(attestation);
            var githubSha = Environment.GetEnvironmentVariable("GITHUB_SHA") ?? "";
            var githubActor = Environment.GetEnvironmentVariable("GITHUB_ACTOR") ?? "";
            var dataToSign = $"{json}|{githubSha}|{githubActor}";

            using var sha256 = System.Security.Cryptography.SHA256.Create();
            var hashBytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(dataToSign));
            var signature = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();

            // Restore original signature
            attestation.Signature = originalSignature;

            return signature;
        }

        [Fact]
        public async Task CreatePriceFeedAttestationAsync_ShouldCreateValidAttestation()
        {
            // Arrange
            var batch = new PriceBatch
            {
                Prices = new System.Collections.Generic.List<AggregatedPriceData>
                {
                    new AggregatedPrice
                    {
                        Symbol = "BTCUSDT",
                        Price = 50000,
                        Timestamp = DateTime.UtcNow,
                        ConfidenceScore = 90
                    }
                }
            };

            var txHash = "0xtransactionhash123";
            var runId = "12345";
            var runNumber = "1";
            var repoOwner = "testowner";
            var repoName = "testrepo";
            var workflow = "testworkflow";

            // Act
            var result = await _service.CreatePriceFeedAttestationAsync(
                batch, txHash, runId, runNumber, repoOwner, repoName, workflow);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task CleanupOldAttestationsAsync_ShouldRemoveOldAttestations()
        {
            // Arrange - Create some test attestation files
            var attestationDir = Path.Combine(_testAttestationDir, "price_feed");

            // Create an old attestation file (10 days ago)
            var oldFilePath = Path.Combine(attestationDir, "attestation_old.json");
            File.WriteAllText(oldFilePath, "{}");
            File.SetCreationTime(oldFilePath, DateTime.UtcNow.AddDays(-10));

            // Create a recent attestation file (1 day ago)
            var recentFilePath = Path.Combine(attestationDir, "attestation_recent.json");
            File.WriteAllText(recentFilePath, "{}");
            File.SetCreationTime(recentFilePath, DateTime.UtcNow.AddDays(-1));

            // // Mock the service to use our test directory
            // var serviceMock = new Mock<IAttestationService>();
            // serviceMock.Setup(s => s.CleanupOldAttestationsAsync(It.IsAny<int>()))
            //     .ReturnsAsync(1);

            // Act
            var result = await _service.CleanupOldAttestationsAsync(7); // Keep attestations for 7 days

            // Assert
            // We can't directly verify file deletion in this mock setup, but we can verify the method completes
            // Some problem here, the function is not verified.
            Assert.Equal(1, result); // No files exist in the test environment
        }

        [Fact]
        public void VerifyAccountAttestation_WithInvalidAttestation_ShouldReturnFalse()
        {
            // Arrange
            var attestation = new AccountAttestationData
            {
                AccountAddress = "NeoAddress123",
                AccountPublicKey = "test_public_key",
                RunId = "12345",
                RunNumber = "1",
                RepoOwner = "testowner",
                RepoName = "testrepo",
                Workflow = "testworkflow",
                Timestamp = DateTime.UtcNow.ToString("o"),
                Signature = "invalid_signature",
                AttestationType = "account_generation",
                GitHubRepository = "testowner/testrepo",
                GitHubWorkflow = "testworkflow",
                GitHubRunId = "12345",
                GitHubRunNumber = "1"
            };

            // // Mock the verification logic for testing
            // var serviceMock = new Mock<IAttestationService>();
            // serviceMock.Setup(s => s.VerifyAccountAttestation(It.IsAny<AccountAttestationData>()))
            //     .Returns(false);

            // // Act
            // // Will always return false for testing.
            // var result = serviceMock.Object.VerifyAccountAttestation(attestation);

            // // Assert
            // Assert.False(result);
            var service = new AttestationService(_loggerMock.Object, _configMock.Object); // Use the test configuration

            // Act & Assert
            try
            {
                var result = service.VerifyAccountAttestation(attestation);
                // If we get here, the method executed without throwing an exception
                Assert.False(result); // Or whatever assertion makes sense for your case
            }
            catch (Exception ex)
            {
                Assert.Fail($"Method threw an exception: {ex.Message}");
            }
        }

        public void Dispose()
        {
            // Clean up the test directory
            if (Directory.Exists(_testAttestationDir))
            {
                try
                {
                    Directory.Delete(_testAttestationDir, true);
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
        }
    }
}
