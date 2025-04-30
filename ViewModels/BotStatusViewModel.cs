using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Collections.Generic;
using RadXPriceBot.Services;

namespace RadXPriceBot.ViewModels
{
    public class BotStatusViewModel : INotifyPropertyChanged
    {
        private string _botId;
        private string _botName;
        private string _pairName;
        private string _currentPrice;
        private bool _isRunning;
        private string _status;
        private string _lastUpdated;
        private PairInfo _pairInfo;

        // New fields for enhanced embeds
        private string _priceChangeEmoji;
        private string _priceChangePercent;
        private string _liquidityEmoji;
        private string _marketTrendEmoji;
        private string _volumeChangeEmoji;
        private bool _isPriceUp;
        private bool _isHighVolume;
        private Dictionary<string, string> _tokenEmojiMap;
        private List<string> _embedTags;
        private string _customFooterText;
        private string _embedThumbnailUrl;

        public BotStatusViewModel()
        {
            // Initialize emoji dictionary for commonly used tokens
            _tokenEmojiMap = new Dictionary<string, string>
            {
                { "WVTRU", "⚡" },
                { "VTRO", "🔷" },
                { "USDC.pol", "💵" },
                { "USDT", "💲" },
                { "WETH", "🔹" },
                { "BTC", "₿" },
                { "WBTC", "₿" },
                // Add more tokens as needed
            };

            _embedTags = new List<string>();
            _priceChangeEmoji = "➖";
            _marketTrendEmoji = "📊";
            _liquidityEmoji = "💧";
            _volumeChangeEmoji = "📈";
        }

        public string BotId
        {
            get => _botId;
            set
            {
                if (_botId != value)
                {
                    _botId = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public string BotName
        {
            get => _botName;
            set
            {
                if (_botName != value)
                {
                    _botName = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public string PairName
        {
            get => _pairName;
            set
            {
                if (_pairName != value)
                {
                    _pairName = value;
                    NotifyPropertyChanged();
                    UpdateTokenEmojis();
                }
            }
        }

        public string CurrentPrice
        {
            get => _currentPrice;
            set
            {
                if (_currentPrice != value)
                {
                    // Store old price to calculate change
                    decimal oldPrice = 0;
                    decimal newPrice = 0;

                    if (!string.IsNullOrEmpty(_currentPrice) && decimal.TryParse(_currentPrice.Replace("$", "").Replace(",", ""), out oldPrice)
                        && decimal.TryParse(value.Replace("$", "").Replace(",", ""), out newPrice))
                    {
                        UpdatePriceChangeIndicators(oldPrice, newPrice);
                    }

                    _currentPrice = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public bool IsRunning
        {
            get => _isRunning;
            set
            {
                if (_isRunning != value)
                {
                    _isRunning = value;
                    Status = value ? "Running" : "Stopped";
                    NotifyPropertyChanged();
                }
            }
        }

        public string Status
        {
            get => _status;
            set
            {
                if (_status != value)
                {
                    _status = value;
                    NotifyPropertyChanged();
                    // Update status emoji based on status text
                    UpdateStatusEmoji();
                }
            }
        }

        public string LastUpdated
        {
            get => _lastUpdated;
            set
            {
                if (_lastUpdated != value)
                {
                    _lastUpdated = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public PairInfo PairInfo
        {
            get => _pairInfo;
            set
            {
                if (_pairInfo != value)
                {
                    _pairInfo = value;
                    NotifyPropertyChanged();
                    UpdatePairInfoVisuals();
                }
            }
        }

        // New properties for enhanced Discord embeds

        public string PriceChangeEmoji
        {
            get => _priceChangeEmoji;
            set
            {
                if (_priceChangeEmoji != value)
                {
                    _priceChangeEmoji = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public string PriceChangePercent
        {
            get => _priceChangePercent;
            set
            {
                if (_priceChangePercent != value)
                {
                    _priceChangePercent = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public string LiquidityEmoji
        {
            get => _liquidityEmoji;
            set
            {
                if (_liquidityEmoji != value)
                {
                    _liquidityEmoji = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public string MarketTrendEmoji
        {
            get => _marketTrendEmoji;
            set
            {
                if (_marketTrendEmoji != value)
                {
                    _marketTrendEmoji = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public string VolumeChangeEmoji
        {
            get => _volumeChangeEmoji;
            set
            {
                if (_volumeChangeEmoji != value)
                {
                    _volumeChangeEmoji = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public bool IsPriceUp
        {
            get => _isPriceUp;
            set
            {
                if (_isPriceUp != value)
                {
                    _isPriceUp = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public bool IsHighVolume
        {
            get => _isHighVolume;
            set
            {
                if (_isHighVolume != value)
                {
                    _isHighVolume = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public List<string> EmbedTags
        {
            get => _embedTags;
            set
            {
                _embedTags = value;
                NotifyPropertyChanged();
            }
        }

        public string CustomFooterText
        {
            get => _customFooterText;
            set
            {
                if (_customFooterText != value)
                {
                    _customFooterText = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public string EmbedThumbnailUrl
        {
            get => _embedThumbnailUrl;
            set
            {
                if (_embedThumbnailUrl != value)
                {
                    _embedThumbnailUrl = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public string StatusWithEmoji => GetStatusWithEmoji();

        public string EnhancedPairDisplay => GetEnhancedPairDisplay();

        public string PriceWithTrend => $"{CurrentPrice} {PriceChangeEmoji} {PriceChangePercent}";

        public event PropertyChangedEventHandler PropertyChanged;

        private void NotifyPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

            // Update dependent properties
            if (propertyName == nameof(Status))
                NotifyPropertyChanged(nameof(StatusWithEmoji));

            if (propertyName == nameof(PairName))
                NotifyPropertyChanged(nameof(EnhancedPairDisplay));

            if (propertyName == nameof(CurrentPrice) ||
                propertyName == nameof(PriceChangeEmoji) ||
                propertyName == nameof(PriceChangePercent))
                NotifyPropertyChanged(nameof(PriceWithTrend));
        }

        // Helper methods for enhanced visuals

        private void UpdatePriceChangeIndicators(decimal oldPrice, decimal newPrice)
        {
            if (newPrice > oldPrice)
            {
                PriceChangeEmoji = "🟢";
                IsPriceUp = true;

                // Calculate percent change
                decimal percentChange = Math.Round(((newPrice - oldPrice) / oldPrice) * 100, 2);
                PriceChangePercent = $"+{percentChange}%";
            }
            else if (newPrice < oldPrice)
            {
                PriceChangeEmoji = "🔴";
                IsPriceUp = false;

                // Calculate percent change
                decimal percentChange = Math.Round(((oldPrice - newPrice) / oldPrice) * 100, 2);
                PriceChangePercent = $"-{percentChange}%";
            }
            else
            {
                PriceChangeEmoji = "⚪";
                PriceChangePercent = "0%";
            }

            // Add trend tag based on significant change
            UpdateTrendTags(oldPrice, newPrice);
        }

        private void UpdateTrendTags(decimal oldPrice, decimal newPrice)
        {
            // Clear existing trend tags
            _embedTags.RemoveAll(tag => tag.Contains("trending") || tag.Contains("dip") || tag.Contains("pump"));

            decimal percentChange = Math.Abs(((newPrice - oldPrice) / oldPrice) * 100);

            if (percentChange >= 10 && newPrice > oldPrice)
            {
                _embedTags.Add("🚀 trending");
                MarketTrendEmoji = "🚀";
            }
            else if (percentChange >= 20 && newPrice > oldPrice)
            {
                _embedTags.Add("💹 big pump");
                MarketTrendEmoji = "💹";
            }
            else if (percentChange >= 10 && newPrice < oldPrice)
            {
                _embedTags.Add("📉 dipping");
                MarketTrendEmoji = "📉";
            }
            else if (percentChange >= 20 && newPrice < oldPrice)
            {
                _embedTags.Add("🔥 massive dip");
                MarketTrendEmoji = "🔥";
            }
            else
            {
                MarketTrendEmoji = "📊";
            }

            NotifyPropertyChanged(nameof(EmbedTags));
            NotifyPropertyChanged(nameof(MarketTrendEmoji));
        }

        private void UpdateStatusEmoji()
        {
            // This could be expanded based on more status values
            switch (_status?.ToLower())
            {
                case "running":
                    CustomFooterText = "✅ Bot is actively monitoring";
                    break;
                case "stopped":
                    CustomFooterText = "⛔ Bot is currently inactive";
                    break;
                case "error":
                    CustomFooterText = "⚠️ Bot encountered an error";
                    break;
                case "updating":
                    CustomFooterText = "🔄 Bot is updating data";
                    break;
                case "limited":
                    CustomFooterText = "⚡ Bot running with limited features";
                    break;
                default:
                    CustomFooterText = "ℹ️ Bot status: " + _status;
                    break;
            }
        }

        private string GetStatusWithEmoji()
        {
            switch (_status?.ToLower())
            {
                case "running":
                    return "✅ Running";
                case "stopped":
                    return "⛔ Stopped";
                case "error":
                    return "⚠️ Error";
                case "updating":
                    return "🔄 Updating";
                case "limited":
                    return "⚡ Limited";
                default:
                    return _status; // Return status without emoji if not recognized
            }
        }

        private string GetEnhancedPairDisplay()
        {
            if (string.IsNullOrEmpty(_pairName))
                return string.Empty;

            string result = _pairName;

            // Look for token emoji matches
            foreach (var token in _tokenEmojiMap.Keys)
            {
                if (_pairName.Contains(token))
                {
                    result = result.Replace(token, $"{_tokenEmojiMap[token]} {token}");
                }
            }

            return result;
        }

        private void UpdateTokenEmojis()
        {
            if (string.IsNullOrEmpty(_pairName))
                return;

            // Extract token symbols from pair name (typically formatted as TOKEN1/TOKEN2)
            string[] tokens = _pairName.Split('/');

            if (tokens.Length >= 2)
            {
                string token0 = tokens[0].Trim();
                string token1 = tokens[1].Trim();

                // Update thumbnail URL based on token (could point to stored token logos)
                // Just a placeholder - you would need actual token icon URLs
                EmbedThumbnailUrl = $"https://example.com/tokens/{token0.ToLower()}.png";

                // Add token-specific tags if they're special tokens
                UpdateTokenSpecificTags(token0, token1);
            }
        }

        private void UpdateTokenSpecificTags(string token0, string token1)
        {
            // Remove existing token tags
            _embedTags.RemoveAll(tag => tag.StartsWith("#"));

            // Add token-specific tags
            if (_tokenEmojiMap.ContainsKey(token0))
            {
                _embedTags.Add($"#{token0.ToLower()}");
            }

            if (_tokenEmojiMap.ContainsKey(token1))
            {
                _embedTags.Add($"#{token1.ToLower()}");
            }

            NotifyPropertyChanged(nameof(EmbedTags));
        }

        private void UpdatePairInfoVisuals()
        {
            if (_pairInfo == null)
                return;

            // Update liquidity emoji based on liquidity amount
            if (_pairInfo.Liquidity > 1000000) // Over 1M liquidity
            {
                LiquidityEmoji = "💰";
                _embedTags.RemoveAll(tag => tag.Contains("liquidity"));
                _embedTags.Add("💰 high liquidity");
            }
            else if (_pairInfo.Liquidity > 100000) // Over 100K liquidity
            {
                LiquidityEmoji = "💦";
                _embedTags.RemoveAll(tag => tag.Contains("liquidity"));
                _embedTags.Add("💦 good liquidity");
            }
            else
            {
                LiquidityEmoji = "💧";
                _embedTags.RemoveAll(tag => tag.Contains("liquidity"));
            }

            // Volume indicator (simplified - in a real app you might track 24h volume change)
            if (_pairInfo.IsWvtruUsdcPair || _pairInfo.IsVtroUsdcPair)
            {
                VolumeChangeEmoji = "📊";
                _embedTags.Add("🔰 key pair");
            }

            // Update holder count related visuals if available
            if (_pairInfo.HolderCount > 1000)
            {
                _embedTags.Add("👥 popular token");
            }

            NotifyPropertyChanged(nameof(LiquidityEmoji));
            NotifyPropertyChanged(nameof(VolumeChangeEmoji));
            NotifyPropertyChanged(nameof(EmbedTags));
        }

        // Method to generate a formatted embed description with emojis
        public string GetEnhancedEmbedDescription()
        {
            var description = new System.Text.StringBuilder();

            description.AppendLine($"**{PriceChangeEmoji} Price**: {CurrentPrice} ({PriceChangePercent})");
            description.AppendLine($"**{LiquidityEmoji} Liquidity**: ${PairInfo?.Liquidity:N2}");

            if (PairInfo != null)
            {
                description.AppendLine($"**{_tokenEmojiMap.GetValueOrDefault(PairInfo.Token0?.Symbol, "🔸")} {PairInfo.Token0?.Symbol}**: {PairInfo.Reserve0:N2}");
                description.AppendLine($"**{_tokenEmojiMap.GetValueOrDefault(PairInfo.Token1?.Symbol, "🔹")} {PairInfo.Token1?.Symbol}**: {PairInfo.Reserve1:N2}");

                if (PairInfo.HolderCount > 0)
                {
                    description.AppendLine($"**👥 Holders**: {PairInfo.HolderCount:N0}");
                }
            }

            // Add tags if there are any
            if (_embedTags.Count > 0)
            {
                description.AppendLine();
                description.AppendLine(string.Join(" • ", _embedTags));
            }

            return description.ToString();
        }

        // Method to get embed color based on price trend
        public int GetEmbedColor()
        {
            // Discord embed colors are decimal values
            if (IsPriceUp)
            {
                return 5763719; // Green color
            }
            else
            {
                return 15548997; // Red color
            }
        }
    }
}
