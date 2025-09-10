using System;
using System.ComponentModel.DataAnnotations;
using System.Numerics;

namespace PriceFeed.R3E.EventIndexer.Models
{
    public class ContractEvent
    {
        [Key]
        public int Id { get; set; }
        
        public string TransactionHash { get; set; } = string.Empty;
        public string ContractHash { get; set; } = string.Empty;
        public string EventName { get; set; } = string.Empty;
        public long BlockIndex { get; set; }
        public DateTime Timestamp { get; set; }
        public string Data { get; set; } = string.Empty; // JSON serialized event data
        public bool Processed { get; set; }
    }

    public class PriceUpdateEvent
    {
        public string Symbol { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public long Timestamp { get; set; }
        public int Confidence { get; set; }
        public string TransactionHash { get; set; } = string.Empty;
        public long BlockIndex { get; set; }
    }

    public class OracleEvent
    {
        public string OracleAddress { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty; // Added/Removed
        public string TransactionHash { get; set; } = string.Empty;
        public long BlockIndex { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public class TeeAccountEvent
    {
        public string TeeAccountAddress { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty; // Added/Removed
        public string TransactionHash { get; set; } = string.Empty;
        public long BlockIndex { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public class ContractManagementEvent
    {
        public string EventType { get; set; } = string.Empty; // Upgraded/Paused/OwnerChanged
        public string Details { get; set; } = string.Empty; // JSON details
        public string TransactionHash { get; set; } = string.Empty;
        public long BlockIndex { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public class IndexerState
    {
        [Key]
        public int Id { get; set; }
        
        public long LastProcessedBlock { get; set; }
        public DateTime LastUpdateTime { get; set; }
        public bool IsRunning { get; set; }
    }
}