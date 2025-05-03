// Data/AppDbContext.cs
using System;
using System.IO;
using Microsoft.EntityFrameworkCore;
using RadXPriceBot.Data.Models;

namespace RadXPriceBot.Data
{
    public class AppDbContext : DbContext
    {
        // Define DbSets for our tables
        public DbSet<TokenEntity> Tokens { get; set; }
        public DbSet<PairEntity> Pairs { get; set; }
        public DbSet<PriceHistoryEntity> PriceHistory { get; set; }
        public DbSet<ReserveHistoryEntity> ReserveHistory { get; set; }
        
        // Path to database file
        private static readonly string DbPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "RadXPriceBot",
            "radxpricebot.db");
            
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            // Ensure directory exists
            Directory.CreateDirectory(Path.GetDirectoryName(DbPath));
            
            // Configure SQLite with connection string
            optionsBuilder.UseSqlite($"Data Source={DbPath}");
        }
        
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            
            // Configure relationships and indexes
            
            // Token configuration
            modelBuilder.Entity<TokenEntity>()
                .HasIndex(t => t.Address)
                .IsUnique();
                
            // Pair configuration
            modelBuilder.Entity<PairEntity>()
                .HasIndex(p => p.Address)
                .IsUnique();
                
            modelBuilder.Entity<PairEntity>()
                .HasOne(p => p.Token0)
                .WithMany()
                .HasForeignKey(p => p.Token0Id)
                .OnDelete(DeleteBehavior.Restrict);
                
            modelBuilder.Entity<PairEntity>()
                .HasOne(p => p.Token1)
                .WithMany()
                .HasForeignKey(p => p.Token1Id)
                .OnDelete(DeleteBehavior.Restrict);
                
            // Price history configuration
            modelBuilder.Entity<PriceHistoryEntity>()
                .HasIndex(p => new { p.PairId, p.Timestamp });
                
            modelBuilder.Entity<PriceHistoryEntity>()
                .HasOne(p => p.Pair)
                .WithMany(p => p.PriceHistory)
                .HasForeignKey(p => p.PairId);
                
            // Reserve history configuration
            modelBuilder.Entity<ReserveHistoryEntity>()
                .HasIndex(r => new { r.PairId, r.Timestamp });
                
            modelBuilder.Entity<ReserveHistoryEntity>()
                .HasOne(r => r.Pair)
                .WithMany(p => p.ReserveHistory)
                .HasForeignKey(r => r.PairId);
        }
    }
}
