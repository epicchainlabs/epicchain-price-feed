using Microsoft.EntityFrameworkCore;
using PriceFeed.R3E.EventIndexer.Models;

namespace PriceFeed.R3E.EventIndexer.Data
{
    public class EventIndexerContext : DbContext
    {
        public EventIndexerContext(DbContextOptions<EventIndexerContext> options) : base(options)
        {
        }

        public DbSet<ContractEvent> ContractEvents { get; set; }
        public DbSet<IndexerState> IndexerStates { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Configure ContractEvent
            modelBuilder.Entity<ContractEvent>(entity =>
            {
                entity.HasIndex(e => e.TransactionHash);
                entity.HasIndex(e => e.ContractHash);
                entity.HasIndex(e => e.EventName);
                entity.HasIndex(e => e.BlockIndex);
                entity.HasIndex(e => e.Timestamp);
                entity.HasIndex(e => new { e.ContractHash, e.EventName, e.BlockIndex });
            });

            // Configure IndexerState
            modelBuilder.Entity<IndexerState>(entity =>
            {
                entity.HasIndex(e => e.LastProcessedBlock);
            });

            base.OnModelCreating(modelBuilder);
        }
    }
}