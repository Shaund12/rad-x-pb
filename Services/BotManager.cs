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

        public BotInstance(string id, string name, DiscordBotService botService)
        {
            Id = id;
            Name = name;
            BotService = botService;
        }

        public void UpdateEmbedSettings(bool sendPeriodicEmbeds, int intervalMinutes, string channelId)
        {
            // Use BotService instead of _discordBot
            if (BotService != null)
            {
                if (sendPeriodicEmbeds && !string.IsNullOrEmpty(channelId)
                    && ulong.TryParse(channelId, out ulong channel))
                {
                    // Use Name instead of _name
                    Console.WriteLine($"Updating embed settings for bot '{Name}': " +
                        $"interval={intervalMinutes}min, channel={channelId}");
                    BotService.SetEmbedInterval(intervalMinutes, channel);
                }
                else
                {
                    // Use Name instead of _name
                    Console.WriteLine($"Disabling periodic embeds for bot '{Name}'");
                    BotService.DisableEmbeds();
                }
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

                // Create a new PriceService for this bot instance
                var priceSvc = new PriceService(config.RpcUrl, config.SwapRouterAddress, config.Path,
                    message => _logAction($"[{config.Name}] {message}"));

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

                // ADDED: Enable swap transaction monitoring if configured
                if (config.MonitorSwapTransactions && !string.IsNullOrEmpty(config.SwapNotificationChannelId)
                    && ulong.TryParse(config.SwapNotificationChannelId, out ulong swapChannelId))
                {
                    botService.EnableSwapMonitoring(swapChannelId, config.SwapCheckIntervalMs);
                    _logAction($"Enabled swap transaction monitoring for bot '{config.Name}' " +
                              $"(channel: {config.SwapNotificationChannelId}, check interval: {config.SwapCheckIntervalMs}ms)");
                }
                else
                {
                    _logAction($"Swap transaction monitoring is disabled for bot '{config.Name}' - " +
                              "to enable it, set MonitorSwapTransactions to true and provide a valid SwapNotificationChannelId");
                }

                await botService.StartAsync();

                // Store bot instance
                var instance = new BotInstance(config.Id, config.Name, botService) { IsRunning = true };

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

                // Create a new PriceService with the new path
                var newPriceService = new PriceService(rpcUrl, routerAddress, newPath,
                    message => _logAction($"[{instance.Name}] {message}"));

                // Update the bot service with the new price service
                await instance.BotService.UpdateTokenPairAsync(newPriceService);

                _logAction($"Successfully switched token pair for bot '{instance.Name}'");
            }
            catch (Exception ex)
            {
                _logAction($"Error switching token pair for bot '{instance.Name}': {ex.Message}");
            }
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
                Name = "Default Bot",
                Token = token,
                GuildId = guildId,
                RpcUrl = rpcUrl,
                SwapRouterAddress = swapRouterAddress,
                Path = path,
                Nickname = nickname,
                StatusType = statusType,
                CustomStatus = customStatus,
                UpdateIntervalSeconds = 30
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

        // Add this method to BotManager.cs
        // Add this to BotManager.cs if it doesn't already exist
        public void UpdateSwapMonitoringSettings(
    string botId,
    bool monitorSwapTransactions,
    string swapNotificationChannelId,
    int swapCheckIntervalMs = 15000)
        {
            _logAction?.Invoke($"DEBUG in UpdateSwapMonitoringSettings for bot {botId}:");
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
                _logAction?.Invoke($"Updating swap monitoring settings for bot {botId}");

                if (monitorSwapTransactions && !string.IsNullOrEmpty(swapNotificationChannelId))
                {
                    if (ulong.TryParse(swapNotificationChannelId, out ulong channelId))
                    {
                        // Enable swap monitoring in the bot service
                        _logAction?.Invoke($"DEBUG: Parsed channel ID successfully: {channelId}");
                        botInstance.BotService.EnableSwapMonitoring(channelId, swapCheckIntervalMs);
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
                    // Disable swap monitoring
                    botInstance.BotService.DisableSwapMonitoring();
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

        // Add to Services/BotManager.cs

        // Add this method to update embed settings for a running bot
        public void UpdateBotEmbedSettings(
            string botId,
            bool sendPeriodicEmbeds,
            int embedIntervalMinutes,
            string embedChannelId)
        {
            if (_botInstances.TryGetValue(botId, out var botInstance))
            {
                if (sendPeriodicEmbeds && !string.IsNullOrEmpty(embedChannelId)
                    && ulong.TryParse(embedChannelId, out ulong channelId))
                {
                    _logAction($"Enabling periodic embeds for bot '{botInstance.Name}' " +
                             $"(interval: {embedIntervalMinutes} minutes, channel: {embedChannelId})");

                    botInstance.BotService.SetEmbedInterval(embedIntervalMinutes, channelId);
                }
                else
                {
                    _logAction($"Disabling periodic embeds for bot '{botInstance.Name}'");
                    botInstance.BotService.DisableEmbeds();
                }
            }
        }

        // Add this method to start a bot with a specific configuration
        public async Task<string> StartBotAsync(BotConfig config)
        {
            return await StartBotAsyncImpl(config);
        }



        #endregion
    }
}
