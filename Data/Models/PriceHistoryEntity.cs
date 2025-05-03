// Data/Models/PriceHistoryEntity.cs
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RadXPriceBot.Data.Models
{
    public class PriceHistoryEntity
    {
        [Key]
        public int Id { get; set; }
        
        [Required]
        public int PairId { get; set; }
        
        [ForeignKey("PairId")]
        public PairEntity Pair { get; set; }
        
        [Required]
        public DateTime Timestamp { get; set; }
        
        public decimal Price { get; set; }
        
        public decimal? UsdPrice { get; set; }
        
        public decimal? MarketCap { get; set; }
        
        public decimal? Volume24h { get; set; }
        
        // Transaction info - optional
        public string? BlockNumber { get; set; }
        
        public string? TransactionHash { get; set; }
    }
}
