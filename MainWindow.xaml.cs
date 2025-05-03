// MainWindow.xaml.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Globalization;
using RadXPriceBot.Services;
using RadXPriceBot.ViewModels;
using Discord;
using Discord.WebSocket;
using System.Windows.Media;

namespace RadXPriceBot
{
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _viewModel;
        private Dictionary<string, decimal> _currentMetrics;
        private PairInfo _currentPair;

        public MainWindow()
        {
            InitializeComponent();
            _viewModel = new MainViewModel(AppendLog);
            DataContext = _viewModel;

            // Load settings from ViewModel to UI
            LoadSettingsToUI();

            LoadDpiSettings();
            // Add to MainWindow constructor after LoadDpiSettings()
            this.SizeChanged += (s, e) =>
            {
                // Ensure minimum window size is respected based on current DPI scaling
                if (_viewModel?.Settings?.DpiScaling > 1.0)
                {
                    double scale = _viewModel.Settings.DpiScaling;
                    double minWidth = 800 * scale;
                    double minHeight = 600 * scale;

                    if (this.Width < minWidth)
                        this.Width = minWidth;

                    if (this.Height < minHeight)
                        this.Height = minHeight;
                }
            };
        }

        private void LoadSettingsToUI()
        {
            var settings = _viewModel.Settings;

            // Load global settings to UI
            var firstConfig = _viewModel.BotConfigs.FirstOrDefault();
            if (firstConfig != null)
            {
                RpcUrlTextBox.Text = firstConfig.RpcUrl;
                SwapRouterAddressTextBox.Text = firstConfig.SwapRouterAddress;
                FactoryAddressTextBox.Text = settings.FactoryAddress;
            }
            else
            {
                RpcUrlTextBox.Text = settings.RpcUrl;
                SwapRouterAddressTextBox.Text = settings.SwapRouterAddress;
                FactoryAddressTextBox.Text = settings.FactoryAddress;
            }

            // Load multi-bot configuration details if a bot is selected
            if (_viewModel.SelectedBotConfig != null)
            {
                UpdateMultiBotUIFromConfig(_viewModel.SelectedBotConfig);
            }
        }

        private void UpdateMultiBotUIFromConfig(BotConfig config)
        {
            if (config == null) return;

            // Update multi-bot tab UI elements
            BotNameTextBox.Text = config.Name;
            MultiTokenTextBox.Text = config.Token;
            MultiGuildIdTextBox.Text = config.GuildId;
            MultiRpcUrlTextBox.Text = config.RpcUrl;
            MultiRouterTextBox.Text = config.SwapRouterAddress;
            MultiNicknameTextBox.Text = config.Nickname;
            UpdateIntervalTextBox.Text = config.UpdateIntervalSeconds.ToString();

            // Status type selection
            foreach (var item in MultiStatusTypeCombo.Items)
            {
                if (item is string statusType && statusType == config.StatusType)
                {
                    MultiStatusTypeCombo.SelectedItem = item;
                    break;
                }
            }

            MultiCustomStatusTextBox.Text = config.CustomStatus;

            // Load embed settings
            EnablePeriodicEmbedsCheckBox.IsChecked = config.SendPeriodicEmbeds;
            EmbedIntervalTextBox.Text = config.EmbedIntervalMinutes.ToString();
            EmbedChannelIdTextBox.Text = config.EmbedChannelId;
            EmbedColorTextBox.Text = config.EmbedColor;
            IncludeChartCheckBox.IsChecked = config.IncludeChartInEmbed;
            IncludeTokenInfoCheckBox.IsChecked = config.IncludeTokenInfoInEmbed;
            IncludeLiquidityInfoCheckBox.IsChecked = config.IncludeLiquidityInfoInEmbed;

            // Load swap monitoring settings
            EnableSwapMonitoringCheckBox.IsChecked = config.MonitorSwapTransactions;
            SwapChannelIdTextBox.Text = config.SwapNotificationChannelId;
            SwapCheckIntervalTextBox.Text = config.SwapCheckIntervalMs.ToString();
        }

        private void SaveSettingsFromUI()
        {
            // Only save the global settings
            var settings = _viewModel.Settings;
            settings.FactoryAddress = FactoryAddressTextBox.Text.Trim();

            // If there's a selected multi-bot config, save those settings
            if (_viewModel.SelectedBotConfig != null)
            {
                SaveMultiBotSettingsFromUI();
            }

            _viewModel.SaveSettings();
        }

        private void SaveMultiBotSettingsFromUI()
        {
            if (_viewModel.SelectedBotConfig == null) return;

            // Get path from either selected pair or manual input
            List<string> path = null;
            if (_viewModel.SelectedPair != null)
            {
                path = new List<string> { _viewModel.SelectedPair.Token0.Address, _viewModel.SelectedPair.Token1.Address };
            }
            else if (!string.IsNullOrEmpty(_viewModel.Settings.ManualPath))
            {
                path = _viewModel.Settings.ManualPath
                    .Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(a => a.Trim())
                    .ToList();
            }

            // Parse int values with error checking
            int updateInterval = 30;
            int.TryParse(UpdateIntervalTextBox.Text, out updateInterval);

            int embedIntervalMinutes = 60;
            int.TryParse(EmbedIntervalTextBox.Text, out embedIntervalMinutes);

            int swapCheckIntervalMs = 15000;
            int.TryParse(SwapCheckIntervalTextBox.Text, out swapCheckIntervalMs);

            // Use the UpdateMultiBotSettings method to update all properties
            _viewModel.UpdateMultiBotSettings(
                _viewModel.SelectedBotConfig.Id,
                BotNameTextBox.Text,
                MultiTokenTextBox.Text,
                MultiGuildIdTextBox.Text,
                MultiRpcUrlTextBox.Text,
                MultiRouterTextBox.Text,
                path,
                MultiNicknameTextBox.Text,
                MultiStatusTypeCombo.SelectedValue as string,
                MultiCustomStatusTextBox.Text,
                updateInterval,
                // Embed parameters
                EnablePeriodicEmbedsCheckBox.IsChecked ?? false,
                embedIntervalMinutes,
                EmbedChannelIdTextBox.Text,
                EmbedColorTextBox.Text,
                IncludeChartCheckBox.IsChecked ?? true,
                IncludeTokenInfoCheckBox.IsChecked ?? true,
                IncludeLiquidityInfoCheckBox.IsChecked ?? true,
                // Swap parameters
                EnableSwapMonitoringCheckBox.IsChecked ?? true,
                SwapChannelIdTextBox.Text,
                swapCheckIntervalMs
            );
        }

        private void SaveMultiBotSettings_Click(object sender, RoutedEventArgs e)
        {
            if (_viewModel.SelectedBotConfig == null)
                return;

            SaveMultiBotSettingsFromUI();
            AppendLog("Multi-bot settings saved.");
        }

        private void MultiStatusTypeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Only run once XAML elements are ready
            if (!(sender is ComboBox combo) || MultiCustomStatusTextBox == null)
                return;

            if (combo.SelectedItem is string sel)
                MultiCustomStatusTextBox.IsEnabled = sel.Equals("Custom", StringComparison.OrdinalIgnoreCase);
            else
                MultiCustomStatusTextBox.IsEnabled = false;
        }

        private async void LpPairsComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!(LpPairsComboBox.SelectedItem is PairInfo pair))
                return;

            try
            {
                AppendLog($"Loading details for {pair.Name}...");

                var rpcUrl = RpcUrlTextBox.Text.Trim();
                var routerAddr = SwapRouterAddressTextBox.Text.Trim();

                // Create path from the selected pair
                var path = new List<string> { pair.Token0.Address, pair.Token1.Address };

                // Get price and reserves with metrics
                var priceSvc = new PriceService(rpcUrl, routerAddr, path, AppendLog);
                var metrics = await priceSvc.GetTokenMetricsAsync();

                // Store current metrics and pair for Discord integration
                _currentMetrics = metrics;
                _currentPair = pair;

                // Format the display with USD values when available
                PriceTextBlock.Text = $"{metrics["Price"]:N6} {pair.Token1.Symbol}";

                // Add USD price if available
                if (metrics.ContainsKey("PriceUsd") && metrics["PriceUsd"] > 0)
                    PriceTextBlock.Text += $" (${metrics["PriceUsd"]:N4} USD)";

                // Calculate and display the reverse price ratio
                if (metrics["Price"] > 0)
                {
                    decimal reversePrice = 1 / metrics["Price"];
                    ReversePriceTextBlock.Text = $"{reversePrice:N6} {pair.Token0.Symbol}";
                }
                else
                {
                    ReversePriceTextBlock.Text = "N/A";
                }

                // Display 24h volume if available
                if (metrics.ContainsKey("Volume24h"))
                {
                    VolumeTextBlock.Text = $"${metrics["Volume24h"]:N2}";
                }
                else
                {
                    // If Volume isn't available, use an estimated value based on liquidity
                    // This is a rough approximation - actual DEXs would use event logs for accurate volume
                    decimal estimatedVolume = metrics["Liquidity"] * 0.1m; // Estimate volume as 10% of liquidity
                    VolumeTextBlock.Text = $"~${estimatedVolume:N2}";
                }

                Reserve0TextBlock.Text = $"{metrics["Reserve0"]:N2} {pair.Token0.Symbol}";
                Reserve1TextBlock.Text = $"{metrics["Reserve1"]:N2} {pair.Token1.Symbol}";
                LiquidityTextBlock.Text = $"${metrics["Liquidity"]:N2}";
                MarketCapTextBlock.Text = $"${metrics["MarketCap"]:N2}";

                // Update additional token info fields if they exist
                if (TotalSupplyTextBlock != null && metrics.ContainsKey("TotalSupply"))
                    TotalSupplyTextBlock.Text = $"{metrics["TotalSupply"]:N0} {pair.Token0.Symbol}";

                if (CirculatingSupplyTextBlock != null && metrics.ContainsKey("CirculatingSupply"))
                    CirculatingSupplyTextBlock.Text = $"{metrics["CirculatingSupply"]:N0} {pair.Token0.Symbol}";

                if (HolderCountTextBlock != null && metrics.ContainsKey("HolderCount"))
                    HolderCountTextBlock.Text = $"{metrics["HolderCount"]:N0}";

                if (Reserve0UsdTextBlock != null && metrics.ContainsKey("Reserve0Usd"))
                    Reserve0UsdTextBlock.Text = $"${metrics["Reserve0Usd"]:N2}";

                if (Reserve1UsdTextBlock != null && metrics.ContainsKey("Reserve1Usd"))
                    Reserve1UsdTextBlock.Text = $"${metrics["Reserve1Usd"]:N2}";

                // Update token address fields
                if (Token0AddressTextBlock != null)
                    Token0AddressTextBlock.Text = FormatAddress(pair.Token0.Address);

                if (Token1AddressTextBlock != null)
                    Token1AddressTextBlock.Text = FormatAddress(pair.Token1.Address);

                if (PairAddressTextBlock != null)
                    PairAddressTextBlock.Text = FormatAddress(pair.Address);

                AppendLog($"Loaded details for {pair.Name}");

                // Update the selected pair in view model
                _viewModel.SelectedPair = pair;

                // If a multi-bot is selected and running, update that specific bot
                if (_viewModel.SelectedBotConfig != null && _viewModel.IsBotConfigRunning(_viewModel.SelectedBotConfig.Id))
                {
                    await _viewModel.SwitchPairAsync(_viewModel.SelectedBotConfig.Id, path, rpcUrl, routerAddr);
                    AppendLog($"Updated multi-bot '{_viewModel.SelectedBotConfig.Name}' to monitor {pair.Name}");

                    // Also update the path in the config
                    _viewModel.SelectedBotConfig.Path = path;
                    _viewModel.SaveSettings();
                }
            }
            catch (Exception ex)
            {
                AppendLog($"ERROR loading pair details: {ex.Message}");
                if (ex.InnerException != null)
                    AppendLog($"Inner exception: {ex.InnerException.Message}");
            }
        }

        private async void LoadPairsButton_Click(object sender, RoutedEventArgs e)
        {
            LoadPairsButton.IsEnabled = false;
            AppendLog("Loading LP pairs…");

            try
            {
                var rpcUrl = RpcUrlTextBox.Text.Trim();
                var factoryAddr = FactoryAddressTextBox.Text.Trim();
                var routerAddr = SwapRouterAddressTextBox.Text.Trim();

                // Create a thread-safe logger that marshals calls to the UI thread
                Action<string> threadSafeLogger = (msg) => Dispatcher.Invoke(() => AppendLog(msg));

                await _viewModel.LoadPairsAsync(rpcUrl, factoryAddr, threadSafeLogger);

                // Update UI
                LpPairsComboBox.ItemsSource = _viewModel.Pairs;
                if (_viewModel.Pairs.Count > 0)
                {
                    LpPairsComboBox.SelectedIndex = 0; // Select the first pair by default

                    // Preload details for the first pair
                    await _viewModel.PreloadPairDetailsAsync(rpcUrl, routerAddr, threadSafeLogger);
                }

                AppendLog($"Loaded {_viewModel.Pairs.Count} pairs");
            }
            catch (Exception ex)
            {
                AppendLog($"ERROR loading pairs: {ex.Message}");
                if (ex.InnerException != null)
                {
                    AppendLog($"Inner exception: {ex.InnerException.Message}");
                }
            }
            finally
            {
                LoadPairsButton.IsEnabled = true;
            }
        }

        private void AddBot_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.AddBotConfig();
            // Update UI with newly selected bot config
            if (_viewModel.SelectedBotConfig != null)
            {
                UpdateMultiBotUIFromConfig(_viewModel.SelectedBotConfig);
            }
        }

        private void RemoveBot_Click(object sender, RoutedEventArgs e)
        {
            if (_viewModel.SelectedBotConfig != null)
            {
                _viewModel.RemoveBotConfig(_viewModel.SelectedBotConfig);
                // Update UI with newly selected bot config
                if (_viewModel.SelectedBotConfig != null)
                {
                    UpdateMultiBotUIFromConfig(_viewModel.SelectedBotConfig);
                }
            }
        }

        private void UseSelectedPair_Click(object sender, RoutedEventArgs e)
        {
            if (_viewModel.SelectedPair != null && _viewModel.SelectedBotConfig != null)
            {
                _viewModel.SelectedBotConfig.Path = new List<string>
                {
                    _viewModel.SelectedPair.Token0.Address,
                    _viewModel.SelectedPair.Token1.Address
                };

                AppendLog($"Set bot '{_viewModel.SelectedBotConfig.Name}' to monitor {_viewModel.SelectedPair.Name}");
                _viewModel.SaveSettings();
            }
        }

        private async void StartSelectedBot_Click(object sender, RoutedEventArgs e)
        {
            if (_viewModel.SelectedBotConfig == null)
            {
                AppendLog("No bot configuration selected");
                return;
            }

            StartSelectedBotButton.IsEnabled = false;
            AppendLog($"Starting bot '{_viewModel.SelectedBotConfig.Name}'...");

            try
            {
                // Save the current settings first
                SaveMultiBotSettingsFromUI();

                // Make sure we have a path set
                if (_viewModel.SelectedBotConfig.Path == null || !_viewModel.SelectedBotConfig.Path.Any())
                {
                    if (_viewModel.SelectedPair != null)
                    {
                        _viewModel.SelectedBotConfig.Path = new List<string>
                        {
                            _viewModel.SelectedPair.Token0.Address,
                            _viewModel.SelectedPair.Token1.Address
                        };
                        AppendLog($"Using selected pair: {_viewModel.SelectedPair.Name}");
                    }
                    else
                    {
                        AppendLog("ERROR: No token path specified. Select a pair first.");
                        StartSelectedBotButton.IsEnabled = true;
                        return;
                    }
                }

                await _viewModel.StartSelectedBotAsync();

                // Update UI
                StartSelectedBotButton.IsEnabled = !_viewModel.IsSelectedBotRunning;
                StopSelectedBotButton.IsEnabled = _viewModel.IsSelectedBotRunning;
            }
            catch (Exception ex)
            {
                AppendLog($"ERROR starting bot: {ex.Message}");
            }
            finally
            {
                StartSelectedBotButton.IsEnabled = !_viewModel.IsSelectedBotRunning;
            }
        }

        private async void StopSelectedBot_Click(object sender, RoutedEventArgs e)
        {
            if (_viewModel.SelectedBotConfig == null)
            {
                AppendLog("No bot configuration selected");
                return;
            }

            StopSelectedBotButton.IsEnabled = false;
            AppendLog($"Stopping bot '{_viewModel.SelectedBotConfig.Name}'...");

            try
            {
                await _viewModel.StopSelectedBotAsync();

                // Update UI
                StartSelectedBotButton.IsEnabled = !_viewModel.IsSelectedBotRunning;
                StopSelectedBotButton.IsEnabled = _viewModel.IsSelectedBotRunning;

                AppendLog($"Bot '{_viewModel.SelectedBotConfig.Name}' stopped.");
            }
            catch (Exception ex)
            {
                AppendLog($"ERROR stopping bot: {ex.Message}");
            }
            finally
            {
                StopSelectedBotButton.IsEnabled = _viewModel.IsSelectedBotRunning;
            }
        }

        private async void StopAllBots_Click(object sender, RoutedEventArgs e)
        {
            StopAllBotsButton.IsEnabled = false;
            AppendLog("Stopping all bots...");

            try
            {
                await _viewModel.StopAllBotsAsync();
                AppendLog("All bots stopped.");
            }
            catch (Exception ex)
            {
                AppendLog($"ERROR stopping bots: {ex.Message}");
            }
            finally
            {
                StopAllBotsButton.IsEnabled = _viewModel.IsAnyBotRunning;
            }
        }

        // In MainWindow.xaml.cs, modify RefreshDashboard_Click method:

        private void RefreshDashboard_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Force UI update of the bot statuses
                if (_viewModel.BotStatuses != null)
                {
                    AppendLog("Refreshing bot status dashboard...");

                    // Update the Last Updated timestamp for all running bots
                    foreach (var status in _viewModel.BotStatuses.Where(s => s.IsRunning))
                    {
                        status.LastUpdated = DateTime.Now.ToString("HH:mm:ss");

                        // Reset historical data state to force a refresh
                        status.ResetHistoricalDataState();
                    }

                    // Force update the DataGrid
                    BotStatusGrid.Items.Refresh();

                    AppendLog("Dashboard refreshed successfully.");
                }
                else
                {
                    AppendLog("No bot statuses available to refresh");
                }
            }
            catch (Exception ex)
            {
                AppendLog($"Error refreshing dashboard: {ex.Message}");
            }
        }



        private async void StartBotFromDashboard_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string botId)
            {
                var botConfig = _viewModel.BotConfigs.FirstOrDefault(b => b.Id == botId);
                if (botConfig != null)
                {
                    try
                    {
                        await _viewModel.StartBotAsync(botConfig);
                        AppendLog($"Started bot '{botConfig.Name}' from dashboard");
                    }
                    catch (Exception ex)
                    {
                        AppendLog($"ERROR starting bot from dashboard: {ex.Message}");
                    }
                }
            }
        }

        private async void StopBotFromDashboard_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string botId)
            {
                try
                {
                    await _viewModel.StopBotAsync(botId);
                    AppendLog($"Stopped bot from dashboard");
                }
                catch (Exception ex)
                {
                    AppendLog($"ERROR stopping bot from dashboard: {ex.Message}");
                }
            }
        }

        private async void ViewBotDetails_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string botId)
            {
                var botStatus = _viewModel.BotStatuses.FirstOrDefault(b => b.BotId == botId);
                if (botStatus == null)
                {
                    AppendLog("Bot status not found.");
                    return;
                }

                // Get the bot configuration for this bot
                var botConfig = _viewModel.BotConfigs.FirstOrDefault(b => b.Id == botId);
                if (botConfig == null)
                {
                    AppendLog("Bot configuration not found.");
                    return;
                }

                try
                {
                    AppendLog($"Retrieving detailed information for {botStatus.BotName}...");

                    // Get fresh metrics from the price service
                    var priceSvc = new PriceService(
                        botConfig.RpcUrl,
                        botConfig.SwapRouterAddress,
                        botConfig.Path,
                        AppendLog);

                    var metrics = await priceSvc.GetTokenMetricsAsync();

                    // Get the pair info - we need to check if this bot has a stored PairInfo in the status
                    PairInfo pair = botStatus.PairInfo;

                    // If we don't have pair info, try to get it based on the path
                    if (pair == null && botConfig.Path != null && botConfig.Path.Count >= 2)
                    {
                        // We need to reconstruct a minimal PairInfo from the tokens in the path
                        pair = await _viewModel.GetPairInfoFromPathAsync(
                            botConfig.Path,
                            botConfig.RpcUrl,
                            botConfig.SwapRouterAddress);
                    }

                    if (pair != null)
                    {
                        // Show the details window
                        var detailsWindow = new PairDetailsWindow
                        {
                            Owner = this
                        };

                        detailsWindow.DisplayPairDetails(botStatus.BotName, pair, metrics);
                        detailsWindow.ShowDialog();
                    }
                    else
                    {
                        AppendLog("Could not retrieve pair information for this bot.");
                    }
                }
                catch (Exception ex)
                {
                    AppendLog($"Error loading pair details: {ex.Message}");
                }
            }
        }

        private async void RefreshSelectedBotData_Click(object sender, RoutedEventArgs e)
        {
            if (_viewModel.SelectedBotConfig == null)
            {
                AppendLog("No bot configuration selected");
                return;
            }

            if (!_viewModel.IsSelectedBotRunning)
            {
                AppendLog($"Bot '{_viewModel.SelectedBotConfig.Name}' is not running");
                return;
            }

            try
            {
                AppendLog($"Refreshing data for bot '{_viewModel.SelectedBotConfig.Name}'...");
                await _viewModel.RefreshSelectedBotDataAsync();
                AppendLog($"Data refreshed for bot '{_viewModel.SelectedBotConfig.Name}'");
            }
            catch (Exception ex)
            {
                AppendLog($"Error refreshing bot data: {ex.Message}");
            }
        }

        private void LoadDpiSettings()
        {
            double dpiScale = _viewModel.Settings.DpiScaling;

            // Set default to 100% if not configured
            if (dpiScale <= 0)
            {
                dpiScale = 1.0;
            }

            // Find the closest match in the combo box
            int selectedIndex = 1; // Default to 100%
            string dpiText = $"{(int)(dpiScale * 100)}%";

            for (int i = 0; i < DpiScalingComboBox.Items.Count; i++)
            {
                if (DpiScalingComboBox.Items[i] is ComboBoxItem item &&
                    item.Content.ToString() == dpiText)
                {
                    selectedIndex = i;
                    break;
                }
            }

            DpiScalingComboBox.SelectedIndex = selectedIndex;
            ApplyDpiScaling(dpiScale);
        }

        private void DpiScalingComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DpiScalingComboBox.SelectedItem is ComboBoxItem item && item.Content is string dpiText)
            {
                if (double.TryParse(dpiText.TrimEnd('%'), out double dpiValue))
                {
                    double scale = dpiValue / 100.0;
                    ApplyDpiScaling(scale);

                    // Save the setting
                    _viewModel.Settings.DpiScaling = scale;
                    _viewModel.SaveSettings();
                }
            }
        }

        private void ApplyDpiScaling(double scale)
        {
            try
            {
                // Create a scale transform for the main content
                ScaleTransform scaleTransform = new ScaleTransform(scale, scale);

                // Apply the transform to the main tab control
                if (MainTabControl != null)
                {
                    MainTabControl.LayoutTransform = scaleTransform;
                }

                // Adjust window size to accommodate the scaling
                if (IsLoaded) // Only adjust if window is already loaded
                {
                    double baseWidth = 860;
                    double baseHeight = 770;

                    // Ensure minimum size while accounting for scaling
                    this.MinWidth = baseWidth * scale;
                    this.MinHeight = baseHeight * scale;

                    // Adjust current window size if needed
                    if (this.Width < this.MinWidth || this.Height < this.MinHeight)
                    {
                        this.Width = this.MinWidth;
                        this.Height = this.MinHeight;
                    }
                }

                // Update UI to reflect changes
                AppendLog($"UI scaling set to {(int)(scale * 100)}%");
            }
            catch (Exception ex)
            {
                AppendLog($"Error applying DPI scaling: {ex.Message}");
            }
        }

        private async void RefreshDataButton_Click(object sender, RoutedEventArgs e)
        {
            if (!(LpPairsComboBox.SelectedItem is PairInfo pair))
            {
                AppendLog("No pair selected to refresh");
                return;
            }

            RefreshDataButton.IsEnabled = false;
            AppendLog($"Refreshing data for {pair.Name}...");

            try
            {
                var rpcUrl = RpcUrlTextBox.Text.Trim();
                var routerAddr = SwapRouterAddressTextBox.Text.Trim();

                // Create a thread-safe logger
                Action<string> threadSafeLogger = (msg) => Dispatcher.Invoke(() => AppendLog(msg));

                // Refresh the pair data
                await _viewModel.LoadPairDetailsAsync(pair, rpcUrl, routerAddr);

                // Trigger the selection changed event to refresh UI
                LpPairsComboBox_SelectionChanged(LpPairsComboBox, null);
                AppendLog($"Data refreshed for {pair.Name}");
            }
            catch (Exception ex)
            {
                AppendLog($"ERROR refreshing data: {ex.Message}");
            }
            finally
            {
                RefreshDataButton.IsEnabled = true;
            }
        }

        private async void SendToDiscordButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPair == null || _currentMetrics == null)
            {
                AppendLog("No pair data available to send. Please select a pair first.");
                return;
            }

            try
            {
                string channelIdText = DiscordChannelIdTextBox?.Text?.Trim();
                if (string.IsNullOrEmpty(channelIdText))
                {
                    AppendLog("ERROR: Please enter a Discord channel ID");
                    return;
                }

                if (!ulong.TryParse(channelIdText, out ulong channelId))
                {
                    AppendLog("ERROR: Invalid Discord channel ID format");
                    return;
                }

                AppendLog($"Sending token information to Discord channel {channelId}...");

                // Get Discord token
                string botToken = "";
                if (_viewModel.SelectedBotConfig != null)
                {
                    botToken = _viewModel.SelectedBotConfig.Token;
                }
                else if (_viewModel.BotConfigs.Any())
                {
                    botToken = _viewModel.BotConfigs.First().Token;
                }

                if (string.IsNullOrEmpty(botToken))
                {
                    AppendLog("ERROR: No bot token available. Please select a bot configuration.");
                    return;
                }

                // Create Discord client with appropriate configuration
                var config = new DiscordSocketConfig
                {
                    GatewayIntents = GatewayIntents.AllUnprivileged
                };

                var discordClient = new DiscordSocketClient(config);

                try
                {
                    await discordClient.LoginAsync(TokenType.Bot, botToken);
                    await discordClient.StartAsync();

                    // Wait for connection
                    int connectionAttempts = 0;
                    while (discordClient.ConnectionState != ConnectionState.Connected && connectionAttempts < 10)
                    {
                        await Task.Delay(1000);
                        connectionAttempts++;
                    }

                    if (discordClient.ConnectionState != ConnectionState.Connected)
                    {
                        AppendLog("ERROR: Failed to connect to Discord");
                        return;
                    }

                    // Find channel and send message
                    var channel = await discordClient.GetChannelAsync(channelId) as IMessageChannel;
                    if (channel == null)
                    {
                        AppendLog($"ERROR: Could not find Discord channel with ID {channelId}");
                        return;
                    }

                    // Create rich embed
                    var embed = CreateTokenEmbed(_currentPair, _currentMetrics);

                    // Send message with embed
                    await channel.SendMessageAsync(
                        text: $"**{_currentPair.Token0.Symbol}/{_currentPair.Token1.Symbol} Token Information**",
                        embed: embed);

                    AppendLog("Successfully sent token details to Discord!");
                }
                finally
                {
                    // Ensure we always disconnect properly
                    await discordClient.StopAsync();
                    await discordClient.DisposeAsync();
                }
            }
            catch (Exception ex)
            {
                AppendLog($"ERROR sending to Discord: {ex.Message}");
                if (ex.InnerException != null)
                    AppendLog($"Inner exception: {ex.InnerException.Message}");
            }
        }

        // Helper method to create a rich Discord embed
        private Embed CreateTokenEmbed(PairInfo pair, Dictionary<string, decimal> metrics)
        {
            var builder = new EmbedBuilder()
                .WithTitle($"{pair.Token0.Symbol}/{pair.Token1.Symbol} Pair Details")
                .WithDescription($"Detailed analytics for {pair.Name} trading pair")
                .WithColor(new Discord.Color(75, 233, 153)) // Green color
                .WithFooter(footer => {
                    footer
                        .WithText($"RadX Price Bot • {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC")
                        .WithIconUrl("https://i.imgur.com/gXdWwTR.png");
                })
                .WithTimestamp(DateTimeOffset.Now);

            // Price Information
            var priceField = new StringBuilder();
            priceField.AppendLine($"**Current Price:** {metrics["Price"]:N6} {pair.Token1.Symbol}");

            if (metrics.ContainsKey("PriceUsd") && metrics["PriceUsd"] > 0)
                priceField.AppendLine($"**USD Price:** ${metrics["PriceUsd"]:N4}");

            decimal reversePrice = metrics["Price"] > 0 ? 1 / metrics["Price"] : 0;
            if (reversePrice > 0)
                priceField.AppendLine($"**Reverse Rate:** {reversePrice:N6} {pair.Token0.Symbol}");

            builder.AddField("💰 Price Information", priceField.ToString(), true);

            // Liquidity Information
            var liquidityField = new StringBuilder();
            liquidityField.AppendLine($"**Total Liquidity:** ${metrics["Liquidity"]:N2}");

            // Add volume information
            if (metrics.ContainsKey("Volume24h"))
                liquidityField.AppendLine($"**24h Volume:** ${metrics["Volume24h"]:N2}");
            else
                liquidityField.AppendLine($"**Est. 24h Volume:** ~${metrics["Liquidity"] * 0.1m:N2}");

            liquidityField.AppendLine($"**{pair.Token0.Symbol} Reserve:** {metrics["Reserve0"]:N2}");
            liquidityField.AppendLine($"**{pair.Token1.Symbol} Reserve:** {metrics["Reserve1"]:N2}");

            builder.AddField("💧 Liquidity Information", liquidityField.ToString(), true);

            // Token Information
            var tokenInfoField = new StringBuilder();
            tokenInfoField.AppendLine($"**Market Cap:** ${metrics["MarketCap"]:N2}");

            if (metrics.ContainsKey("TotalSupply") && metrics["TotalSupply"] > 0)
                tokenInfoField.AppendLine($"**Total Supply:** {metrics["TotalSupply"]:N0} {pair.Token0.Symbol}");

            if (metrics.ContainsKey("CirculatingSupply") && metrics["CirculatingSupply"] > 0)
                tokenInfoField.AppendLine($"**Circulating Supply:** {metrics["CirculatingSupply"]:N0} {pair.Token0.Symbol}");

            if (metrics.ContainsKey("HolderCount") && metrics["HolderCount"] > 0)
                tokenInfoField.AppendLine($"**Holders:** {metrics["HolderCount"]:N0}");

            builder.AddField("📊 Token Information", tokenInfoField.ToString(), false);

            // Contract Addresses
            var addressField = new StringBuilder();
            addressField.AppendLine($"**Pair Address:** `{pair.Address}`");
            addressField.AppendLine($"**{pair.Token0.Symbol} Address:** `{pair.Token0.Address}`");
            addressField.AppendLine($"**{pair.Token1.Symbol} Address:** `{pair.Token1.Address}`");

            builder.AddField("🔗 Contract Information", addressField.ToString(), false);

            return builder.Build();
        }

        // Helper method to format addresses for display
        private string FormatAddress(string address)
        {
            if (string.IsNullOrEmpty(address) || address.Length < 10)
                return address;

            return $"{address.Substring(0, 6)}...{address.Substring(address.Length - 4)}";
        }

        private void BotConfigSelection_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ComboBox comboBox && comboBox.SelectedItem is BotConfig config)
            {
                // Update the UI with the selected bot's configuration
                UpdateMultiBotUIFromConfig(config);
                // Refresh the enable/disable state of the start/stop buttons
                StartSelectedBotButton.IsEnabled = !_viewModel.IsSelectedBotRunning;
                StopSelectedBotButton.IsEnabled = _viewModel.IsSelectedBotRunning;
            }
        }

        private void AppendLog(string message)
        {
            // Check if we need to invoke on UI thread
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(() => AppendLog(message));
                return;
            }

            LogTextBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}\n");
            LogTextBox.ScrollToEnd();

            // Also update multi-log if available
            if (MultiLogTextBox != null)
            {
                MultiLogTextBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}\n");
                MultiLogTextBox.ScrollToEnd();
            }
        }

        // Menu Handler Methods
        private void MenuItem_Settings_Click(object sender, RoutedEventArgs e)
        {
            var settingsWindow = new SettingsWindow(_viewModel.Settings)
            {
                Owner = this
            };

            if (settingsWindow.ShowDialog() == true)
            {
                // Reload settings to UI
                LoadSettingsToUI();
            }
        }

        private void MenuItem_Exit_Click(object sender, RoutedEventArgs e)
        {
            SaveSettingsFromUI(); // Save settings before exiting

            // Stop all bots if any are running
            if (_viewModel.IsAnyBotRunning)
            {
                _viewModel.StopAllBotsAsync().Wait();
            }

            Close();
        }

        private void MenuItem_Documentation_Click(object sender, RoutedEventArgs e)
        {
            string content = @"# RadX Price Bot Documentation

A comprehensive tool for displaying real-time token prices and analytics from decentralized exchanges directly in your Discord server.

## Getting Started

### Initial Setup

1. Create a Discord application and bot at https://discord.com/developers/applications
   - Create a new application in the Discord Developer Portal
   - Navigate to the Bot tab and click 'Add Bot'
   - Under Privileged Gateway Intents, enable 'Presence Intent' and 'Server Members Intent'
   - Copy your bot token for use in the application

2. Add the bot to your Discord server
   - Go to OAuth2 > URL Generator
   - Select 'bot' scope and permissions: 'Change Nickname', 'Send Messages', 'Embed Links'
   - Use the generated URL to add the bot to your server

3. Configure your bot in RadX Price Bot
   - Enter your bot token and guild ID in the Bot Manager tab
   - Set basic information like bot name and nickname
   - Choose a status display type: Price, Reserves, Market Cap, or Custom

### Blockchain Connection Setup

1. Obtain a reliable RPC endpoint URL for your target blockchain
   - Services like Infura, Alchemy, or QuickNode provide RPC endpoints
   - Free tier endpoints may have rate limits that affect bot performance
   - Example format: https://mainnet.infura.io/v3/YOUR_API_KEY

2. Configure contract addresses
   - Factory Address: The DEX factory contract that creates trading pairs
   - Router Address: The DEX router contract that handles swaps
   - Common examples:
     • Uniswap V2 Factory: 0x5C69bEe701ef814a2B6a3EDD4B1652CB9cc5aA6f
     • Uniswap V2 Router: 0x7a250d5630B4cF539739dF2C5dAcb4c659F2488D

3. Load trading pairs and select the pair to monitor
   - Click 'Load Pairs' to retrieve all available pairs from the factory
   - Select a pair from the dropdown to load its metrics
   - Use 'Refresh Data' to update the selected pair information

## Multi-Bot Management

The Bot Manager allows you to create and manage multiple bot instances, each monitoring different token pairs.

### Creating and Managing Bots

1. Add a new bot configuration using the 'Add Bot' button
2. Configure each bot with unique settings:
   - Name: Identifying name for the bot instance
   - Token: Discord bot token (can reuse the same token across bots)
   - Guild ID: Discord server ID where the bot will display information
   - RPC URL: Blockchain endpoint for this specific bot
   - Router Address: DEX router contract for price calculations
   - Token Path: The token pair to monitor (set via 'Use Selected Pair')

3. Starting and stopping bots
   - Use 'Start Selected Bot' to activate the current configuration
   - 'Stop Selected Bot' deactivates only the selected bot
   - 'Stop All Bots' deactivates all running instances
   - The dashboard shows the status of all configured bots

## Advanced Features

### Discord Embed Configuration

Periodic embeds provide detailed token analytics in Discord channels:

1. Enable periodic embeds in the Discord Embeds tab
2. Set the embed interval in minutes
3. Enter the target Discord channel ID
4. Customize appearance:
   - Embed Color: Hex color code for the embed sidebar
   - Include Chart: Adds price chart visualization
   - Include Token Info: Adds supply and holder statistics
   - Include Liquidity Info: Adds pool and volume metrics

### Swap Transaction Monitoring

Monitor and report token swaps in real-time:

1. Enable swap monitoring in the Swap Monitoring tab
2. Enter the Discord channel ID for swap notifications
3. Set the swap check interval (milliseconds)
4. The bot will detect and report:
   - Buy/sell transactions in the monitored pair
   - Transaction size and impact
   - Price impact from swaps

### UI Customization

1. Adjust UI scaling via the dropdown in the upper right corner
2. Available scaling options: 75%, 100%, 125%, 150%, 175%, 200%
3. Settings persist between application restarts

## Technical Reference

### Contract Requirements

The bot requires DEX contracts that implement standard Uniswap V2 interfaces:

- Router Interface:
  • `getAmountsOut(uint amountIn, address[] memory path)`: Calculate output amount
  • `factory()`: Get the factory address

- Factory Interface:
  • `getPair(address tokenA, address tokenB)`: Get the liquidity pair address

- Pair Interface:
  • `getReserves()`: Get current token reserves
  • `token0()`: Get first token address
  • `token1()`: Get second token address

- ERC20 Interface:
  • `symbol()`: Get token symbol
  • `name()`: Get token name
  • `decimals()`: Get token decimal places
  • `totalSupply()`: Get total token supply

### Status Display Types

Configure how the bot appears in Discord:

- **Price**: Shows current token price (e.g., ""$RADX: $0.12345"")
- **Reserves**: Shows liquidity pool reserves (e.g., ""LP: 1.2M RADX | 500K USDC"")
- **Market Cap**: Shows token market capitalization (e.g., ""Market Cap: $12.5M"")
- **Custom**: Define your own status text with variables:
  • {price}: Current token price
  • {symbol0}: Base token symbol
  • {symbol1}: Quote token symbol
  • {liquidity}: Total pool liquidity in USD
  • {marketcap}: Token market capitalization

### Data Storage

The application stores historical data locally:

- Price history for supported tokens
- Liquidity reserves history
- Configuration settings

Database maintenance is automatic with cleanup of old records after 30 days.

## Troubleshooting

- **Connection Issues**: Verify your RPC URL is active and has sufficient rate limits
- **Missing Pairs**: Ensure the factory address is correct for your chosen DEX
- **Discord Errors**: Check that your bot token is valid and has required permissions
- **Incorrect Prices**: Verify token path is in the correct order (typically base token first)
- **Performance Issues**: Consider increasing update intervals or using premium RPC providers

";

            var helpWindow = new HelpWindow("Documentation", content)
            {
                Owner = this
            };
            helpWindow.ShowDialog();
        }


        private void MenuItem_FAQ_Click(object sender, RoutedEventArgs e)
        {
            string content = @"# Frequently Asked Questions

## General Questions

Q: What is the RadX Price Bot?
A: The RadX Price Bot is a Discord bot that displays real-time token prices and metrics from decentralized exchanges directly in your Discord server. It connects to blockchain networks to monitor token pairs and can show information like price, liquidity, market cap, and trading volume.

Q: How do I invite the bot to my server?
A: You need to host this application yourself using your own Discord bot token. This gives you full control over the data sources and configuration. Follow these steps:
   1. Create a Discord application at discord.com/developers/applications
   2. Set up a bot for your application and copy the token
   3. Add the bot to your server using the OAuth2 URL generator
   4. Input your bot token and guild ID in this application
   5. Configure your blockchain connection settings
   6. Start the bot

Q: Can I monitor multiple token pairs at once?
A: Yes! Use the Bot Manager tab to create and configure multiple bot instances, each monitoring a different token pair. You can run them simultaneously with different settings and status displays.

## Features & Configuration

Q: What status information can the bot display?
A: The bot offers several status display options:
   • Price: Shows the current token price (e.g., ""$RADX: $0.12345"")
   • Reserves: Shows the liquidity pool reserves (e.g., ""LP: 1.2M RADX | 500K USDC"")
   • Market Cap: Shows the token market capitalization (e.g., ""Market Cap: $12.5M"")
   • Custom: Any text format you specify

Q: How do I set up Discord embeds?
A: In the Bot Manager tab:
   1. Select a bot configuration
   2. Check ""Send Periodic Embeds"" in the Discord Embeds tab
   3. Enter the Channel ID where embeds should be sent
   4. Set the interval (in minutes)
   5. Choose which information to include (chart, token info, liquidity info)
   6. Click ""Apply Embed Settings"" and ensure the bot is running

Q: What is swap transaction monitoring?
A: This feature tracks token swap transactions in the liquidity pool. When enabled, the bot will detect and report trades in your selected Discord channel. Configure this in the Swap Monitoring tab under Bot Manager.

Q: How do I adjust the UI scaling?
A: Use the UI Scale dropdown in the top-right corner to adjust the application size from 75% to 200%. This is particularly useful for high-DPI displays or to fit more information on smaller screens.

## Technical Questions

Q: What blockchain networks does this support?
A: Any EVM-compatible network that implements Uniswap V2-style contracts, including:
   • Ethereum
   • Polygon
   • BNB Smart Chain
   • Arbitrum
   • Optimism
   • Avalanche C-Chain
   • Base
   • And many others

Q: Why can't I see any pairs after connecting?
A: Check the following:
   1. Verify the factory contract address is correct for the DEX you're connecting to
   2. Ensure your RPC URL is valid and responding
   3. Check that the blockchain network has properly implemented Uniswap V2 interfaces
   4. Look for errors in the log console that might indicate connection problems

Q: How do I find the correct contract addresses?
A: You can find contract addresses from:
   • The DEX's documentation or GitHub repository
   • Blockchain explorers like Etherscan, PolygonScan, etc.
   • Common factory addresses:
     - Uniswap V2: 0x5C69bEe701ef814a2B6a3EDD4B1652CB9cc5aA6f (Ethereum)
     - SushiSwap: 0xC0AEe478e3658e2610c5F7A4A2E1777cE9e4f2Ac (Ethereum)
     - QuickSwap: 0x5757371414417b8C6CAad45bAeF941aBc7d3Ab32 (Polygon)

Q: What's the difference between RPC URL, factory address, and router address?
A: • RPC URL: The endpoint to connect to the blockchain (provided by services like Infura or Alchemy)
   • Factory Address: The contract that creates and stores liquidity pair addresses
   • Router Address: The contract that handles token swaps and provides pricing information

## Troubleshooting

Q: The bot is not responding or connecting to Discord
A: Check these common issues:
   1. Verify your bot token is correct and not expired
   2. Ensure the bot has been added to your server with proper permissions (Send Messages, Embed Links, Change Nickname)
   3. Check that you've enabled the required Gateway Intents in the Discord Developer Portal
   4. Restart the application to refresh the connection
   5. Look for any firewall or network issues blocking Discord connections

Q: Price information is incorrect or not updating
A: Try these solutions:
   1. Make sure you've selected the correct LP pair
   2. Verify the token path is in the right order (typically base token first, quote token second)
   3. Check that your RPC provider is supplying current blockchain data
   4. Try increasing the update interval if you're experiencing rate limiting
   5. Use the ""Refresh Data"" button to force a manual update
   6. Restart the bot if data appears stale

Q: I'm getting RPC errors or timeouts
A: RPC issues are usually related to:
   1. Rate limiting on free tier endpoints - consider using a paid service
   2. Network congestion or outages - try a different RPC provider
   3. Incorrect RPC URL format - double-check for typos
   4. Using the wrong network (e.g., Ethereum RPC for a Polygon token)
   5. Firewall or network restrictions - ensure outbound connections are allowed

Q: The bot keeps disconnecting from Discord
A: This might be caused by:
   1. Discord API rate limits - increase your update intervals
   2. Unstable internet connection - ensure you have reliable connectivity
   3. Memory issues - restart the application periodically
   4. Discord service disruptions - check Discord status page

Q: My bot's nickname isn't changing
A: Ensure the bot has the ""Change Nickname"" permission in your Discord server, and that its role is positioned below the server owner's role in the hierarchy.

## Advanced Usage

Q: Can I customize the embed appearance?
A: Yes, you can:
   1. Change the embed color by entering a hex color code
   2. Toggle which information sections appear
   3. Edit the bot's nickname to change how it appears in the server
   4. Use a custom status format for unique displays

Q: How can I monitor swap transactions more efficiently?
A: To optimize swap monitoring:
   1. Set an appropriate interval based on token activity
   2. For busy pairs, increase the interval to avoid rate limiting
   3. Use a dedicated bot instance just for swap monitoring
   4. Consider using a premium RPC provider for higher throughput

Q: Is there a way to backup my bot configurations?
A: The application automatically saves your settings to a local file. For manual backups, you can copy your settings file from the application directory.";

            var helpWindow = new HelpWindow("Frequently Asked Questions", content)
            {
                Owner = this
            };
            helpWindow.ShowDialog();
        }


        private void MenuItem_About_Click(object sender, RoutedEventArgs e)
        {
            string content = @"# RadX Price Bot

Version 2.3.0
© 2025 RadX Development Team

A comprehensive Discord bot for displaying real-time token prices and analytics from decentralized exchanges.

## Key Features
- 🔄 Real-time price monitoring from blockchain data
- 🤖 Multi-bot support for tracking multiple token pairs simultaneously
- 📊 Rich Discord embeds with detailed token analytics and charts
- 📈 Market statistics including liquidity, volume, and market cap
- 🔔 Swap transaction monitoring and notifications
- 🎨 Customizable presence status and update intervals
- 🌐 Support for any EVM-compatible blockchain network

## Technologies Used
- C# 13.0 / .NET 9 Framework
- WPF with modern UI design principles
- Nethereum for blockchain interaction
- Discord.NET for seamless Discord integration
- Entity Framework Core for data persistence
- Multi-threading for responsive UI and background tasks

## Recent Improvements
- Enhanced Discord embed visualizations
- Improved token data caching for faster performance
- Advanced multi-bot management interface
- Token swap monitoring system
- Dynamic UI scaling

## License
MIT License

## Acknowledgments
Special thanks to the Nethereum and Discord.NET development teams for their excellent libraries.
Thanks to the RadX community for testing and feedback.";

            var helpWindow = new HelpWindow("About RadX Price Bot", content)
            {
                Owner = this
            };
            helpWindow.ShowDialog();
        }


        // Method to apply and save embed settings changes
        private async void ApplyEmbedSettings_Click(object sender, RoutedEventArgs e)
        {
            if (_viewModel.SelectedBotConfig == null)
            {
                AppendLog("No bot configuration selected");
                return;
            }

            try
            {
                // Parse values from UI
                bool sendEmbeds = EnablePeriodicEmbedsCheckBox.IsChecked ?? false;
                int interval = 60;
                int.TryParse(EmbedIntervalTextBox.Text, out interval);
                string channelId = EmbedChannelIdTextBox.Text?.Trim();
                string embedColor = EmbedColorTextBox.Text?.Trim();
                bool includeChart = IncludeChartCheckBox.IsChecked ?? true;
                bool includeTokenInfo = IncludeTokenInfoCheckBox.IsChecked ?? true;
                bool includeLiquidityInfo = IncludeLiquidityInfoCheckBox.IsChecked ?? true;

                // Update the bot's embed settings
                _viewModel.UpdateBotEmbedSettings(
                    _viewModel.SelectedBotConfig.Id,
                    sendEmbeds,
                    interval,
                    channelId,
                    embedColor,
                    includeChart,
                    includeTokenInfo,
                    includeLiquidityInfo);

                AppendLog($"Applied embed settings for bot '{_viewModel.SelectedBotConfig.Name}'");
            }
            catch (Exception ex)
            {
                AppendLog($"ERROR applying embed settings: {ex.Message}");
            }
        }

        // Method to apply and save swap monitoring settings
        private async void ApplySwapSettings_Click(object sender, RoutedEventArgs e)
        {
            if (_viewModel.SelectedBotConfig == null)
            {
                AppendLog("No bot configuration selected");
                return;
            }

            try
            {
                // Parse values from UI
                bool monitorSwaps = EnableSwapMonitoringCheckBox.IsChecked ?? false;
                string channelId = SwapChannelIdTextBox.Text?.Trim();
                int interval = 15000;
                int.TryParse(SwapCheckIntervalTextBox.Text, out interval);

                // Update the bot's swap monitoring settings
                await _viewModel.UpdateBotSwapMonitoringSettings(
                    _viewModel.SelectedBotConfig.Id,
                    monitorSwaps,
                    channelId,
                    interval);

                AppendLog($"Applied swap monitoring settings for bot '{_viewModel.SelectedBotConfig.Name}'");
            }
            catch (Exception ex)
            {
                AppendLog($"ERROR applying swap monitoring settings: {ex.Message}");
            }
        }
    }
}
