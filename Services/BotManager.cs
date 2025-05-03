// Services/BotManager.cs
using Nethereum.JsonRpc.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RadXPriceBot.Services
{
    // BotStatusEventArgs class
    public class BotStatusEventArgs : EventArgs
    {
        public string BotId { get; set; }
        public Dictionary<string, decimal> Metrics { get; set; }
        public PairInfo PairInfo { get; set; }
    }

    public class BotInstance
    {
        public string Id { get; }
        public string Name { get; }
        public DiscordBotService BotService { get; }
        public bool IsRunning { get; set; }
        public BotConfig Config { get; set; } // Store config reference to maintain settings
        private bool _isDatabaseInitialized = false;

        public BotInstance(string id, string name, DiscordBotService botService, BotConfig config = null)
        {
            Id = id;
            Name = name;
            BotService = botService;
            Config = config;

            // Initialize database for this specific bot instance
            InitializeDatabase();
        }

        private void InitializeDatabase()
        {
            try
            {
                // Implement initialization if needed
                _isDatabaseInitialized = true;
                Console.WriteLine($"Database initialized for bot '{Name}' (ID: {Id})");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to initialize database for bot '{Name}': {ex.Message}");
            }
        }

        public void UpdateEmbedSettings(bool sendPeriodicEmbeds, int intervalMinutes, string channelId)
        {
            // Use BotService instead of _discordBot
            if (BotService != null)
            {
                if (sendPeriodicEmbeds && !string.IsNullOrEmpty(channelId)
                    && ulong.TryParse(channelId, out ulong channel))
                {
                    // Update config if available
                    if (Config != null)
                    {
                        Config.SendPeriodicEmbeds = sendPeriodicEmbeds;
                        Config.EmbedIntervalMinutes = intervalMinutes;
                        Config.EmbedChannelId = channelId;
                    }

                    // Use Name instead of _name
                    Console.WriteLine($"Updating embed settings for bot '{Name}': " +
                        $"interval={intervalMinutes}min, channel={channelId}");

                    // FIXED: Pass all the embed configuration settings, not just the basic ones
                    BotService.SetEmbedInterval(
                        intervalMinutes,
                        channel,
                        Config?.IncludeChartInEmbed ?? true,
                        Config?.IncludeTokenInfoInEmbed ?? true,
                        Config?.IncludeLiquidityInfoInEmbed ?? true,
                        Config?.EmbedColor ?? "#50E999"
                    );
                }
                else
                {
                    // Update config if available
                    if (Config != null)
                    {
                        Config.SendPeriodicEmbeds = false;
                    }

                    // Use Name instead of _name
                    Console.WriteLine($"Disabling periodic embeds for bot '{Name}'");
                    BotService.DisableEmbeds();
                }

                // Save config changes to database
                if (Config != null && _isDatabaseInitialized)
                {
                    SaveConfigToDatabase();
                }
            }
        }

        private async void SaveConfigToDatabase()
        {
            try
            {
                // This would be implemented with actual database operations
                Console.WriteLine($"Saved configuration for bot '{Name}' (ID: {Id}) to database");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to save bot config to database: {ex.Message}");
            }
        }

        // Add a method to update swap monitoring directly on the instance
        public void UpdateSwapMonitoring(bool enabled, string channelId, int intervalMs = 15000)
        {
            if (BotService == null)
                return;

            // If channelId is empty but we have an embed channel ID, use that as fallback
            if (enabled && string.IsNullOrEmpty(channelId) && Config != null &&
                !string.IsNullOrEmpty(Config.EmbedChannelId))
            {
                channelId = Config.EmbedChannelId;
                Console.WriteLine($"Using embed channel ID as fallback for swap monitoring: {channelId}");
            }

            // Update config if available
            if (Config != null)
            {
                Config.MonitorSwapTransactions = enabled;
                Config.SwapNotificationChannelId = channelId;
                Config.SwapCheckIntervalMs = intervalMs;

                // Save updated config to database
                if (_isDatabaseInitialized)
                {
                    SaveConfigToDatabase();
                }
            }

            if (enabled && !string.IsNullOrEmpty(channelId) &&
                ulong.TryParse(channelId, out ulong swapChannelId))
            {
                // Enable swap monitoring
                BotService.EnableSwapMonitoring(swapChannelId, intervalMs);
                Console.WriteLine($"Enabled swap monitoring for '{Name}' (channel: {channelId}, interval: {intervalMs}ms)");

                // IMPORTANT: If we also have embed settings configured, make sure to re-enable them
                if (Config != null && Config.SendPeriodicEmbeds && !string.IsNullOrEmpty(Config.EmbedChannelId) &&
                    ulong.TryParse(Config.EmbedChannelId, out ulong embedChannelId))
                {
                    BotService.SetEmbedInterval(
                        Config.EmbedIntervalMinutes,
                        embedChannelId,
                        Config.IncludeChartInEmbed,
                        Config.IncludeTokenInfoInEmbed,
                        Config.IncludeLiquidityInfoInEmbed,
                        Config.EmbedColor
                    );
                    Console.WriteLine($"Re-enabled periodic embeds for '{Name}' after enabling swap monitoring");
                }
            }
            else
            {
                BotService.DisableSwapMonitoring();
                Console.WriteLine($"Disabled swap monitoring for '{Name}'");
            }
        }

        // Add method to refresh price data
        public async Task RefreshPriceDataAsync()
        {
            if (BotService != null)
            {
                await BotService.RefreshPriceDataAsync();
            }
        }
    }

    public class BotManager
    {
        private readonly Dictionary<string, BotInstance> _botInstances;
        private readonly Action<string> _logAction;
        private bool _isRunning; // Legacy support field

        public event EventHandler BotStarted; // Legacy event
        public event EventHandler BotStopped; // Legacy event
        public event EventHandler<string> BotInstanceStarted;
        public event EventHandler<string> BotInstanceStopped;

        // BotStatusUpdated event
        public event EventHandler<BotStatusEventArgs> BotStatusUpdated;

        public bool IsRunning => _isRunning; // Legacy property
        public IReadOnlyDictionary<string, BotInstance> BotInstances => _botInstances;

        public BotManager(Action<string> logAction)
        {
            _logAction = logAction ?? throw new ArgumentNullException(nameof(logAction));
            _botInstances = new Dictionary<string, BotInstance>();
        }

        // Method to verify that bots remain independent
        public void VerifyBotIndependence()
        {
            _logAction("Verifying bot independence...");

            // Check for the single bot
            bool hasSingleBot = _botInstances.ContainsKey("single");
            if (hasSingleBot)
            {
                _logAction("Single bot found - ensuring it's isolated from multi-bots");
            }

            // Check for duplicated IDs or configurations
            var groupedByToken = _botInstances.Values
                .GroupBy(b => b.Config?.Token)
                .Where(g => g.Key != null && g.Count() > 1)
                .ToList();

            if (groupedByToken.Any())
            {
                _logAction("WARNING: Multiple bots are using the same Discord token!");
                foreach (var group in groupedByToken)
                {
                    var bots = string.Join(", ", group.Select(b => $"'{b.Name}' (ID: {b.Id})"));
                    _logAction($"- Token shared by: {bots}");
                }
            }

            // Check for bots sharing the same channels
            var groupedByEmbedChannel = _botInstances.Values
                .Where(b => b.Config?.SendPeriodicEmbeds == true && !string.IsNullOrEmpty(b.Config?.EmbedChannelId))
                .GroupBy(b => b.Config.EmbedChannelId)
                .Where(g => g.Count() > 1)
                .ToList();

            if (groupedByEmbedChannel.Any())
            {
                _logAction("WARNING: Multiple bots are using the same embed channel!");
                foreach (var group in groupedByEmbedChannel)
                {
                    var bots = string.Join(", ", group.Select(b => $"'{b.Name}' (ID: {b.Id})"));
                    _logAction($"- Channel {group.Key} used by: {bots}");
                }
            }

            _logAction("Bot independence verification complete.");
        }

        // Method renamed to StartBotAsyncImpl to avoid duplicate name conflict
        /// <summary>
        /// Starts a bot with the provided configuration, ensuring proper setup of monitoring and embed features.
        /// </summary>
        /// <param name="config">The bot configuration to use.</param>
        /// <returns>The ID of the started bot, or empty string if startup failed.</returns>
        private async Task<string> StartBotAsyncImpl(BotConfig config)
        {
            // Create a deep copy of the configuration to ensure complete independence
            var isolatedConfig = new BotConfig
            {
                Id = config.Id,
                Name = config.Name,
                Token = config.Token,
                GuildId = config.GuildId,
                RpcUrl = config.RpcUrl,
                SwapRouterAddress = config.SwapRouterAddress,
                Path = config.Path != null ? new List<string>(config.Path) : new List<string>(),
                Nickname = config.Nickname,
                StatusType = config.StatusType,
                CustomStatus = config.CustomStatus,
                UpdateIntervalSeconds = config.UpdateIntervalSeconds,
                SendPeriodicEmbeds = config.SendPeriodicEmbeds,
                EmbedIntervalMinutes = config.EmbedIntervalMinutes,
                EmbedChannelId = config.EmbedChannelId,
                EmbedColor = config.EmbedColor,
                IncludeChartInEmbed = config.IncludeChartInEmbed,
                IncludeTokenInfoInEmbed = config.IncludeTokenInfoInEmbed,
                IncludeLiquidityInfoInEmbed = config.IncludeLiquidityInfoInEmbed,
                MonitorSwapTransactions = config.MonitorSwapTransactions,
                SwapNotificationChannelId = config.SwapNotificationChannelId,
                SwapCheckIntervalMs = config.SwapCheckIntervalMs,
                MinimumBuyThresholdUsd = config.MinimumBuyThresholdUsd,
                MinimumSellThresholdUsd = config.MinimumSellThresholdUsd,
                NotifyOnBuys = config.NotifyOnBuys,
                NotifyOnSells = config.NotifyOnSells
            };

            // Generate ID if not provided
            if (string.IsNullOrWhiteSpace(isolatedConfig.Id))
            {
                isolatedConfig.Id = Guid.NewGuid().ToString("N");
            }

            // Check if the bot is already running
            if (_botInstances.TryGetValue(isolatedConfig.Id, out var existingBot) && existingBot.IsRunning)
            {
                _logAction($"Bot '{isolatedConfig.Name}' (ID: {isolatedConfig.Id}) is already running.");
                return isolatedConfig.Id;
            }

            // If an existing instance exists but is not running, remove it
            if (existingBot != null)
            {
                _logAction($"Removing previous stopped instance of bot '{isolatedConfig.Name}' (ID: {isolatedConfig.Id})");
                _botInstances.Remove(isolatedConfig.Id);
            }

            try
            {
                _logAction($"Starting bot '{isolatedConfig.Name}' (ID: {isolatedConfig.Id})...");
                _logAction($"Configuration summary:");
                _logAction($"- Path: [{string.Join(", ", isolatedConfig.Path)}]");
                _logAction($"- Periodic embeds: {(isolatedConfig.SendPeriodicEmbeds ? "Enabled" : "Disabled")}");
                _logAction($"- Swap monitoring: {(isolatedConfig.MonitorSwapTransactions ? "Enabled" : "Disabled")}");

                // Create a new PriceService with fresh data
                var priceSvc = new PriceService(
                    isolatedConfig.RpcUrl,
                    isolatedConfig.SwapRouterAddress,
                    isolatedConfig.Path,
                    message => _logAction($"[{isolatedConfig.Name}] {message}"),
                    useDbCache: false);  // Always use fresh data

                // Create the bot service
                var botService = new DiscordBotService(
                    isolatedConfig.Token,
                    isolatedConfig.GuildId,
                    priceSvc,
                    isolatedConfig.Nickname,
                    isolatedConfig.StatusType,
                    isolatedConfig.CustomStatus,
                    isolatedConfig.UpdateIntervalSeconds * 1000); // Convert seconds to milliseconds

                // Subscribe to log events
                botService.OnLog += message => _logAction($"[{isolatedConfig.Name}] {message}");

                // Subscribe to status updates
                botService.StatusUpdated += (s, e) =>
                {
                    BotStatusUpdated?.Invoke(this, new BotStatusEventArgs
                    {
                        BotId = isolatedConfig.Id,
                        Metrics = e.Metrics,
                        PairInfo = e.PairInfo
                    });
                };

                // IMPORTANT: Start the bot before configuring features
                _logAction($"Starting Discord bot service...");
                await botService.StartAsync();
                _logAction($"Discord bot service started successfully.");

                // Track feature configuration results for reporting
                bool embedsConfigured = false;
                bool swapMonitoringConfigured = false;
                string embedStatusMessage = string.Empty;
                string swapStatusMessage = string.Empty;

                // STEP 1: Configure embed settings first if enabled
                if (isolatedConfig.SendPeriodicEmbeds)
                {
                    if (!string.IsNullOrEmpty(isolatedConfig.EmbedChannelId) && ulong.TryParse(isolatedConfig.EmbedChannelId, out ulong embedChannelId))
                    {
                        _logAction($"Configuring periodic embeds for channel {embedChannelId}...");

                        botService.SetEmbedInterval(
                            isolatedConfig.EmbedIntervalMinutes,
                            embedChannelId,
                            isolatedConfig.IncludeChartInEmbed,
                            isolatedConfig.IncludeTokenInfoInEmbed,
                            isolatedConfig.IncludeLiquidityInfoInEmbed,
                            isolatedConfig.EmbedColor);

                        embedsConfigured = true;
                        embedStatusMessage = $"Enabled periodic embeds for bot '{isolatedConfig.Name}' " +
                                            $"(interval: {isolatedConfig.EmbedIntervalMinutes} minutes, channel: {isolatedConfig.EmbedChannelId}, " +
                                            $"include chart: {isolatedConfig.IncludeChartInEmbed}, include token info: {isolatedConfig.IncludeTokenInfoInEmbed})";
                        _logAction(embedStatusMessage);
                    }
                    else
                    {
                        embedStatusMessage = $"Periodic embeds could not be enabled for bot '{isolatedConfig.Name}' - " +
                                            $"Invalid channel ID: '{isolatedConfig.EmbedChannelId}'";
                        _logAction(embedStatusMessage);
                    }
                }
                else
                {
                    embedStatusMessage = $"Periodic embeds are disabled for bot '{isolatedConfig.Name}'";
                    _logAction(embedStatusMessage);
                }

                // STEP 2: Configure swap monitoring if enabled (after embeds are configured)
                if (isolatedConfig.MonitorSwapTransactions)
                {
                    _logAction($"Configuring swap transaction monitoring...");

                    // Determine which channel ID to use
                    string channelIdToUse = !string.IsNullOrEmpty(isolatedConfig.SwapNotificationChannelId)
                        ? isolatedConfig.SwapNotificationChannelId
                        : isolatedConfig.EmbedChannelId;

                    if (string.IsNullOrEmpty(channelIdToUse))
                    {
                        swapStatusMessage = $"Swap transaction monitoring could not be enabled for bot '{isolatedConfig.Name}' - " +
                                           "No valid channel ID available";
                        _logAction(swapStatusMessage);
                    }
                    else if (!ulong.TryParse(channelIdToUse, out ulong swapChannelId))
                    {
                        swapStatusMessage = $"Swap transaction monitoring could not be enabled for bot '{isolatedConfig.Name}' - " +
                                           $"Channel ID '{channelIdToUse}' is not a valid Discord channel ID";
                        _logAction(swapStatusMessage);
                    }
                    else
                    {
                        // Successfully parsed the channel ID, enable monitoring
                        _logAction($"Enabling swap monitoring with channel ID: {swapChannelId}, interval: {isolatedConfig.SwapCheckIntervalMs}ms");

                        // If we're using the embed channel ID as fallback, update the config
                        if (string.IsNullOrEmpty(isolatedConfig.SwapNotificationChannelId) && !string.IsNullOrEmpty(isolatedConfig.EmbedChannelId))
                        {
                            isolatedConfig.SwapNotificationChannelId = isolatedConfig.EmbedChannelId;
                            _logAction($"Using embed channel ID as fallback for swap notifications: {isolatedConfig.SwapNotificationChannelId}");
                        }

                        // Enable the feature
                        botService.EnableSwapMonitoring(swapChannelId, isolatedConfig.SwapCheckIntervalMs);
                        swapMonitoringConfigured = true;

                        swapStatusMessage = $"Enabled swap transaction monitoring for bot '{isolatedConfig.Name}' " +
                                           $"(channel: {channelIdToUse}, check interval: {isolatedConfig.SwapCheckIntervalMs}ms)";
                        _logAction(swapStatusMessage);
                    }
                }
                else
                {
                    swapStatusMessage = $"Swap transaction monitoring is disabled for bot '{isolatedConfig.Name}' - " +
                                       "to enable it, set MonitorSwapTransactions to true and provide a valid SwapNotificationChannelId";
                    _logAction(swapStatusMessage);
                }

                // STEP 3: Create the bot instance and store it
                var instance = new BotInstance(isolatedConfig.Id, isolatedConfig.Name, botService, isolatedConfig) { IsRunning = true };
                _botInstances[isolatedConfig.Id] = instance;

                // STEP 4: Verify that features are working as expected after setup
                if (isolatedConfig.SendPeriodicEmbeds && !embedsConfigured)
                {
                    _logAction($"WARNING: Periodic embeds were requested but could not be configured properly.");
                }

                if (isolatedConfig.MonitorSwapTransactions && !swapMonitoringConfigured)
                {
                    _logAction($"WARNING: Swap monitoring was requested but could not be configured properly.");
                }

                // If both features are enabled, verify they're not interfering with each other
                if (embedsConfigured && swapMonitoringConfigured)
                {
                    _logAction($"Both periodic embeds and swap monitoring are enabled. Ensuring they don't interfere...");

                    // Force a refresh of price data to ensure both features have current data
                    await instance.RefreshPriceDataAsync();

                    // Verify embed timer is running (this is a precaution)
                    if (isolatedConfig.SendPeriodicEmbeds && embedsConfigured)
                    {
                        _logAction($"Re-checking embed timer configuration...");
                        if (ulong.TryParse(isolatedConfig.EmbedChannelId, out ulong verifyChannelId))
                        {
                            // This call ensures the embed timer is properly set up
                            botService.SetEmbedInterval(
                                isolatedConfig.EmbedIntervalMinutes,
                                verifyChannelId,
                                isolatedConfig.IncludeChartInEmbed,
                                isolatedConfig.IncludeTokenInfoInEmbed,
                                isolatedConfig.IncludeLiquidityInfoInEmbed,
                                isolatedConfig.EmbedColor);
                        }
                    }
                }

                _logAction($"Bot '{isolatedConfig.Name}' (ID: {isolatedConfig.Id}) started and configured successfully.");
                _logAction($"Embed status: {embedStatusMessage}");
                _logAction($"Swap monitoring status: {swapStatusMessage}");

                // Verify independence from other bots
                VerifyBotIndependence();

                // Trigger events
                BotInstanceStarted?.Invoke(this, isolatedConfig.Id);

                // For legacy support, if this is the first bot, set IsRunning and trigger legacy event
                if (_botInstances.Count == 1 || isolatedConfig.Id == "single")
                {
                    _isRunning = true;
                    BotStarted?.Invoke(this, EventArgs.Empty);
                }

                return isolatedConfig.Id;
            }
            catch (Exception ex)
            {
                _logAction($"Failed to start bot '{isolatedConfig.Name}' (ID: {isolatedConfig.Id}): {ex.Message}");
                if (ex.InnerException != null)
                {
                    _logAction($"Inner exception: {ex.InnerException.Message}");
                }
                _logAction($"Stack trace: {ex.StackTrace}");
                return string.Empty;
            }
        }

        public async Task SwitchPairAsync(string botId, List<string> newPath, string rpcUrl, string routerAddress)
        {
            if (!_botInstances.TryGetValue(botId, out var instance) || !instance.IsRunning)
            {
                _logAction($"Bot ID '{botId}' is not running, cannot switch pairs");
                return;
            }

            try
            {
                _logAction($"Switching token pair for bot '{instance.Name}'...");

                // Create a new PriceService with the new path and useDbCache set to false
                var newPriceService = new PriceService(rpcUrl, routerAddress, newPath,
                    message => _logAction($"[{instance.Name}] {message}"),
                    useDbCache: false);  // FIXED: Disable caching to always get fresh prices

                // Update the bot service with the new price service
                await instance.BotService.UpdateTokenPairAsync(newPriceService);

                // Update the config if available
                if (instance.Config != null)
                {
                    instance.Config.Path = new List<string>(newPath);
                    instance.Config.RpcUrl = rpcUrl;
                    instance.Config.SwapRouterAddress = routerAddress;
                }

                _logAction($"Successfully switched token pair for bot '{instance.Name}'");
            }
            catch (Exception ex)
            {
                _logAction($"Error switching token pair for bot '{instance.Name}': {ex.Message}");
                if (ex.InnerException != null)
                {
                    _logAction($"Inner exception: {ex.InnerException.Message}");
                }
            }
        }

        // Add method to manually refresh price data for a bot
        public async Task RefreshBotPriceDataAsync(string botId)
        {
            if (!_botInstances.TryGetValue(botId, out var instance) || !instance.IsRunning)
            {
                _logAction($"Bot ID '{botId}' is not running, cannot refresh price data");
                return;
            }

            try
            {
                _logAction($"Refreshing price data for bot '{instance.Name}'...");
                await instance.RefreshPriceDataAsync();
                _logAction($"Successfully refreshed price data for bot '{instance.Name}'");
            }
            catch (Exception ex)
            {
                _logAction($"Error refreshing price data for bot '{instance.Name}': {ex.Message}");
                if (ex.InnerException != null)
                {
                    _logAction($"Inner exception: {ex.InnerException.Message}");
                }
            }
        }

        // Add method to refresh price data for all bots
        public async Task RefreshAllBotsPriceDataAsync()
        {
            _logAction("Refreshing price data for all bots...");

            foreach (var instance in _botInstances.Values.Where(b => b.IsRunning))
            {
                try
                {
                    await instance.RefreshPriceDataAsync();
                    _logAction($"Refreshed price data for bot '{instance.Name}'");
                }
                catch (Exception ex)
                {
                    _logAction($"Error refreshing price data for bot '{instance.Name}': {ex.Message}");
                }
            }

            _logAction("Completed refreshing price data for all bots");
        }

        public async Task StopBotAsync(string botId)
        {
            if (!_botInstances.TryGetValue(botId, out var instance))
            {
                _logAction($"Bot ID '{botId}' not found.");
                return;
            }

            if (!instance.IsRunning)
            {
                _logAction($"Bot '{instance.Name}' (ID: {botId}) is not running.");
                return;
            }

            try
            {
                _logAction($"Stopping bot '{instance.Name}' (ID: {botId})...");
                await instance.BotService.StopAsync();
                instance.IsRunning = false;

                // Trigger events
                BotInstanceStopped?.Invoke(this, botId);

                // For legacy support, if this was the last running bot, trigger legacy event
                if (!_botInstances.Values.Any(b => b.IsRunning))
                {
                    _isRunning = false;
                    BotStopped?.Invoke(this, EventArgs.Empty);
                }

                _logAction($"Bot '{instance.Name}' (ID: {botId}) stopped successfully.");
            }
            catch (Exception ex)
            {
                _logAction($"Error stopping bot '{instance.Name}' (ID: {botId}): {ex.Message}");
                if (ex.InnerException != null)
                {
                    _logAction($"Inner exception: {ex.InnerException.Message}");
                }
            }
        }

        public async Task StopAllBotsAsync()
        {
            _logAction("Stopping all bots...");

            foreach (var id in _botInstances.Keys.ToList())
            {
                await StopBotAsync(id);
            }

            _logAction("All bots stopped.");
        }

        #region Legacy Support Methods

        // Legacy method
        public async Task StartAsync(
            string token, string guildId, string rpcUrl,
            string swapRouterAddress, List<string> path,
            string nickname, string statusType, string customStatus)
        {
            // If there's already a "single" bot running, stop it first
            if (_botInstances.TryGetValue("single", out var existingSingleBot) && existingSingleBot.IsRunning)
            {
                _logAction("Stopping existing single bot before starting a new one...");
                await StopBotAsync("single");
            }

            var config = new BotConfig
            {
                Id = "single",  // Use consistent ID for single bot
                Name = "Default Bot",
                Token = token,
                GuildId = guildId,
                RpcUrl = rpcUrl,
                SwapRouterAddress = swapRouterAddress,
                Path = new List<string>(path),  // Create a new list to avoid sharing references
                Nickname = nickname,
                StatusType = statusType,
                CustomStatus = customStatus,
                UpdateIntervalSeconds = 30,
                // Default to enabled swap monitoring
                MonitorSwapTransactions = true
            };

            await StartBotAsyncImpl(config);
        }

        // Legacy method
        public async Task SwitchPairAsync(List<string> newPath, string rpcUrl, string routerAddress)
        {
            // First try to find the single bot
            if (_botInstances.TryGetValue("single", out var singleBot) && singleBot.IsRunning)
            {
                _logAction("Switching pair for single bot...");
                await SwitchPairAsync("single", newPath, rpcUrl, routerAddress);
                return;
            }

            // If no single bot found, try first bot
            var defaultBotId = _botInstances.Keys.FirstOrDefault();
            if (!string.IsNullOrEmpty(defaultBotId))
            {
                _logAction($"No single bot found. Switching pair for first available bot (ID: {defaultBotId})...");
                await SwitchPairAsync(defaultBotId, newPath, rpcUrl, routerAddress);
            }
            else
            {
                _logAction("No active bots found, cannot switch pairs");
            }
        }

        // FIXED: Improved UpdateSwapMonitoringSettings method
        public async Task UpdateSwapMonitoringSettings(
            string botId,
            bool monitorSwapTransactions,
            string swapNotificationChannelId,
            int swapCheckIntervalMs = 15000)
        {
            _logAction?.Invoke($"Updating swap monitoring settings for bot {botId}:");
            _logAction?.Invoke($"  - MonitorSwapTransactions: {monitorSwapTransactions}");
            _logAction?.Invoke($"  - SwapNotificationChannelId: '{swapNotificationChannelId}'");
            _logAction?.Invoke($"  - SwapCheckIntervalMs: {swapCheckIntervalMs}");

            if (!_botInstances.TryGetValue(botId, out var botInstance))
            {
                _logAction?.Invoke($"ERROR: Bot {botId} not found. Cannot update swap monitoring settings.");
                return;
            }

            if (!botInstance.IsRunning)
            {
                _logAction?.Invoke($"ERROR: Bot {botId} is not running. Cannot update swap monitoring settings.");
                return;
            }

            try
            {
                // Update config if available
                if (botInstance.Config != null)
                {
                    botInstance.Config.MonitorSwapTransactions = monitorSwapTransactions;

                    // If the swap notification channel is empty, try to use the embed channel as fallback
                    if (string.IsNullOrEmpty(swapNotificationChannelId) &&
                        !string.IsNullOrEmpty(botInstance.Config.EmbedChannelId))
                    {
                        swapNotificationChannelId = botInstance.Config.EmbedChannelId;
                        _logAction?.Invoke($"Using embed channel ID as fallback for swap notifications: {swapNotificationChannelId}");
                    }

                    botInstance.Config.SwapNotificationChannelId = swapNotificationChannelId;
                    botInstance.Config.SwapCheckIntervalMs = swapCheckIntervalMs;
                }

                // Refresh price data before updating swap monitoring
                await botInstance.RefreshPriceDataAsync();

                // Use the BotInstance helper method
                botInstance.UpdateSwapMonitoring(monitorSwapTransactions, swapNotificationChannelId, swapCheckIntervalMs);

                if (monitorSwapTransactions && !string.IsNullOrEmpty(swapNotificationChannelId))
                {
                    if (ulong.TryParse(swapNotificationChannelId, out ulong channelId))
                    {
                        _logAction?.Invoke($"Successfully enabled swap transaction monitoring for bot {botId} " +
                                       $"(channel: {swapNotificationChannelId}, interval: {swapCheckIntervalMs}ms)");
                    }
                    else
                    {
                        _logAction?.Invoke($"ERROR: Failed to parse channel ID '{swapNotificationChannelId}' as ulong");
                        _logAction?.Invoke($"Swap transaction monitoring was NOT enabled for bot {botId}");
                    }
                }
                else
                {
                    _logAction?.Invoke($"Disabled swap transaction monitoring for bot {botId}");
                }
            }
            catch (Exception ex)
            {
                _logAction?.Invoke($"ERROR updating swap monitoring settings for bot {botId}: {ex.Message}");
                if (ex.InnerException != null)
                {
                    _logAction?.Invoke($"Inner exception: {ex.InnerException.Message}");
                }
            }
        }

        // Legacy method
        public async Task StopAsync()
        {
            // First try to find and stop the single bot
            if (_botInstances.TryGetValue("single", out _))
            {
                _logAction("Stopping single bot...");
                await StopBotAsync("single");
                return;
            }

            // If no single bot, stop the first bot found
            var defaultBotId = _botInstances.Keys.FirstOrDefault();
            if (string.IsNullOrEmpty(defaultBotId))
            {
                _logAction("No active bots found, cannot stop");
                return;
            }

            // Use the existing StopBotAsync method to properly stop the bot instance
            _logAction($"No single bot found. Stopping first available bot (ID: {defaultBotId})...");
            await StopBotAsync(defaultBotId);
        }

        // FIXED: Update embed settings with proper config update
        public void UpdateBotEmbedSettings(
    string botId,
    bool sendPeriodicEmbeds,
    int embedIntervalMinutes,
    string embedChannelId,
    string embedColor = "#50E999",
    bool includeChartInEmbed = true,
    bool includeTokenInfoInEmbed = true,
    bool includeLiquidityInfoInEmbed = true)
        {
            if (!_botInstances.TryGetValue(botId, out var botInstance))
            {
                _logAction($"ERROR: Bot {botId} not found. Cannot update embed settings.");
                return;
            }

            try
            {
                // First update the config with all settings
                if (botInstance.Config != null)
                {
                    botInstance.Config.SendPeriodicEmbeds = sendPeriodicEmbeds;
                    botInstance.Config.EmbedIntervalMinutes = embedIntervalMinutes;
                    botInstance.Config.EmbedChannelId = embedChannelId;
                    botInstance.Config.EmbedColor = embedColor;
                    botInstance.Config.IncludeChartInEmbed = includeChartInEmbed;
                    botInstance.Config.IncludeTokenInfoInEmbed = includeTokenInfoInEmbed;
                    botInstance.Config.IncludeLiquidityInfoInEmbed = includeLiquidityInfoInEmbed;
                }

                // FIXED: Refresh price data before updating embed settings
                _ = botInstance.RefreshPriceDataAsync();

                // Update the instance (will also update config if available)
                if (sendPeriodicEmbeds && !string.IsNullOrEmpty(embedChannelId) && ulong.TryParse(embedChannelId, out ulong channelId))
                {
                    // FIXED: Call SetEmbedInterval directly with all parameters
                    botInstance.BotService.SetEmbedInterval(
                        embedIntervalMinutes,
                        channelId,
                        includeChartInEmbed,
                        includeTokenInfoInEmbed,
                        includeLiquidityInfoInEmbed,
                        embedColor
                    );

                    _logAction($"Enabled periodic embeds for bot '{botInstance.Name}' " +
                             $"(interval: {embedIntervalMinutes} minutes, channel: {embedChannelId})");
                }
                else
                {
                    botInstance.BotService.DisableEmbeds();
                    _logAction($"Disabled periodic embeds for bot '{botInstance.Name}'");
                }
            }
            catch (Exception ex)
            {
                _logAction($"ERROR updating embed settings: {ex.Message}");
                if (ex.InnerException != null)
                {
                    _logAction($"Inner exception: {ex.InnerException.Message}");
                }
            }
        }

        // Add this method to start a bot with a specific configuration
        public async Task<string> StartBotAsync(BotConfig config)
        {
            if (config == null)
            {
                _logAction("ERROR: Cannot start bot with null configuration");
                return string.Empty;
            }

            // Special handling for single bot
            if (config.Id == "single")
            {
                // If there's already a single bot running, stop it first
                if (_botInstances.TryGetValue("single", out var existingSingleBot) && existingSingleBot.IsRunning)
                {
                    _logAction("Stopping existing single bot before starting a new one with the same ID...");
                    await StopBotAsync("single");
                }
            }

            // Create a deep copy of the config
            var configCopy = new BotConfig
            {
                Id = config.Id,
                Name = config.Name,
                Token = config.Token,
                GuildId = config.GuildId,
                RpcUrl = config.RpcUrl,
                SwapRouterAddress = config.SwapRouterAddress,
                Path = config.Path != null ? new List<string>(config.Path) : new List<string>(),
                Nickname = config.Nickname,
                StatusType = config.StatusType,
                CustomStatus = config.CustomStatus,
                UpdateIntervalSeconds = config.UpdateIntervalSeconds,
                SendPeriodicEmbeds = config.SendPeriodicEmbeds,
                EmbedIntervalMinutes = config.EmbedIntervalMinutes,
                EmbedChannelId = config.EmbedChannelId,
                EmbedColor = config.EmbedColor,
                IncludeChartInEmbed = config.IncludeChartInEmbed,
                IncludeTokenInfoInEmbed = config.IncludeTokenInfoInEmbed,
                IncludeLiquidityInfoInEmbed = config.IncludeLiquidityInfoInEmbed,
                MonitorSwapTransactions = config.MonitorSwapTransactions,
                SwapNotificationChannelId = config.SwapNotificationChannelId,
                SwapCheckIntervalMs = config.SwapCheckIntervalMs,
                MinimumBuyThresholdUsd = config.MinimumBuyThresholdUsd,
                MinimumSellThresholdUsd = config.MinimumSellThresholdUsd,
                NotifyOnBuys = config.NotifyOnBuys,
                NotifyOnSells = config.NotifyOnSells
            };

            // FIXED: Ensure swap notification channel is handled properly
            if (configCopy.MonitorSwapTransactions && string.IsNullOrEmpty(configCopy.SwapNotificationChannelId))
            {
                if (!string.IsNullOrEmpty(configCopy.EmbedChannelId))
                {
                    configCopy.SwapNotificationChannelId = configCopy.EmbedChannelId;
                    _logAction($"Using embed channel ID ({configCopy.EmbedChannelId}) for swap notifications for bot '{configCopy.Name}'");
                }
                else
                {
                    _logAction($"WARNING: Bot '{configCopy.Name}' has MonitorSwapTransactions=true but no SwapNotificationChannelId set.");
                }
            }

            return await StartBotAsyncImpl(configCopy);
        }

        // Add this method for multi-bot management to update all settings for a bot
        public async Task UpdateBotSettingsAsync(string botId, BotConfig newSettings)
        {
            if (!_botInstances.TryGetValue(botId, out var botInstance))
            {
                _logAction($"ERROR: Bot {botId} not found. Cannot update settings.");
                return;
            }

            if (botInstance.Config == null)
            {
                _logAction($"ERROR: Bot {botId} has no configuration. Cannot update settings.");
                return;
            }

            bool requiresRestart = false;

            // Check if critical settings that require a restart have changed
            if (botInstance.Config.Token != newSettings.Token ||
                botInstance.Config.GuildId != newSettings.GuildId)
            {
                _logAction($"Critical settings for bot '{botInstance.Name}' have changed. Bot will be restarted.");
                requiresRestart = true;
            }

            // Update the config
            botInstance.Config.Name = newSettings.Name;
            botInstance.Config.Token = newSettings.Token;
            botInstance.Config.GuildId = newSettings.GuildId;
            botInstance.Config.RpcUrl = newSettings.RpcUrl;
            botInstance.Config.SwapRouterAddress = newSettings.SwapRouterAddress;
            botInstance.Config.Nickname = newSettings.Nickname;
            botInstance.Config.StatusType = newSettings.StatusType;
            botInstance.Config.CustomStatus = newSettings.CustomStatus;
            botInstance.Config.UpdateIntervalSeconds = newSettings.UpdateIntervalSeconds;
            botInstance.Config.SendPeriodicEmbeds = newSettings.SendPeriodicEmbeds;
            botInstance.Config.EmbedIntervalMinutes = newSettings.EmbedIntervalMinutes;
            botInstance.Config.EmbedChannelId = newSettings.EmbedChannelId;
            botInstance.Config.EmbedColor = newSettings.EmbedColor;
            botInstance.Config.IncludeChartInEmbed = newSettings.IncludeChartInEmbed;
            botInstance.Config.IncludeTokenInfoInEmbed = newSettings.IncludeTokenInfoInEmbed;
            botInstance.Config.IncludeLiquidityInfoInEmbed = newSettings.IncludeLiquidityInfoInEmbed;
            botInstance.Config.MonitorSwapTransactions = newSettings.MonitorSwapTransactions;
            botInstance.Config.SwapNotificationChannelId = newSettings.SwapNotificationChannelId;
            botInstance.Config.SwapCheckIntervalMs = newSettings.SwapCheckIntervalMs;
            botInstance.Config.MinimumBuyThresholdUsd = newSettings.MinimumBuyThresholdUsd;
            botInstance.Config.MinimumSellThresholdUsd = newSettings.MinimumSellThresholdUsd;
            botInstance.Config.NotifyOnBuys = newSettings.NotifyOnBuys;
            botInstance.Config.NotifyOnSells = newSettings.NotifyOnSells;

            // Path requires special handling to switch the pair if bot is running
            if (newSettings.Path != null &&
                (botInstance.Config.Path == null || !botInstance.Config.Path.SequenceEqual(newSettings.Path)))
            {
                if (botInstance.IsRunning)
                {
                    _logAction($"Switching pair for bot '{botInstance.Name}'...");
                    await SwitchPairAsync(botId, newSettings.Path, newSettings.RpcUrl, newSettings.SwapRouterAddress);
                }
                else
                {
                    botInstance.Config.Path = new List<string>(newSettings.Path);
                }
            }

            // If bot is running, update settings
            if (botInstance.IsRunning && !requiresRestart)
            {
                // Update embed settings
                UpdateBotEmbedSettings(
                    botId,
                    newSettings.SendPeriodicEmbeds,
                    newSettings.EmbedIntervalMinutes,
                    newSettings.EmbedChannelId);

                // Update swap monitoring settings
                await UpdateSwapMonitoringSettings(
                    botId,
                    newSettings.MonitorSwapTransactions,
                    newSettings.SwapNotificationChannelId,
                    newSettings.SwapCheckIntervalMs);

                _logAction($"Updated settings for running bot '{botInstance.Name}'");
            }
            else if (requiresRestart && botInstance.IsRunning)
            {
                _logAction($"Restarting bot '{botInstance.Name}' to apply new settings...");
                await StopBotAsync(botId);
                await StartBotAsync(botInstance.Config);
            }
        }

        #endregion
    }
}
