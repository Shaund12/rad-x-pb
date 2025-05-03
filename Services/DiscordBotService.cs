using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Net;
using Discord.WebSocket;
using System.Timers;
using System.Collections.Generic;
using System.Threading;
using System.Text;
using System.Globalization;

namespace RadXPriceBot.Services
{
    public class BotStatusUpdateEventArgs : EventArgs
    {
        public Dictionary<string, decimal> Metrics { get; set; }
        public PairInfo PairInfo { get; set; }
    }

    public class DiscordBotService
    {
        private readonly string _token;
        private readonly ulong _guildId;
        private PriceService _priceSvc; // Changed from readonly to allow updates
        private string _nickname;
        private readonly string _statusType;
        private readonly string _customStatus;
        private readonly int _updateIntervalMs;

        private DiscordSocketClient _client;
        private System.Timers.Timer _updateTimer;
        private System.Timers.Timer _embedTimer; // Timer for sending embeds
        private System.Timers.Timer _historyUpdateTimer; // Timer for updating price history
        private const int UPDATE_INTERVAL_MS = 45000; // Update every 45 seconds by default
        private TokenInfo _token0Info;
        private TokenInfo _token1Info;
        private CancellationTokenSource _cts;
        private bool _isRunning;

        // Dictionary to map token symbols to their image URLs
        private Dictionary<string, string> _tokenImageUrls = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        
        // Historical price data for different time intervals
        private Dictionary<string, decimal> _priceHistory = new Dictionary<string, decimal>();
        private Dictionary<string, DateTime> _priceTimestamps = new Dictionary<string, DateTime>();
        private const string PRICE_15M = "price_15m";
        private const string PRICE_30M = "price_30m";
        private const string PRICE_1H = "price_1h";
        private const string PRICE_4H = "price_4h";
        private const string PRICE_24H = "price_24h";
        private const string PRICE_7D = "price_7d";

        // Properties for embed functionality
        private ulong _embedChannelId;
        private int _embedIntervalMinutes = 0; // 0 means disabled
        private DateTime _lastEmbedSent = DateTime.MinValue; // For rate limiting
        private string _embedColor = "#50E999"; // Default embed color
        private bool _includeChartInEmbed = true;
        private bool _includeTokenInfoInEmbed = true;
        private bool _includeLiquidityInfoInEmbed = true;

        // Swap monitoring properties
        private ulong _swapNotificationChannelId;
        private bool _monitorSwapTransactions = true;
        private System.Timers.Timer _transactionMonitorTimer;
        private int _transactionCheckIntervalMs = 30000; // Default to check every 30 seconds
        private Dictionary<string, decimal> _lastPrices;
        private List<string> _lastProcessedTransactions = new List<string>();
        private const int MAX_STORED_TX_HASHES = 100; // Store only last 100 processed transactions

        public event Action<string> OnLog = delegate { };
        public event EventHandler<BotStatusUpdateEventArgs> StatusUpdated;

        public DiscordBotService(
            string token,
            string guildId,
            PriceService priceSvc,
            string nickname,
            string statusType,
            string customStatus,
            int updateIntervalMs = 30000) // Default to 30 seconds
        {
            _token = token;
            _priceSvc = priceSvc ?? throw new ArgumentNullException(nameof(priceSvc), "Price service cannot be null");
            
            if (!ulong.TryParse(guildId, out _guildId))
            {
                throw new ArgumentException("Invalid Guild ID format. Must be a numeric value.", nameof(guildId));
            }
            
            _nickname = nickname;
            _statusType = statusType;
            _customStatus = customStatus;
            _updateIntervalMs = Math.Max(5000, updateIntervalMs); // Ensure minimum interval of 5 seconds
            _isRunning = false;

            // Initialize token image URLs and price history
            InitializeTokenImageUrls();
            InitializePriceHistory();
        }

        // Method to initialize token image URLs
        private void InitializeTokenImageUrls()
        {
            try
            {
                // Define the URL mapping for token symbols
                _tokenImageUrls["WVTRU"] = GetTokenImageUrl("wVTRU.png");
                _tokenImageUrls["VTRO"] = GetTokenImageUrl("VTRO.png");
                _tokenImageUrls["USDC.POL"] = GetTokenImageUrl("USDC.pol.png");
                _tokenImageUrls["USDC"] = GetTokenImageUrl("USDC.png");
                _tokenImageUrls["ALFC"] = GetTokenImageUrl("alfc.png");
                _tokenImageUrls["SHIBDOGE"] = GetTokenImageUrl("shibdoge.png");

                OnLog($"Initialized token image URLs for {_tokenImageUrls.Count} tokens");
            }
            catch (Exception ex)
            {
                OnLog($"Error initializing token image URLs: {ex.Message}");
            }
        }

        // Initialize price history tracking
        private void InitializePriceHistory()
        {
            _priceHistory[PRICE_15M] = 0;
            _priceHistory[PRICE_30M] = 0;
            _priceHistory[PRICE_1H] = 0;
            _priceHistory[PRICE_4H] = 0;
            _priceHistory[PRICE_24H] = 0;
            _priceHistory[PRICE_7D] = 0;

            // Initialize timestamps to "never updated"
            DateTime never = DateTime.MinValue;
            _priceTimestamps[PRICE_15M] = never;
            _priceTimestamps[PRICE_30M] = never;
            _priceTimestamps[PRICE_1H] = never;
            _priceTimestamps[PRICE_4H] = never;
            _priceTimestamps[PRICE_24H] = never;
            _priceTimestamps[PRICE_7D] = never;
        }

        // Method to get token image URL for Discord embeds
        private string GetTokenImageUrl(string fileName)
        {
            try
            {
                // Map filenames to the online URLs based on the filename
                string lowerFileName = fileName.ToLower();
                
                if (lowerFileName.Contains("wvtru"))
                    return "https://swap.vitruveo.xyz/images/coins/wVTRU.png";
                else if (lowerFileName.Contains("vtro"))
                    return "https://swap.vitruveo.xyz/images/coins/VTRO.png";
                else if (lowerFileName.Contains("usdc.pol"))
                    return "https://swap.vitruveo.xyz/images/coins/USDC.pol.png";
                else if (lowerFileName.Contains("usdc"))
                    return "https://swap.vitruveo.xyz/images/coins/USDC.png";
                else if (lowerFileName.Contains("alfc"))
                    return "https://bafybeibbr22g26v4shx26uuzgzrfnsydo3w5dksdfzfwzeqauoavsvm6uy.ipfs.w3s.link/alfc.png";
                else if (lowerFileName.Contains("shibdoge"))
                    return "https://bafybeibrj6kjtdkby7mrjiczomffdnlewildba2pgc6oafktizocvkfny4.ipfs.w3s.link/shibdoge.png";
                else
                    return "https://i.imgur.com/gXdWwTR.png"; // Default generic token icon
            }
            catch (Exception ex)
            {
                OnLog($"Error loading token image {fileName}: {ex.Message}");
                return "https://i.imgur.com/gXdWwTR.png"; // Default fallback
            }
        }

        // Method to get token thumbnail URL
        private string GetTokenThumbnailUrl(string symbol)
        {
            // Try to get the image URL from the dictionary
            if (symbol != null && _tokenImageUrls.TryGetValue(symbol, out string imageUrl) && !string.IsNullOrEmpty(imageUrl))
            {
                // Return the URL if found
                return imageUrl;
            }

            // Default generic token icon
            return "https://i.imgur.com/gXdWwTR.png";
        }

        // Method to get current metrics from PriceService with enhanced historical data
        public async Task<Dictionary<string, decimal>> GetCurrentMetricsAsync()
        {
            try
            {
                if (_priceSvc == null)
                    return null;
                    
                // Get base metrics from price service
                var metrics = await _priceSvc.GetTokenMetricsAsync();
                if (metrics == null)
                    return null;
                
                // Add price change data if we have historical prices
                if (metrics.ContainsKey("Price"))
                {
                    decimal currentPrice = metrics["Price"];
                    
                    // Add price changes for different time intervals if we have historical data
                    if (_priceHistory[PRICE_15M] > 0)
                    {
                        decimal percentChange = CalculatePercentageChange(_priceHistory[PRICE_15M], currentPrice);
                        metrics["PriceChange15m"] = percentChange;
                    }
                    
                    if (_priceHistory[PRICE_30M] > 0)
                    {
                        decimal percentChange = CalculatePercentageChange(_priceHistory[PRICE_30M], currentPrice);
                        metrics["PriceChange30m"] = percentChange;
                    }
                    
                    if (_priceHistory[PRICE_1H] > 0)
                    {
                        decimal percentChange = CalculatePercentageChange(_priceHistory[PRICE_1H], currentPrice);
                        metrics["PriceChange1h"] = percentChange;
                    }
                    
                    if (_priceHistory[PRICE_4H] > 0)
                    {
                        decimal percentChange = CalculatePercentageChange(_priceHistory[PRICE_4H], currentPrice);
                        metrics["PriceChange4h"] = percentChange;
                    }
                    
                    if (_priceHistory[PRICE_24H] > 0)
                    {
                        decimal percentChange = CalculatePercentageChange(_priceHistory[PRICE_24H], currentPrice);
                        metrics["PriceChange24h"] = percentChange;
                    }
                    
                    if (_priceHistory[PRICE_7D] > 0)
                    {
                        decimal percentChange = CalculatePercentageChange(_priceHistory[PRICE_7D], currentPrice);
                        metrics["PriceChange7d"] = percentChange;
                    }
                }
                
                return metrics;
            }
            catch (Exception ex)
            {
                OnLog($"Error getting current metrics: {ex.Message}");
                return null;
            }
        }

        // Helper method to calculate percentage change
        private decimal CalculatePercentageChange(decimal oldPrice, decimal newPrice)
        {
            if (oldPrice == 0)
                return 0;
                
            return ((newPrice - oldPrice) / oldPrice) * 100;
        }

        // Start the bot service
        public async Task StartAsync()
        {
            if (_isRunning)
            {
                OnLog("Bot is already running");
                return;
            }

            try
            {
                _cts = new CancellationTokenSource();
                _client = new DiscordSocketClient(new DiscordSocketConfig
                {
                    GatewayIntents = GatewayIntents.Guilds,
                    LogLevel = LogSeverity.Info,
                    AlwaysDownloadUsers = false,
                    MessageCacheSize = 100
                });

                _client.Log += LogAsync;
                _client.Ready += ReadyAsync;
                _client.SlashCommandExecuted += SlashCommandHandler;

                await _client.LoginAsync(TokenType.Bot, _token);
                await _client.StartAsync();

                OnLog("Discord client started. Waiting for Ready...");

                // Setup timers
                SetupUpdateTimer();
                SetupEmbedTimer();
                SetupHistoryUpdateTimer();

                // Wait for client to be fully ready
                int attempts = 0;
                while (_client.ConnectionState != ConnectionState.Connected && attempts < 30)
                {
                    await Task.Delay(500);
                    attempts++;
                }

                if (_client.ConnectionState != ConnectionState.Connected)
                {
                    OnLog($"Warning: Discord client not fully connected after {attempts} attempts. Current state: {_client.ConnectionState}");
                }
                else
                {
                    OnLog($"Discord client connected successfully after {attempts} attempts");
                }

                // Keep the bot running without blocking
                _ = Task.Run(async () =>
                {
                    try
                    {
                        while (!_cts.Token.IsCancellationRequested)
                        {
                            await Task.Delay(1000, _cts.Token);
                        }
                    }
                    catch (TaskCanceledException)
                    {
                        // Expected when cancellation is requested
                    }
                    catch (Exception ex)
                    {
                        OnLog($"Error in bot background task: {ex.Message}");
                    }
                }, _cts.Token);

                _isRunning = true;
                OnLog("Bot service started successfully");
            }
            catch (Exception ex)
            {
                OnLog($"Failed to start bot service: {ex.Message}");
                if (ex.InnerException != null)
                {
                    OnLog($"Inner exception: {ex.InnerException.Message}");
                }
                
                // Clean up resources
                _client?.Dispose();
                _client = null;
                _cts?.Dispose();
                _cts = null;
                
                throw; // Re-throw to notify caller
            }
        }

        // Report status updates
        public void ReportStatus(Dictionary<string, decimal> metrics, PairInfo pairInfo)
        {
            StatusUpdated?.Invoke(this, new BotStatusUpdateEventArgs { Metrics = metrics, PairInfo = pairInfo });
        }

        // Setup timer for regular status updates
        private void SetupUpdateTimer()
        {
            // Clean up existing timer if any
            _updateTimer?.Stop();
            _updateTimer?.Dispose();
            
            _updateTimer = new System.Timers.Timer(_updateIntervalMs);
            _updateTimer.Elapsed += async (s, e) =>
            {
                try
                {
                    // Capture any exceptions during timer callback
                    await SafeExecuteAsync(async () => 
                    {
                        await UpdateBotNicknameAsync();
                        await UpdateBotStatusAsync();
                    });
                }
                catch (Exception ex)
                {
                    OnLog($"Error in timer update: {ex.Message}");
                }
            };
            _updateTimer.AutoReset = true;
            _updateTimer.Start();
            OnLog($"Status update timer started (interval: {_updateIntervalMs / 1000} seconds)");
        }

        // Setup timer for historical price tracking
        private void SetupHistoryUpdateTimer()
        {
            // Clean up existing timer if any
            _historyUpdateTimer?.Stop();
            _historyUpdateTimer?.Dispose();
            
            // Update historical prices every 5 minutes
            const int HISTORY_UPDATE_INTERVAL = 5 * 60 * 1000; // 5 minutes
            
            _historyUpdateTimer = new System.Timers.Timer(HISTORY_UPDATE_INTERVAL);
            _historyUpdateTimer.Elapsed += async (s, e) =>
            {
                try
                {
                    await UpdateHistoricalPricesAsync();
                }
                catch (Exception ex)
                {
                    OnLog($"Error updating historical prices: {ex.Message}");
                }
            };
            _historyUpdateTimer.AutoReset = true;
            _historyUpdateTimer.Start();
            OnLog($"Historical price update timer started (interval: {HISTORY_UPDATE_INTERVAL / 1000 / 60} minutes)");
            
            // Initial capture of historical prices
            _ = Task.Run(async () => 
            {
                try
                {
                    await Task.Delay(10000); // Wait 10 seconds after startup
                    await UpdateHistoricalPricesAsync();
                }
                catch (Exception ex)
                {
                    OnLog($"Error in initial historical price capture: {ex.Message}");
                }
            });
        }

        // Update historical price records at different time intervals
        // Update historical price records at different time intervals
        private async Task UpdateHistoricalPricesAsync()
        {
            try
            {
                OnLog("Updating historical price data...");

                // Get current price from price service
                var metrics = await _priceSvc.GetTokenMetricsAsync();
                if (metrics == null || !metrics.ContainsKey("Price") || metrics["Price"] <= 0)
                {
                    OnLog("Cannot update historical prices: current price not available");
                    return;
                }

                decimal currentPrice = metrics["Price"];
                DateTime now = DateTime.Now;

                // Check time thresholds for each interval and update if needed

                // 15 minutes
                if ((now - _priceTimestamps[PRICE_15M]).TotalMinutes >= 15)
                {
                    _priceHistory[PRICE_15M] = currentPrice;
                    _priceTimestamps[PRICE_15M] = now;
                    OnLog($"Updated 15m historical price: {currentPrice}");
                }

                // 30 minutes
                if ((now - _priceTimestamps[PRICE_30M]).TotalMinutes >= 30)
                {
                    _priceHistory[PRICE_30M] = currentPrice;
                    _priceTimestamps[PRICE_30M] = now;
                    OnLog($"Updated 30m historical price: {currentPrice}");
                }

                // 1 hour
                if ((now - _priceTimestamps[PRICE_1H]).TotalMinutes >= 60)
                {
                    _priceHistory[PRICE_1H] = currentPrice;
                    _priceTimestamps[PRICE_1H] = now;
                    OnLog($"Updated 1h historical price: {currentPrice}");
                }

                // 4 hours
                if ((now - _priceTimestamps[PRICE_4H]).TotalMinutes >= 240)
                {
                    _priceHistory[PRICE_4H] = currentPrice;
                    _priceTimestamps[PRICE_4H] = now;
                    OnLog($"Updated 4h historical price: {currentPrice}");
                }

                // 24 hours
                if ((now - _priceTimestamps[PRICE_24H]).TotalMinutes >= 1440)
                {
                    _priceHistory[PRICE_24H] = currentPrice;
                    _priceTimestamps[PRICE_24H] = now;
                    OnLog($"Updated 24h historical price: {currentPrice}");
                }

                // 7 days
                if ((now - _priceTimestamps[PRICE_7D]).TotalMinutes >= 10080)
                {
                    _priceHistory[PRICE_7D] = currentPrice;
                    _priceTimestamps[PRICE_7D] = now;
                    OnLog($"Updated 7d historical price: {currentPrice}");
                }

                // If any price was updated, notify via ReportStatus
                if (_priceHistory.Any(p => p.Value > 0))
                {
                    try
                    {
                        // Get latest token info for the report
                        var pairDetails = await _priceSvc.GetPairDetailsAsync();
                        if (pairDetails.pairAddress == null || pairDetails.token0Info == null || pairDetails.token1Info == null)
                        {
                            OnLog("Cannot report status: missing pair details");
                            return;
                        }

                        // Create PairInfo for reporting
                        var pairInfo = new PairInfo
                        {
                            Address = pairDetails.pairAddress,
                            Token0 = pairDetails.token0Info,
                            Token1 = pairDetails.token1Info
                        };

                        // Safely set the Name field using reflection with null checks
                        var nameField = typeof(PairInfo).GetField("Name");
                        if (nameField != null && pairDetails.token0Info?.Symbol != null && pairDetails.token1Info?.Symbol != null)
                        {
                            nameField.SetValue(pairInfo, $"{pairDetails.token0Info.Symbol}/{pairDetails.token1Info.Symbol}");
                        }
                        else
                        {
                            OnLog("Warning: Could not set PairInfo.Name field - using reflection safely");
                            // Set a default name if we can't set it properly
                            if (nameField != null)
                            {
                                nameField.SetValue(pairInfo, "Token Pair");
                            }
                        }

                        // Report status with updated historical price data
                        if (metrics != null && pairInfo != null)
                        {
                            ReportStatus(metrics, pairInfo);
                        }
                    }
                    catch (Exception ex)
                    {
                        OnLog($"Error in status reporting part of historical price update: {ex.Message}");
                        if (ex.InnerException != null)
                        {
                            OnLog($"Inner exception: {ex.InnerException.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                OnLog($"Error updating historical prices: {ex.Message}");
                if (ex.InnerException != null)
                {
                    OnLog($"Inner exception: {ex.InnerException.Message}");
                }
            }
        }


        // Setup timer for sending embeds
        private void SetupEmbedTimer()
        {
            // Clean up any existing timer first
            if (_embedTimer != null)
            {
                _embedTimer.Stop();
                _embedTimer.Dispose();
                _embedTimer = null;
            }

            // Only setup if both interval and channel are configured
            if (_embedIntervalMinutes > 0 && _embedChannelId > 0)
            {
                // Create a new timer with the configured interval
                _embedTimer = new System.Timers.Timer(_embedIntervalMinutes * 60 * 1000);
                _embedTimer.Elapsed += async (s, e) =>
                {
                    try
                    {
                        // Safely execute the embed sending logic
                        await SafeExecuteAsync(async () => 
                        {
                            OnLog($"Embed timer triggered - attempting to send periodic embed to channel {_embedChannelId}");
                            await SendPeriodicEmbed();
                        });
                    }
                    catch (Exception ex)
                    {
                        OnLog($"Error in embed timer handler: {ex.Message}");
                        if (ex.InnerException != null)
                        {
                            OnLog($"Inner exception: {ex.InnerException.Message}");
                        }
                    }
                };
                _embedTimer.AutoReset = true;
                _embedTimer.Start();
                OnLog($"Embed timer started (interval: {_embedIntervalMinutes} minutes, channel: {_embedChannelId})");

                // Send an initial embed after startup
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await Task.Delay(5000); // Wait 5 seconds after startup
                        OnLog("Sending initial embed after setup");
                        await SendPeriodicEmbed();
                    }
                    catch (Exception ex)
                    {
                        OnLog($"Error sending initial embed: {ex.Message}");
                    }
                });
            }
            else
            {
                OnLog("Cannot setup embed timer: interval or channel ID is not properly configured");
            }
        }

        // Helper method to safely execute async operations
        private async Task SafeExecuteAsync(Func<Task> action)
        {
            try
            {
                // First check if we're still running and connected
                if (!_isRunning || _client == null || _client.ConnectionState != ConnectionState.Connected)
                {
                    OnLog($"Cannot execute action: Bot not running or client not connected (State: {_client?.ConnectionState})");
                    return;
                }
                
                // Create a cancellation token with timeout
                using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30))) // 30 second timeout
                {
                    // Execute the action with the timeout
                    var task = action();
                    await Task.WhenAny(task, Task.Delay(Timeout.Infinite, cts.Token));
                    
                    // If the action is still running, it's been too long
                    if (!task.IsCompleted)
                    {
                        throw new TimeoutException("The operation took too long to complete");
                    }
                    
                    // Propagate any exceptions from the task
                    await task;
                }
            }
            catch (TaskCanceledException)
            {
                OnLog("Operation was canceled");
            }
            catch (Exception ex)
            {
                OnLog($"Error executing operation: {ex.Message}");
                if (ex.InnerException != null)
                {
                    OnLog($"Inner exception: {ex.InnerException.Message}");
                }
            }
        }

        // Configure automated embeds
        public void SetEmbedInterval(int minutes, ulong channelId,
            bool includeChart = true,
            bool includeTokenInfo = true,
            bool includeLiquidityInfo = true,
            string embedColor = "#50E999")
        {
            OnLog($"Setting embed interval to {minutes} minutes for channel {channelId} (chart={includeChart}, info={includeTokenInfo}, liquidity={includeLiquidityInfo}, color={embedColor})");

            // Clean up old timer
            if (_embedTimer != null)
            {
                _embedTimer.Stop();
                _embedTimer.Dispose();
                _embedTimer = null;
            }

            // Update all settings
            _embedIntervalMinutes = minutes;
            _embedChannelId = channelId;
            _includeChartInEmbed = includeChart;
            _includeTokenInfoInEmbed = includeTokenInfo;
            _includeLiquidityInfoInEmbed = includeLiquidityInfo;
            _embedColor = embedColor;

            // Disable if minutes is 0 or channelId is 0
            if (minutes <= 0 || channelId == 0)
            {
                OnLog("Automatic embeds disabled due to invalid settings");
                return;
            }

            // Create a new timer
            _embedTimer = new System.Timers.Timer(minutes * 60 * 1000);
            _embedTimer.Elapsed += async (s, e) =>
            {
                try
                {
                    OnLog($"Embed timer triggered at {DateTime.UtcNow:HH:mm:ss}");
                    await SendPeriodicEmbed();
                }
                catch (Exception ex)
                {
                    OnLog($"Error in embed timer callback: {ex.Message}");
                }
            };
            _embedTimer.AutoReset = true;
            _embedTimer.Start();

            OnLog($"Embed timer set to trigger every {minutes} minutes");

            // Send an initial embed
            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(2000); // Small delay to ensure bot is ready
                    OnLog("Sending initial embed after setup");
                    await SendPeriodicEmbed();
                }
                catch (Exception ex)
                {
                    OnLog($"Error sending initial embed: {ex.Message}");
                }
            });
        }

        // Method to disable periodic embeds
        public void DisableEmbeds()
        {
            OnLog("Disabling periodic embeds");

            // Clean up timer
            _embedTimer?.Stop();
            _embedTimer?.Dispose();
            _embedTimer = null;

            // Reset interval to indicate disabled
            _embedIntervalMinutes = 0;
            _embedChannelId = 0;
        }

        // Enable swap transaction monitoring
        public void EnableSwapMonitoring(ulong channelId, int checkIntervalMs = 15000)
        {
            // Save current embed settings before making any changes
            bool embedsWereEnabled = _embedIntervalMinutes > 0 && _embedChannelId > 0;
            int currentEmbedIntervalMinutes = _embedIntervalMinutes;
            ulong currentEmbedChannelId = _embedChannelId;
            bool currentIncludeChartInEmbed = _includeChartInEmbed;
            bool currentIncludeTokenInfoInEmbed = _includeTokenInfoInEmbed;
            bool currentIncludeLiquidityInfoInEmbed = _includeLiquidityInfoInEmbed;
            string currentEmbedColor = _embedColor;

            // Configure swap monitoring
            _swapNotificationChannelId = channelId;
            _monitorSwapTransactions = true;
            _transactionCheckIntervalMs = Math.Max(5000, checkIntervalMs); // Ensure minimum interval of 5 seconds

            // Clean up existing timer if any
            _transactionMonitorTimer?.Stop();
            _transactionMonitorTimer?.Dispose();

            // Setup monitoring timer
            _transactionMonitorTimer = new System.Timers.Timer(_transactionCheckIntervalMs);
            _transactionMonitorTimer.Elapsed += async (s, e) =>
            {
                try
                {
                    await SafeExecuteAsync(async () => await CheckForNewTransactionsAsync());
                }
                catch (Exception ex)
                {
                    OnLog($"Error monitoring transactions: {ex.Message}");
                }
            };
            _transactionMonitorTimer.AutoReset = true;
            _transactionMonitorTimer.Start();

            OnLog($"Swap monitoring enabled. Channel: {channelId}, Interval: {checkIntervalMs}ms");

            // IMPORTANT: Restore embed timer if it was previously enabled
            if (embedsWereEnabled && (_embedTimer == null || !_embedTimer.Enabled))
            {
                OnLog($"Restoring embed timer after enabling swap monitoring: Interval={currentEmbedIntervalMinutes}min, Channel={currentEmbedChannelId}");
                SetEmbedInterval(
                    currentEmbedIntervalMinutes,
                    currentEmbedChannelId,
                    currentIncludeChartInEmbed,
                    currentIncludeTokenInfoInEmbed,
                    currentIncludeLiquidityInfoInEmbed,
                    currentEmbedColor
                );
            }
        }

        // Disable swap transaction monitoring
        public void DisableSwapMonitoring()
        {
            // Save current embed timer state
            bool embedTimerWasEnabled = _embedTimer != null && _embedTimer.Enabled;
            int currentEmbedIntervalMinutes = _embedIntervalMinutes;
            ulong currentEmbedChannelId = _embedChannelId;

            _monitorSwapTransactions = false;
            _transactionMonitorTimer?.Stop();
            _transactionMonitorTimer?.Dispose();
            _transactionMonitorTimer = null;

            OnLog("Swap monitoring disabled");

            // Ensure embed timer is still properly configured after disabling swap monitoring
            if (currentEmbedIntervalMinutes > 0 && currentEmbedChannelId > 0 && (!embedTimerWasEnabled || _embedTimer == null || !_embedTimer.Enabled))
            {
                OnLog($"Restoring embed timer configuration after disabling swap monitoring (interval: {currentEmbedIntervalMinutes} minutes, channel: {currentEmbedChannelId})");
                SetEmbedInterval(currentEmbedIntervalMinutes, currentEmbedChannelId,
                    _includeChartInEmbed,
                    _includeTokenInfoInEmbed,
                    _includeLiquidityInfoInEmbed,
                    _embedColor);
            }
        }

        // Reset transaction tracking
        public async Task ResetTransactionTrackingAsync()
        {
            // Clear the processed transactions list to avoid duplicate detection issues
            _lastProcessedTransactions.Clear();
            OnLog("Transaction tracking reset");
        }

        // Check for new transactions and send notifications
        // Check for new transactions and send notifications
        private async Task CheckForNewTransactionsAsync()
        {
            if (!_monitorSwapTransactions || _swapNotificationChannelId == 0 || _client == null || _client.ConnectionState != ConnectionState.Connected)
            {
                return;
            }

            try
            {
                // Get recent transactions from PriceService
                var recentSwaps = await _priceSvc.GetRecentSwapsAsync(10);
                if (recentSwaps == null || !recentSwaps.Any())
                {
                    return; // No logging needed here - just return quietly
                }

                // Process each transaction that we haven't seen before - NO UNNECESSARY LOGGING
                int newTxCount = 0;
                foreach (var swap in recentSwaps)
                {
                    // Skip if transaction hash is null or already processed
                    if (string.IsNullOrEmpty(swap.TransactionHash) || _lastProcessedTransactions.Contains(swap.TransactionHash))
                    {
                        continue;
                    }

                    // Count new transactions
                    newTxCount++;

                    // Add to processed list to avoid duplicates
                    _lastProcessedTransactions.Add(swap.TransactionHash);

                    // Trim the list to prevent memory buildup
                    while (_lastProcessedTransactions.Count > MAX_STORED_TX_HASHES)
                    {
                        _lastProcessedTransactions.RemoveAt(0);
                    }

                    // Skip transactions with invalid amounts
                    if (swap.TokenAmount <= 0)
                    {
                        continue;
                    }

                    // Send notification
                    await SendSwapNotificationAsync(swap);
                }

                // Only log if we actually processed new transactions
                if (newTxCount > 0)
                {
                    OnLog($"Processed {newTxCount} new {(_token0Info?.Symbol ?? "token")} {(newTxCount == 1 ? "transaction" : "transactions")}");
                }
            }
            catch (TaskCanceledException)
            {
                // No need to log cancellation
            }
            catch (Exception ex)
            {
                OnLog($"Error checking for transactions: {ex.Message}");
                if (ex.InnerException != null)
                {
                    OnLog($"Inner exception: {ex.InnerException.Message}");
                }
            }
        }


        // Send swap notification to Discord
        private async Task SendSwapNotificationAsync(SwapTransaction swap)
        {
            var channel = await _client.GetChannelAsync(_swapNotificationChannelId) as IMessageChannel;
            if (channel == null)
            {
                OnLog($"Could not find swap notification channel {_swapNotificationChannelId}");
                return;
            }

            // Create a detailed swap embed
            var embed = CreateSwapEmbed(swap);

            try
            {
                // Send to Discord channel
                await channel.SendMessageAsync(embed: embed);
            }
            catch (Discord.Net.HttpException ex) when (ex.DiscordCode.HasValue &&
                                          (int)ex.DiscordCode.Value == 50001)
            {
                OnLog($"Missing permissions to send swap notification to channel {_swapNotificationChannelId}");
            }
            catch (Exception ex)
            {
                OnLog($"Error sending swap notification: {ex.Message}");
            }
        }

        // Create swap transaction embed
        private Embed CreateSwapEmbed(SwapTransaction swap)
        {
            // Determine if it's a buy or sell
            bool isBuy = swap.IsBuyTransaction;

            // Base colors - Green for buy, Red for sell
            Color color = isBuy ? new Color(46, 204, 113) : new Color(231, 76, 60);

            // Format amounts with the proper number of decimals
            string token0Amount = FormatTokenAmount(swap.TokenAmount, swap.Token0Decimals);
            string token1Amount = FormatTokenAmount(swap.ValueAmount, swap.Token1Decimals);

            // Calculate USD value if available
            string usdValue = swap.UsdValue > 0 ? $"${swap.UsdValue:N2}" : "Unknown";

            // Get token emojis
            string token0Emoji = GetTokenEmoji(swap.Token0Symbol);
            string token1Emoji = GetTokenEmoji(swap.Token1Symbol);

            // Get the token thumbnail URL - use the primary token (token0 for buys, token1 for sells)
            string tokenImageUrl = isBuy ?
                GetTokenThumbnailUrl(swap.Token0Symbol) :
                GetTokenThumbnailUrl(swap.Token1Symbol);

            // Categorize transaction size and get appropriate emojis
            (string sizeCategory, string sizeEmoji) = CategorizeTransactionSize(swap.UsdValue);

            // Title based on swap type and size
            string title;
            if (isBuy)
            {
                title = $"{sizeEmoji} {sizeCategory} Buy: {token0Emoji} {swap.Token0Symbol}";
            }
            else
            {
                title = $"{sizeEmoji} {sizeCategory} Sell: {token0Emoji} {swap.Token0Symbol}";
            }

            // Get block explorer URL
            string explorerUrl = $"https://explorer.vitruveo.xyz/tx/{swap.TransactionHash}";

            // Create the embed with attention-grabbing features
            var builder = new EmbedBuilder()
                .WithTitle(title)
                .WithColor(color)
                .WithTimestamp(swap.Timestamp)
                .WithFooter(footer => {
                    footer.WithText($"RadX Price Bot • Block #{swap.BlockNumber}");
                    footer.WithIconUrl("https://i.imgur.com/gXdWwTR.png");
                });

            // Add the token image as thumbnail if available
            if (!string.IsNullOrEmpty(tokenImageUrl))
            {
                builder.WithThumbnailUrl(tokenImageUrl);
            }

            // Add author with wallet info - ensure we're using the real sender, not the router
            builder.WithAuthor(author => {
                author.Name = $"Trader: {FormatAddress(swap.FromAddress)}";
            });

            // Symbol formatting for display
            string token0DisplayName = $"{token0Emoji} {swap.Token0Symbol}";
            string token1DisplayName = $"{token1Emoji} {swap.Token1Symbol}";

            // Main swap info field
            var swapDetailsField = new StringBuilder();

            // Show the swap flow with arrows and proper formatting
            if (isBuy)
            {
                swapDetailsField.AppendLine($"**{token1DisplayName} → {token0DisplayName}**");
                swapDetailsField.AppendLine($"`{token1Amount} {swap.Token1Symbol}` → `{token0Amount} {swap.Token0Symbol}`");
            }
            else
            {
                swapDetailsField.AppendLine($"**{token0DisplayName} → {token1DisplayName}**");
                swapDetailsField.AppendLine($"`{token0Amount} {swap.Token0Symbol}` → `{token1Amount} {swap.Token1Symbol}`");
            }

            // Add USD value with special formatting
            if (swap.UsdValue > 0)
            {
                swapDetailsField.AppendLine($"\n**Value:** {GetValueDisplay(swap.UsdValue)}");
            }

            // Add the swap details to the embed
            builder.AddField($"{(isBuy ? "💰 Buy" : "💸 Sell")} Details", swapDetailsField.ToString(), false);

            // Add transaction metadata field
            var metadataField = new StringBuilder();
            metadataField.AppendLine($"**TX Hash:** `{FormatAddress(swap.TransactionHash)}`");
            metadataField.AppendLine($"**Time:** <t:{swap.Timestamp.ToUnixTimeSeconds()}:R>");
            metadataField.AppendLine($"**Trader:** `{FormatAddress(swap.FromAddress)}`");

            builder.AddField("📝 Transaction Data", metadataField.ToString(), false);

            // Add impact/slippage data if available, with more visual indicators
            if (swap.PriceImpact > 0)
            {
                var impactField = new StringBuilder();
                impactField.AppendLine($"**Price Impact:** {swap.PriceImpact:P2}");

                string impactEmoji;
                string impactDescription;

                if (swap.PriceImpact < 0.001m)
                {
                    impactEmoji = "✅";
                    impactDescription = "Minimal impact";
                }
                else if (swap.PriceImpact < 0.005m)
                {
                    impactEmoji = "✓";
                    impactDescription = "Low impact";
                }
                else if (swap.PriceImpact < 0.01m)
                {
                    impactEmoji = "⚠️";
                    impactDescription = "Moderate impact";
                }
                else if (swap.PriceImpact < 0.03m)
                {
                    impactEmoji = "🔴";
                    impactDescription = "High impact";
                }
                else
                {
                    impactEmoji = "⛔";
                    impactDescription = "Extreme impact";
                }

                impactField.AppendLine($"{impactEmoji} **{impactDescription}**");

                if (swap.PriceImpact > 0.01m)
                {
                    impactField.AppendLine("*Significant price movement expected*");
                }

                builder.AddField("📊 Market Impact", impactField.ToString(), false);
            }

            // Add a more prominent explorer link with emoji
            builder.AddField("🔗 Links", $"[View on Explorer]({explorerUrl})", false);

            return builder.Build();
        }

        // Helper method to categorize transaction size
        private (string category, string emoji) CategorizeTransactionSize(decimal usdValue)
        {
            if (usdValue <= 0) return ("Unknown", "❓");

            if (usdValue < 50)
                return ("Plankton", "🦐");
            if (usdValue < 200)
                return ("Shrimp", "🦐");
            if (usdValue < 1000)
                return ("Fish", "🐟");
            if (usdValue < 5000)
                return ("Dolphin", "🐬");
            if (usdValue < 20000)
                return ("Shark", "🦈");
            if (usdValue < 100000)
                return ("Orca", "🐋");

            return ("Whale", "🐳");
        }

        // Helper to format USD value with appropriate decoration
        private string GetValueDisplay(decimal usdValue)
        {
            if (usdValue < 50)
                return $"${usdValue:N2}";
            if (usdValue < 1000)
                return $"${usdValue:N2} 💰";
            if (usdValue < 10000)
                return $"${usdValue:N2} 💰💰";
            if (usdValue < 50000)
                return $"${usdValue:N2} 💰💰💰";

            return $"${usdValue:N2} 🔥💰💰💰";
        }

        // Helper method to format token amounts with proper decimals
        private string FormatTokenAmount(decimal amount, int decimals)
        {
            if (amount == 0) return "0";

            if (decimals <= 0) decimals = 18; // Default to 18 if not specified

            // For very small values
            if (amount < (decimal)Math.Pow(10, -5))
            {
                return amount.ToString("E4"); // Scientific notation
            }

            // For normal values
            string format = "0.";
            format = format.PadRight(format.Length + Math.Min(decimals, 8), '#'); // Show up to 8 decimals
            return amount.ToString(format);
        }

        // Helper method to get token emoji
        private string GetTokenEmoji(string symbol)
        {
            return symbol?.ToUpper() switch
            {
                "WVTRU" => "⚡",
                "VTRO" => "🔷",
                "USDC.POL" => "💵",
                "USDC" => "💵",
                "USDT" => "💲",
                "WETH" => "🔹",
                "ETH" => "🔹",
                "BTC" => "₿",
                "WBTC" => "₿",
                _ => "🪙" // Default token emoji
            };
        }

        // Helper method to format address for display
        private string FormatAddress(string address)
        {
            if (string.IsNullOrEmpty(address) || address.Length < 10)
                return address ?? "Unknown";

            return $"{address.Substring(0, 6)}...{address.Substring(address.Length - 4)}";
        }

        // Add this method to DiscordBotService class
        private async Task UpdateBotAvatarAsync()
        {
            if (!_isRunning || _client == null || _client.ConnectionState != ConnectionState.Connected)
            {
                return;
            }

            try
            {
                // Get the token image URL to use for the bot avatar
                string avatarUrl = null;

                // Try to get token0's image first
                if (_token0Info != null && !string.IsNullOrEmpty(_token0Info.Symbol))
                {
                    if (_tokenImageUrls.TryGetValue(_token0Info.Symbol, out string token0ImageUrl))
                    {
                        avatarUrl = token0ImageUrl;
                    }
                }

                // If no token0 image, try token1
                if (string.IsNullOrEmpty(avatarUrl) && _token1Info != null && !string.IsNullOrEmpty(_token1Info.Symbol))
                {
                    if (_tokenImageUrls.TryGetValue(_token1Info.Symbol, out string token1ImageUrl))
                    {
                        avatarUrl = token1ImageUrl;
                    }
                }

                // If we found an image URL, download it and set as bot avatar
                if (!string.IsNullOrEmpty(avatarUrl))
                {
                    OnLog($"Setting bot avatar using token image: {avatarUrl}");

                    using (var httpClient = new System.Net.Http.HttpClient())
                    {
                        // Download the image
                        var imageBytes = await httpClient.GetByteArrayAsync(avatarUrl);

                        // Convert to stream and set as the bot's avatar
                        using (var stream = new MemoryStream(imageBytes))
                        {
                            await _client.CurrentUser.ModifyAsync(props => props.Avatar = new Image(stream));
                            OnLog("Bot avatar updated successfully");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                OnLog($"Failed to update bot avatar: {ex.Message}");
                if (ex.InnerException != null)
                {
                    OnLog($"Inner exception: {ex.InnerException.Message}");
                }
            }
        }


        // Send periodic embed to Discord channel
        private async Task SendPeriodicEmbed()
        {
            try
            {
                if (_client == null)
                {
                    OnLog("Cannot send embed: Discord client is null");
                    return;
                }

                if (_client.ConnectionState != ConnectionState.Connected)
                {
                    OnLog($"Cannot send embed: Discord client not connected (current state: {_client.ConnectionState})");
                    return;
                }

                // Rate limiting protection - don't send more than once in 30 seconds
                if ((DateTime.UtcNow - _lastEmbedSent).TotalSeconds < 30)
                {
                    OnLog("Rate limit protection: Skipping embed as one was recently sent");
                    return;
                }

                OnLog($"Sending periodic embed to channel {_embedChannelId}");

                // Get the latest data
                var metrics = await _priceSvc.GetTokenMetricsAsync();
                var pairDetails = await _priceSvc.GetPairDetailsAsync();

                // Add historical data
                if (metrics.ContainsKey("Price"))
                {
                    decimal currentPrice = metrics["Price"];
                    
                    // Add historical price changes
                    if (_priceHistory[PRICE_15M] > 0)
                    {
                        metrics["PriceChange15m"] = CalculatePercentageChange(_priceHistory[PRICE_15M], currentPrice);
                    }
                    
                    if (_priceHistory[PRICE_30M] > 0)
                    {
                        metrics["PriceChange30m"] = CalculatePercentageChange(_priceHistory[PRICE_30M], currentPrice);
                    }
                    
                    if (_priceHistory[PRICE_1H] > 0)
                    {
                        metrics["PriceChange1h"] = CalculatePercentageChange(_priceHistory[PRICE_1H], currentPrice);
                    }
                    
                    if (_priceHistory[PRICE_4H] > 0)
                    {
                        metrics["PriceChange4h"] = CalculatePercentageChange(_priceHistory[PRICE_4H], currentPrice);
                    }
                    
                    if (_priceHistory[PRICE_24H] > 0)
                    {
                        metrics["PriceChange24h"] = CalculatePercentageChange(_priceHistory[PRICE_24H], currentPrice);
                    }
                    
                    if (_priceHistory[PRICE_7D] > 0)
                    {
                        metrics["PriceChange7d"] = CalculatePercentageChange(_priceHistory[PRICE_7D], currentPrice);
                    }
                }

                // Find the channel
                var channel = await _client.GetChannelAsync(_embedChannelId) as IMessageChannel;
                if (channel == null)
                {
                    OnLog($"Could not find channel with ID {_embedChannelId}. Make sure the bot has access to this channel.");
                    return;
                }

                OnLog("Creating embed with price and token data");
                var embed = CreateDetailedPriceEmbed(metrics, pairDetails.token0Info, pairDetails.token1Info, pairDetails.pairAddress);

                OnLog("Sending embed to Discord channel");
                await channel.SendMessageAsync(embed: embed);

                _lastEmbedSent = DateTime.UtcNow;
                OnLog("Periodic embed sent successfully");
                
                // Also update bot status after sending an embed
                await UpdateBotStatusAsync();
            }
            catch (Discord.Net.HttpException ex) when (ex.DiscordCode.HasValue && (int)ex.DiscordCode.Value == 50001)
            {
                OnLog($"Missing permissions to send embed to channel {_embedChannelId}");
            }
            catch (TaskCanceledException)
            {
                OnLog("Embed sending was canceled");
            }
            catch (Exception ex)
            {
                OnLog($"Error sending periodic embed: {ex.Message}");
                if (ex.InnerException != null)
                {
                    OnLog($"Inner exception: {ex.InnerException.Message}");
                }
            }
        }

        // Create detailed price embed with enhanced formatting and historical data
        private Embed CreateDetailedPriceEmbed(Dictionary<string, decimal> metrics, TokenInfo token0, TokenInfo token1, string pairAddress)
        {
            // Store previous prices for trend detection if available
            if (_lastPrices == null)
                _lastPrices = new Dictionary<string, decimal>();

            string pairKey = $"{token0.Symbol}/{token1.Symbol}";

            // Parse the custom color or use default green
            var color = new Color(75, 233, 153); // Default green
            try
            {
                if (_embedColor.StartsWith("#"))
                {
                    var hex = _embedColor.TrimStart('#');
                    if (hex.Length == 6 || hex.Length == 8)
                    {
                        var r = Convert.ToByte(hex.Substring(0, 2), 16);
                        var g = Convert.ToByte(hex.Substring(2, 2), 16);
                        var b = Convert.ToByte(hex.Substring(4, 2), 16);
                        color = new Color(r, g, b);
                    }
                }
            }
            catch
            {
                OnLog($"Invalid color format: {_embedColor}, using default");
            }

            // Price trend detection
            string priceChangeEmoji = "➖";
            string priceTrendIndicator = "";
            bool isPriceIncreasing = false;

            if (metrics.ContainsKey("Price"))
            {
                decimal currentPrice = metrics["Price"];
                if (_lastPrices.ContainsKey(pairKey))
                {
                    decimal lastPrice = _lastPrices[pairKey];
                    decimal change = currentPrice - lastPrice;
                    decimal percentChange = lastPrice != 0 ? (change / lastPrice) * 100 : 0;

                    if (percentChange > 0.5m)
                    {
                        priceChangeEmoji = "🟢";
                        isPriceIncreasing = true;

                        // Add trend indicators based on magnitude
                        if (percentChange > 10)
                            priceTrendIndicator = " 🚀 **PUMPING!**";
                        else if (percentChange > 5)
                            priceTrendIndicator = " 📈 **Rising Fast**";
                        else if (percentChange > 1)
                            priceTrendIndicator = " ↗️ Rising";

                        color = new Color(46, 204, 113); // Green for price increase
                    }
                    else if (percentChange < -0.5m)
                    {
                        priceChangeEmoji = "🔴";

                        // Add trend indicators based on magnitude
                        if (percentChange < -10)
                            priceTrendIndicator = " 📉 **DUMPING!**";
                        else if (percentChange < -5)
                            priceTrendIndicator = " ↘️ **Falling Fast**";
                        else if (percentChange < -1)
                            priceTrendIndicator = " ↘️ Falling";

                        color = new Color(231, 76, 60); // Red for price decrease
                    }
                }

                // Store current price for next comparison
                _lastPrices[pairKey] = currentPrice;
            }

            // Get token-specific emojis
            string token0Emoji = GetTokenEmoji(token0.Symbol);
            string token1Emoji = GetTokenEmoji(token1.Symbol);

            // Get token thumbnail URL
            string tokenThumbnailUrl = GetTokenThumbnailUrl(token0.Symbol);

            // Create title with emojis
            string title = $"{token0Emoji} {token0.Symbol}/{token1.Symbol} Market Update {token1Emoji}";

            // Build description
            var descBuilder = new StringBuilder();
            descBuilder.AppendLine($"**Latest data for the {token0.Symbol}/{token1.Symbol} trading pair**");

            // Add market sentiment if we have a trend
            if (!string.IsNullOrEmpty(priceTrendIndicator))
            {
                descBuilder.AppendLine($"\n**Market Sentiment:** {priceChangeEmoji} {(isPriceIncreasing ? "Bullish" : "Bearish")}{priceTrendIndicator}");
            }

            // Add historical changes to description if available
            var historicalChanges = new StringBuilder();
            bool hasHistoricalData = false;
            
            if (metrics.ContainsKey("PriceChange15m"))
            {
                decimal change = metrics["PriceChange15m"];
                string changeText = change >= 0 ? $"+{change:N2}%" : $"{change:N2}%";
                string emoji = change >= 0 ? "🟢" : "🔴";
                historicalChanges.AppendLine($"**15m:** {emoji} {changeText}");
                hasHistoricalData = true;
            }
            
            if (metrics.ContainsKey("PriceChange1h"))
            {
                decimal change = metrics["PriceChange1h"];
                string changeText = change >= 0 ? $"+{change:N2}%" : $"{change:N2}%";
                string emoji = change >= 0 ? "🟢" : "🔴";
                historicalChanges.AppendLine($"**1h:** {emoji} {changeText}");
                hasHistoricalData = true;
            }
            
            if (metrics.ContainsKey("PriceChange24h"))
            {
                decimal change = metrics["PriceChange24h"];
                string changeText = change >= 0 ? $"+{change:N2}%" : $"{change:N2}%";
                string emoji = change >= 0 ? "🟢" : "🔴";
                historicalChanges.AppendLine($"**24h:** {emoji} {changeText}");
                hasHistoricalData = true;
            }
            
            if (hasHistoricalData)
            {
                descBuilder.AppendLine("\n**Historical Price Changes:**");
                descBuilder.Append(historicalChanges);
            }

            // Add timestamp information to description
            descBuilder.AppendLine($"\n*Last Updated: {DateTime.UtcNow:HH:mm:ss} UTC*");

            var builder = new EmbedBuilder()
                .WithTitle(title)
                .WithDescription(descBuilder.ToString())
                .WithColor(color)
                .WithFooter(footer => {
                    footer.WithText($"RadX Price Bot • {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
                    footer.WithIconUrl("https://i.imgur.com/gXdWwTR.png");
                })
                .WithTimestamp(DateTimeOffset.Now);

            // Add the token image as thumbnail if available
            if (_includeChartInEmbed && !string.IsNullOrEmpty(tokenThumbnailUrl))
            {
                builder.WithThumbnailUrl(tokenThumbnailUrl);
            }

            // Add author with token logo if available
            builder.WithAuthor(author => {
                author.Name = $"{token0.Name} ({token0.Symbol})";
                // Add token logo URL if available
                if (_tokenImageUrls.TryGetValue(token0.Symbol, out string logoUrl) && !string.IsNullOrEmpty(logoUrl))
                {
                    author.IconUrl = logoUrl;
                }
            });

            // Price Information - always included - now with trends & emojis
            var priceField = new StringBuilder();
            priceField.AppendLine($"{priceChangeEmoji} **Current Price:** {metrics["Price"]:N6} {token1.Symbol}");

            if (metrics.ContainsKey("PriceUsd") && metrics["PriceUsd"] > 0)
            {
                decimal usdPrice = metrics["PriceUsd"];
                string usdEmoji = "💵";
                if (usdPrice > 100m) usdEmoji = "💰";
                else if (usdPrice > 1000m) usdEmoji = "💎";
                else if (usdPrice < 0.01m) usdEmoji = "🪙";

                priceField.AppendLine($"{usdEmoji} **USD Price:** ${metrics["PriceUsd"]:N4}");
            }

            decimal reversePrice = metrics["Price"] > 0 ? 1 / metrics["Price"] : 0;
            if (reversePrice > 0)
                priceField.AppendLine($"🔄 **Reverse Rate:** {reversePrice:N6} {token0.Symbol}/{token1.Symbol}");

            builder.AddField($"{GetPriceFieldEmoji(metrics["Price"])} Price Information", priceField.ToString(), true);

            // Liquidity Information - conditionally included
            if (_includeLiquidityInfoInEmbed)
            {
                var liquidityField = new StringBuilder();

                decimal liquidity = metrics.ContainsKey("Liquidity") ? metrics["Liquidity"] : 0;
                string liquidityEmoji = GetLiquidityEmoji(liquidity);

                liquidityField.AppendLine($"{liquidityEmoji} **Total Liquidity:** ${FormatLargeNumberWithEmoji(liquidity)}");

                // Volume information if available
                if (metrics.ContainsKey("Volume24h") && metrics["Volume24h"] > 0)
                {
                    decimal volume = metrics["Volume24h"];
                    string volumeEmoji = GetVolumeEmoji(volume, liquidity);
                    liquidityField.AppendLine($"{volumeEmoji} **24h Volume:** ${FormatLargeNumberWithEmoji(volume)}");
                }

                liquidityField.AppendLine($"{token0Emoji} **{token0.Symbol} Reserve:** {FormatLargeNumberWithEmoji(metrics["Reserve0"])}");
                liquidityField.AppendLine($"{token1Emoji} **{token1.Symbol} Reserve:** {FormatLargeNumberWithEmoji(metrics["Reserve1"])}");

                builder.AddField($"💧 Liquidity Information", liquidityField.ToString(), true);
            }

            // Token Information - conditionally included
            if (_includeTokenInfoInEmbed)
            {
                var tokenInfoField = new StringBuilder();

                decimal marketCap = metrics.ContainsKey("MarketCap") ? metrics["MarketCap"] : 0;
                string mcapEmoji = GetMarketCapEmoji(marketCap);

                tokenInfoField.AppendLine($"{mcapEmoji} **Market Cap:** ${FormatLargeNumberWithEmoji(marketCap)}");

                if (metrics.ContainsKey("TotalSupply") && metrics["TotalSupply"] > 0)
                {
                    decimal totalSupply = metrics["TotalSupply"];
                    tokenInfoField.AppendLine($"📊 **Total Supply:** {FormatLargeNumberWithEmoji(totalSupply)} {token0.Symbol}");
                }

                if (metrics.ContainsKey("CirculatingSupply") && metrics["CirculatingSupply"] > 0)
                {
                    decimal circSupply = metrics["CirculatingSupply"];
                    decimal totalSupply = metrics.ContainsKey("TotalSupply") ? metrics["TotalSupply"] : 0;

                    // Calculate the percentage of circulating supply if total supply is available
                    if (totalSupply > 0)
                    {
                        decimal circulationPercent = (circSupply / totalSupply) * 100;
                        tokenInfoField.AppendLine($"🔄 **Circulating Supply:** {FormatLargeNumberWithEmoji(circSupply)} {token0.Symbol} ({circulationPercent:N2}%)");
                    }
                    else
                    {
                        tokenInfoField.AppendLine($"🔄 **Circulating Supply:** {FormatLargeNumberWithEmoji(circSupply)} {token0.Symbol}");
                    }
                }

                // Add holder count if available
                if (metrics.ContainsKey("HolderCount") && metrics["HolderCount"] > 0)
                {
                    decimal holders = metrics["HolderCount"];
                    string holderEmoji = holders > 1000 ? "👥" : "👤";
                    tokenInfoField.AppendLine($"{holderEmoji} **Holders:** {FormatLargeNumberWithEmoji(holders)}");
                }

                builder.AddField($"📈 Token Information", tokenInfoField.ToString(), false);
            }

            // Contract Information - always included
            var addressField = new StringBuilder();
            addressField.AppendLine($"🔗 **Pair Address:** `{pairAddress}`");
            addressField.AppendLine($"{token0Emoji} **{token0.Symbol} Address:** `{token0.Address}`");
            addressField.AppendLine($"{token1Emoji} **{token1.Symbol} Address:** `{token1.Address}`");

            // Add block explorer links if available
            addressField.AppendLine($"\n🔍 **View on Explorer:** [Pair](https://explorer.vitruveo.xyz/address/{pairAddress}) | " +
                                   $"[{token0.Symbol}](https://explorer.vitruveo.xyz/address/{token0.Address}) | " +
                                   $"[{token1.Symbol}](https://explorer.vitruveo.xyz/address/{token1.Address})");

            builder.AddField($"📝 Contract Information", addressField.ToString(), false);

            // Add trading tips based on metrics
            if (metrics.ContainsKey("Volume24h") && metrics.ContainsKey("Liquidity") &&
                metrics["Volume24h"] > 0 && metrics["Liquidity"] > 0)
            {
                decimal volumeToLiquidityRatio = metrics["Volume24h"] / metrics["Liquidity"];
                string tradeTip;

                if (volumeToLiquidityRatio > 0.5m)
                    tradeTip = "⚠️ **High volume/liquidity ratio** - Trading may cause significant slippage";
                else if (volumeToLiquidityRatio > 0.2m)
                    tradeTip = "⚠️ **Moderate volume/liquidity ratio** - Watch for slippage on larger trades";
                else
                    tradeTip = "✅ **Good liquidity depth** - Should handle normal trading volume with minimal slippage";

                builder.AddField("💡 Trading Insight", tradeTip, false);
            }

            return builder.Build();
        }

        // Helper methods for the enhanced embed
        private string GetPriceFieldEmoji(decimal price)
        {
            if (price < 0.00001m) return "🔬"; // Microscopic price
            if (price < 0.01m) return "💰";    // Small price
            if (price > 1000m) return "💎";    // Large price
            return "💲";                        // Default price emoji
        }        private string GetLiquidityEmoji(decimal liquidity)
        {
            if (liquidity < 10000) return "💧"; // Very low liquidity
            if (liquidity < 100000) return "💦"; // Low liquidity
            if (liquidity < 1000000) return "🌊"; // Medium liquidity
            return "🌋";                         // High liquidity
        }

        private string GetVolumeEmoji(decimal volume, decimal liquidity)
        {
            if (liquidity == 0) return "📊"; // Default

            decimal volumeRatio = volume / liquidity;

            if (volumeRatio > 0.5m) return "🔥"; // High volume relative to liquidity
            if (volumeRatio > 0.2m) return "📈"; // Good volume
            if (volumeRatio > 0.05m) return "📊"; // Moderate volume
            return "📉";                         // Low volume
        }

        private string GetMarketCapEmoji(decimal marketCap)
        {
            if (marketCap > 1000000000) return "🏆"; // >1B market cap
            if (marketCap > 100000000) return "💰"; // >100M market cap
            if (marketCap > 10000000) return "💵"; // >10M market cap
            return "🪙";                          // Small market cap
        }

        private string FormatLargeNumberWithEmoji(decimal number)
        {
            string formatted = FormatLargeNumber(number);

            // Add emoji based on number magnitude
            if (formatted.EndsWith("T"))
                return $"{formatted} 🏆"; // Trillion
            if (formatted.EndsWith("B"))
                return $"{formatted} 💰"; // Billion
            if (formatted.EndsWith("M"))
                return $"{formatted} 💵"; // Million
            if (formatted.EndsWith("K"))
                return $"{formatted} 💴"; // Thousand

            return formatted;
        }

        public async Task StopAsync()
        {
            if (!_isRunning)
            {
                OnLog("Bot is not running");
                return;
            }

            OnLog("Stopping bot...");

            // Clean up timers
            if (_updateTimer != null)
            {
                _updateTimer.Stop();
                _updateTimer.Dispose();
                _updateTimer = null;
            }

            if (_embedTimer != null)
            {
                _embedTimer.Stop();
                _embedTimer.Dispose();
                _embedTimer = null;
            }

            if (_historyUpdateTimer != null)
            {
                _historyUpdateTimer.Stop();
                _historyUpdateTimer.Dispose();
                _historyUpdateTimer = null;
            }

            if (_transactionMonitorTimer != null)
            {
                _transactionMonitorTimer.Stop();
                _transactionMonitorTimer.Dispose();
                _transactionMonitorTimer = null;
            }

            // Cancel running tasks
            if (_cts != null)
            {
                _cts.Cancel();
                _cts.Dispose();
                _cts = null;
            }

            // Disconnect Discord client
            if (_client != null)
            {
                try
                {
                    await _client.StopAsync();
                    await _client.LogoutAsync();
                }
                catch (Exception ex)
                {
                    OnLog($"Error stopping Discord client: {ex.Message}");
                }
                finally
                {
                    _client.Dispose();
                    _client = null;
                }
            }

            _isRunning = false;
            OnLog("Bot stopped successfully.");
        }

        // Update the bot's token pair
        public async Task UpdateTokenPairAsync(PriceService newPriceService, string newNickname = null)
        {
            if (!_isRunning)
            {
                OnLog("Bot is not running, cannot update token pair");
                return;
            }

            _priceSvc = newPriceService ?? throw new ArgumentNullException(nameof(newPriceService), "Price service cannot be null");

            if (!string.IsNullOrEmpty(newNickname))
            {
                _nickname = newNickname;
            }

            // Reset transaction tracking to prevent duplicate detection issues
            _lastProcessedTransactions.Clear();
            OnLog("Transaction tracking reset after switching token pair");

            // Reset historical price data
            InitializePriceHistory();
            OnLog("Historical price data reset after switching token pair");

            // Refresh token information and update displays
            await LoadTokenInformationAsync();
            await UpdateBotNicknameAsync();
            await UpdateBotStatusAsync();
            await UpdateBotAvatarAsync(); // Update avatar with new token image

            OnLog("Bot token pair updated successfully");
        }


        // Refresh price data and reset tracking
        public async Task RefreshPriceDataAsync()
        {
            if (_priceSvc == null)
                return;

            try
            {
                // Force refresh metrics
                var metrics = await _priceSvc.GetTokenMetricsAsync(forceRefresh: true);
                var pairDetails = await _priceSvc.GetPairDetailsAsync(forceRefresh: true);

                // Update token info
                _token0Info = pairDetails.token0Info;
                _token1Info = pairDetails.token1Info;

                // Reset transaction tracking to prevent duplicate detection issues
                _lastProcessedTransactions.Clear();
                OnLog("Transaction list cleared");

                // Update historical prices with latest price
                if (metrics != null && metrics.ContainsKey("Price") && metrics["Price"] > 0)
                {
                    // Update the most recent timeframe (15m) with current price
                    _priceHistory[PRICE_15M] = metrics["Price"];
                    _priceTimestamps[PRICE_15M] = DateTime.Now;
                    OnLog("Updated 15m historical price reference point");
                }

                // Report refreshed status
                var pairInfo = new PairInfo
                {
                    Address = pairDetails.pairAddress,
                    Token0 = pairDetails.token0Info,
                    Token1 = pairDetails.token1Info
                };
                // Use reflection to set the Name field directly
                typeof(PairInfo).GetField("Name").SetValue(pairInfo, $"{pairDetails.token0Info.Symbol}/{pairDetails.token1Info.Symbol}");

                OnLog("Price data refreshed successfully");
            }
            catch (Exception ex)
            {
                OnLog($"Error refreshing price data: {ex.Message}");
                if (ex.InnerException != null)
                {
                    OnLog($"Inner exception: {ex.InnerException.Message}");
                }
            }
        }

        private Task LogAsync(LogMessage msg)
        {
            OnLog(msg.ToString());
            return Task.CompletedTask;
        }

        private Task ReadyAsync()
        {
            // Don't block the gateway task - handle everything asynchronously
            _ = Task.Run(async () =>
            {
                try
                {
                    OnLog("Client ready. Registering commands...");

                    // Initialize token image URLs
                    InitializeTokenImageUrls();

                    var guild = _client.GetGuild(_guildId);
                    if (guild == null)
                    {
                        OnLog($"ERROR: Guild {_guildId} not found!");
                        return;
                    }

                    var cmds = new List<SlashCommandBuilder>
                    {
                        new SlashCommandBuilder()
                            .WithName("price")
                            .WithDescription("Get token price (default 1)")
                            .AddOption(new SlashCommandOptionBuilder()
                                .WithName("amount")
                                .WithDescription("Input amount")
                                .WithRequired(false)
                                .WithType(ApplicationCommandOptionType.Number)),

                        new SlashCommandBuilder()
                            .WithName("marketcap")
                            .WithDescription("Show token market capitalization"),

                        new SlashCommandBuilder()
                            .WithName("liquidity")
                            .WithDescription("Show token pair liquidity"),

                        new SlashCommandBuilder()
                            .WithName("tokeninfo")
                            .WithDescription("Show detailed token information"),

                        new SlashCommandBuilder()
                            .WithName("refresh")
                            .WithDescription("Refresh token data and update bot status"),

                        new SlashCommandBuilder()
                            .WithName("embed")
                            .WithDescription("Send a detailed price and information embed"),
                            
                        new SlashCommandBuilder()
                            .WithName("chart")
                            .WithDescription("Show price change over time")
                            .AddOption(new SlashCommandOptionBuilder()
                                .WithName("timeframe")
                                .WithDescription("Timeframe (1h, 24h, 7d)")
                                .WithRequired(false)
                                .WithType(ApplicationCommandOptionType.String))
                    };

                    try
                    {
                        foreach (var cmd in cmds)
                        {
                            await guild.CreateApplicationCommandAsync(cmd.Build());
                            OnLog($"Registered /{cmd.Name} command");
                        }
                    }
                    catch (HttpException ex)
                    {
                        OnLog($"Failed registering commands: {ex.Message}");
                    }

                    await ConfigureBotAsync();
                }
                catch (Exception ex)
                {
                    OnLog($"Error in ready handler: {ex.Message}");
                    if (ex.InnerException != null)
                    {
                        OnLog($"Inner exception: {ex.InnerException.Message}");
                    }
                }
            });

            // Return completed task immediately to avoid blocking gateway
            return Task.CompletedTask;
        }

        private async Task ConfigureBotAsync()
        {
            // Load token information
            await LoadTokenInformationAsync();

            // Update nickname and status
            await UpdateBotNicknameAsync();
            await UpdateBotStatusAsync();

            // Load token information will already try to update the avatar, but ensure it's set
            if (_client.CurrentUser.GetAvatarUrl() == null)
            {
                await UpdateBotAvatarAsync();
            }
        }


        private async Task LoadTokenInformationAsync()
        {
            try
            {
                // Get pair details and token information
                var (pairAddress, token0, token1) = await _priceSvc.GetPairDetailsAsync();
                _token0Info = token0;
                _token1Info = token1;

                // Get USD prices
                var usdPrice = await _priceSvc.GetTokenUsdPriceAsync(_token0Info.Address);
                _token0Info.UsdPrice = usdPrice;

                OnLog($"Loaded token information: {_token0Info.Symbol}/{_token1Info.Symbol} (${usdPrice:N6} USD)");

                // Update the bot's avatar with the token image
                await UpdateBotAvatarAsync();
            }
            catch (Exception ex)
            {
                OnLog($"Failed to load token information: {ex.Message}");
                if (ex.InnerException != null)
                {
                    OnLog($"Inner exception: {ex.InnerException.Message}");
                }
            }
        }


        private async Task UpdateBotNicknameAsync()
        {
            if (!_isRunning || _client == null || _client.ConnectionState != ConnectionState.Connected)
            {
                return;
            }

            try
            {
                var guild = _client.GetGuild(_guildId);
                if (guild == null)
                {
                    OnLog("Guild not found when updating nickname");
                    return;
                }

                var user = guild.GetUser(_client.CurrentUser.Id);
                if (user == null)
                {
                    OnLog("Bot user not found in guild");
                    return;
                }

                // Use custom nickname if provided
                if (!string.IsNullOrWhiteSpace(_nickname))
                {
                    await user.ModifyAsync(x => x.Nickname = _nickname);
                    OnLog($"Set custom bot nickname → {_nickname}");
                    return;
                }

                // Otherwise build a dynamic nickname with token info and price
                if (_token0Info != null && _token1Info != null)
                {
                    var metrics = await _priceSvc.GetTokenMetricsAsync();
                    decimal price = metrics.ContainsKey("Price") ? metrics["Price"] : 0;
                    decimal usdPrice = metrics.ContainsKey("PriceUsd") ? metrics["PriceUsd"] : 0;
                    decimal marketCap = metrics.ContainsKey("MarketCap") ? metrics["MarketCap"] : 0;

                    // Get price change if available
                    string priceChangeIndicator = string.Empty;
                    if (metrics.ContainsKey("PriceChange24h"))
                    {
                        decimal change24h = metrics["PriceChange24h"];
                        priceChangeIndicator = change24h >= 0 ? $" ↗️{change24h:N1}%" : $" ↘️{change24h:N1}%";
                        
                        // Truncate if nickname would be too long
                        if (priceChangeIndicator.Length > 8)
                        {
                            priceChangeIndicator = change24h >= 0 ? " ↗️" : " ↘️";
                        }
                    }

                    string botName;
                    if (usdPrice > 0)
                    {
                        // Include USD price in nickname
                        if (usdPrice < 0.0001m)
                            botName = $"{_token0Info.Symbol} ${usdPrice.ToString("E4")}"; // Scientific notation for very small values
                        else
                            botName = $"{_token0Info.Symbol} ${usdPrice:N4}{priceChangeIndicator}";

                        // Add market cap indicator
                        if (marketCap > 0)
                        {
                            string mcapIndicator = FormatLargeNumber(marketCap);
                            botName = $"{_token0Info.Symbol} ${usdPrice:N4}{priceChangeIndicator} | MCap: {mcapIndicator}";

                            // If nickname would be too long, simplify it
                            if (botName.Length > 32)
                            {
                                botName = $"{_token0Info.Symbol} ${usdPrice:N4}{priceChangeIndicator}";
                            }
                        }
                    }
                    else if (price > 0)
                    {
                        // Use pair price if USD price isn't available
                        botName = $"{_token0Info.Symbol} {price:N6} {_token1Info.Symbol}{priceChangeIndicator}";
                        
                        // Truncate if too long
                        if (botName.Length > 32)
                        {
                            botName = $"{_token0Info.Symbol} {price:N4} {_token1Info.Symbol}{priceChangeIndicator}";
                        }
                    }
                    else
                    {
                        botName = $"{_token0Info.Symbol} Bot";
                    }

                    // Make sure the nickname isn't too long for Discord (max 32 characters)
                    if (botName.Length > 32)
                    {
                        botName = botName.Substring(0, 32);
                    }

                    await user.ModifyAsync(x => x.Nickname = botName);
                    OnLog($"Set dynamic bot nickname → {botName}");
                }
            }
            catch (Exception ex)
            {
                OnLog($"Failed to set nickname: {ex.Message}");
            }
        }

        private async Task UpdateBotStatusAsync()
        {
            if (!_isRunning || _client == null || _client.ConnectionState != ConnectionState.Connected)
            {
                return;
            }

            try
            {
                // Get metrics for status display - use GetCurrentMetricsAsync() instead of directly
                // calling GetTokenMetricsAsync() to include historical data
                var metrics = await GetCurrentMetricsAsync();
                if (metrics == null)
                {
                    OnLog("Failed to get current metrics for status update");
                    return;
                }

                string statusText;

                // Make status type case-insensitive and parse it properly
                string statusTypeKey = _statusType?.ToLower().Trim() ?? "price";

                switch (statusTypeKey)
                {
                    case "price":
                        var price = metrics.ContainsKey("Price") ? metrics["Price"] : 0;
                        var usdPrice = metrics.ContainsKey("PriceUsd") ? metrics["PriceUsd"] : 0;
                        decimal change24h = metrics.ContainsKey("PriceChange24h") ? metrics["PriceChange24h"] : 0;

                        // Format based on price magnitude
                        string priceDisplay = FormatPrice(price);
                        string usdDisplay = usdPrice > 0 ? $"${FormatPrice(usdPrice)}" : "";
                        string changeDisplay = change24h != 0 ? (change24h > 0 ? $" (+{change24h:N2}%)" : $" ({change24h:N2}%)") : "";

                        if (!string.IsNullOrEmpty(usdDisplay))
                            statusText = $"{_token0Info?.Symbol} {priceDisplay} ({usdDisplay}{changeDisplay})";
                        else
                            statusText = $"{_token0Info?.Symbol} {priceDisplay}{changeDisplay}";
                        break;

                    case "reserves":
                        var reserve0 = metrics.ContainsKey("Reserve0") ? metrics["Reserve0"] : 0;
                        var reserve0Usd = metrics.ContainsKey("Reserve0Usd") ? metrics["Reserve0Usd"] : 0;

                        // Add USD value for token reserves if available
                        string reserveDisplay = $"{FormatLargeNumber(reserve0)} {_token0Info?.Symbol}";
                        if (reserve0Usd > 0)
                            reserveDisplay += $" (${FormatLargeNumber(reserve0Usd)})";

                        statusText = reserveDisplay;
                        break;

                    case "market cap":
                    case "marketcap":
                    case "mcap":
                        var marketCap = metrics.ContainsKey("MarketCap") ? metrics["MarketCap"] : 0;
                        statusText = $"MCap: ${FormatLargeNumber(marketCap)}";
                        break;

                    case "volume":
                    case "vol":
                        var volume = metrics.ContainsKey("Volume24h") ? metrics["Volume24h"] : 0;
                        statusText = $"24h Vol: ${FormatLargeNumber(volume)}";
                        break;

                    case "custom":
                        if (!string.IsNullOrWhiteSpace(_customStatus))
                        {
                            // Allow placeholders in custom status
                            string customText = _customStatus
                                .Replace("{symbol}", _token0Info?.Symbol ?? "Token")
                                .Replace("{price}", FormatPrice(metrics.ContainsKey("Price") ? metrics["Price"] : 0))
                                .Replace("{usdprice}", FormatPrice(metrics.ContainsKey("PriceUsd") ? metrics["PriceUsd"] : 0))
                                .Replace("{mcap}", FormatLargeNumber(metrics.ContainsKey("MarketCap") ? metrics["MarketCap"] : 0))
                                .Replace("{volume}", FormatLargeNumber(metrics.ContainsKey("Volume24h") ? metrics["Volume24h"] : 0))
                                .Replace("{change24h}", metrics.ContainsKey("PriceChange24h") ?
                                    (metrics["PriceChange24h"] >= 0 ? $"+{metrics["PriceChange24h"]:N2}%" : $"{metrics["PriceChange24h"]:N2}%") : "0%")
                                .Replace("{change1h}", metrics.ContainsKey("PriceChange1h") ?
                                    (metrics["PriceChange1h"] >= 0 ? $"+{metrics["PriceChange1h"]:N2}%" : $"{metrics["PriceChange1h"]:N2}%") : "0%");

                            statusText = customText;
                        }
                        else
                        {
                            statusText = $"{_token0Info?.Symbol} Price Bot";
                        }
                        break;

                    default:
                        // Default format with the most useful information
                        var defaultPrice = metrics.ContainsKey("Price") ? metrics["Price"] : 0;
                        var defaultUsdPrice = metrics.ContainsKey("PriceUsd") ? metrics["PriceUsd"] : 0;
                        decimal defaultChange24h = metrics.ContainsKey("PriceChange24h") ? metrics["PriceChange24h"] : 0;
                        string defaultChangeDisplay = defaultChange24h != 0 ?
                            (defaultChange24h > 0 ? $" (+{defaultChange24h:N2}%)" : $" ({defaultChange24h:N2}%)") : "";

                        if (defaultUsdPrice > 0)
                            statusText = $"{_token0Info?.Symbol}: ${FormatPrice(defaultUsdPrice)}{defaultChangeDisplay}";
                        else
                            statusText = $"{_token0Info?.Symbol}: {FormatPrice(defaultPrice)} {_token1Info?.Symbol}{defaultChangeDisplay}";
                        break;
                }

                // Ensure status text isn't too long (Discord limits presence to 128 characters)
                if (statusText.Length > 128)
                {
                    statusText = statusText.Substring(0, 128);
                }

                // Set the activity type based on status type
                var activityType = GetActivityTypeForStatus(_statusType);

                // Log what we're setting
                OnLog($"Setting bot status to: '{statusText}' with activity type: {activityType}");

                // Set the actual activity
                await _client.SetActivityAsync(new Game(statusText, activityType));
            }
            catch (Exception ex)
            {
                OnLog($"Failed to update status: {ex.Message}");
                if (ex.InnerException != null)
                {
                    OnLog($"Inner exception: {ex.InnerException.Message}");
                }
            }
        }


        private ActivityType GetActivityTypeForStatus(string statusType)
        {
            return statusType?.ToLower() switch
            {
                "price" => ActivityType.Watching,
                "reserves" => ActivityType.Playing,
                "market cap" => ActivityType.Watching,
                "volume" => ActivityType.Watching,
                "custom" => ActivityType.Playing,
                _ => ActivityType.Watching
            };
        }

        private string FormatPrice(decimal price)
        {
            // Format price based on magnitude
            if (price == 0) return "0";

            if (price < 0.00000001m)
                return price.ToString("E6", CultureInfo.InvariantCulture); // Scientific notation for extremely small values
            else if (price < 0.0001m)
                return price.ToString("0.00000000", CultureInfo.InvariantCulture); // Show 8 decimals for very small values
            else if (price < 0.01m)
                return price.ToString("0.000000", CultureInfo.InvariantCulture); // 6 decimals for small values
            else if (price < 1m)
                return price.ToString("0.0000", CultureInfo.InvariantCulture); // 4 decimals for values under 1
            else if (price < 1000)
                return price.ToString("0.00", CultureInfo.InvariantCulture); // 2 decimals for normal values
            else
                return FormatLargeNumber(price); // Formatted large number
        }

        private string FormatLargeNumber(decimal number)
        {
            if (number >= 1_000_000_000_000)
                return $"{number / 1_000_000_000_000:N2}T";
            else if (number >= 1_000_000_000)
                return $"{number / 1_000_000_000:N2}B";
            else if (number >= 1_000_000)
                return $"{number / 1_000_000:N2}M";
            else if (number >= 1_000)
                return $"{number / 1_000:N2}K";
            else
                return $"{number:N2}";
        }

        private async Task SlashCommandHandler(SocketSlashCommand command)
        {
            try
            {
                switch (command.CommandName)
                {
                    case "price":
                        await HandlePriceCommand(command);
                        break;

                    case "marketcap":
                        await HandleMarketCapCommand(command);
                        break;

                    case "liquidity":
                        await HandleLiquidityCommand(command);
                        break;

                    case "tokeninfo":
                        await HandleTokenInfoCommand(command);
                        break;

                    case "refresh":
                        await HandleRefreshCommand(command);
                        break;

                    case "embed":
                        await HandleEmbedCommand(command);
                        break;
                        
                    case "chart":
                        await HandleChartCommand(command);
                        break;
                }
            }
            catch (Exception ex)
            {
                OnLog($"Error handling command {command.CommandName}: {ex.Message}");
                try
                {
                    await command.RespondAsync("❌ An error occurred while processing this command.");
                }
                catch { /* Already responded */ }
            }
        }

        private async Task HandleEmbedCommand(SocketSlashCommand command)
        {
            OnLog("/embed invoked");
            try
            {
                await command.DeferAsync(); // Let Discord know we're working on it

                // Get the latest price data
                var metrics = await _priceSvc.GetTokenMetricsAsync();
                var pairDetails = await _priceSvc.GetPairDetailsAsync();

                // Create and send the embed
                var embed = CreateDetailedPriceEmbed(metrics, pairDetails.token0Info, pairDetails.token1Info, pairDetails.pairAddress);
                await command.FollowupAsync(embed: embed);
                _lastEmbedSent = DateTime.UtcNow; // Update last sent time for rate limiting

                OnLog("Manual embed sent successfully via /embed command");
            }
            catch (Exception ex)
            {
                OnLog($"Error creating embed: {ex.Message}");
                await command.FollowupAsync("❌ Failed to create price embed");
            }
        }

        private async Task HandleRefreshCommand(SocketSlashCommand command)
        {
            OnLog("/refresh invoked");
            try
            {
                await command.DeferAsync(); // Let Discord know we're working on it

                // Reload token information and update the historical prices
                await RefreshPriceDataAsync();
                await LoadTokenInformationAsync();
                await UpdateBotNicknameAsync();
                await UpdateBotStatusAsync();

                // Get updated metrics after refresh
                var metrics = await _priceSvc.GetTokenMetricsAsync();

                // Create a response with current price and change info
                var embedBuilder = new EmbedBuilder()
                    .WithTitle("Token Data Refreshed")
                    .WithColor(Color.Green)
                    .WithCurrentTimestamp();
                
                // Build description with price and change data
                var description = new StringBuilder();
                description.AppendLine($"Successfully refreshed data for {_token0Info?.Symbol}/{_token1Info?.Symbol}");
                
                if (metrics.ContainsKey("Price"))
                {
                    description.AppendLine($"\n**Current Price:** {metrics["Price"]:N6} {_token1Info?.Symbol}");
                }
                
                if (metrics.ContainsKey("PriceUsd") && metrics["PriceUsd"] > 0)
                {
                    description.AppendLine($"**USD Price:** ${metrics["PriceUsd"]:N4}");
                }
                
                // Add change data if available
                if (metrics.ContainsKey("PriceChange24h"))
                {
                    decimal change = metrics["PriceChange24h"];
                    string changeText = change >= 0 ? $"+{change:N2}%" : $"{change:N2}%";
                    string emoji = change >= 0 ? "🟢" : "🔴";
                    description.AppendLine($"**24h Change:** {emoji} {changeText}");
                }
                
                embedBuilder.WithDescription(description.ToString());
                
                // Add timestamp field
                embedBuilder.AddField("Refresh Time", $"<t:{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}:F>", true);

                await command.FollowupAsync(embed: embedBuilder.Build());
            }
            catch (Exception ex)
            {
                OnLog($"Error refreshing data: {ex.Message}");
                await command.FollowupAsync("❌ Failed to refresh token data");
            }
        }

        private async Task HandlePriceCommand(SocketSlashCommand command)
        {
            decimal amt = 1m;
            var opt = command.Data.Options.FirstOrDefault(o => o.Name == "amount");
            if (opt?.Value is double d) amt = Convert.ToDecimal(d);

            OnLog($"/price invoked (amount={amt})");
            try
            {
                var price = await _priceSvc.GetPriceAsync(amt);
                var metrics = await _priceSvc.GetTokenMetricsAsync();
                decimal usdPrice = metrics.ContainsKey("PriceUsd") ? metrics["PriceUsd"] * amt : 0;

                // Get token thumbnail URL for the embed
                string tokenThumbnailUrl = GetTokenThumbnailUrl(_token0Info?.Symbol);

                // Determine color based on 24h price change if available
                Color embedColor = Color.LightGrey;
                if (metrics.ContainsKey("PriceChange24h"))
                {
                    decimal change = metrics["PriceChange24h"];
                    if (change > 1) embedColor = new Color(46, 204, 113); // Green for increase
                    else if (change < -1) embedColor = new Color(231, 76, 60); // Red for decrease
                }

                var embedBuilder = new EmbedBuilder()
                    .WithTitle($"{_token0Info?.Symbol ?? "Token"} Price")
                    .WithColor(embedColor)
                    .WithCurrentTimestamp()
                    .WithFooter("Data from Vitruveo Network");

                // Add the token image as thumbnail if available
                if (!string.IsNullOrEmpty(tokenThumbnailUrl))
                {
                    embedBuilder.WithThumbnailUrl(tokenThumbnailUrl);
                }

                embedBuilder.AddField($"{amt} {_token0Info?.Symbol ?? "Token"} =", $"{price:N6} {_token1Info?.Symbol ?? ""}");

                if (usdPrice > 0)
                {
                    embedBuilder.AddField("USD Value", $"${usdPrice:N4}");
                }

                // Add price change info if available
                if (metrics.ContainsKey("PriceChange24h"))
                {
                    decimal change = metrics["PriceChange24h"];
                    string changeFormatted = change >= 0 ? $"+{change:N2}%" : $"{change:N2}%";
                    string emoji = change >= 0 ? "🟢" : "🔴";
                    embedBuilder.AddField("24h Change", $"{emoji} {changeFormatted}", true);
                }

                // Add historical timeframes if available
                var historicalChanges = new StringBuilder();
                bool hasHistorical = false;
                
                if (metrics.ContainsKey("PriceChange1h") && metrics["PriceChange1h"] != 0)
                {
                    decimal change = metrics["PriceChange1h"];
                    string formatted = change >= 0 ? $"+{change:N2}%" : $"{change:N2}%";
                    string emoji = change >= 0 ? "🟢" : "🔴";
                    historicalChanges.AppendLine($"**1h:** {emoji} {formatted}");
                    hasHistorical = true;
                }
                
                if (metrics.ContainsKey("PriceChange24h") && metrics["PriceChange24h"] != 0)
                {
                    decimal change = metrics["PriceChange24h"];
                    string formatted = change >= 0 ? $"+{change:N2}%" : $"{change:N2}%";
                    string emoji = change >= 0 ? "🟢" : "🔴";
                    historicalChanges.AppendLine($"**24h:** {emoji} {formatted}");
                    hasHistorical = true;
                }
                
                if (metrics.ContainsKey("PriceChange7d") && metrics["PriceChange7d"] != 0)
                {
                    decimal change = metrics["PriceChange7d"];
                    string formatted = change >= 0 ? $"+{change:N2}%" : $"{change:N2}%";
                    string emoji = change >= 0 ? "🟢" : "🔴";
                    historicalChanges.AppendLine($"**7d:** {emoji} {formatted}");
                    hasHistorical = true;
                }
                
                if (hasHistorical)
                {
                    embedBuilder.AddField("Price Changes", historicalChanges.ToString(), true);
                }

                await command.RespondAsync(embed: embedBuilder.Build());
            }
            catch (Exception ex)
            {
                OnLog($"Error fetching price: {ex.Message}");
                await command.RespondAsync("❌ Failed to fetch price. Check logs.");
            }
        }

        private async Task HandleMarketCapCommand(SocketSlashCommand command)
        {
            OnLog("/marketcap invoked");
            try
            {
                var metrics = await _priceSvc.GetTokenMetricsAsync();
                var marketCap = metrics.ContainsKey("MarketCap") ? metrics["MarketCap"] : 0;
                var totalSupply = metrics.ContainsKey("TotalSupply") ? metrics["TotalSupply"] : 0;
                var price = metrics.ContainsKey("PriceUsd") ? metrics["PriceUsd"] : 0;
                var liquidity = metrics.ContainsKey("Liquidity") ? metrics["Liquidity"] : 0;
                var circSupply = metrics.ContainsKey("CirculatingSupply") ? metrics["CirculatingSupply"] : 0;

                // Get token thumbnail URL for the embed
                string tokenThumbnailUrl = GetTokenThumbnailUrl(_token0Info?.Symbol);

                var embedBuilder = new EmbedBuilder()
                    .WithTitle($"{_token0Info?.Symbol ?? "Token"} Market Statistics")
                    .WithColor(Color.Blue)
                    .WithCurrentTimestamp()
                    .WithFooter($"Data from Vitruveo Network • {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC");

                // Add the token image as thumbnail if available
                if (!string.IsNullOrEmpty(tokenThumbnailUrl))
                {
                    embedBuilder.WithThumbnailUrl(tokenThumbnailUrl);
                }

                embedBuilder
                    .AddField("Market Cap", $"${FormatLargeNumber(marketCap)}", true)
                    .AddField("Price", $"${price:N6}", true)
                    .AddField("Liquidity", $"${FormatLargeNumber(liquidity)}", true);

                // Add supply info
                if (totalSupply > 0)
                {
                    embedBuilder.AddField("Total Supply", $"{FormatLargeNumber(totalSupply)} {_token0Info?.Symbol ?? "tokens"}", true);
                }
                
                if (circSupply > 0)
                {
                    embedBuilder.AddField("Circulating Supply", $"{FormatLargeNumber(circSupply)} {_token0Info?.Symbol ?? "tokens"}", true);
                    
                    // Calculate and add percent circulating if both values are available
                    if (totalSupply > 0 && circSupply > 0)
                    {
                        decimal percentCirculating = (circSupply / totalSupply) * 100;
                        embedBuilder.AddField("% Circulating", $"{percentCirculating:N2}%", true);
                    }
                }

                // Add token address
                if (_token0Info != null)
                {
                    embedBuilder.AddField("Contract", _token0Info.Address, false);
                    
                    // Add explorer link
                    embedBuilder.AddField("Explorer", 
                        $"[View on Explorer](https://explorer.vitruveo.xyz/address/{_token0Info.Address})", false);
                }

                await command.RespondAsync(embed: embedBuilder.Build());
            }
            catch (Exception ex)
            {
                OnLog($"Error fetching market cap: {ex.Message}");
                await command.RespondAsync("❌ Failed to fetch market cap. Check logs.");
            }
        }

        private async Task HandleLiquidityCommand(SocketSlashCommand command)
        {
            OnLog("/liquidity invoked");
            try
            {
                var metrics = await _priceSvc.GetTokenMetricsAsync();
                var liquidity = metrics.ContainsKey("Liquidity") ? metrics["Liquidity"] : 0;
                var reserve0 = metrics.ContainsKey("Reserve0") ? metrics["Reserve0"] : 0;
                var reserve1 = metrics.ContainsKey("Reserve1") ? metrics["Reserve1"] : 0;
                var reserve0Usd = metrics.ContainsKey("Reserve0Usd") ? metrics["Reserve0Usd"] : 0;
                var reserve1Usd = metrics.ContainsKey("Reserve1Usd") ? metrics["Reserve1Usd"] : 0;
                var volume24h = metrics.ContainsKey("Volume24h") ? metrics["Volume24h"] : 0;

                // Get token thumbnail URL for the embed
                string tokenThumbnailUrl = GetTokenThumbnailUrl(_token0Info?.Symbol);

                var embedBuilder = new EmbedBuilder()
                    .WithTitle($"{_token0Info?.Symbol ?? "Token"}/{_token1Info?.Symbol ?? "Token"} Liquidity")
                    .WithColor(Color.Gold)
                    .WithCurrentTimestamp()
                    .AddField("Total Liquidity", $"${FormatLargeNumber(liquidity)}", true);
                
                // Add Volume if available
                if (volume24h > 0)
                {
                    embedBuilder.AddField("24h Volume", $"${FormatLargeNumber(volume24h)}", true);
                    
                    // Add Volume/Liquidity ratio if both are available
                    if (liquidity > 0)
                    {
                        decimal volLiqRatio = (volume24h / liquidity) * 100;
                        string emoji = volLiqRatio > 20 ? "🔥" : "📊";
                        embedBuilder.AddField("Vol/Liq Ratio", $"{emoji} {volLiqRatio:N2}%", true);
                    }
                }
                
                embedBuilder
                    .AddField($"{_token0Info?.Symbol ?? "Token0"} Reserve", $"{FormatLargeNumber(reserve0)} (${FormatLargeNumber(reserve0Usd)})")
                    .AddField($"{_token1Info?.Symbol ?? "Token1"} Reserve", $"{FormatLargeNumber(reserve1)} (${FormatLargeNumber(reserve1Usd)})");
                
                // Add the token image as thumbnail if available
                if (!string.IsNullOrEmpty(tokenThumbnailUrl))
                {
                    embedBuilder.WithThumbnailUrl(tokenThumbnailUrl);
                }

                // Add pair address and explorer link
                if (!string.IsNullOrEmpty(_token0Info?.Address) && !string.IsNullOrEmpty(_token1Info?.Address))
                {
                    var (pairAddress, _, _) = await _priceSvc.GetPairDetailsAsync();
                    embedBuilder.AddField("Pair Address", pairAddress);
                    embedBuilder.AddField("Explorer", 
                        $"[View Pair on Explorer](https://explorer.vitruveo.xyz/address/{pairAddress})", false);
                }

                await command.RespondAsync(embed: embedBuilder.Build());
            }
            catch (Exception ex)
            {
                OnLog($"Error fetching liquidity: {ex.Message}");
                await command.RespondAsync("❌ Failed to fetch liquidity. Check logs.");
            }
        }

        private async Task HandleTokenInfoCommand(SocketSlashCommand command)
        {
            OnLog("/tokeninfo invoked");
            try
            {
                var metrics = await _priceSvc.GetTokenMetricsAsync();

                // Get token thumbnail URL for the embed
                string tokenThumbnailUrl = GetTokenThumbnailUrl(_token0Info?.Symbol);

                var embedBuilder = new EmbedBuilder()
                    .WithTitle($"{_token0Info?.Name ?? "Token"} Information")
                    .WithColor(Color.Purple)
                    .WithCurrentTimestamp();

                // Add the token image as thumbnail if available
                if (!string.IsNullOrEmpty(tokenThumbnailUrl))
                {
                    embedBuilder.WithThumbnailUrl(tokenThumbnailUrl);
                }

                // Basic token info
                embedBuilder
                    .AddField("Symbol", _token0Info?.Symbol ?? "Unknown", true)
                    .AddField("Name", _token0Info?.Name ?? "Unknown", true)
                    .AddField("Decimals", _token0Info?.Decimals.ToString() ?? "18", true)
                    .AddField("Contract Address", _token0Info?.Address ?? "Unknown", false);

                // Financial metrics
                if (metrics.ContainsKey("PriceUsd") && metrics["PriceUsd"] > 0)
                {
                    embedBuilder.AddField("USD Price", $"${metrics["PriceUsd"]:N6}", true);
                    
                    // Add price change if available
                    if (metrics.ContainsKey("PriceChange24h"))
                    {
                        decimal change = metrics["PriceChange24h"];
                        string formatted = change >= 0 ? $"+{change:N2}%" : $"{change:N2}%";
                        string emoji = change >= 0 ? "🟢" : "🔴";
                        embedBuilder.AddField("24h Change", $"{emoji} {formatted}", true);
                    }
                }

                if (metrics.ContainsKey("MarketCap") && metrics["MarketCap"] > 0)
                {
                    embedBuilder.AddField("Market Cap", $"${FormatLargeNumber(metrics["MarketCap"])}", true);
                }

                if (_token0Info?.TotalSupply > 0)
                {
                    embedBuilder.AddField("Total Supply", FormatLargeNumber(_token0Info.TotalSupply), true);
                }
                
                if (metrics.ContainsKey("CirculatingSupply") && metrics["CirculatingSupply"] > 0)
                {
                    embedBuilder.AddField("Circulating Supply", FormatLargeNumber(metrics["CirculatingSupply"]), true);
                }

                // Pool/LP info
                if (metrics.ContainsKey("Liquidity") && metrics["Liquidity"] > 0)
                {
                    embedBuilder.AddField("Total Liquidity", $"${FormatLargeNumber(metrics["Liquidity"])}", true);
                }
                
                if (metrics.ContainsKey("Volume24h") && metrics["Volume24h"] > 0)
                {
                    embedBuilder.AddField("24h Volume", $"${FormatLargeNumber(metrics["Volume24h"])}", true);
                }
                
                if (metrics.ContainsKey("HolderCount") && metrics["HolderCount"] > 0)
                {
                    embedBuilder.AddField("Holder Count", FormatLargeNumber(metrics["HolderCount"]), true);
                }

                // Add explorer link
                if (!string.IsNullOrEmpty(_token0Info?.Address))
                {
                    embedBuilder.AddField("Explorer", 
                        $"[View on Explorer](https://explorer.vitruveo.xyz/address/{_token0Info.Address})", false);
                }

                await command.RespondAsync(embed: embedBuilder.Build());
            }
            catch (Exception ex)
            {
                OnLog($"Error fetching token info: {ex.Message}");
                await command.RespondAsync("❌ Failed to fetch token info. Check logs.");
            }
        }
        
        private async Task HandleChartCommand(SocketSlashCommand command)
        {
            OnLog("/chart invoked");
            try
            {
                // Default to 24h timeframe if not specified
                string timeframe = "24h";
                var timeframeOption = command.Data.Options.FirstOrDefault(o => o.Name == "timeframe");
                if (timeframeOption != null && timeframeOption.Value is string tf)
                {
                    timeframe = tf.ToLower();
                }
                
                await command.DeferAsync(); // Let Discord know we're working on it
                
                var metrics = await _priceSvc.GetTokenMetricsAsync();
                
                // Determine which price change to show based on timeframe
                string timeframeKey;
                string timeframeLabel;
                string priceKey;
                
                switch (timeframe)
                {
                    case "15m":
                    case "15":
                    case "15min":
                        timeframeKey = "PriceChange15m";
                        timeframeLabel = "15 Minutes";
                        priceKey = PRICE_15M;
                        break;
                        
                    case "1h":
                    case "1":
                    case "hour":
                        timeframeKey = "PriceChange1h";
                        timeframeLabel = "1 Hour";
                        priceKey = PRICE_1H;
                        break;
                        
                    case "4h":
                    case "4":
                        timeframeKey = "PriceChange4h";
                        timeframeLabel = "4 Hours";
                        priceKey = PRICE_4H;
                        break;
                        
                    case "7d":
                    case "7":
                    case "week":
                        timeframeKey = "PriceChange7d";
                        timeframeLabel = "7 Days";
                        priceKey = PRICE_7D;
                        break;
                        
                    default: // Default to 24h
                        timeframeKey = "PriceChange24h";
                        timeframeLabel = "24 Hours";
                        priceKey = PRICE_24H;
                        break;
                }
                
                decimal currentPrice = metrics.ContainsKey("Price") ? metrics["Price"] : 0;
                decimal currentUsdPrice = metrics.ContainsKey("PriceUsd") ? metrics["PriceUsd"] : 0;
                decimal priceChange = metrics.ContainsKey(timeframeKey) ? metrics[timeframeKey] : 0;
                decimal oldPrice = _priceHistory.ContainsKey(priceKey) ? _priceHistory[priceKey] : 0;
                
                if (oldPrice == 0 && priceChange != 0 && currentPrice > 0)
                {
                    // Calculate the old price from the percentage change if we have it
                    oldPrice = currentPrice / (1 + (priceChange / 100));
                }
                
                // Get token thumbnail URL for the embed
                string tokenThumbnailUrl = GetTokenThumbnailUrl(_token0Info?.Symbol);
                
                // Set color based on price change
                Color color;
                if (priceChange > 0)
                    color = new Color(46, 204, 113); // Green for increase
                else if (priceChange < 0)
                    color = new Color(231, 76, 60); // Red for decrease
                else
                    color = Color.LightGrey; // Neutral for no change
                    
                // Create the chart embed
                var embedBuilder = new EmbedBuilder()
                    .WithTitle($"{_token0Info?.Symbol ?? "Token"} Price Chart - {timeframeLabel}")
                    .WithColor(color)
                    .WithCurrentTimestamp()
                    .WithFooter($"RadX Price Bot • {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
                
                // Add the token image as thumbnail if available
                if (!string.IsNullOrEmpty(tokenThumbnailUrl))
                {
                    embedBuilder.WithThumbnailUrl(tokenThumbnailUrl);
                }
                
                // Build the price change visualization
                var chartBuilder = new StringBuilder();
                chartBuilder.AppendLine($"**{_token0Info?.Symbol ?? "Token"} Price Change Last {timeframeLabel}**");
                chartBuilder.AppendLine();
                
                if (priceChange != 0)
                {
                    string changeEmoji = priceChange > 0 ? "🟢" : "🔴";
                    string changeDirection = priceChange > 0 ? "+" : "";
                    chartBuilder.AppendLine($"**{changeEmoji} {changeDirection}{priceChange:N2}%** in the last {timeframeLabel.ToLower()}");
                    
                    if (oldPrice > 0 && currentPrice > 0)
                    {
                        // Add starting and ending prices
                        chartBuilder.AppendLine();
                        chartBuilder.AppendLine($"**Starting Price:** {oldPrice:N6} {_token1Info?.Symbol ?? ""}");
                        chartBuilder.AppendLine($"**Current Price:** {currentPrice:N6} {_token1Info?.Symbol ?? ""}");
                        
                        // Add USD prices if available
                        if (currentUsdPrice > 0)
                        {
                            decimal oldUsdPrice = currentUsdPrice / (1 + (priceChange / 100));
                            chartBuilder.AppendLine();
                            chartBuilder.AppendLine($"**USD Starting:** ${oldUsdPrice:N6}");
                            chartBuilder.AppendLine($"**USD Current:** ${currentUsdPrice:N6}");
                        }
                        
                        // Visual price chart using emojis - this is just a simple representation
                        chartBuilder.AppendLine();
                        chartBuilder.AppendLine("**Chart:**");
                        int chartLength = 8; // Number of bars in chart
                        
                        if (priceChange > 0)
                        {
                            // Upward trend visualization
                            for (int i = 0; i < chartLength; i++)
                            {
                                double position = (double)i / (chartLength - 1);
                                chartBuilder.Append(position < 0.3 ? "📊" : (position < 0.7 ? "📈" : "📉"));
                            }
                        }
                        else
                        {
                            // Downward trend visualization 
                            for (int i = 0; i < chartLength; i++)
                            {
                                double position = (double)i / (chartLength - 1);
                                chartBuilder.Append(position < 0.3 ? "📊" : (position < 0.7 ? "📉" : "📈"));
                            }
                        }
                    }
                }
                else
                {
                    // No price change or data not available
                    chartBuilder.AppendLine("No price change data available for this timeframe yet.");
                    chartBuilder.AppendLine("Please check back later as price data is being collected.");
                }
                
                embedBuilder.WithDescription(chartBuilder.ToString());
                
                // Add related metrics
                if (metrics.ContainsKey("Volume24h") && metrics["Volume24h"] > 0)
                {
                    embedBuilder.AddField("24h Volume", $"${FormatLargeNumber(metrics["Volume24h"])}", true);
                }
                
                if (metrics.ContainsKey("MarketCap") && metrics["MarketCap"] > 0)
                {
                    embedBuilder.AddField("Market Cap", $"${FormatLargeNumber(metrics["MarketCap"])}", true);
                }
                
                if (metrics.ContainsKey("Liquidity") && metrics["Liquidity"] > 0)
                {
                    embedBuilder.AddField("Liquidity", $"${FormatLargeNumber(metrics["Liquidity"])}", true);
                }
                
                // Send response
                await command.FollowupAsync(embed: embedBuilder.Build());
            }
            catch (Exception ex)
            {
                OnLog($"Error generating chart: {ex.Message}");
                await command.FollowupAsync("❌ Failed to generate price chart. Check logs.");
            }
        }
    }
}
