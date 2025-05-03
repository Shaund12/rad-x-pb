// Data/Models/BotConfigEntity.cs
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;

namespace RadXPriceBot.Data.Models
{
    public class BotConfigEntity
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(50)]
        public string BotId { get; set; }

        [Required]
        [MaxLength(100)]
        public string Name { get; set; }

        [Required]
        public string Token { get; set; }

        [Required]
        public string GuildId { get; set; }

        [Required]
        public string RpcUrl { get; set; }

        [Required]
        public string SwapRouterAddress { get; set; }

        // Stored as comma-separated list of addresses
        public string PathAddresses { get; set; }

        // Direct references to token addresses for easier querying
        public string Token0Address { get; set; }
        public string Token1Address { get; set; }

        // LP pair address
        public string PairAddress { get; set; }

        // Factory address for the LP
        public string FactoryAddress { get; set; }

        [MaxLength(100)]
        public string Nickname { get; set; }

        [MaxLength(50)]
        public string StatusType { get; set; } = "Price";

        [MaxLength(200)]
        public string CustomStatus { get; set; }

        public int UpdateIntervalSeconds { get; set; } = 30;

        // Discord channel settings
        public bool SendPeriodicEmbeds { get; set; } = false;
        public int EmbedIntervalMinutes { get; set; } = 60;
        public string EmbedChannelId { get; set; }
        public string EmbedColor { get; set; } = "#50E999";
        public bool IncludeChartInEmbed { get; set; } = true;
        public bool IncludeTokenInfoInEmbed { get; set; } = true;
        public bool IncludeLiquidityInfoInEmbed { get; set; } = true;

        // Swap transaction monitoring
        public bool MonitorSwapTransactions { get; set; } = true;
        public string SwapNotificationChannelId { get; set; }
        public int SwapCheckIntervalMs { get; set; } = 15000;

        // Threshold settings
        public decimal MinimumBuyThresholdUsd { get; set; } = 10.0m;
        public decimal MinimumSellThresholdUsd { get; set; } = 10.0m;
        public bool NotifyOnBuys { get; set; } = true;
        public bool NotifyOnSells { get; set; } = true;

        // Command channel settings
        public string CommandChannelId { get; set; }
        public bool RestrictCommandsToChannel { get; set; } = false;

        // Admin settings
        public string AdminRoleId { get; set; }
        public bool RequireAdminRole { get; set; } = false;

        // Metadata
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
        public DateTime? LastStarted { get; set; }
        public bool IsEnabled { get; set; } = true;

        // Navigation property to related PairEntity (if exists)
        public int? PairEntityId { get; set; }

        [ForeignKey("PairEntityId")]
        public PairEntity PairEntity { get; set; }

        // Helper method to convert path string to List<string>
        [NotMapped]
        public List<string> Path
        {
            get => string.IsNullOrEmpty(PathAddresses)
                ? new List<string>()
                : PathAddresses.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();
            set
            {
                PathAddresses = value != null && value.Any()
                    ? string.Join(",", value)
                    : null;

                // Update Token0Address and Token1Address
                if (value != null && value.Count >= 2)
                {
                    Token0Address = value[0];
                    Token1Address = value[1];
                }
            }
        }
    }
}
