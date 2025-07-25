// Services/SettingsService.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Windows;

namespace RadXPriceBot.Services
{
    public class BotConfig
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Name { get; set; } = "New Bot";
        public string Token { get; set; } = "";
        public string GuildId { get; set; } = "";
        public string RpcUrl { get; set; } = "";
        public string SwapRouterAddress { get; set; } = "";
        public List<string> Path { get; set; } = new List<string>();
        public string Nickname { get; set; } = "";
        public string StatusType { get; set; } = "Price";
        public string CustomStatus { get; set; } = "";
        public int UpdateIntervalSeconds { get; set; } = 30;

        public bool SendPeriodicEmbeds { get; set; } = true;
        public int EmbedIntervalMinutes { get; set; } = Constants.DefaultEmbedIntervalMinutes;  // Default: once per hour
        public string EmbedChannelId { get; set; } = "";     // Discord channel ID to send embeds to
        public string EmbedColor { get; set; } = Constants.DefaultEmbedColor;  // Default color in hex format
        public bool IncludeChartInEmbed { get; set; } = true;
        public bool IncludeTokenInfoInEmbed { get; set; } = true;
        public bool IncludeLiquidityInfoInEmbed { get; set; } = true;
        // New properties for swap monitoring
        public bool MonitorSwapTransactions { get; set; } = true;  // Enable by default
        public string SwapNotificationChannelId { get; set; } = ""; // Discord channel ID for swap notifications
        public int SwapCheckIntervalMs { get; set; } = Constants.DefaultSwapMonitoringIntervalMs;

        // Add these new properties for minimum thresholds
        public decimal MinimumBuyThresholdUsd { get; set; } = Constants.DefaultMinBuyThresholdUsd;
        public decimal MinimumSellThresholdUsd { get; set; } = Constants.DefaultMinSellThresholdUsd;
        public bool NotifyOnBuys { get; set; } = true;  // Whether to notify on buy transactions
        public bool NotifyOnSells { get; set; } = true; // Whether to notify on sell transactions
    }

    public class BotSettings
    {
        // Original properties for backward compatibility
        public string BotToken { get; set; } = "";
        public string GuildId { get; set; } = "";
        public string RpcUrl { get; set; } = "https://polygon-mainnet.g.alchemy.com/v2/";
        public string SwapRouterAddress { get; set; } = "";
        public string FactoryAddress { get; set; } = "";
        public string BotNickname { get; set; } = "";
        public string StatusType { get; set; } = "Price";
        public string CustomStatus { get; set; } = "";
        public string ManualPath { get; set; } = "";

        // New property to store multiple bot configurations
        public List<BotConfig> BotConfigurations { get; set; } = new List<BotConfig>();
        // Add this property to BotSettings class
        // UI Settings
        public double DpiScaling { get; set; } = 1.0;
        public bool MinimizeOnClose { get; set; } = true;
        public bool StartWithWindows { get; set; } = false;
        public bool StartMinimized { get; set; } = false;

        // Data Settings
        public int DefaultRefreshInterval { get; set; } = 45; // In seconds
        public int DefaultChartTimeRange { get; set; } = 168; // In hours (1 week)

        // Notification Settings
        public bool EnableNotifications { get; set; } = true;
        public bool NotifyOnPriceAlerts { get; set; } = true;
        public bool NotifyOnErrors { get; set; } = true;

        // Advanced Settings
        public bool? DebugMode { get; set; } = false;
        public bool? SaveLogsToFile { get; set; } = true;
        public bool UseDbCache { get; set; } = true;
        public int PriceHistoryRetentionDays { get; set; } = Constants.DefaultPriceHistoryRetentionDays;
        public bool EnableHistoricalDataCollection { get; set; } = true;
        public int MaxPriceHistoryPointsToReturn { get; set; } = Constants.MaxPriceHistoryPoints;
    }

    public static class SettingsService
    {
        private static readonly string SettingsFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "RadXPriceBot",
            "settings.json");

        private static readonly Action<string> DefaultLogger = msg => Console.WriteLine($"[SettingsService] {msg}");

        public static void SaveSettings(BotSettings settings, Action<string> logger = null)
        {
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));

            var log = logger ?? DefaultLogger;

            try
            {
                log("Saving settings...");
                
                string directoryPath = Path.GetDirectoryName(SettingsFilePath);
                if (!Directory.Exists(directoryPath))
                {
                    Directory.CreateDirectory(directoryPath);
                    log($"Created settings directory: {directoryPath}");
                }

                var options = new JsonSerializerOptions 
                { 
                    WriteIndented = true,
                    DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
                };
                
                string jsonString = JsonSerializer.Serialize(settings, options);
                File.WriteAllText(SettingsFilePath, jsonString);
                
                log($"Settings saved successfully to: {SettingsFilePath}");
            }
            catch (Exception ex)
            {
                var errorMessage = $"Failed to save settings: {ex.Message}";
                log($"ERROR: {errorMessage}");
                
                // Only show MessageBox if in a WPF context
                if (System.Windows.Application.Current != null)
                {
                    MessageBox.Show(errorMessage, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                
                throw; // Re-throw to allow caller to handle
            }
        }

        public static BotSettings LoadSettings(Action<string> logger = null)
        {
            var log = logger ?? DefaultLogger;

            try
            {
                if (File.Exists(SettingsFilePath))
                {
                    log($"Loading settings from: {SettingsFilePath}");
                    
                    string jsonString = File.ReadAllText(SettingsFilePath);
                    
                    if (string.IsNullOrWhiteSpace(jsonString))
                    {
                        log("Settings file is empty, returning default settings");
                        return new BotSettings();
                    }

                    var settings = JsonSerializer.Deserialize<BotSettings>(jsonString);
                    log("Settings loaded successfully");
                    return settings ?? new BotSettings();
                }
                else
                {
                    log("Settings file not found, returning default settings");
                }
            }
            catch (Exception ex)
            {
                var errorMessage = $"Failed to load settings: {ex.Message}";
                log($"ERROR: {errorMessage}");
                
                // Only show MessageBox if in a WPF context
                if (System.Windows.Application.Current != null)
                {
                    MessageBox.Show(errorMessage, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                
                log("Returning default settings due to error");
            }

            return new BotSettings();
        }

        public static string GetSettingsFilePath() => SettingsFilePath;

        public static bool SettingsFileExists() => File.Exists(SettingsFilePath);
    }
}
