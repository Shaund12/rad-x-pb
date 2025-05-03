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

        public BotInstance(string id, string name, DiscordBotService botService, BotConfig config = null)
        {
            Id = id;
            Name = name;
            BotService = botService;
            Config = config;
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
                    BotService.SetEmbedInterval(intervalMinutes, channel);
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
            }

            if (enabled && !string.IsNullOrEmpty(channelId) &&
                ulong.TryParse(channelId, out ulong swapChannelId))
            {
                // FIXED: Use fresh price data for swap monitoring
                BotService.EnableSwapMonitoring(swapChannelId, intervalMs);
                Console.WriteLine($"Enabled swap monitoring for '{Name}' (channel: {channelId}, interval: {intervalMs}ms)");
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

        // Method renamed to StartBotAsyncImpl to avoid duplicate name conflict
        private async Task<string> StartBotAsyncImpl(BotConfig config)
        {
            if (string.IsNullOrWhiteSpace(config.Id))
            {
                config.Id = Guid.NewGuid().ToString("N");
            }

            if (_botInstances.TryGetValue(config.Id, out var existingBot) && existingBot.IsRunning)
            {
                _logAction($"Bot '{config.Name}' (ID: {config.Id}) is already running.");
                return config.Id;
            }

            try
            {
                _logAction($"Starting bot '{config.Name}' (ID: {config.Id})...");

                // Create a new PriceService for this bot instance with useDbCache set to false to always fetch fresh prices
                var priceSvc = new PriceService(config.RpcUrl, config.SwapRouterAddress, config.Path,
                    message => _logAction($"[{config.Name}] {message}"),
                    useDbCache: false);  // FIXED: Disable caching to always get fresh prices

                var botService = new DiscordBotService(
                    config.Token,
                    config.GuildId,
                    priceSvc,
                    config.Nickname,
                    config.StatusType,
                    config.CustomStatus,
                    config.UpdateIntervalSeconds * 1000); // Convert seconds to milliseconds

                // Subscribe to log events with bot name prefix
                botService.OnLog += message => _logAction($"[{config.Name}] {message}");

                // Subscribe to status updates
                botService.StatusUpdated += (s, e) =>
                {
                    BotStatusUpdated?.Invoke(this, new BotStatusEventArgs
                    {
                        BotId = config.Id,
                        Metrics = e.Metrics,
                        PairInfo = e.PairInfo
                    });
                };

                // Configure embed settings if enabled
                if (config.SendPeriodicEmbeds && !string.IsNullOrEmpty(config.EmbedChannelId)
                    && ulong.TryParse(config.EmbedChannelId, out ulong channelId))
                {
                    botService.SetEmbedInterval(config.EmbedIntervalMinutes, channelId,
                        config.IncludeChartInEmbed,
                        config.IncludeTokenInfoInEmbed,
                        config.IncludeLiquidityInfoInEmbed,
                        config.EmbedColor);

                    _logAction($"Enabled periodic embeds for bot '{config.Name}' " +
                              $"(interval: {config.EmbedIntervalMinutes} minutes, channel: {config.EmbedChannelId})");
                }

                // FIXED: Enable swap transaction monitoring with detailed logging
                // FIXED: Enable swap transaction monitoring with detailed logging
                string swapStatusMessage;
                if (config.MonitorSwapTransactions)
                {
                    _logAction($"Swap monitoring is enabled in config for bot '{config.Name}'");

                    // Use SwapNotificationChannelId if available, otherwise fallback to EmbedChannelId
                    string channelIdToUse = !string.IsNullOrEmpty(config.SwapNotificationChannelId)
                        ? config.SwapNotificationChannelId
                        : config.EmbedChannelId;

                    if (string.IsNullOrEmpty(channelIdToUse))
                    {
                        swapStatusMessage = $"Swap transaction monitoring could not be enabled for bot '{config.Name}' - No valid channel ID available";
                    }
                    else if (!ulong.TryParse(channelIdToUse, out ulong swapChannelId))
                    {
                        swapStatusMessage = $"Swap transaction monitoring could not be enabled for bot '{config.Name}' - " +
                            $"Channel ID '{channelIdToUse}' is not a valid Discord channel ID";
                    }
                    else
                    {
                        // Successfully parsed the channel ID
                        _logAction($"Enabling swap monitoring with channel ID: {swapChannelId}");
                        botService.EnableSwapMonitoring(swapChannelId, config.SwapCheckIntervalMs);

                        // Update the config to store the used channel ID if it was using the fallback
                        if (string.IsNullOrEmpty(config.SwapNotificationChannelId) && !string.IsNullOrEmpty(config.EmbedChannelId))
                        {
                            config.SwapNotificationChannelId = config.EmbedChannelId;
                            _logAction($"Using embed channel ID for swap notifications: {config.SwapNotificationChannelId}");
                        }

                        swapStatusMessage = $"Enabled swap transaction monitoring for bot '{config.Name}' " +
                            $"(channel: {channelIdToUse}, check interval: {config.SwapCheckIntervalMs}ms)";
                    }
                }
                else
                {
                    swapStatusMessage = $"Swap transaction monitoring is disabled for bot '{config.Name}' - " +
                                        "to enable it, set MonitorSwapTransactions to true and provide a valid SwapNotificationChannelId";
                }


                _logAction(swapStatusMessage);

                await botService.StartAsync();

                // Store bot instance with a reference to its config
                var instance = new BotInstance(config.Id, config.Name, botService, config) { IsRunning = true };

                // Add or update the instance in the dictionary
                _botInstances[config.Id] = instance;

                _logAction($"Bot '{config.Name}' (ID: {config.Id}) started successfully.");

                // Trigger events
                BotInstanceStarted?.Invoke(this, config.Id);

                // For legacy support, if this is the first bot, set IsRunning and trigger legacy event
                if (_botInstances.Count == 1)
                {
                    _isRunning = true;
                    BotStarted?.Invoke(this, EventArgs.Empty);
                }

                return config.Id;
            }
            catch (Exception ex)
            {
                _logAction($"Failed to start bot '{config.Name}' (ID: {config.Id}): {ex.Message}");
                if (ex.InnerException != null)
                {
                    _logAction($"Inner exception: {ex.InnerException.Message}");
                }
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
            var config = new BotConfig
            {
                Id = "single",  // Use consistent ID for single bot
                Name = "Default Bot",
                Token = token,
                GuildId = guildId,
                RpcUrl = rpcUrl,
                SwapRouterAddress = swapRouterAddress,
                Path = path,
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
            var defaultBotId = _botInstances.Keys.FirstOrDefault();
            if (!string.IsNullOrEmpty(defaultBotId))
            {
                await SwitchPairAsync(defaultBotId, newPath, rpcUrl, routerAddress);
            }
            else
            {
                _logAction("No active bots found, cannot switch pairs");
            }
        }

        // FIXED: Improved UpdateSwapMonitoringSettings method
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
            // Find the first bot instance
            var defaultBotId = _botInstances.Keys.FirstOrDefault();
            if (string.IsNullOrEmpty(defaultBotId))
            {
                _logAction("No active bots found, cannot stop");
                return;
            }

            // Use the existing StopBotAsync method to properly stop the bot instance
            await StopBotAsync(defaultBotId);
        }

        // FIXED: Update embed settings with proper config update
        public void UpdateBotEmbedSettings(
            string botId,
            bool sendPeriodicEmbeds,
            int embedIntervalMinutes,
            string embedChannelId)
        {
            if (!_botInstances.TryGetValue(botId, out var botInstance))
            {
                _logAction($"ERROR: Bot {botId} not found. Cannot update embed settings.");
                return;
            }

            try
            {
                // FIXED: Refresh price data before updating embed settings
                _ = botInstance.RefreshPriceDataAsync();

                // Update the instance (will also update config if available)
                botInstance.UpdateEmbedSettings(
                    sendPeriodicEmbeds,
                    embedIntervalMinutes,
                    embedChannelId);

                if (sendPeriodicEmbeds && !string.IsNullOrEmpty(embedChannelId)
                    && ulong.TryParse(embedChannelId, out _))
                {
                    _logAction($"Enabled periodic embeds for bot '{botInstance.Name}' " +
                             $"(interval: {embedIntervalMinutes} minutes, channel: {embedChannelId})");
                }
                else
                {
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
            // FIXED: Ensure swap notification channel is handled properly
            if (config.MonitorSwapTransactions && string.IsNullOrEmpty(config.SwapNotificationChannelId))
            {
                if (!string.IsNullOrEmpty(config.EmbedChannelId))
                {
                    config.SwapNotificationChannelId = config.EmbedChannelId;
                    _logAction($"Using embed channel ID ({config.EmbedChannelId}) for swap notifications for bot '{config.Name}'");
                }
                else
                {
                    _logAction($"WARNING: Bot '{config.Name}' has MonitorSwapTransactions=true but no SwapNotificationChannelId set.");
                }
            }

            return await StartBotAsyncImpl(config);
        }

        #endregion
    }
}
