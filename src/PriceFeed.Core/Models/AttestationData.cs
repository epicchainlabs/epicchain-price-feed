using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace PriceFeed.Core.Models
{
    /// <summary>
    /// Base class for attestation data
    /// </summary>
    public abstract class AttestationData
    {
        /// <summary>
        /// The type of attestation
        /// </summary>
        public required string AttestationType { get; set; }

        /// <summary>
        /// The GitHub repository (owner/name)
        /// </summary>
        public required string GitHubRepository { get; set; }

        /// <summary>
        /// The GitHub workflow name
        /// </summary>
        public required string GitHubWorkflow { get; set; }

        /// <summary>
        /// The GitHub run ID
        /// </summary>
        public required string GitHubRunId { get; set; }

        /// <summary>
        /// The GitHub run number
        /// </summary>
        public required string GitHubRunNumber { get; set; }

        /// <summary>
        /// The signature of the attestation
        /// </summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public required string Signature { get; set; }
    }

    /// <summary>
    /// Attestation data for account generation
    /// </summary>
    public class AccountAttestationData : AttestationData
    {
        /// <summary>
        /// The Neo account address
        /// </summary>
        public required string AccountAddress { get; set; }

        /// <summary>
        /// The Neo account public key
        /// </summary>
        public required string AccountPublicKey { get; set; }

        /// <summary>
        /// The timestamp when the account was created
        /// </summary>
        public DateTime CreatedAt { get; set; }

        // For backward compatibility with tests
        public string RunId
        {
            get => GitHubRunId;
            set => GitHubRunId = value;
        }

        public string RunNumber
        {
            get => GitHubRunNumber;
            set => GitHubRunNumber = value;
        }

        public string RepoOwner
        {
            get => GitHubRepository?.Split('/').Length > 0 ? GitHubRepository.Split('/')[0] : string.Empty;
            set => GitHubRepository = value + (RepoName != string.Empty ? "/" + RepoName : "");
        }

        public string RepoName
        {
            get => GitHubRepository?.Split('/').Length > 1 ? GitHubRepository.Split('/')[1] : string.Empty;
            set => GitHubRepository = (RepoOwner != string.Empty ? RepoOwner + "/" : "") + value;
        }

        public string Workflow
        {
            get => GitHubWorkflow;
            set => GitHubWorkflow = value;
        }

        public string Timestamp
        {
            get => CreatedAt.ToString("o");
            set => CreatedAt = DateTime.Parse(value);
        }
    }

    /// <summary>
    /// Attestation data for price feed updates
    /// </summary>
    public class PriceFeedAttestationData : AttestationData
    {
        /// <summary>
        /// The batch ID
        /// </summary>
        public required string BatchId { get; set; }

        /// <summary>
        /// The transaction hash
        /// </summary>
        public required string TransactionHash { get; set; }

        /// <summary>
        /// The number of prices in the batch
        /// </summary>
        public int PriceCount { get; set; }

        /// <summary>
        /// The timestamp of the attestation
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Summaries of the prices in the batch
        /// </summary>
        public List<PriceSummary> PriceSummaries { get; set; } = new List<PriceSummary>();

        // For backward compatibility with tests
        public string RunId
        {
            get => GitHubRunId;
            set => GitHubRunId = value;
        }

        public string RunNumber
        {
            get => GitHubRunNumber;
            set => GitHubRunNumber = value;
        }

        public string RepoOwner
        {
            get => GitHubRepository?.Split('/').Length > 0 ? GitHubRepository.Split('/')[0] : string.Empty;
            set => GitHubRepository = value + (RepoName != string.Empty ? "/" + RepoName : "");
        }

        public string RepoName
        {
            get => GitHubRepository?.Split('/').Length > 1 ? GitHubRepository.Split('/')[1] : string.Empty;
            set => GitHubRepository = (RepoOwner != string.Empty ? RepoOwner + "/" : "") + value;
        }

        public string Workflow
        {
            get => GitHubWorkflow;
            set => GitHubWorkflow = value;
        }
    }

    /// <summary>
    /// Summary of a price for attestation
    /// </summary>
    public class PriceSummary
    {
        /// <summary>
        /// The symbol
        /// </summary>
        public required string Symbol { get; set; }

        /// <summary>
        /// The price
        /// </summary>
        public double Price { get; set; }

        /// <summary>
        /// The confidence score
        /// </summary>
        public int ConfidenceScore { get; set; }
    }
}
