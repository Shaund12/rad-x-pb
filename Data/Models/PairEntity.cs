// Data/Models/PairEntity.cs
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RadXPriceBot.Data.Models
{
    public class PairEntity
    {
        [Key]
        public int Id { get; set; }
        
        [Required, MaxLength(42)]
        public string Address { get; set; }
        
        [Required]
        public int Token0Id { get; set; }
        
        [Required]
        public int Token1Id { get; set; }
        
        [ForeignKey("Token0Id")]
        public TokenEntity Token0 { get; set; }
        
        [ForeignKey("Token1Id")]
        public TokenEntity Token1 { get; set; }
        
        public decimal LastPrice { get; set; }
        
        public decimal Reserve0 { get; set; }
        
        public decimal Reserve1 { get; set; }
        
        public decimal Liquidity { get; set; }
        
        public string FactoryAddress { get; set; }
        
        public DateTime LastUpdated { get; set; }
        
        public DateTime Created { get; set; } = DateTime.UtcNow;
        
        public ICollection<PriceHistoryEntity> PriceHistory { get; set; }
        
        public ICollection<ReserveHistoryEntity> ReserveHistory { get; set; }
        
        // Get pair name based on token symbols
        [NotMapped]
        public string Name => $"{Token0?.Symbol}/{Token1?.Symbol}";
    }
}
