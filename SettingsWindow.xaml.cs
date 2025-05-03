// SettingsWindow.xaml.cs
using System;
using System.Windows;
using System.Windows.Controls;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using RadXPriceBot.Services;
using System.Windows.Media;

namespace RadXPriceBot
{
    public partial class SettingsWindow : Window
    {
        private readonly BotSettings _settings;
        private readonly BotSettings _originalSettings;

        public SettingsWindow(BotSettings settings)
        {
            InitializeComponent();
            _settings = settings;
            // Create a deep copy of the original settings for Cancel operation
            _originalSettings = DeepCopySettings(settings);
            LoadSettingsToUI();

            // Subscribe to the Closing event
            this.Closing += SettingsWindow_Closing;

            // Wire up the buttons that are missing handlers
            SetupButtonHandlers();
        }

        private void SetupButtonHandlers()
        {
            // Find the reset button and attach event handler
            var resetButton = FindName("ResetToDefaultsButton") as Button ??
                              FindVisualChild<Button>(this, btn => btn.Content?.ToString() == "Reset to Defaults");
            if (resetButton != null)
                resetButton.Click += ResetToDefaults_Click;

            // Find the view logs button and attach event handler
            var viewLogsButton = FindName("ViewLogsButton") as Button ??
                                FindVisualChild<Button>(this, btn => btn.Content?.ToString() == "View Logs");
            if (viewLogsButton != null)
                viewLogsButton.Click += ViewLogs_Click;

            // Find the clear cache button and attach event handler
            var clearCacheButton = FindName("ClearCacheButton") as Button ??
                                  FindVisualChild<Button>(this, btn => btn.Content?.ToString() == "Clear Cache");
            if (clearCacheButton != null)
                clearCacheButton.Click += ClearCache_Click;
        }

        private T FindVisualChild<T>(DependencyObject parent, Func<T, bool> condition) where T : DependencyObject
        {
            int childCount = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < childCount; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);

                if (child is T typedChild && condition(typedChild))
                    return typedChild;

                var foundChild = FindVisualChild<T>(child, condition);
                if (foundChild != null)
                    return foundChild;
            }
            return null;
        }

        private BotSettings DeepCopySettings(BotSettings source)
        {
            var newSettings = new BotSettings
            {
                // Copy all properties
                BotToken = source.BotToken,
                GuildId = source.GuildId,
                RpcUrl = source.RpcUrl,
                SwapRouterAddress = source.SwapRouterAddress,
                FactoryAddress = source.FactoryAddress,
                BotNickname = source.BotNickname,
                StatusType = source.StatusType,
                CustomStatus = source.CustomStatus,
                ManualPath = source.ManualPath,

                // UI Settings
                DpiScaling = source.DpiScaling,
                MinimizeOnClose = source.MinimizeOnClose,
                StartWithWindows = source.StartWithWindows,
                StartMinimized = source.StartMinimized,

                // Data Settings
                DefaultRefreshInterval = source.DefaultRefreshInterval,
                DefaultChartTimeRange = source.DefaultChartTimeRange,

                // Notification Settings
                EnableNotifications = source.EnableNotifications,
                NotifyOnPriceAlerts = source.NotifyOnPriceAlerts,
                NotifyOnErrors = source.NotifyOnErrors,

                // Advanced Settings
                DebugMode = source.DebugMode,
                SaveLogsToFile = source.SaveLogsToFile
            };

            // Copy BotConfigurations list if it exists
            if (source.BotConfigurations != null && source.BotConfigurations.Count > 0)
            {
                newSettings.BotConfigurations = new List<BotConfig>();
                foreach (var config in source.BotConfigurations)
                {
                    newSettings.BotConfigurations.Add(new BotConfig
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
                        IncludeLiquidityInfoInEmbed = config.IncludeLiquidityInfoInEmbed
                    });
                }
            }

            return newSettings;
        }

        private void SettingsWindow_Closing(object sender, CancelEventArgs e)
        {
            // If MinimizeOnClose is enabled and the DialogResult isn't set (user clicked X)
            if (_settings.MinimizeOnClose && !this.DialogResult.HasValue)
            {
                e.Cancel = true;  // Cancel the close
                this.Hide();      // Hide the window instead
            }
        }

        private void LoadSettingsToUI()
        {
            // Load RPC URL
            if (RpcUrlTextBox != null)
                RpcUrlTextBox.Text = _settings.RpcUrl ?? "https://polygon-mainnet.g.alchemy.com/v2/";

            // Load UI settings
            if (MinimizeOnCloseCheckBox != null)
                MinimizeOnCloseCheckBox.IsChecked = _settings.MinimizeOnClose;

            if (DpiScalingSlider != null)
                DpiScalingSlider.Value = _settings.DpiScaling > 0 ? _settings.DpiScaling : 1.0;

            // Load startup settings
            if (StartWithWindowsCheckBox != null)
                StartWithWindowsCheckBox.IsChecked = _settings.StartWithWindows;

            if (StartMinimizedCheckBox != null)
                StartMinimizedCheckBox.IsChecked = _settings.StartMinimized;

            // Load notification settings
            if (EnableNotificationsCheckBox != null)
                EnableNotificationsCheckBox.IsChecked = _settings.EnableNotifications;

            if (NotifyPriceAlertsCheckBox != null)
                NotifyPriceAlertsCheckBox.IsChecked = _settings.NotifyOnPriceAlerts;

            if (NotifyErrorsCheckBox != null)
                NotifyErrorsCheckBox.IsChecked = _settings.NotifyOnErrors;

            // Load advanced settings
            if (EnableDebugModeCheckBox != null)
                EnableDebugModeCheckBox.IsChecked = _settings.DebugMode ?? false;

            if (SaveLogsToFileCheckBox != null)
                SaveLogsToFileCheckBox.IsChecked = _settings.SaveLogsToFile ?? true;

            // Set values for combo boxes
            SetComboBoxValues();
        }

        private void SetComboBoxValues()
        {
            if (RefreshIntervalComboBox == null || ChartTimeRangeComboBox == null)
                return;

            // Set refresh interval
            int refreshInterval = _settings.DefaultRefreshInterval;
            if (refreshInterval <= 0) refreshInterval = 30; // Default to 30 seconds

            foreach (ComboBoxItem item in RefreshIntervalComboBox.Items)
            {
                if (item.Tag != null && item.Tag.ToString() == refreshInterval.ToString())
                {
                    RefreshIntervalComboBox.SelectedItem = item;
                    break;
                }
            }

            // If nothing was selected, default to 30 seconds
            if (RefreshIntervalComboBox.SelectedItem == null)
            {
                foreach (ComboBoxItem item in RefreshIntervalComboBox.Items)
                {
                    if (item.Tag != null && item.Tag.ToString() == "30")
                    {
                        RefreshIntervalComboBox.SelectedItem = item;
                        break;
                    }
                }
            }

            // Set chart time range
            int chartTimeRange = _settings.DefaultChartTimeRange;
            if (chartTimeRange <= 0) chartTimeRange = 168; // Default to 1 week (168 hours)

            foreach (ComboBoxItem item in ChartTimeRangeComboBox.Items)
            {
                if (item.Tag != null && item.Tag.ToString() == chartTimeRange.ToString())
                {
                    ChartTimeRangeComboBox.SelectedItem = item;
                    break;
                }
            }

            // If nothing was selected, default to 1 week
            if (ChartTimeRangeComboBox.SelectedItem == null)
            {
                foreach (ComboBoxItem item in ChartTimeRangeComboBox.Items)
                {
                    if (item.Tag != null && item.Tag.ToString() == "168")
                    {
                        ChartTimeRangeComboBox.SelectedItem = item;
                        break;
                    }
                }
            }
        }

        private void SaveSettingsFromUI()
        {
            // Save RPC URL
            if (RpcUrlTextBox != null)
                _settings.RpcUrl = RpcUrlTextBox.Text?.Trim();

            // Save UI settings
            if (MinimizeOnCloseCheckBox != null)
                _settings.MinimizeOnClose = MinimizeOnCloseCheckBox.IsChecked ?? true;

            if (DpiScalingSlider != null)
                _settings.DpiScaling = DpiScalingSlider.Value;

            // Save startup settings
            if (StartWithWindowsCheckBox != null)
                _settings.StartWithWindows = StartWithWindowsCheckBox.IsChecked ?? false;

            if (StartMinimizedCheckBox != null)
                _settings.StartMinimized = StartMinimizedCheckBox.IsChecked ?? false;

            // Save notification settings
            if (EnableNotificationsCheckBox != null)
                _settings.EnableNotifications = EnableNotificationsCheckBox.IsChecked ?? true;

            if (NotifyPriceAlertsCheckBox != null)
                _settings.NotifyOnPriceAlerts = NotifyPriceAlertsCheckBox.IsChecked ?? true;

            if (NotifyErrorsCheckBox != null)
                _settings.NotifyOnErrors = NotifyErrorsCheckBox.IsChecked ?? true;

            // Save advanced settings
            if (EnableDebugModeCheckBox != null)
                _settings.DebugMode = EnableDebugModeCheckBox.IsChecked;

            if (SaveLogsToFileCheckBox != null)
                _settings.SaveLogsToFile = SaveLogsToFileCheckBox.IsChecked;

            // Save combo box selections
            if (RefreshIntervalComboBox?.SelectedItem is ComboBoxItem refreshItem && refreshItem.Tag != null)
            {
                if (int.TryParse(refreshItem.Tag.ToString(), out int refreshInterval))
                {
                    _settings.DefaultRefreshInterval = refreshInterval;
                }
            }

            if (ChartTimeRangeComboBox?.SelectedItem is ComboBoxItem chartItem && chartItem.Tag != null)
            {
                if (int.TryParse(chartItem.Tag.ToString(), out int chartTimeRange))
                {
                    _settings.DefaultChartTimeRange = chartTimeRange;
                }
            }

            // Save settings to disk
            SettingsService.SaveSettings(_settings);
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            SaveSettingsFromUI();
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            // Restore original settings
            RestoreSettings(_originalSettings, _settings);
            DialogResult = false;
            Close();
        }

        private void RestoreSettings(BotSettings source, BotSettings target)
        {
            // Copy all properties back to the target
            target.BotToken = source.BotToken;
            target.GuildId = source.GuildId;
            target.RpcUrl = source.RpcUrl;
            target.SwapRouterAddress = source.SwapRouterAddress;
            target.FactoryAddress = source.FactoryAddress;
            target.BotNickname = source.BotNickname;
            target.StatusType = source.StatusType;
            target.CustomStatus = source.CustomStatus;
            target.ManualPath = source.ManualPath;

            // UI Settings
            target.DpiScaling = source.DpiScaling;
            target.MinimizeOnClose = source.MinimizeOnClose;
            target.StartWithWindows = source.StartWithWindows;
            target.StartMinimized = source.StartMinimized;

            // Data Settings
            target.DefaultRefreshInterval = source.DefaultRefreshInterval;
            target.DefaultChartTimeRange = source.DefaultChartTimeRange;

            // Notification Settings
            target.EnableNotifications = source.EnableNotifications;
            target.NotifyOnPriceAlerts = source.NotifyOnPriceAlerts;
            target.NotifyOnErrors = source.NotifyOnErrors;

            // Advanced Settings
            target.DebugMode = source.DebugMode;
            target.SaveLogsToFile = source.SaveLogsToFile;

            // Restore BotConfigurations
            target.BotConfigurations = source.BotConfigurations;
        }

        private void ResetToDefaults_Click(object sender, RoutedEventArgs e)
        {
            MessageBoxResult result = MessageBox.Show(
                "Are you sure you want to reset all settings to their default values?",
                "Reset Settings",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                // Create new settings object with default values
                var defaultSettings = new BotSettings();

                // Preserve bot configurations and essential connection data
                defaultSettings.BotConfigurations = _settings.BotConfigurations;
                defaultSettings.RpcUrl = _settings.RpcUrl;

                // Update UI with default values
                _settings.DpiScaling = defaultSettings.DpiScaling;
                _settings.MinimizeOnClose = defaultSettings.MinimizeOnClose;
                _settings.StartWithWindows = defaultSettings.StartWithWindows;
                _settings.StartMinimized = defaultSettings.StartMinimized;
                _settings.DefaultRefreshInterval = defaultSettings.DefaultRefreshInterval;
                _settings.DefaultChartTimeRange = defaultSettings.DefaultChartTimeRange;
                _settings.EnableNotifications = defaultSettings.EnableNotifications;
                _settings.NotifyOnPriceAlerts = defaultSettings.NotifyOnPriceAlerts;
                _settings.NotifyOnErrors = defaultSettings.NotifyOnErrors;
                _settings.DebugMode = defaultSettings.DebugMode;
                _settings.SaveLogsToFile = defaultSettings.SaveLogsToFile;

                // Refresh UI
                LoadSettingsToUI();
            }
        }

        private void ViewLogs_Click(object sender, RoutedEventArgs e)
        {
            string logDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "RadXPriceBot", "Logs");

            // Create the directory if it doesn't exist
            if (!Directory.Exists(logDirectory))
            {
                Directory.CreateDirectory(logDirectory);
                MessageBox.Show("Log directory created. No logs found.", "Logs", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Try to open the logs directory
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = logDirectory,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Cannot open logs directory: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);

                // Fallback - show logs in a simple window
                ShowLogsWindow(logDirectory);
            }
        }

        private void ShowLogsWindow(string logDirectory)
        {
            var logFiles = Directory.GetFiles(logDirectory, "*.log");
            if (logFiles.Length == 0)
            {
                MessageBox.Show("No log files found.", "Logs", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Create a simple window to display log files
            var logWindow = new Window
            {
                Title = "Log Files",
                Width = 600,
                Height = 400,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this
            };

            var grid = new Grid();
            logWindow.Content = grid;

            var listView = new ListView();
            grid.Children.Add(listView);

            foreach (var file in logFiles)
            {
                var item = new ListViewItem
                {
                    Content = Path.GetFileName(file),
                    Tag = file
                };
                listView.Items.Add(item);
            }

            // Double-click to view log file content
            listView.MouseDoubleClick += (s, e) =>
            {
                if (listView.SelectedItem is ListViewItem selectedItem)
                {
                    try
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = selectedItem.Tag.ToString(),
                            UseShellExecute = true
                        });
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Cannot open log file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            };

            logWindow.ShowDialog();
        }

        private void ClearCache_Click(object sender, RoutedEventArgs e)
        {
            MessageBoxResult result = MessageBox.Show(
                "Are you sure you want to clear all application cache? This will remove temporary files but not your settings.",
                "Clear Cache",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    // Clear temporary files
                    string cacheDirectory = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                        "RadXPriceBot", "Cache");

                    if (Directory.Exists(cacheDirectory))
                    {
                        Directory.Delete(cacheDirectory, true);
                        Directory.CreateDirectory(cacheDirectory);
                    }

                    MessageBox.Show("Cache cleared successfully.", "Cache Cleared", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error clearing cache: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }
}
