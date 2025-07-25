// ViewModels/BotStatusViewModel.cs
using RadXPriceBot.Data;
using RadXPriceBot.Data.Models;
using RadXPriceBot.Services;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media; // Added for SolidColorBrush

namespace RadXPriceBot.ViewModels
{
    public class BotStatusViewModel : ViewModelBase
    {
        private readonly DatabaseService _databaseService;
        private bool _isHistoricalDataLoaded = false;
        private bool _isRunning;
        private string _currentPrice;
        private string _pairName;
        private string _status;
        private string _lastUpdated;
        private string _volume24h;
        private string _priceChange24h;
        private string _priceChange7d;
        private string _liquidityChange24h;
        private decimal _currentLiquidity;
        private int _holdersCount;
        private PairInfo _pairInfo;

        // Historical data points for different time intervals
        private decimal _price15MinAgo;
        private decimal _price30MinAgo;
        private decimal _price1HourAgo;
        private decimal _price4HourAgo;
        private decimal _price24HourAgo;
        private decimal _price7DayAgo;

        // Percentage changes for different time intervals
        private string _priceChange15Min;
        private string _priceChange30Min;
        private string _priceChange1Hour;
        private string _priceChange4Hour;

        // Emoji indicators
        private string _priceChangeEmoji;

        // Add this property to BotStatusViewModel.cs
        public bool CanStart => !IsRunning;


        public BotStatusViewModel(DatabaseService databaseService = null)
        {
            _databaseService = databaseService;
            Status = "Idle";
            _lastUpdated = DateTime.Now.ToString("HH:mm:ss");
        }

        public string BotId { get; set; }
        public string BotName { get; set; }

        public string PairName
        {
            get => _pairName;
            set
            {
                if (_pairName != value)
                {
                    _pairName = value;
                    OnPropertyChanged();
                    UpdatePairInfoVisuals();
                    // Notify dependent properties
                    OnPropertyChanged(nameof(EnhancedPairDisplay));
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
                    // Extract the numeric part of the price for tracking changes
                    decimal oldPrice = ExtractPriceValue(_currentPrice);
                    decimal newPrice = ExtractPriceValue(value);

                    _currentPrice = value;
                    OnPropertyChanged();
                    // Notify dependent properties
                    OnPropertyChanged(nameof(PriceWithTrend));

                    // Only update indicators if we have valid prices
                    if (oldPrice > 0 && newPrice > 0)
                    {
                        UpdatePriceChangeIndicators(oldPrice, newPrice);
                    }
                }
            }
        }

        private decimal ExtractPriceValue(string priceString)
        {
            if (string.IsNullOrEmpty(priceString))
                return 0;

            // Try to extract the decimal value from price text like "0.12345 ETH ($123.45)"
            try
            {
                string numericPart = priceString.Split(' ')[0];
                if (decimal.TryParse(numericPart, out decimal price))
                    return price;
            }
            catch { }
            return 0;
        }

        public bool IsRunning
        {
            get => _isRunning;
            set
            {
                if (_isRunning != value)
                {
                    _isRunning = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(CanStart)); // Add this line
                    Status = _isRunning ? "Running" : "Stopped";
                    UpdateStatusEmoji();
                    // Notify dependent properties
                    OnPropertyChanged(nameof(StatusWithEmoji));
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
                    OnPropertyChanged();
                    UpdateStatusEmoji();
                    // Notify dependent properties
                    OnPropertyChanged(nameof(StatusWithEmoji));
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
                    OnPropertyChanged();
                }
            }
        }

        public PairInfo PairInfo
        {
            get => _pairInfo;
            set
            {
                _pairInfo = value;
                OnPropertyChanged();
                UpdatePairInfoVisuals();
                // Notify dependent properties
                OnPropertyChanged(nameof(EnhancedPairDisplay));

                // If we get a new pair info, we should reset historical data
                _isHistoricalDataLoaded = false;
                // Load historical data asynchronously when pair info changes
                Task.Run(LoadHistoricalDataAsync);
            }
        }

        public string Volume24h
        {
            get => _volume24h;
            set
            {
                if (_volume24h != value)
                {
                    _volume24h = value;
                    OnPropertyChanged();
                    // Notify dependent properties
                    OnPropertyChanged(nameof(DetailedLiquidityInfo));
                }
            }
        }

        public string PriceChange24h
        {
            get => _priceChange24h;
            set
            {
                if (_priceChange24h != value)
                {
                    _priceChange24h = value;
                    OnPropertyChanged();
                    // Notify dependent properties
                    OnPropertyChanged(nameof(PriceChangeBrush));
                }
            }
        }

        public string PriceChange7d
        {
            get => _priceChange7d;
            set
            {
                if (_priceChange7d != value)
                {
                    _priceChange7d = value;
                    OnPropertyChanged();
                    // Notify dependent properties
                    OnPropertyChanged(nameof(PriceChangeBrush));
                }
            }
        }

        public string LiquidityChange24h
        {
            get => _liquidityChange24h;
            set
            {
                if (_liquidityChange24h != value)
                {
                    _liquidityChange24h = value;
                    OnPropertyChanged();
                    // Notify dependent properties
                    OnPropertyChanged(nameof(DetailedLiquidityInfo));
                }
            }
        }

        public decimal CurrentLiquidity
        {
            get => _currentLiquidity;
            set
            {
                if (_currentLiquidity != value)
                {
                    _currentLiquidity = value;
                    OnPropertyChanged();
                    // Notify dependent properties
                    OnPropertyChanged(nameof(DetailedLiquidityInfo));
                }
            }
        }

        public int HoldersCount
        {
            get => _holdersCount;
            set
            {
                if (_holdersCount != value)
                {
                    _holdersCount = value;
                    OnPropertyChanged();
                }
            }
        }

        public string PriceChangeEmoji
        {
            get => _priceChangeEmoji;
            set
            {
                if (_priceChangeEmoji != value)
                {
                    _priceChangeEmoji = value;
                    OnPropertyChanged();
                    // Notify dependent properties
                    OnPropertyChanged(nameof(PriceWithTrend));
                }
            }
        }

        // New properties for different time intervals
        public string PriceChange15Min
        {
            get => _priceChange15Min;
            set
            {
                if (_priceChange15Min != value)
                {
                    _priceChange15Min = value;
                    OnPropertyChanged();
                }
            }
        }

        public string PriceChange30Min
        {
            get => _priceChange30Min;
            set
            {
                if (_priceChange30Min != value)
                {
                    _priceChange30Min = value;
                    OnPropertyChanged();
                }
            }
        }

        public string PriceChange1Hour
        {
            get => _priceChange1Hour;
            set
            {
                if (_priceChange1Hour != value)
                {
                    _priceChange1Hour = value;
                    OnPropertyChanged();
                }
            }
        }

        public string PriceChange4Hour
        {
            get => _priceChange4Hour;
            set
            {
                if (_priceChange4Hour != value)
                {
                    _priceChange4Hour = value;
                    OnPropertyChanged();
                }
            }
        }

        // Dashboard display properties
        public string EnhancedPairDisplay => GetEnhancedPairDisplay();

        public string PriceWithTrend => $"{CurrentPrice} {PriceChangeEmoji}";

        public SolidColorBrush PriceChangeBrush
        {
            get
            {
                if (string.IsNullOrEmpty(PriceChange24h))
                    return new SolidColorBrush(Colors.White);

                if (PriceChange24h.StartsWith("+"))
                    return new SolidColorBrush(Color.FromRgb(80, 233, 153)); // Green
                else if (PriceChange24h.StartsWith("-"))
                    return new SolidColorBrush(Color.FromRgb(255, 85, 85)); // Red
                else
                    return new SolidColorBrush(Colors.White);
            }
        }

        public string DetailedLiquidityInfo
        {
            get
            {
                if (CurrentLiquidity <= 0)
                    return "N/A";

                string liquidityText = FormatLargeNumber(CurrentLiquidity);

                if (!string.IsNullOrEmpty(LiquidityChange24h))
                    liquidityText += $" ({LiquidityChange24h})";

                return liquidityText;
            }
        }

        public string StatusWithEmoji => Status;

        public void UpdateFromMetrics(Dictionary<string, decimal> metrics, PairInfo pairInfo = null)
        {
            if (metrics == null)
                return;

            try
            {
                // Update PairInfo if provided
                if (pairInfo != null)
                {
                    PairInfo = pairInfo;
                }

                // Update price
                if (metrics.ContainsKey("Price"))
                {
                    decimal price = metrics["Price"];
                    string priceStr = $"{price:N6}";

                    // Add token symbol if available
                    if (PairInfo?.Token1 != null)
                        priceStr += $" {PairInfo.Token1.Symbol}";

                    // Add USD price if available
                    if (metrics.ContainsKey("PriceUsd") && metrics["PriceUsd"] > 0)
                        priceStr += $" (${metrics["PriceUsd"]:N4})";

                    CurrentPrice = priceStr;
                }

                // Update liquidity
                if (metrics.ContainsKey("Liquidity"))
                    CurrentLiquidity = metrics["Liquidity"];

                // Update volume
                if (metrics.ContainsKey("Volume24h"))
                    Volume24h = FormatLargeNumber(metrics["Volume24h"]);

                // Update price changes if available
                if (metrics.ContainsKey("PriceChange15m"))
                    PriceChange15Min = FormatPercentChange(metrics["PriceChange15m"]);

                if (metrics.ContainsKey("PriceChange30m"))
                    PriceChange30Min = FormatPercentChange(metrics["PriceChange30m"]);

                if (metrics.ContainsKey("PriceChange1h"))
                    PriceChange1Hour = FormatPercentChange(metrics["PriceChange1h"]);

                if (metrics.ContainsKey("PriceChange4h"))
                    PriceChange4Hour = FormatPercentChange(metrics["PriceChange4h"]);

                if (metrics.ContainsKey("PriceChange24h"))
                    PriceChange24h = FormatPercentChange(metrics["PriceChange24h"]);

                if (metrics.ContainsKey("PriceChange7d"))
                    PriceChange7d = FormatPercentChange(metrics["PriceChange7d"]);

                // Update liquidity change if available
                if (metrics.ContainsKey("LiquidityChange24h"))
                    LiquidityChange24h = FormatPercentChange(metrics["LiquidityChange24h"]);

                // Update holder count if available
                if (metrics.ContainsKey("HolderCount"))
                    HoldersCount = (int)metrics["HolderCount"];
                else if (pairInfo?.HolderCount > 0)
                    HoldersCount = pairInfo.HolderCount;

                // Update the last updated timestamp
                LastUpdated = DateTime.Now.ToString("HH:mm:ss");

                // If we don't have the historical data yet, or if certain key metrics are missing,
                // trigger a load of historical data to ensure we have complete information
                if (!_isHistoricalDataLoaded ||
                    string.IsNullOrEmpty(PriceChange24h) ||
                    string.IsNullOrEmpty(PriceChange7d) ||
                    string.IsNullOrEmpty(Volume24h))
                {
                    Task.Run(LoadHistoricalDataAsync);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating bot status from metrics: {ex.Message}");
            }
        }


        private async Task LoadHistoricalDataAsync()
        {
            // Only bail out if we don't have the necessary services or pair info
            if (_databaseService == null || PairInfo == null)
                return;

            try
            {
                // Get current time for reference
                var now = DateTime.Now;

                // Get different time points for historical comparison
                var fifteenMinutesAgo = now.AddMinutes(-15);
                var thirtyMinutesAgo = now.AddMinutes(-30);
                var oneHourAgo = now.AddHours(-1);
                var fourHoursAgo = now.AddHours(-4);
                var oneDayAgo = now.AddDays(-1);
                var sevenDaysAgo = now.AddDays(-7);

                // Get the pair entity from database
                var pairEntity = await _databaseService.GetPairByAddressAsync(PairInfo.Address);
                if (pairEntity == null)
                    return;

                // Get historical price data
                var priceHistory = await _databaseService.GetPriceHistoryAsync(pairEntity.Id);
                if (priceHistory == null || priceHistory.Count == 0)
                    return;

                // Find prices at different time points
                decimal currentPrice = ExtractPriceValue(_currentPrice);
                _price15MinAgo = FindClosestPrice(priceHistory, fifteenMinutesAgo);
                _price30MinAgo = FindClosestPrice(priceHistory, thirtyMinutesAgo);
                _price1HourAgo = FindClosestPrice(priceHistory, oneHourAgo);
                _price4HourAgo = FindClosestPrice(priceHistory, fourHoursAgo);
                _price24HourAgo = FindClosestPrice(priceHistory, oneDayAgo);
                _price7DayAgo = FindClosestPrice(priceHistory, sevenDaysAgo);

                // Calculate and format percentage changes
                if (currentPrice > 0)
                {
                    if (_price15MinAgo > 0)
                    {
                        decimal change15Min = CalculatePercentageChange(_price15MinAgo, currentPrice);
                        PriceChange15Min = FormatPercentChange(change15Min);
                    }

                    if (_price30MinAgo > 0)
                    {
                        decimal change30Min = CalculatePercentageChange(_price30MinAgo, currentPrice);
                        PriceChange30Min = FormatPercentChange(change30Min);
                    }

                    if (_price1HourAgo > 0)
                    {
                        decimal change1Hour = CalculatePercentageChange(_price1HourAgo, currentPrice);
                        PriceChange1Hour = FormatPercentChange(change1Hour);
                    }

                    if (_price4HourAgo > 0)
                    {
                        decimal change4Hour = CalculatePercentageChange(_price4HourAgo, currentPrice);
                        PriceChange4Hour = FormatPercentChange(change4Hour);
                    }

                    if (_price24HourAgo > 0)
                    {
                        decimal change24h = CalculatePercentageChange(_price24HourAgo, currentPrice);
                        PriceChange24h = FormatPercentChange(change24h);
                    }

                    if (_price7DayAgo > 0)
                    {
                        decimal change7d = CalculatePercentageChange(_price7DayAgo, currentPrice);
                        PriceChange7d = FormatPercentChange(change7d);
                    }
                }

                // Also look for volume data in the most recent price history entry
                var mostRecentEntry = priceHistory.OrderByDescending(p => p.Timestamp).FirstOrDefault();
                if (mostRecentEntry?.Volume24h.HasValue == true && mostRecentEntry.Volume24h.Value > 0)
                {
                    Volume24h = FormatLargeNumber(mostRecentEntry.Volume24h.Value);
                }

                // Mark as loaded
                _isHistoricalDataLoaded = true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading historical data: {ex.Message}");
                _isHistoricalDataLoaded = false; // Mark as not loaded so we can try again
            }
        }


        private decimal FindClosestPrice(List<PriceHistoryEntity> history, DateTime targetTime)
        {
            // Find the price entry closest to the target time
            PriceHistoryEntity closest = null;
            TimeSpan smallestDiff = TimeSpan.MaxValue;

            foreach (var entry in history)
            {
                var diff = entry.Timestamp > targetTime ?
                    entry.Timestamp - targetTime :
                    targetTime - entry.Timestamp;

                if (diff < smallestDiff)
                {
                    smallestDiff = diff;
                    closest = entry;
                }
            }

            return closest?.Price ?? 0;
        }

        private decimal CalculatePercentageChange(decimal oldPrice, decimal newPrice)
        {
            if (oldPrice == 0)
                return 0;

            return ((newPrice - oldPrice) / oldPrice) * 100;
        }

        private string FormatPercentChange(decimal percentChange)
        {
            string prefix = percentChange >= 0 ? "+" : "";
            return $"{prefix}{percentChange:N2}%";
        }

        private string FormatLargeNumber(decimal number)
        {
            if (number >= 1_000_000_000)
                return $"${number / 1_000_000_000:N2}B";
            if (number >= 1_000_000)
                return $"${number / 1_000_000:N2}M";
            if (number >= 1_000)
                return $"${number / 1_000:N2}K";
            return $"${number:N2}";
        }

        private void UpdatePriceChangeIndicators(decimal oldPrice, decimal newPrice)
        {
            if (oldPrice <= 0 || newPrice <= 0)
                return;

            // Calculate percentage change
            decimal percentChange = CalculatePercentageChange(oldPrice, newPrice);

            // Update emoji based on change
            if (percentChange > 5)
                PriceChangeEmoji = "🚀"; // Rocket for big increase
            else if (percentChange > 2)
                PriceChangeEmoji = "📈"; // Chart up
            else if (percentChange > 0)
                PriceChangeEmoji = "↗️"; // Up-right arrow
            else if (percentChange < -5)
                PriceChangeEmoji = "📉"; // Chart down 
            else if (percentChange < 0)
                PriceChangeEmoji = "↘️"; // Down-right arrow
            else
                PriceChangeEmoji = "➡️"; // Right arrow (no change)
        }

        private void UpdateStatusEmoji()
        {
            // Update status emoji based on running status and other indicators
            if (!_isRunning)
            {
                Status = "Stopped ⛔";
            }
            else
            {
                Status = "Running ✅";
            }
        }

        private void UpdatePairInfoVisuals()
        {
            if (PairInfo == null) return;

            // Update token pair display with enhanced formatting
            PairName = GetEnhancedPairDisplay();

            // When pair info changes, update token-specific indicators
            if (PairInfo.Token0 != null && PairInfo.Token1 != null)
            {
                UpdateTokenEmojis();
                UpdateTokenSpecificTags(PairInfo.Token0.Symbol, PairInfo.Token1.Symbol);
            }
        }

        private string GetEnhancedPairDisplay()
        {
            if (PairInfo == null) return "Unknown Pair";

            // Create enhanced display with token symbols and emoji if available
            string token0 = PairInfo.Token0?.Symbol ?? "?";
            string token1 = PairInfo.Token1?.Symbol ?? "?";

            return $"{token0}/{token1}";
        }

        private void UpdateTokenEmojis()
        {
            // This could map common tokens to relevant emojis
            // For now just using a simple approach
            if (PairInfo?.Token0 == null) return;

            // Common token symbols to emoji mappings
            Dictionary<string, string> tokenEmojis = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "BTC", "₿" },
                { "ETH", "Ξ" },
                { "USDT", "💵" },
                { "USDC", "💲" },
                { "WETH", "Ξ" },
                { "DAI", "◈" },
                // Add more as needed
            };

            string token0 = PairInfo.Token0.Symbol;

            // Check if we have an emoji for this token
            if (tokenEmojis.TryGetValue(token0, out string emoji))
            {
                // We could store this for display somewhere
                // For now just updating the pair display is enough
                PairName = $"{emoji} {PairName}";
            }
        }

        private void UpdateTokenSpecificTags(string token0, string token1)
        {
            // Could add token-specific tags or display elements
            // Not implemented in detail for this solution
        }

        public void ResetHistoricalDataState()
        {
            _isHistoricalDataLoaded = false;
            // Trigger asynchronous reload of historical data
            Task.Run(LoadHistoricalDataAsync);
        }

        // Method to generate a formatted embed description with emojis
        public string GetEnhancedEmbedDescription()
        {
            var description = new System.Text.StringBuilder();

            // Add price with change indicators
            description.AppendLine($"**Current Price:** {CurrentPrice} {PriceChangeEmoji}");

            // Add time-based changes if available
            if (!string.IsNullOrEmpty(PriceChange15Min))
                description.AppendLine($"**15min:** {PriceChange15Min}");

            if (!string.IsNullOrEmpty(PriceChange30Min))
                description.AppendLine($"**30min:** {PriceChange30Min}");

            if (!string.IsNullOrEmpty(PriceChange1Hour))
                description.AppendLine($"**1h:** {PriceChange1Hour}");

            if (!string.IsNullOrEmpty(PriceChange24h))
                description.AppendLine($"**24h:** {PriceChange24h}");

            if (!string.IsNullOrEmpty(PriceChange7d))
                description.AppendLine($"**7d:** {PriceChange7d}");

            // Add volume if available
            if (!string.IsNullOrEmpty(Volume24h))
                description.AppendLine($"**24h Volume:** {Volume24h}");

            // Add liquidity if available
            if (CurrentLiquidity > 0)
                description.AppendLine($"**Liquidity:** {FormatLargeNumber(CurrentLiquidity)}");

            return description.ToString();
        }

        // Helper function to format addresses for display
        private string FormatAddress(string address)
        {
            if (string.IsNullOrEmpty(address) || address.Length < 10)
                return address;

            return $"{address.Substring(0, 6)}...{address.Substring(address.Length - 4)}";
        }
    }
}
