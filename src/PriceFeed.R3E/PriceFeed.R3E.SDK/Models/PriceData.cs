using System;
using System.Numerics;

namespace PriceFeed.R3E.SDK.Models
{
    /// <summary>
    /// Represents price data from the R3E PriceFeed Oracle
    /// </summary>
    public class PriceData
    {
        /// <summary>
        /// Trading pair symbol (e.g., "BTCUSDT")
        /// </summary>
        public string Symbol { get; set; } = string.Empty;

        /// <summary>
        /// Current price in USD (converted from contract's 8-decimal format)
        /// </summary>
        public decimal Price { get; set; }

        /// <summary>
        /// Raw price value from contract (BigInteger with 8 decimals)
        /// </summary>
        public BigInteger RawPrice { get; set; }

        /// <summary>
        /// Unix timestamp of the last price update (milliseconds)
        /// </summary>
        public long Timestamp { get; set; }

        /// <summary>
        /// DateTime representation of the timestamp
        /// </summary>
        public DateTime UpdateTime => DateTimeOffset.FromUnixTimeMilliseconds(Timestamp).DateTime;

        /// <summary>
        /// Confidence score (0-100)
        /// </summary>
        public int Confidence { get; set; }

        /// <summary>
        /// Age of the price data in seconds
        /// </summary>
        public int AgeSeconds => (int)(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - Timestamp) / 1000;

        /// <summary>
        /// Whether the price data is considered fresh (less than 1 hour old)
        /// </summary>
        public bool IsFresh => AgeSeconds < 3600;

        /// <summary>
        /// Whether the confidence score meets the minimum threshold (≥50%)
        /// </summary>
        public bool IsHighConfidence => Confidence >= 50;

        /// <summary>
        /// Overall quality score combining freshness and confidence
        /// </summary>
        public double QualityScore
        {
            get
            {
                var freshnessScore = Math.Max(0, 1.0 - (AgeSeconds / 3600.0)); // Decreases over 1 hour
                var confidenceScore = Confidence / 100.0;
                return (freshnessScore + confidenceScore) / 2.0;
            }
        }

        public override string ToString()
        {
            return $"{Symbol}: ${Price:F2} (Confidence: {Confidence}%, Age: {AgeSeconds}s)";
        }
    }

    /// <summary>
    /// Represents contract information
    /// </summary>
    public class ContractInfo
    {
        public string Version { get; set; } = string.Empty;
        public string Framework { get; set; } = string.Empty;
        public string Compiler { get; set; } = string.Empty;
        public bool IsInitialized { get; set; }
        public string Owner { get; set; } = string.Empty;
        public bool IsPaused { get; set; }
    }

    /// <summary>
    /// Represents a price update event
    /// </summary>
    public class PriceUpdateEvent
    {
        public string Symbol { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public long Timestamp { get; set; }
        public int Confidence { get; set; }
        public string TransactionHash { get; set; } = string.Empty;
        public long BlockIndex { get; set; }
        public DateTime EventTime => DateTimeOffset.FromUnixTimeMilliseconds(Timestamp).DateTime;
    }

    /// <summary>
    /// Configuration for the SDK
    /// </summary>
    public class PriceFeedConfig
    {
        /// <summary>
        /// Neo RPC endpoint URL
        /// </summary>
        public string RpcEndpoint { get; set; } = "http://seed1t5.neo.org:20332";

        /// <summary>
        /// R3E PriceFeed contract script hash
        /// </summary>
        public string ContractHash { get; set; } = string.Empty;

        /// <summary>
        /// HTTP timeout for RPC calls (seconds)
        /// </summary>
        public int TimeoutSeconds { get; set; } = 30;

        /// <summary>
        /// Cache duration for price data (seconds)
        /// </summary>
        public int CacheDurationSeconds { get; set; } = 60;

        /// <summary>
        /// Whether to validate price data freshness
        /// </summary>
        public bool ValidateFreshness { get; set; } = true;

        /// <summary>
        /// Maximum acceptable age for price data (seconds)
        /// </summary>
        public int MaxDataAgeSeconds { get; set; } = 3600;

        /// <summary>
        /// Minimum acceptable confidence score
        /// </summary>
        public int MinConfidenceScore { get; set; } = 50;
    }

    /// <summary>
    /// Exception thrown when price data is not available or invalid
    /// </summary>
    public class PriceDataException : Exception
    {
        public string Symbol { get; }
        public PriceDataException(string symbol, string message) : base(message)
        {
            Symbol = symbol;
        }
        public PriceDataException(string symbol, string message, Exception innerException) : base(message, innerException)
        {
            Symbol = symbol;
        }
    }

    /// <summary>
    /// Exception thrown when contract operations fail
    /// </summary>
    public class ContractException : Exception
    {
        public string ContractHash { get; }
        public ContractException(string contractHash, string message) : base(message)
        {
            ContractHash = contractHash;
        }
        public ContractException(string contractHash, string message, Exception innerException) : base(message, innerException)
        {
            ContractHash = contractHash;
        }
    }
}