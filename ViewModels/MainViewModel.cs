// ViewModels/MainViewModel.cs
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

        // Add a dedicated field for the single bot configuration
        private BotConfig _singleBotConfig;
        // Add a field to track the single bot instance ID
        private string _singleBotInstanceId;

        public MainViewModel(Action<string> logAction)
        {
            _logAction = logAction;
            _botManager = new BotManager(logAction);
            _settings = SettingsService.LoadSettings();

            // Initialize collections
            Pairs = new ObservableCollection<PairInfo>();
            BotConfigs = new ObservableCollection<BotConfig>(_settings.BotConfigurations ?? new List<BotConfig>());
            BotStatuses = new ObservableCollection<BotStatusViewModel>();

            // Initialize single bot config
            _singleBotConfig = new BotConfig
            {
                Id = "single",
                Name = "Default Bot",
                Token = _settings.BotToken,
                GuildId = _settings.GuildId,
                RpcUrl = _settings.RpcUrl,
                SwapRouterAddress = _settings.SwapRouterAddress,
                Nickname = _settings.BotNickname,
                StatusType = _settings.StatusType,
                CustomStatus = _settings.CustomStatus,
                UpdateIntervalSeconds = 30
            };

            if (!string.IsNullOrEmpty(_settings.ManualPath))
            {
                _singleBotConfig.Path = _settings.ManualPath
                    .Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(a => a.Trim())
                    .ToList();
            }

            // Subscribe to bot events
            _botManager.BotInstanceStarted += BotManager_BotInstanceStarted;
            _botManager.BotInstanceStopped += BotManager_BotInstanceStopped;
            _botManager.BotStatusUpdated += BotManager_BotStatusUpdated;
        }

        private void BotManager_BotInstanceStarted(object sender, string botId)
        {
            // Find the bot config
            var botConfig = BotConfigs.FirstOrDefault(c => c.Id == botId)
                         ?? (_singleBotConfig.Id == botId ? _singleBotConfig : null);

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
        }

        private void BotManager_BotInstanceStopped(object sender, string botId)
        {
            var status = BotStatuses.FirstOrDefault(s => s.BotId == botId);
            if (status != null)
            {
                status.IsRunning = false;
                status.LastUpdated = DateTime.Now.ToString("HH:mm:ss");
            }
        }

        private void BotManager_BotStatusUpdated(object sender, BotStatusEventArgs e)
        {
            // Find the status for this bot
            var status = BotStatuses.FirstOrDefault(s => s.BotId == e.BotId);
            if (status == null)
            {
                // Get bot config to get the name
                var botConfig = BotConfigs.FirstOrDefault(c => c.Id == e.BotId)
                             ?? (_singleBotConfig.Id == e.BotId ? _singleBotConfig : null);

                if (botConfig == null)
                    return;

                // Create a new status
                status = new BotStatusViewModel
                {
                    BotId = e.BotId,
                    BotName = botConfig.Name,
                    IsRunning = true,
                    LastUpdated = DateTime.Now.ToString("HH:mm:ss")
                };

                // Add to collection
                Application.Current.Dispatcher.Invoke(() => BotStatuses.Add(status));
            }

            // Update status information
            if (e.PairInfo != null)
            {
                status.PairName = e.PairInfo.Name;
                status.PairInfo = e.PairInfo;
            }

            if (e.Metrics != null && e.Metrics.ContainsKey("Price") && e.PairInfo != null)
            {
                string priceText = $"{e.Metrics["Price"]:N6} {e.PairInfo.Token1.Symbol}";
                if (e.Metrics.ContainsKey("PriceUsd") && e.Metrics["PriceUsd"] > 0)
                    priceText += $" (${e.Metrics["PriceUsd"]:N4})";

                status.CurrentPrice = priceText;
            }

            status.LastUpdated = DateTime.Now.ToString("HH:mm:ss");
        }



        // Add SingleBotConfig property
        public BotConfig SingleBotConfig => _singleBotConfig;

        // Property to check if the single bot is running
        public bool IsSingleBotRunning => !string.IsNullOrEmpty(_singleBotInstanceId) &&
                                          _botManager.BotInstances?.TryGetValue(_singleBotInstanceId, out var instance) == true &&
                                          instance.IsRunning;

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
            set => SetProperty(ref _selectedBotConfig, value);
        }

        public PairInfo SelectedPair
        {
            get => _selectedPair;
            set => SetProperty(ref _selectedPair, value);
        }

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

        // Save settings for both single bot and multi-bots
        public void SaveSettings()
        {
            // Update settings from the single bot config
            _settings.BotToken = _singleBotConfig.Token;
            _settings.GuildId = _singleBotConfig.GuildId;
            _settings.RpcUrl = _singleBotConfig.RpcUrl;
            _settings.SwapRouterAddress = _singleBotConfig.SwapRouterAddress;
            _settings.BotNickname = _singleBotConfig.Nickname;
            _settings.StatusType = _singleBotConfig.StatusType;
            _settings.CustomStatus = _singleBotConfig.CustomStatus;
            if (_singleBotConfig.Path != null && _singleBotConfig.Path.Any())
            {
                _settings.ManualPath = string.Join(",", _singleBotConfig.Path);
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
                UpdateIntervalSeconds = config.UpdateIntervalSeconds
            }).ToList();

            SettingsService.SaveSettings(_settings);
        }




        // Update single bot settings from UI
        public void UpdateSingleBotSettings(
            string token, string guildId, string rpcUrl,
            string swapRouterAddress, List<string> path,
            string nickname, string statusType, string customStatus)
        {
            _singleBotConfig.Token = token;
            _singleBotConfig.GuildId = guildId;
            _singleBotConfig.RpcUrl = rpcUrl;
            _singleBotConfig.SwapRouterAddress = swapRouterAddress;
            _singleBotConfig.Path = path;
            _singleBotConfig.Nickname = nickname;
            _singleBotConfig.StatusType = statusType;
            _singleBotConfig.CustomStatus = customStatus;

            SaveSettings();
        }

        public void UpdateBotStatus(string botId, string price, string pairName)
        {
            // Find existing status or create a new one
            var status = BotStatuses.FirstOrDefault(s => s.BotId == botId);

            if (status == null)
            {
                // Find the bot config to get the name
                var botConfig = BotConfigs.FirstOrDefault(b => b.Id == botId);

                // Special case for the single bot
                if (botId == _singleBotInstanceId && botConfig == null)
                {
                    botConfig = _singleBotConfig;
                }

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


        // Add this method to MainViewModel.cs to update and save multi-bot settings
        // Add to the UpdateMultiBotSettings method to include embed configuration
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
            // New parameters
            bool sendPeriodicEmbeds = false,
            int embedIntervalMinutes = 60,
            string embedChannelId = "",
            string embedColor = "#50E999",
            bool includeChartInEmbed = true,
            bool includeTokenInfoInEmbed = true,
            bool includeLiquidityInfoInEmbed = true)
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

            // New embed settings
            botConfig.SendPeriodicEmbeds = sendPeriodicEmbeds;
            botConfig.EmbedIntervalMinutes = embedIntervalMinutes;
            botConfig.EmbedChannelId = embedChannelId;
            botConfig.EmbedColor = embedColor;
            botConfig.IncludeChartInEmbed = includeChartInEmbed;
            botConfig.IncludeTokenInfoInEmbed = includeTokenInfoInEmbed;
            botConfig.IncludeLiquidityInfoInEmbed = includeLiquidityInfoInEmbed;

            // Save all settings to persist changes
            SaveSettings();
        }

        // Add this method to update just the embed settings for a bot
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
            if (botConfig == null && botId == "single")
                botConfig = _singleBotConfig;

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
                _botManager.UpdateBotEmbedSettings(
                    botId,
                    sendPeriodicEmbeds,
                    embedIntervalMinutes,
                    embedChannelId);
            }

            SaveSettings();
        }


        // Methods specific to the single bot
        public async Task StartSingleBotAsync()
        {
            try
            {
                // Ensure we have a path
                if (_singleBotConfig.Path == null || !_singleBotConfig.Path.Any())
                {
                    // Try to use selected pair
                    if (SelectedPair != null)
                    {
                        _singleBotConfig.Path = new List<string> { SelectedPair.Token0.Address, SelectedPair.Token1.Address };
                    }
                    else if (!string.IsNullOrEmpty(_settings.ManualPath))
                    {
                        _singleBotConfig.Path = _settings.ManualPath
                            .Split(',', StringSplitOptions.RemoveEmptyEntries)
                            .Select(a => a.Trim())
                            .ToList();
                    }
                    else
                    {
                        throw new InvalidOperationException("No token path specified for the bot.");
                    }
                }

                _singleBotInstanceId = await _botManager.StartBotAsync(_singleBotConfig);

                OnPropertyChanged(nameof(IsSingleBotRunning));
                OnPropertyChanged(nameof(IsAnyBotRunning));
            }
            catch
            {
                _singleBotInstanceId = null;
                throw;
            }
        }

        public async Task StopSingleBotAsync()
        {
            if (!string.IsNullOrEmpty(_singleBotInstanceId))
            {
                await _botManager.StopBotAsync(_singleBotInstanceId);
                _singleBotInstanceId = null;

                OnPropertyChanged(nameof(IsSingleBotRunning));
                OnPropertyChanged(nameof(IsAnyBotRunning));
            }
        }

        public async Task SwitchSingleBotPairAsync(List<string> newPath, string rpcUrl, string routerAddress)
        {
            if (!string.IsNullOrEmpty(_singleBotInstanceId))
            {
                await _botManager.SwitchPairAsync(_singleBotInstanceId, newPath, rpcUrl, routerAddress);
                // Update the path in the single bot config
                _singleBotConfig.Path = newPath;
            }
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

                // Since Name is a read-only property, we can't set it directly
                // Let it be computed from the Token0 and Token1 properties

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

        // Generic methods for bot management
        public async Task SwitchPairAsync(string botId, List<string> newPath, string rpcUrl, string routerAddress)
        {
            await _botManager.SwitchPairAsync(botId, newPath, rpcUrl, routerAddress);
            OnPropertyChanged(nameof(IsBotRunning));
        }

        // Legacy method - keep for backward compatibility
        public async Task SwitchPairAsync(List<string> newPath, string rpcUrl, string routerAddress)
        {
            // If single bot is running, update it
            if (IsSingleBotRunning)
            {
                await SwitchSingleBotPairAsync(newPath, rpcUrl, routerAddress);
            }
            else
            {
                // Legacy approach
                await _botManager.SwitchPairAsync(newPath, rpcUrl, routerAddress);
            }

            OnPropertyChanged(nameof(IsBotRunning));
        }

        public async Task StartBotAsync(BotConfig config)
        {
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

            await _botManager.StartBotAsync(config);
            OnPropertyChanged(nameof(IsBotRunning));
            OnPropertyChanged(nameof(IsAnyBotRunning));
        }

        // Legacy method for starting the single bot - keep for backward compatibility
        public async Task StartBotAsync(
            string token, string guildId, string rpcUrl,
            string swapRouterAddress, List<string> path,
            string nickname, string statusType, string customStatus)
        {
            // Update the single bot config
            UpdateSingleBotSettings(token, guildId, rpcUrl, swapRouterAddress, path, nickname, statusType, customStatus);

            // Start the single bot
            await StartSingleBotAsync();

            OnPropertyChanged(nameof(IsBotRunning));
            OnPropertyChanged(nameof(IsAnyBotRunning));
        }

        public async Task StopBotAsync(string botId)
        {
            await _botManager.StopBotAsync(botId);

            // If this was the single bot instance, clear the ID
            if (_singleBotInstanceId == botId)
            {
                _singleBotInstanceId = null;
                OnPropertyChanged(nameof(IsSingleBotRunning));
            }

            OnPropertyChanged(nameof(IsBotRunning));
            OnPropertyChanged(nameof(IsAnyBotRunning));
        }

        // Legacy method - keep for backward compatibility 
        public async Task StopBotAsync()
        {
            await StopSingleBotAsync();

            OnPropertyChanged(nameof(IsBotRunning));
            OnPropertyChanged(nameof(IsAnyBotRunning));
        }

        public async Task StopAllBotsAsync()
        {
            await _botManager.StopAllBotsAsync();

            // Reset single bot instance ID
            _singleBotInstanceId = null;

            OnPropertyChanged(nameof(IsBotRunning));
            OnPropertyChanged(nameof(IsSingleBotRunning));
            OnPropertyChanged(nameof(IsAnyBotRunning));
        }

        // Data methods
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
