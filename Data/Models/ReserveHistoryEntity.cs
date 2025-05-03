// Data/Models/ReserveHistoryEntity.cs
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RadXPriceBot.Data.Models
{
    public class ReserveHistoryEntity
    {
        [Key]
        public int Id { get; set; }
        
        [Required]
        public int PairId { get; set; }
        
        [ForeignKey("PairId")]
        public PairEntity Pair { get; set; }
        
        [Required]
        public DateTime Timestamp { get; set; }
        
        public decimal Reserve0 { get; set; }
        
        public decimal Reserve1 { get; set; }
        
        public decimal Liquidity { get; set; }
        
        // Transaction info - optional
        public string? BlockNumber { get; set; }
        
        public string? TransactionHash { get; set; }
    }
}
