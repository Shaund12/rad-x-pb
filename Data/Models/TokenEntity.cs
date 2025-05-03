// Data/Models/TokenEntity.cs
using System;
using System.ComponentModel.DataAnnotations;

namespace RadXPriceBot.Data.Models
{
    public class TokenEntity
    {
        [Key]
        public int Id { get; set; }
        
        [Required, MaxLength(42)]
        public string Address { get; set; }
        
        [Required, MaxLength(50)]
        public string Symbol { get; set; }
        
        [Required, MaxLength(255)]
        public string Name { get; set; }
        
        public int Decimals { get; set; }
        
        public decimal TotalSupply { get; set; }
        
        public int? HolderCount { get; set; }
        
        public decimal? UsdPrice { get; set; }
        
        public DateTime LastUpdated { get; set; }
        
        public DateTime Created { get; set; } = DateTime.UtcNow;
    }
}
