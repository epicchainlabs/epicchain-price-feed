using System;
using System.Threading.Tasks;
using PriceFeed.Core.Models;

namespace PriceFeed.Core.Interfaces
{
    /// <summary>
    /// Interface for services that create and verify attestations
    /// </summary>
    public interface IAttestationService
    {
        /// <summary>
        /// Creates an attestation for the TEE account
        /// </summary>
        /// <param name="accountAddress">The account address</param>
        /// <param name="accountPublicKey">The account public key</param>
        /// <returns>True if the attestation was created successfully</returns>
        Task<bool> CreateAccountAttestationAsync(string accountAddress, string accountPublicKey);

        /// <summary>
        /// Verifies an account attestation
        /// </summary>
        /// <param name="attestation">The attestation to verify</param>
        /// <returns>True if the attestation is valid</returns>
        bool VerifyAccountAttestation(AccountAttestationData attestation);

        /// <summary>
        /// Creates an attestation for a price feed update
        /// </summary>
        /// <param name="batch">The price batch</param>
        /// <param name="transactionHash">The transaction hash</param>
        /// <param name="runId">The GitHub run ID</param>
        /// <param name="runNumber">The GitHub run number</param>
        /// <param name="repositoryOwner">The GitHub repository owner</param>
        /// <param name="repositoryName">The GitHub repository name</param>
        /// <param name="workflowName">The GitHub workflow name</param>
        /// <returns>True if the attestation was created successfully</returns>
        Task<bool> CreatePriceFeedAttestationAsync(
            PriceBatch batch,
            string transactionHash,
            string runId,
            string runNumber,
            string repositoryOwner,
            string repositoryName,
            string workflowName);

        /// <summary>
        /// Verifies a price feed attestation
        /// </summary>
        /// <param name="attestation">The attestation to verify</param>
        /// <returns>True if the attestation is valid</returns>
        bool VerifyPriceFeedAttestation(PriceFeedAttestationData attestation);

        /// <summary>
        /// Cleans up old price feed attestations
        /// </summary>
        /// <param name="retentionDays">The number of days to retain attestations</param>
        /// <returns>The number of attestations removed</returns>
        Task<int> CleanupOldAttestationsAsync(int retentionDays);
    }
}
