using System;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Net;
using Discord.WebSocket;
using System.Timers;
using System.Collections.Generic;
using System.Threading;
using System.Text;

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
        private const int UPDATE_INTERVAL_MS = 30000; // Update every 30 seconds
        private TokenInfo _token0Info;
        private TokenInfo _token1Info;
        private CancellationTokenSource _cts;
        private bool _isRunning;

        // Properties for embed functionality
        private ulong _embedChannelId;
        private int _embedIntervalMinutes = 0; // 0 means disabled
        private DateTime _lastEmbedSent = DateTime.MinValue; // For rate limiting
        private string _embedColor = "#50E999"; // Default embed color
        private bool _includeChartInEmbed = true;
        private bool _includeTokenInfoInEmbed = true;
        private bool _includeLiquidityInfoInEmbed = true;

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
            _priceSvc = priceSvc;
            _guildId = ulong.Parse(guildId);
            _nickname = nickname;
            _statusType = statusType;
            _customStatus = customStatus;
            _updateIntervalMs = updateIntervalMs;
            _isRunning = false;
        }

        public async Task StartAsync()
        {
            if (_isRunning)
            {
                OnLog("Bot is already running");
                return;
            }

            _cts = new CancellationTokenSource();
            _client = new DiscordSocketClient(new DiscordSocketConfig
            {
                GatewayIntents = GatewayIntents.Guilds,
                LogLevel = LogSeverity.Info
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
        }

        // Report status updates
        public void ReportStatus(Dictionary<string, decimal> metrics, PairInfo pairInfo)
        {
            StatusUpdated?.Invoke(this, new BotStatusUpdateEventArgs { Metrics = metrics, PairInfo = pairInfo });
        }

        private void SetupUpdateTimer()
        {
            _updateTimer = new System.Timers.Timer(_updateIntervalMs);
            _updateTimer.Elapsed += async (s, e) =>
            {
                try
                {
                    await UpdateBotNicknameAsync();
                    await UpdateBotStatusAsync();
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

        private void SetupEmbedTimer()
        {
            // Only setup if both interval and channel are configured
            if (_embedIntervalMinutes > 0 && _embedChannelId > 0)
            {
                _embedTimer = new System.Timers.Timer(_embedIntervalMinutes * 60 * 1000);
                _embedTimer.Elapsed += async (s, e) =>
                {
                    try
                    {
                        await SendPeriodicEmbed();
                    }
                    catch (Exception ex)
                    {
                        OnLog($"Error sending periodic embed: {ex.Message}");
                    }
                };
                _embedTimer.AutoReset = true;
                _embedTimer.Start();
                OnLog($"Embed timer started (interval: {_embedIntervalMinutes} minutes)");

                // Send an initial embed after startup
                _ = Task.Run(async () =>
                {
                    await Task.Delay(5000); // Wait 5 seconds after startup
                    await SendPeriodicEmbed();
                });
            }
        }

        // Configure automated embeds
        public void SetEmbedInterval(int minutes, ulong channelId)
        {
            OnLog($"Setting embed interval to {minutes} minutes for channel {channelId}");

            // Clean up old timer
            _embedTimer?.Stop();
            _embedTimer?.Dispose();
            _embedTimer = null;

            _embedIntervalMinutes = minutes;
            _embedChannelId = channelId;

            // Disable if minutes is 0 or channelId is 0
            if (minutes <= 0 || channelId == 0)
            {
                OnLog("Automatic embeds disabled");
                return;
            }

            // Create a new timer
            _embedTimer = new System.Timers.Timer(minutes * 60 * 1000);
            _embedTimer.Elapsed += async (s, e) => await SendPeriodicEmbed();
            _embedTimer.AutoReset = true;
            _embedTimer.Start();

            OnLog($"Embed timer set to trigger every {minutes} minutes");

            // Send an initial embed
            _ = Task.Run(async () =>
            {
                await Task.Delay(2000); // Small delay to ensure bot is ready
                await SendPeriodicEmbed();
            });
        }

        // Overload with additional embed customization options
        public void SetEmbedInterval(int minutes, ulong channelId,
            bool includeChart = true,
            bool includeTokenInfo = true,
            bool includeLiquidityInfo = true,
            string embedColor = "#50E999")
        {
            _includeChartInEmbed = includeChart;
            _includeTokenInfoInEmbed = includeTokenInfo;
            _includeLiquidityInfoInEmbed = includeLiquidityInfo;
            _embedColor = embedColor;

            // Call the original method to configure the timer
            SetEmbedInterval(minutes, channelId);
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

        private async Task SendPeriodicEmbed()
        {
            try
            {
                if (_client == null || _client.ConnectionState != ConnectionState.Connected)
                {
                    OnLog("Cannot send embed: Discord client not connected");
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

                // Find the channel and send the embed
                var channel = await _client.GetChannelAsync(_embedChannelId) as IMessageChannel;
                if (channel == null)
                {
                    OnLog($"Could not find channel with ID {_embedChannelId}");
                    return;
                }

                var embed = CreateDetailedPriceEmbed(metrics, pairDetails.token0Info, pairDetails.token1Info, pairDetails.pairAddress);
                await channel.SendMessageAsync(embed: embed);

                _lastEmbedSent = DateTime.UtcNow;
                OnLog("Periodic embed sent successfully");
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

        private Embed CreateDetailedPriceEmbed(Dictionary<string, decimal> metrics, TokenInfo token0, TokenInfo token1, string pairAddress)
        {
            // Parse the custom color or use default green if invalid
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

            var builder = new EmbedBuilder()
                .WithTitle($"{token0.Symbol}/{token1.Symbol} Market Update")
                .WithDescription($"Current data for the {token0.Symbol}/{token1.Symbol} trading pair")
                .WithColor(color)
                .WithFooter(footer => {
                    footer
                        .WithText($"RadX Price Bot • {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC")
                        .WithIconUrl("https://i.imgur.com/gXdWwTR.png");
                })
                .WithTimestamp(DateTimeOffset.Now);

            // Price Information - always included
            var priceField = new StringBuilder();
            priceField.AppendLine($"**Current Price:** {metrics["Price"]:N6} {token1.Symbol}");

            if (metrics.ContainsKey("PriceUsd") && metrics["PriceUsd"] > 0)
                priceField.AppendLine($"**USD Price:** ${metrics["PriceUsd"]:N4}");

            decimal reversePrice = metrics["Price"] > 0 ? 1 / metrics["Price"] : 0;
            if (reversePrice > 0)
                priceField.AppendLine($"**Reverse Rate:** {reversePrice:N6} {token0.Symbol}");

            builder.AddField("💰 Price Information", priceField.ToString(), true);

            // Liquidity Information - conditionally included
            if (_includeLiquidityInfoInEmbed)
            {
                var liquidityField = new StringBuilder();
                liquidityField.AppendLine($"**Total Liquidity:** ${FormatLargeNumber(metrics["Liquidity"])}");

                // Volume information if available
                if (metrics.ContainsKey("Volume24h"))
                    liquidityField.AppendLine($"**24h Volume:** ${FormatLargeNumber(metrics["Volume24h"])}");

                liquidityField.AppendLine($"**{token0.Symbol} Reserve:** {FormatLargeNumber(metrics["Reserve0"])}");
                liquidityField.AppendLine($"**{token1.Symbol} Reserve:** {FormatLargeNumber(metrics["Reserve1"])}");

                builder.AddField("💧 Liquidity Information", liquidityField.ToString(), true);
            }

            // Token Information - conditionally included
            if (_includeTokenInfoInEmbed)
            {
                var tokenInfoField = new StringBuilder();
                tokenInfoField.AppendLine($"**Market Cap:** ${FormatLargeNumber(metrics["MarketCap"])}");

                if (metrics.ContainsKey("TotalSupply") && metrics["TotalSupply"] > 0)
                    tokenInfoField.AppendLine($"**Total Supply:** {FormatLargeNumber(metrics["TotalSupply"])} {token0.Symbol}");

                if (metrics.ContainsKey("CirculatingSupply") && metrics["CirculatingSupply"] > 0)
                    tokenInfoField.AppendLine($"**Circulating Supply:** {FormatLargeNumber(metrics["CirculatingSupply"])} {token0.Symbol}");

                builder.AddField("📊 Token Information", tokenInfoField.ToString(), false);
            }

            // Contract Information - always included
            var addressField = new StringBuilder();
            addressField.AppendLine($"**Pair Address:** `{pairAddress}`");
            addressField.AppendLine($"**{token0.Symbol} Address:** `{token0.Address}`");
            addressField.AppendLine($"**{token1.Symbol} Address:** `{token1.Address}`");

            builder.AddField("🔗 Contract Information", addressField.ToString(), false);

            // Chart - conditionally included
            if (_includeChartInEmbed)
            {
                // In a real implementation, you would generate and include a chart image
                // For now, we'll just include a placeholder URL or comment about it
                // builder.WithImageUrl("https://chart-url-here.com");
            }

            return builder.Build();
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

            _priceSvc = newPriceService;

            if (!string.IsNullOrEmpty(newNickname))
            {
                _nickname = newNickname;
            }

            // Refresh token information and update displays
            await LoadTokenInformationAsync();
            await UpdateBotNicknameAsync();
            await UpdateBotStatusAsync();

            OnLog("Bot token pair updated successfully");
        }

        private Task LogAsync(LogMessage msg)
        {
            OnLog(msg.ToString());
            return Task.CompletedTask;
        }

        private async Task ReadyAsync()
        {
            OnLog("Client ready. Registering commands...");
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

        private async Task ConfigureBotAsync()
        {
            // Load token information
            await LoadTokenInformationAsync();

            // Update nickname and status
            await UpdateBotNicknameAsync();
            await UpdateBotStatusAsync();
        }

        private async Task LoadTokenInformationAsync()
        {
            try
            {
                // Get pair details and token information
                var (_, token0, token1) = await _priceSvc.GetPairDetailsAsync();
                _token0Info = token0;
                _token1Info = token1;

                // Get USD prices
                var usdPrice = await _priceSvc.GetTokenUsdPriceAsync(_token0Info.Address);
                _token0Info.UsdPrice = usdPrice;

                OnLog($"Loaded token information: {_token0Info.Symbol}/{_token1Info.Symbol} (${usdPrice:N6} USD)");
            }
            catch (Exception ex)
            {
                OnLog($"Failed to load token information: {ex.Message}");
                if (ex.InnerException != null)
                {
                    OnLog($"Inner error: {ex.InnerException.Message}");
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

                    string botName;
                    if (usdPrice > 0)
                    {
                        // Include USD price in nickname
                        if (usdPrice < 0.0001m)
                            botName = $"{_token0Info.Symbol} ${usdPrice.ToString("E4")}"; // Scientific notation for very small values
                        else
                            botName = $"{_token0Info.Symbol} ${usdPrice:N4}";

                        // Add market cap indicator
                        if (marketCap > 0)
                        {
                            string mcapIndicator = FormatLargeNumber(marketCap);
                            botName = $"{_token0Info.Symbol} ${usdPrice:N4} | MCap: {mcapIndicator}";

                            // If nickname would be too long, simplify it
                            if (botName.Length > 32)
                            {
                                botName = $"{_token0Info.Symbol} ${usdPrice:N4}";
                            }
                        }
                    }
                    else if (price > 0)
                    {
                        // Use pair price if USD price isn't available
                        botName = $"{_token0Info.Symbol} {price:N6} {_token1Info.Symbol}";
                    }
                    else
                    {
                        botName = $"{_token0Info.Symbol} Bot";
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
                // Get metrics for status display
                var metrics = await _priceSvc.GetTokenMetricsAsync();
                string statusText;

                switch (_statusType?.ToLower())
                {
                    case "price":
                        var price = metrics.ContainsKey("Price") ? metrics["Price"] : 0;
                        var usdPrice = metrics.ContainsKey("PriceUsd") ? metrics["PriceUsd"] : 0;

                        // Format based on price magnitude
                        string priceDisplay = FormatPrice(price);
                        string usdDisplay = usdPrice > 0 ? $"${FormatPrice(usdPrice)}" : "";

                        if (!string.IsNullOrEmpty(usdDisplay))
                            statusText = $"{_token0Info?.Symbol} {priceDisplay} ({usdDisplay})";
                        else
                            statusText = $"{_token0Info?.Symbol} {priceDisplay}";
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
                        var marketCap = metrics.ContainsKey("MarketCap") ? metrics["MarketCap"] : 0;
                        statusText = $"MCap: ${FormatLargeNumber(marketCap)}";
                        break;

                    case "custom":
                        if (!string.IsNullOrWhiteSpace(_customStatus))
                        {
                            // Allow placeholders in custom status
                            string customText = _customStatus
                                .Replace("{symbol}", _token0Info?.Symbol ?? "Token")
                                .Replace("{price}", FormatPrice(metrics.ContainsKey("Price") ? metrics["Price"] : 0))
                                .Replace("{usdprice}", FormatPrice(metrics.ContainsKey("PriceUsd") ? metrics["PriceUsd"] : 0))
                                .Replace("{mcap}", FormatLargeNumber(metrics.ContainsKey("MarketCap") ? metrics["MarketCap"] : 0));

                            statusText = customText;
                        }
                        else
                        {
                            statusText = $"{_token0Info?.Symbol} Price Bot";
                        }
                        break;

                    default:
                        // Default format with the most useful information
                        var defaultPrice = await _priceSvc.GetPriceAsync();
                        var defaultUsdPrice = metrics.ContainsKey("PriceUsd") ? metrics["PriceUsd"] : 0;

                        if (defaultUsdPrice > 0)
                            statusText = $"{_token0Info?.Symbol}: ${FormatPrice(defaultUsdPrice)}";
                        else
                            statusText = $"{_token0Info?.Symbol}: {FormatPrice(defaultPrice)} {_token1Info?.Symbol}";
                        break;
                }

                // Set the activity type based on status type
                var activityType = GetActivityTypeForStatus(_statusType);
                await _client.SetActivityAsync(new Game(statusText, activityType));
                OnLog($"Updated bot status → {statusText} ({activityType})");
            }
            catch (Exception ex)
            {
                OnLog($"Failed to update status: {ex.Message}");
            }
        }

        private ActivityType GetActivityTypeForStatus(string statusType)
        {
            return statusType?.ToLower() switch
            {
                "price" => ActivityType.Watching,
                "reserves" => ActivityType.Playing,
                "market cap" => ActivityType.Watching,
                "custom" => ActivityType.Playing,
                _ => ActivityType.Watching
            };
        }

        private string FormatPrice(decimal price)
        {
            // Format price based on magnitude
            if (price == 0) return "0";

            if (price < 0.00000001m)
                return price.ToString("E6"); // Scientific notation for extremely small values
            else if (price < 0.0001m)
                return price.ToString("0.00000000"); // Show 8 decimals for very small values
            else if (price < 0.01m)
                return price.ToString("0.000000"); // 6 decimals for small values
            else if (price < 1m)
                return price.ToString("0.0000"); // 4 decimals for values under 1
            else if (price < 1000)
                return price.ToString("0.00"); // 2 decimals for normal values
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

                // Reload token information
                await LoadTokenInformationAsync();
                await UpdateBotNicknameAsync();
                await UpdateBotStatusAsync();

                var embedBuilder = new EmbedBuilder()
                    .WithTitle("Token Data Refreshed")
                    .WithColor(Color.Green)
                    .WithCurrentTimestamp()
                    .WithDescription($"Successfully refreshed data for {_token0Info?.Symbol}/{_token1Info?.Symbol}");

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

                var embedBuilder = new EmbedBuilder()
                    .WithTitle($"{_token0Info?.Symbol ?? "Token"} Price")
                    .WithColor(Color.Green)
                    .WithCurrentTimestamp()
                    .WithFooter("Data from Vitruveo Network");

                embedBuilder.AddField($"{amt} {_token0Info?.Symbol ?? "Token"} =", $"{price:N6} {_token1Info?.Symbol ?? ""}");

                if (usdPrice > 0)
                {
                    embedBuilder.AddField("USD Value", $"${usdPrice:N4}");
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

                var embedBuilder = new EmbedBuilder()
                    .WithTitle($"{_token0Info?.Symbol ?? "Token"} Market Statistics")
                    .WithColor(Color.Blue)
                    .WithCurrentTimestamp()
                    .WithFooter($"Data from Vitruveo Network • {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC");

                embedBuilder
                    .AddField("Market Cap", $"${FormatLargeNumber(marketCap)}", true)
                    .AddField("Price", $"${price:N6}", true)
                    .AddField("Liquidity", $"${FormatLargeNumber(liquidity)}", true)
                    .AddField("Total Supply", $"{FormatLargeNumber(totalSupply)} {_token0Info?.Symbol ?? "tokens"}", false);

                if (_token0Info != null)
                {
                    embedBuilder.AddField("Contract", _token0Info.Address, false);
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

                var embedBuilder = new EmbedBuilder()
                    .WithTitle($"{_token0Info?.Symbol ?? "Token"}/{_token1Info?.Symbol ?? "Token"} Liquidity")
                    .WithColor(Color.Gold)
                    .WithCurrentTimestamp()
                    .AddField("Total Liquidity", $"${FormatLargeNumber(liquidity)}")
                    .AddField($"{_token0Info?.Symbol ?? "Token0"} Reserve", $"{FormatLargeNumber(reserve0)} (${FormatLargeNumber(reserve0Usd)})")
                    .AddField($"{_token1Info?.Symbol ?? "Token1"} Reserve", $"{FormatLargeNumber(reserve1)} (${FormatLargeNumber(reserve1Usd)})");

                // Add pair address
                if (!string.IsNullOrEmpty(_token0Info?.Address) && !string.IsNullOrEmpty(_token1Info?.Address))
                {
                    var (pairAddress, _, _) = await _priceSvc.GetPairDetailsAsync();
                    embedBuilder.AddField("Pair Address", pairAddress);
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

                var embedBuilder = new EmbedBuilder()
                    .WithTitle($"{_token0Info?.Name ?? "Token"} Information")
                    .WithColor(Color.Purple)
                    .WithCurrentTimestamp();

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
                }

                if (metrics.ContainsKey("MarketCap") && metrics["MarketCap"] > 0)
                {
                    embedBuilder.AddField("Market Cap", $"${FormatLargeNumber(metrics["MarketCap"])}", true);
                }

                if (_token0Info?.TotalSupply > 0)
                {
                    embedBuilder.AddField("Total Supply", FormatLargeNumber(_token0Info.TotalSupply), true);
                }

                // Pool/LP info
                if (metrics.ContainsKey("Liquidity") && metrics["Liquidity"] > 0)
                {
                    embedBuilder.AddField("Liquidity", $"${FormatLargeNumber(metrics["Liquidity"])}", true);
                }

                await command.RespondAsync(embed: embedBuilder.Build());
            }
            catch (Exception ex)
            {
                OnLog($"Error fetching token info: {ex.Message}");
                await command.RespondAsync("❌ Failed to fetch token info. Check logs.");
            }
        }
    }
}
