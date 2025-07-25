// ViewModels/MainViewModel.cs
using RadXPriceBot.Data;
using RadXPriceBot.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;

namespace RadXPriceBot.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private readonly BotManager _botManager;
        private readonly Action<string> _logAction;
        private BotSettings _settings;
        private PairInfo _selectedPair;
        private ObservableCollection<PairInfo> _pairs;
        private ObservableCollection<BotConfig> _botConfigs;
        private BotConfig _selectedBotConfig;
        private bool _isLoadingPairs;
        private ObservableCollection<BotStatusViewModel> _botStatuses = new ObservableCollection<BotStatusViewModel>();

        private readonly DatabaseService _databaseService;

        public MainViewModel(Action<string> logAction)
        {
            _logAction = logAction ?? throw new ArgumentNullException(nameof(logAction));
            
            try
            {
                InitializeServices();
                InitializeSettings();
                InitializeCollections();
                InitializeBotManager();
                EnsureAtLeastOneBotConfig();
            }
            catch (Exception ex)
            {
                _logAction($"ERROR initializing MainViewModel: {ex.Message}");
                throw;
            }
        }

        private void InitializeServices()
        {
            _databaseService = new DatabaseService(_logAction);
        }

        private void InitializeSettings()
        {
            _settings = SettingsService.LoadSettings(_logAction) ?? new BotSettings();
        }

        private void InitializeCollections()
        {
            Pairs = new ObservableCollection<PairInfo>();
            BotConfigs = new ObservableCollection<BotConfig>(_settings.BotConfigurations ?? new List<BotConfig>());
            BotStatuses = new ObservableCollection<BotStatusViewModel>();
        }

        private void InitializeBotManager()
        {
            _botManager = new BotManager(_logAction);
            
            // Subscribe to bot events
            _botManager.BotInstanceStarted += BotManager_BotInstanceStarted;
            _botManager.BotInstanceStopped += BotManager_BotInstanceStopped;
            _botManager.BotStatusUpdated += BotManager_BotStatusUpdated;
        }

        private void EnsureAtLeastOneBotConfig()
        {
            // If no bot configs exist, create at least one default config
            if (!BotConfigs.Any())
            {
                AddBotConfig();
            }
        }

        private void BotManager_BotInstanceStarted(object sender, string botId)
        {
            // Find the bot config
            var botConfig = BotConfigs.FirstOrDefault(c => c.Id == botId);
            if (botConfig == null)
                return;

            // Create or update bot status
            var status = BotStatuses.FirstOrDefault(s => s.BotId == botId);
            if (status == null)
            {
                status = new BotStatusViewModel
                {
                    BotId = botId,
                    BotName = botConfig.Name,
                    PairName = "Loading...",
                    CurrentPrice = "Loading...",
                    IsRunning = true,
                    LastUpdated = DateTime.Now.ToString("HH:mm:ss")
                };

                Application.Current.Dispatcher.Invoke(() => BotStatuses.Add(status));
            }
            else
            {
                status.IsRunning = true;
                status.LastUpdated = DateTime.Now.ToString("HH:mm:ss");
            }

            OnPropertyChanged(nameof(IsSelectedBotRunning));
            OnPropertyChanged(nameof(IsAnyBotRunning));
        }

        private void BotManager_BotInstanceStopped(object sender, string botId)
        {
            var status = BotStatuses.FirstOrDefault(s => s.BotId == botId);
            if (status != null)
            {
                status.IsRunning = false;
                status.LastUpdated = DateTime.Now.ToString("HH:mm:ss");
            }

            OnPropertyChanged(nameof(IsSelectedBotRunning));
            OnPropertyChanged(nameof(IsAnyBotRunning));
        }


        private void BotManager_BotStatusUpdated(object sender, BotStatusEventArgs e)
        {
            if (e == null) return;

            // Find the status for this bot
            var status = BotStatuses.FirstOrDefault(s => s.BotId == e.BotId);
            if (status == null)
            {
                status = CreateNewBotStatus(e.BotId);
                if (status == null) return; // Could not create status
            }

            UpdateBotStatusWithEventData(status, e);
        }

        private BotStatusViewModel CreateNewBotStatus(string botId)
        {
            // Get bot config to get the name
            var botConfig = BotConfigs.FirstOrDefault(c => c.Id == botId);
            if (botConfig == null)
            {
                _logAction($"Warning: Could not find bot config for ID {botId}");
                return null;
            }

            // Create a new status with DatabaseService (safely handle null)
            var status = _databaseService != null 
                ? new BotStatusViewModel(_databaseService)
                : new BotStatusViewModel();

            status.BotId = botId;
            status.BotName = botConfig.Name;
            status.IsRunning = true;
            status.LastUpdated = DateTime.Now.ToString("HH:mm:ss");

            // Add to collection on UI thread
            Application.Current?.Dispatcher?.Invoke(() => BotStatuses.Add(status));
            
            return status;
        }

        private void UpdateBotStatusWithEventData(BotStatusViewModel status, BotStatusEventArgs e)
        {
            // Update status information
            if (e.PairInfo != null)
            {
                status.PairName = e.PairInfo.Name;
                status.PairInfo = e.PairInfo;
            }

            if (e.Metrics != null)
            {
                UpdateStatusMetrics(status, e.Metrics, e.PairInfo);
            }

            status.LastUpdated = DateTime.Now.ToString("HH:mm:ss");

            // Force a refresh of the historical data
            status.ResetHistoricalDataState();
        }

        private void UpdateStatusMetrics(BotStatusViewModel status, Dictionary<string, decimal> metrics, PairInfo pairInfo)
        {
            // Update simple price information
            if (metrics.ContainsKey("Price") && pairInfo != null)
            {
                string priceText = $"{metrics["Price"]:N6} {pairInfo.Token1.Symbol}";
                if (metrics.ContainsKey("PriceUsd") && metrics["PriceUsd"] > 0)
                    priceText += $" (${metrics["PriceUsd"]:N4})";

                status.CurrentPrice = priceText;
            }

            // Update volume
            if (metrics.ContainsKey("Volume24h"))
            {
                status.Volume24h = FormatLargeNumber(metrics["Volume24h"]);
            }

            // Update price changes for various intervals
            UpdatePriceChanges(status, metrics);

            // Update liquidity information
            UpdateLiquidityInfo(status, metrics);
        }

        private void UpdatePriceChanges(BotStatusViewModel status, Dictionary<string, decimal> metrics)
        {
            var priceChangeKeys = new[]
            {
                ("PriceChange15m", (Action<string>)(value => status.PriceChange15Min = value)),
                ("PriceChange30m", (Action<string>)(value => status.PriceChange30Min = value)),
                ("PriceChange1h", (Action<string>)(value => status.PriceChange1Hour = value)),
                ("PriceChange4h", (Action<string>)(value => status.PriceChange4Hour = value)),
                ("PriceChange24h", (Action<string>)(value => status.PriceChange24h = value)),
                ("PriceChange7d", (Action<string>)(value => status.PriceChange7d = value))
            };

            foreach (var (key, setter) in priceChangeKeys)
            {
                if (metrics.ContainsKey(key))
                {
                    decimal change = metrics[key];
                    setter(change >= 0 ? $"+{change:N2}%" : $"{change:N2}%");
                }
            }
        }

        private void UpdateLiquidityInfo(BotStatusViewModel status, Dictionary<string, decimal> metrics)
        {
            // Update liquidity change if available
            if (metrics.ContainsKey("LiquidityChange24h"))
            {
                decimal change = metrics["LiquidityChange24h"];
                status.LiquidityChange24h = change >= 0 ? $"+{change:N2}%" : $"{change:N2}%";
            }

            // Update current liquidity
            if (metrics.ContainsKey("Liquidity"))
            {
                status.CurrentLiquidity = metrics["Liquidity"];
            }

            // Update holder count if available
            if (metrics.ContainsKey("HolderCount"))
            {
                status.HoldersCount = (int)metrics["HolderCount"];
            }
        }


        // Helper method to format large numbers
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




        public ObservableCollection<PairInfo> Pairs
        {
            get => _pairs;
            set => SetProperty(ref _pairs, value);
        }

        public ObservableCollection<BotStatusViewModel> BotStatuses
        {
            get => _botStatuses;
            set => SetProperty(ref _botStatuses, value);
        }

        public ObservableCollection<BotConfig> BotConfigs
        {
            get => _botConfigs;
            set => SetProperty(ref _botConfigs, value);
        }

        public BotConfig SelectedBotConfig
        {
            get => _selectedBotConfig;
            set
            {
                if (SetProperty(ref _selectedBotConfig, value))
                {
                    // Update related properties when the selected bot changes
                    OnPropertyChanged(nameof(IsSelectedBotRunning));
                    OnPropertyChanged(nameof(SelectedBotStatus));
                }
            }
        }

        public BotStatusViewModel SelectedBotStatus => _selectedBotConfig != null ?
                                                      BotStatuses.FirstOrDefault(s => s.BotId == _selectedBotConfig.Id) :
                                                      null;

        public PairInfo SelectedPair
        {
            get => _selectedPair;
            set => SetProperty(ref _selectedPair, value);
        }

        // Property to check if the selected bot is running
        public bool IsSelectedBotRunning => _selectedBotConfig != null &&
                                          IsBotConfigRunning(_selectedBotConfig.Id);

        // Legacy property - use for backwards compatibility with older code
        public bool IsBotRunning => _botManager.IsRunning;

        public bool IsAnyBotRunning => _botManager.BotInstances?.Any(b => b.Value.IsRunning) ?? false;

        public bool IsLoadingPairs
        {
            get => _isLoadingPairs;
            set => SetProperty(ref _isLoadingPairs, value);
        }

        public BotSettings Settings => _settings;

        public bool IsBotConfigRunning(string configId)
        {
            return _botManager.BotInstances?.TryGetValue(configId, out var instance) == true && instance.IsRunning;
        }

        // Save settings for multi-bots only
        public void SaveSettings()
        {
            // Store global settings based on the first bot config if available
            var firstConfig = BotConfigs.FirstOrDefault();
            if (firstConfig != null)
            {
                _settings.BotToken = firstConfig.Token;
                _settings.GuildId = firstConfig.GuildId;
                _settings.RpcUrl = firstConfig.RpcUrl;
                _settings.SwapRouterAddress = firstConfig.SwapRouterAddress;
                _settings.BotNickname = firstConfig.Nickname;
                _settings.StatusType = firstConfig.StatusType;
                _settings.CustomStatus = firstConfig.CustomStatus;

                if (firstConfig.Path != null && firstConfig.Path.Any())
                {
                    _settings.ManualPath = string.Join(",", firstConfig.Path);
                }
            }

            // Make a deep copy of the bot configurations to avoid reference issues
            _settings.BotConfigurations = BotConfigs.Select(config => new BotConfig
            {
                Id = config.Id,
                Name = config.Name,
                Token = config.Token,
                GuildId = config.GuildId,
                RpcUrl = config.RpcUrl,
                SwapRouterAddress = config.SwapRouterAddress,
                Path = config.Path?.ToList() ?? new List<string>(),
                Nickname = config.Nickname,
                StatusType = config.StatusType,
                CustomStatus = config.CustomStatus,
                UpdateIntervalSeconds = config.UpdateIntervalSeconds,
                // Embed settings
                SendPeriodicEmbeds = config.SendPeriodicEmbeds,
                EmbedIntervalMinutes = config.EmbedIntervalMinutes,
                EmbedChannelId = config.EmbedChannelId,
                EmbedColor = config.EmbedColor,
                IncludeChartInEmbed = config.IncludeChartInEmbed,
                IncludeTokenInfoInEmbed = config.IncludeTokenInfoInEmbed,
                IncludeLiquidityInfoInEmbed = config.IncludeLiquidityInfoInEmbed,
                // Swap settings
                MonitorSwapTransactions = config.MonitorSwapTransactions,
                SwapNotificationChannelId = config.SwapNotificationChannelId,
                SwapCheckIntervalMs = config.SwapCheckIntervalMs,
                MinimumBuyThresholdUsd = config.MinimumBuyThresholdUsd,
                MinimumSellThresholdUsd = config.MinimumSellThresholdUsd,
                NotifyOnBuys = config.NotifyOnBuys,
                NotifyOnSells = config.NotifyOnSells
            }).ToList();

            SettingsService.SaveSettings(_settings, _logAction);
        }

        public void UpdateBotStatus(string botId, string price, string pairName)
        {
            // Find existing status or create a new one
            var status = BotStatuses.FirstOrDefault(s => s.BotId == botId);

            if (status == null)
            {
                // Find the bot config to get the name
                var botConfig = BotConfigs.FirstOrDefault(b => b.Id == botId);
                if (botConfig == null) return;

                // Create new status view model
                status = new BotStatusViewModel
                {
                    BotId = botId,
                    BotName = botConfig.Name,
                    IsRunning = true,
                    PairName = pairName,
                    Status = "Running"
                };

                // Ensure updates happen on the UI thread
                Application.Current.Dispatcher.Invoke(() =>
                {
                    BotStatuses.Add(status);
                });
            }

            // Update existing status on UI thread
            Application.Current.Dispatcher.Invoke(() =>
            {
                status.CurrentPrice = price;
                status.LastUpdated = DateTime.Now.ToString("HH:mm:ss");
                status.Status = "Running";
                status.IsRunning = true;
                status.PairName = pairName;
            });

            // Notify that the collection has changed
            OnPropertyChanged(nameof(BotStatuses));
        }

        // Method to update and save multi-bot settings
        public void UpdateMultiBotSettings(
         string id,
         string name,
         string token,
         string guildId,
         string rpcUrl,
         string swapRouterAddress,
         List<string> path,
         string nickname,
         string statusType,
         string customStatus,
         int updateIntervalSeconds,
         // Embed parameters
         bool sendPeriodicEmbeds = false,
         int embedIntervalMinutes = 60,
         string embedChannelId = "",
         string embedColor = "#50E999",
         bool includeChartInEmbed = true,
         bool includeTokenInfoInEmbed = true,
         bool includeLiquidityInfoInEmbed = true,
         // Swap parameters
         bool monitorSwapTransactions = true,
         string swapNotificationChannelId = "",
         int swapCheckIntervalMs = 30000)
        {
            var botConfig = BotConfigs.FirstOrDefault(b => b.Id == id);
            if (botConfig == null)
            {
                // If not found, create a new config with the provided ID
                botConfig = new BotConfig { Id = id };
                BotConfigs.Add(botConfig);
            }

            // Update all properties of the bot config
            botConfig.Name = name;
            botConfig.Token = token;
            botConfig.GuildId = guildId;
            botConfig.RpcUrl = rpcUrl;
            botConfig.SwapRouterAddress = swapRouterAddress;
            botConfig.Path = path ?? new List<string>();
            botConfig.Nickname = nickname;
            botConfig.StatusType = statusType;
            botConfig.CustomStatus = customStatus;
            botConfig.UpdateIntervalSeconds = updateIntervalSeconds;

            // Embed settings
            botConfig.SendPeriodicEmbeds = sendPeriodicEmbeds;
            botConfig.EmbedIntervalMinutes = embedIntervalMinutes;
            botConfig.EmbedChannelId = embedChannelId;
            botConfig.EmbedColor = embedColor;
            botConfig.IncludeChartInEmbed = includeChartInEmbed;
            botConfig.IncludeTokenInfoInEmbed = includeTokenInfoInEmbed;
            botConfig.IncludeLiquidityInfoInEmbed = includeLiquidityInfoInEmbed;

            // Swap notification settings
            botConfig.MonitorSwapTransactions = monitorSwapTransactions;
            botConfig.SwapNotificationChannelId = swapNotificationChannelId;
            botConfig.SwapCheckIntervalMs = swapCheckIntervalMs;

            // Save all settings to persist changes
            SaveSettings();
        }

        // Method to update embed settings for a bot
        public void UpdateBotEmbedSettings(
            string botId,
            bool sendPeriodicEmbeds,
            int embedIntervalMinutes,
            string embedChannelId,
            string embedColor,
            bool includeChartInEmbed,
            bool includeTokenInfoInEmbed,
            bool includeLiquidityInfoInEmbed)
        {
            var botConfig = BotConfigs.FirstOrDefault(b => b.Id == botId);
            if (botConfig == null)
                return;

            botConfig.SendPeriodicEmbeds = sendPeriodicEmbeds;
            botConfig.EmbedIntervalMinutes = embedIntervalMinutes;
            botConfig.EmbedChannelId = embedChannelId;
            botConfig.EmbedColor = embedColor;
            botConfig.IncludeChartInEmbed = includeChartInEmbed;
            botConfig.IncludeTokenInfoInEmbed = includeTokenInfoInEmbed;
            botConfig.IncludeLiquidityInfoInEmbed = includeLiquidityInfoInEmbed;

            // If this bot is running, update its embed settings
            if (IsBotConfigRunning(botId))
            {
                // Pass ALL parameters to the BotManager method
                _botManager.UpdateBotEmbedSettings(
                    botId,
                    sendPeriodicEmbeds,
                    embedIntervalMinutes,
                    embedChannelId,
                    embedColor,
                    includeChartInEmbed,
                    includeTokenInfoInEmbed,
                    includeLiquidityInfoInEmbed);
            }

            SaveSettings();
        }

        // Method to update swap monitoring settings for a bot
        public async Task UpdateBotSwapMonitoringSettings(
            string botId,
            bool monitorSwapTransactions,
            string swapNotificationChannelId,
            int swapCheckIntervalMs = 15000)
        {
            var botConfig = BotConfigs.FirstOrDefault(b => b.Id == botId);
            if (botConfig == null)
                return;

            botConfig.MonitorSwapTransactions = monitorSwapTransactions;
            botConfig.SwapNotificationChannelId = swapNotificationChannelId;
            botConfig.SwapCheckIntervalMs = swapCheckIntervalMs;

            // If this bot is running, update its swap monitoring settings
            if (IsBotConfigRunning(botId))
            {
                await _botManager.UpdateSwapMonitoringSettings(
                    botId,
                    monitorSwapTransactions,
                    swapNotificationChannelId,
                    swapCheckIntervalMs);
            }

            SaveSettings();
        }

        public async Task<PairInfo> GetPairInfoFromPathAsync(List<string> path, string rpcUrl, string routerAddress)
        {
            if (path == null || path.Count < 2)
                return null;

            try
            {
                // Create a minimal price service to fetch token information
                var priceService = new PriceService(rpcUrl, routerAddress, path, _logAction);

                // Get token information for both tokens in the path
                var token0Info = await priceService.GetTokenInfoAsync(path[0]);
                var token1Info = await priceService.GetTokenInfoAsync(path[1]);

                // Create a PairInfo object
                var pair = new PairInfo
                {
                    Address = await GetPairAddressAsync(path[0], path[1], rpcUrl, routerAddress),
                    Token0 = token0Info,
                    Token1 = token1Info
                };

                return pair;
            }
            catch (Exception ex)
            {
                _logAction($"Error creating pair info from path: {ex.Message}");
                return null;
            }
        }

        // Helper method to get pair address since PriceService doesn't have this method
        private async Task<string> GetPairAddressAsync(string token0Address, string token1Address, string rpcUrl, string routerAddress)
        {
            try
            {
                var priceService = new PriceService(rpcUrl, routerAddress, new List<string> { token0Address, token1Address }, _logAction);
                return await priceService.GetPairAddress(token0Address, token1Address);
            }
            catch (Exception ex)
            {
                _logAction($"Error getting pair address: {ex.Message}");
                return "0x0000000000000000000000000000000000000000";
            }
        }

        // Methods for multi-bot management
        public void AddBotConfig()
        {
            var newConfig = new BotConfig
            {
                Id = Guid.NewGuid().ToString("N"),
                Name = $"Bot {BotConfigs.Count + 1}",
                Token = _settings.BotToken, // Inherit from main settings
                GuildId = _settings.GuildId, // Inherit from main settings
                RpcUrl = _settings.RpcUrl,
                SwapRouterAddress = _settings.SwapRouterAddress,
                Path = new List<string>(), // Initialize with empty path
                Nickname = _settings.BotNickname, // Inherit from main settings
                StatusType = _settings.StatusType, // Inherit from main settings
                CustomStatus = _settings.CustomStatus, // Inherit from main settings
                UpdateIntervalSeconds = 30
            };

            BotConfigs.Add(newConfig);
            SelectedBotConfig = newConfig;
            SaveSettings();
        }

        public void RemoveBotConfig(BotConfig config)
        {
            // Stop the bot if running
            if (IsBotConfigRunning(config.Id))
            {
                StopBotAsync(config.Id).Wait();
            }

            BotConfigs.Remove(config);

            if (!BotConfigs.Any())
            {
                AddBotConfig(); // Always have at least one config
            }

            if (SelectedBotConfig == null || !BotConfigs.Contains(SelectedBotConfig))
            {
                SelectedBotConfig = BotConfigs.FirstOrDefault();
            }

            SaveSettings();
        }

        // Start the selected bot
        public async Task StartSelectedBotAsync()
        {
            if (_selectedBotConfig == null)
            {
                _logAction("No bot configuration selected.");
                return;
            }

            try
            {
                // Ensure we have a path
                if (_selectedBotConfig.Path == null || !_selectedBotConfig.Path.Any())
                {
                    // Try to use selected pair
                    if (SelectedPair != null)
                    {
                        _selectedBotConfig.Path = new List<string> { SelectedPair.Token0.Address, SelectedPair.Token1.Address };
                    }
                    else if (!string.IsNullOrEmpty(_settings.ManualPath))
                    {
                        _selectedBotConfig.Path = _settings.ManualPath
                            .Split(',', StringSplitOptions.RemoveEmptyEntries)
                            .Select(a => a.Trim())
                            .ToList();
                    }
                    else
                    {
                        throw new InvalidOperationException("No token path specified for the bot.");
                    }
                }

                // Save settings before starting
                SaveSettings();

                // Start the bot
                await _botManager.StartBotAsync(_selectedBotConfig);

                // Update UI states
                OnPropertyChanged(nameof(IsSelectedBotRunning));
                OnPropertyChanged(nameof(IsAnyBotRunning));
            }
            catch (Exception ex)
            {
                _logAction($"ERROR starting bot: {ex.Message}");
                if (ex.InnerException != null)
                {
                    _logAction($"Inner exception: {ex.InnerException.Message}");
                }
                throw;
            }
        }

        // Stop the selected bot
        public async Task StopSelectedBotAsync()
        {
            if (_selectedBotConfig == null)
            {
                _logAction("No bot configuration selected.");
                return;
            }

            if (IsBotConfigRunning(_selectedBotConfig.Id))
            {
                await _botManager.StopBotAsync(_selectedBotConfig.Id);

                // Update UI states
                OnPropertyChanged(nameof(IsSelectedBotRunning));
                OnPropertyChanged(nameof(IsAnyBotRunning));
            }
        }

        // Generic methods for bot management
        public async Task SwitchPairAsync(string botId, List<string> newPath, string rpcUrl, string routerAddress)
        {
            await _botManager.SwitchPairAsync(botId, newPath, rpcUrl, routerAddress);

            // Update the path in the config
            var botConfig = BotConfigs.FirstOrDefault(b => b.Id == botId);
            if (botConfig != null)
            {
                botConfig.Path = newPath;
                SaveSettings();
            }

            OnPropertyChanged(nameof(IsBotRunning));
        }

        // Method to switch the selected bot's pair
        public async Task SwitchSelectedBotPairAsync(List<string> newPath, string rpcUrl, string routerAddress)
        {
            if (_selectedBotConfig != null && IsBotConfigRunning(_selectedBotConfig.Id))
            {
                await SwitchPairAsync(_selectedBotConfig.Id, newPath, rpcUrl, routerAddress);
                _selectedBotConfig.Path = newPath;
                SaveSettings();
            }
            else
            {
                _logAction("No bot selected or the selected bot is not running.");
            }
        }

        // Start a bot with the provided configuration
        public async Task StartBotAsync(BotConfig config)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));

            // Make sure the config has a path
            if (config.Path == null || !config.Path.Any())
            {
                // Try to get path from selected pair or manual path
                if (SelectedPair != null)
                {
                    config.Path = new List<string> { SelectedPair.Token0.Address, SelectedPair.Token1.Address };
                }
                else if (!string.IsNullOrEmpty(_settings.ManualPath))
                {
                    config.Path = _settings.ManualPath
                        .Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(a => a.Trim())
                        .ToList();
                }
                else
                {
                    throw new InvalidOperationException("No token path specified for the bot.");
                }
            }

            // Start the bot instance
            await _botManager.StartBotAsync(config);

            OnPropertyChanged(nameof(IsBotRunning));
            OnPropertyChanged(nameof(IsAnyBotRunning));
        }

        // Method to stop a bot by ID
        public async Task StopBotAsync(string botId)
        {
            await _botManager.StopBotAsync(botId);
            OnPropertyChanged(nameof(IsBotRunning));
            OnPropertyChanged(nameof(IsAnyBotRunning));
        }

        // Method to stop all bots
        public async Task StopAllBotsAsync()
        {
            await _botManager.StopAllBotsAsync();
            OnPropertyChanged(nameof(IsBotRunning));
            OnPropertyChanged(nameof(IsAnyBotRunning));
        }

        // Method to refresh data for the selected bot
        public async Task RefreshSelectedBotDataAsync()
        {
            if (_selectedBotConfig != null && IsBotConfigRunning(_selectedBotConfig.Id))
            {
                await _botManager.RefreshBotPriceDataAsync(_selectedBotConfig.Id);
                _logAction($"Refreshed data for bot '{_selectedBotConfig.Name}'");
            }
        }

        // Data loading methods
        public async Task LoadPairsAsync(string rpcUrl, string factoryAddress, Action<string> logger)
        {
            IsLoadingPairs = true;
            try
            {
                var lpService = new LpService(rpcUrl, factoryAddress, logger);
                var pairs = await Task.Run(() => lpService.GetAllPairsAsync());

                Application.Current.Dispatcher.Invoke(() =>
                {
                    Pairs.Clear();
                    foreach (var pair in pairs)
                    {
                        Pairs.Add(pair);
                    }
                });
            }
            catch (Exception ex)
            {
                logger($"Error loading pairs: {ex.Message}");
                if (ex.InnerException != null)
                {
                    logger($"Inner exception: {ex.InnerException.Message}");
                }
            }
            finally
            {
                IsLoadingPairs = false;
            }
        }

        public async Task PreloadPairDetailsAsync(string rpcUrl, string routerAddress, Action<string> logger)
        {
            if (Pairs.Count == 0)
                return;

            try
            {
                // Preload details for first pair
                var firstPair = Pairs[0];
                await LoadPairDetailsAsync(firstPair, rpcUrl, routerAddress);
                logger($"Preloaded details for {firstPair.Name}");
            }
            catch (Exception ex)
            {
                logger($"Failed to preload pair details: {ex.Message}");
            }
        }

        public async Task<(decimal price, decimal reserve0, decimal reserve1, decimal liquidity, decimal marketCap)>
            LoadPairDetailsAsync(PairInfo pair, string rpcUrl, string routerAddress)
        {
            try
            {
                // Create path from the selected pair
                var path = new List<string> { pair.Token0.Address, pair.Token1.Address };

                // Get price and reserves
                var priceSvc = new PriceService(rpcUrl, routerAddress, path);
                var price = await priceSvc.GetPriceAsync();
                var (reserve0, reserve1) = await priceSvc.GetReservesAsync();

                // Calculate liquidity
                var liquidity = reserve0 * price + reserve1;

                // Calculate market cap
                var marketCap = pair.Token0.TotalSupply * price;

                return (price, reserve0, reserve1, liquidity, marketCap);
            }
            catch
            {
                return (0, 0, 0, 0, 0);
            }
        }
    }
}
