// Constants.cs
namespace RadXPriceBot
{
    /// <summary>
    /// Application-wide constants to eliminate magic numbers and improve maintainability.
    /// </summary>
    public static class Constants
    {
        #region UI Constants
        
        /// <summary>
        /// Default minimum window width in pixels.
        /// </summary>
        public const double DefaultMinWindowWidth = 800;
        
        /// <summary>
        /// Default minimum window height in pixels.
        /// </summary>
        public const double DefaultMinWindowHeight = 600;
        
        /// <summary>
        /// Maximum Discord nickname length.
        /// </summary>
        public const int MaxDiscordNicknameLength = 32;
        
        /// <summary>
        /// Maximum Discord presence status length.
        /// </summary>
        public const int MaxDiscordPresenceLength = 128;
        
        #endregion

        #region Timing Constants
        
        /// <summary>
        /// Default bot update interval in milliseconds (30 seconds).
        /// </summary>
        public const int DefaultBotUpdateIntervalMs = 30000;
        
        /// <summary>
        /// Default swap monitoring interval in milliseconds (15 seconds).
        /// </summary>
        public const int DefaultSwapMonitoringIntervalMs = 15000;
        
        /// <summary>
        /// Default embed sending interval in minutes (60 minutes).
        /// </summary>
        public const int DefaultEmbedIntervalMinutes = 60;
        
        /// <summary>
        /// Minimum update interval to prevent rate limiting (5 seconds).
        /// </summary>
        public const int MinimumUpdateIntervalMs = 5000;
        
        /// <summary>
        /// Discord connection timeout in seconds.
        /// </summary>
        public const int DiscordConnectionTimeoutSeconds = 10;
        
        /// <summary>
        /// Default price history retention in days.
        /// </summary>
        public const int DefaultPriceHistoryRetentionDays = 30;
        
        #endregion

        #region Network Constants
        
        /// <summary>
        /// Maximum stored transaction hashes to prevent memory issues.
        /// </summary>
        public const int MaxStoredTransactionHashes = 100;
        
        /// <summary>
        /// Default decimal precision for token amounts.
        /// </summary>
        public const int DefaultTokenDecimals = 18;
        
        #endregion

        #region Financial Constants
        
        /// <summary>
        /// Default minimum buy transaction threshold in USD.
        /// </summary>
        public const decimal DefaultMinBuyThresholdUsd = 10.0m;
        
        /// <summary>
        /// Default minimum sell transaction threshold in USD.
        /// </summary>
        public const decimal DefaultMinSellThresholdUsd = 10.0m;
        
        #endregion

        #region Format Constants
        
        /// <summary>
        /// Default embed color in hex format.
        /// </summary>
        public const string DefaultEmbedColor = "#50E999";
        
        /// <summary>
        /// Default currency formatting for USD amounts.
        /// </summary>
        public const string UsdCurrencyFormat = "N2";
        
        /// <summary>
        /// Default token amount formatting.
        /// </summary>
        public const string TokenAmountFormat = "N6";
        
        #endregion

        #region Database Constants
        
        /// <summary>
        /// Maximum price history points to return in queries.
        /// </summary>
        public const int MaxPriceHistoryPoints = 1000;
        
        #endregion

        #region Token Addresses (Network-specific)
        
        /// <summary>
        /// USDC Polygon token address.
        /// </summary>
        public const string UsdcPolygonAddress = "0xbCfB3FCa16b12C7756CD6C24f1cC0AC0E38569CF";
        
        /// <summary>
        /// VTRO token address.
        /// </summary>
        public const string VtroAddress = "0xDECAF2f187Cb837a42D26FA364349Abc3e80Aa5D";
        
        /// <summary>
        /// WVTRU token address.
        /// </summary>
        public const string WvtruAddress = "0x3ccc3F22462cAe34766820894D04a40381201ef9";
        
        #endregion
    }
}