// HelpWindow.xaml.cs
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Navigation;

namespace RadXPriceBot
{
    public partial class HelpWindow : Window
    {
        // Dictionary to store content sections for easy navigation and search
        private Dictionary<string, string> _contentSections;

        public HelpWindow(string title, string content)
        {
            InitializeComponent();

            // Set version info
            VersionTextBlock.Text = $"v1.0.0";

            // Initialize title
            Title = title;
            ContentTitleBlock.Text = title;

            // For compatibility, maintain the original property
            ContentTextBlock.Text = content;

            // Setup content sections
            InitializeContentSections();

            // Default to showing the content that was passed in
            // Match it to one of our sections if possible, otherwise show as custom content
            if (!MatchContentToSection(content))
            {
                DisplayCustomContent(content);
            }
        }

        private void InitializeContentSections()
        {
            _contentSections = new Dictionary<string, string>
            {
                ["getting-started"] = @"# Getting Started with RadX Price Bot

RadX Price Bot is a comprehensive tool for displaying real-time token prices and analytics from decentralized exchanges directly in your Discord server.

This application helps you set up and run Discord bots that monitor token prices and other metrics 
from blockchain networks that implement Uniswap V2-style contracts.

## Quick Start

1. Create a Discord application and bot
2. Configure your RPC endpoint and contract addresses
3. Start the bot to monitor your selected token pairs
4. Access real-time price updates directly in your Discord server

## Key Features
- Real-time price monitoring from blockchain data
- Multi-bot support for tracking multiple token pairs simultaneously
- Rich Discord embeds with detailed token analytics and charts
- Market statistics including liquidity, volume, and market cap
- Swap transaction monitoring and notifications
- Customizable presence status and update intervals",

                ["setup"] = @"# Setup Instructions

## Initial Setup

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

## Blockchain Connection Setup

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
   - Use 'Refresh Data' to update the selected pair information",

                ["contracts"] = @"# Contract Requirements

The bot requires DEX contracts that implement standard Uniswap V2 interfaces:

## Router Interface
- `getAmountsOut(uint amountIn, address[] memory path)`: Calculate output amount
- `factory()`: Get the factory address

## Factory Interface
- `getPair(address tokenA, address tokenB)`: Get the liquidity pair address

## Pair Interface
- `getReserves()`: Get current token reserves
- `token0()`: Get first token address
- `token1()`: Get second token address

## ERC20 Interface
- `symbol()`: Get token symbol
- `name()`: Get token name
- `decimals()`: Get token decimal places
- `totalSupply()`: Get total token supply

## Blockchain Compatibility

The bot works with any EVM-compatible network that implements these standard interfaces, including:
- Ethereum
- Polygon
- BNB Smart Chain
- Arbitrum
- Optimism
- Avalanche C-Chain
- Base
- And many others",

                ["configuration"] = @"# Bot Configuration

## Multi-Bot Management

The Bot Manager allows you to create and manage multiple bot instances, each monitoring different token pairs:

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

## Status Types

Configure how the bot appears in Discord:

- **Price**: Shows current token price (e.g., ""$RADX: $0.12345"")
- **Reserves**: Shows liquidity pool reserves (e.g., ""LP: 1.2M RADX | 500K USDC"")
- **Market Cap**: Shows token market capitalization (e.g., ""Market Cap: $12.5M"")
- **Custom**: Define your own status text with variables:
  • {price}: Current token price
  • {symbol0}: Base token symbol
  • {symbol1}: Quote token symbol
  • {liquidity}: Total pool liquidity in USD
  • {marketcap}: Token market capitalization",

                ["discord"] = @"# Discord Integration

## Discord Embed Configuration

Periodic embeds provide detailed token analytics in Discord channels:

1. Enable periodic embeds in the Discord Embeds tab
2. Set the embed interval in minutes
3. Enter the target Discord channel ID
4. Customize appearance:
   - Embed Color: Hex color code for the embed sidebar
   - Include Chart: Adds price chart visualization
   - Include Token Info: Adds supply and holder statistics
   - Include Liquidity Info: Adds pool and volume metrics

## Swap Transaction Monitoring

Monitor and report token swaps in real-time:

1. Enable swap monitoring in the Swap Monitoring tab
2. Enter the Discord channel ID for swap notifications
3. Set the swap check interval (milliseconds)
4. The bot will detect and report:
   - Buy/sell transactions in the monitored pair
   - Transaction size and impact
   - Price impact from swaps

## Manual Updates

You can also manually send token information to Discord:
1. Select a token pair to view its details
2. Enter a channel ID in the 'Discord Channel ID' field
3. Click 'Send to Discord' to post a rich embed message with current token metrics",

                ["faq-general"] = @"# General Questions

## What is the RadX Price Bot?
The RadX Price Bot is a Discord bot that displays real-time token prices and metrics from decentralized exchanges directly in your Discord server. It connects to blockchain networks to monitor token pairs and can show information like price, liquidity, market cap, and trading volume.

## How do I invite the bot to my server?
You need to host this application yourself using your own Discord bot token. This gives you full control over the data sources and configuration. Follow these steps:
   1. Create a Discord application at discord.com/developers/applications
   2. Set up a bot for your application and copy the token
   3. Add the bot to your server using the OAuth2 URL generator
   4. Input your bot token and guild ID in this application
   5. Configure your blockchain connection settings
   6. Start the bot

## Can I monitor multiple token pairs at once?
Yes! Use the Bot Manager tab to create and configure multiple bot instances, each monitoring a different token pair. You can run them simultaneously with different settings and status displays.

## What status information can the bot display?
The bot offers several status display options:
   • Price: Shows the current token price (e.g., ""$RADX: $0.12345"")
   • Reserves: Shows the liquidity pool reserves (e.g., ""LP: 1.2M RADX | 500K USDC"")
   • Market Cap: Shows the token market capitalization (e.g., ""Market Cap: $12.5M"")
   • Custom: Any text format you specify

## How do I set up Discord embeds?
In the Bot Manager tab:
   1. Select a bot configuration
   2. Check ""Send Periodic Embeds"" in the Discord Embeds tab
   3. Enter the Channel ID where embeds should be sent
   4. Set the interval (in minutes)
   5. Choose which information to include (chart, token info, liquidity info)
   6. Click ""Apply Embed Settings"" and ensure the bot is running",

                ["faq-technical"] = @"# Technical Questions

## What blockchain networks does this support?
Any EVM-compatible network that implements Uniswap V2-style contracts, including:
   • Ethereum
   • Polygon
   • BNB Smart Chain
   • Arbitrum
   • Optimism
   • Avalanche C-Chain
   • Base
   • And many others

## Why can't I see any pairs after connecting?
Check the following:
   1. Verify the factory contract address is correct for the DEX you're connecting to
   2. Ensure your RPC URL is valid and responding
   3. Check that the blockchain network has properly implemented Uniswap V2 interfaces
   4. Look for errors in the log console that might indicate connection problems

## How do I find the correct contract addresses?
You can find contract addresses from:
   • The DEX's documentation or GitHub repository
   • Blockchain explorers like Etherscan, PolygonScan, etc.
   • Common factory addresses:
     - Uniswap V2: 0x5C69bEe701ef814a2B6a3EDD4B1652CB9cc5aA6f (Ethereum)
     - SushiSwap: 0xC0AEe478e3658e2610c5F7A4A2E1777cE9e4f2Ac (Ethereum)
     - QuickSwap: 0x5757371414417b8C6CAad45bAeF941aBc7d3Ab32 (Polygon)

## What's the difference between RPC URL, factory address, and router address?
• RPC URL: The endpoint to connect to the blockchain (provided by services like Infura or Alchemy)
• Factory Address: The contract that creates and stores liquidity pair addresses
• Router Address: The contract that handles token swaps and provides pricing information

## What is swap transaction monitoring?
This feature tracks token swap transactions in the liquidity pool. When enabled, the bot will detect and report trades in your selected Discord channel. Configure this in the Swap Monitoring tab under Bot Manager.

## How do I adjust the UI scaling?
Use the UI Scale dropdown in the top-right corner to adjust the application size from 75% to 200%. This is particularly useful for high-DPI displays or to fit more information on smaller screens.",

                ["troubleshooting"] = @"# Troubleshooting

## The bot is not responding or connecting to Discord
Check these common issues:
   1. Verify your bot token is correct and not expired
   2. Ensure the bot has been added to your server with proper permissions (Send Messages, Embed Links, Change Nickname)
   3. Check that you've enabled the required Gateway Intents in the Discord Developer Portal
   4. Restart the application to refresh the connection
   5. Look for any firewall or network issues blocking Discord connections

## Price information is incorrect or not updating
Try these solutions:
   1. Make sure you've selected the correct LP pair
   2. Verify the token path is in the right order (typically base token first, quote token second)
   3. Check that your RPC provider is supplying current blockchain data
   4. Try increasing the update interval if you're experiencing rate limiting
   5. Use the ""Refresh Data"" button to force a manual update
   6. Restart the bot if data appears stale

## I'm getting RPC errors or timeouts
RPC issues are usually related to:
   1. Rate limiting on free tier endpoints - consider using a paid service
   2. Network congestion or outages - try a different RPC provider
   3. Incorrect RPC URL format - double-check for typos
   4. Using the wrong network (e.g., Ethereum RPC for a Polygon token)
   5. Firewall or network restrictions - ensure outbound connections are allowed

## The bot keeps disconnecting from Discord
This might be caused by:
   1. Discord API rate limits - increase your update intervals
   2. Unstable internet connection - ensure you have reliable connectivity
   3. Memory issues - restart the application periodically
   4. Discord service disruptions - check Discord status page

## My bot's nickname isn't changing
Ensure the bot has the ""Change Nickname"" permission in your Discord server, and that its role is positioned below the server owner's role in the hierarchy.",

                ["advanced-usage"] = @"# Advanced Usage

## Customizing the embed appearance
Yes, you can:
   1. Change the embed color by entering a hex color code
   2. Toggle which information sections appear
   3. Edit the bot's nickname to change how it appears in the server
   4. Use a custom status format for unique displays

## Optimizing swap transaction monitoring
To optimize swap monitoring:
   1. Set an appropriate interval based on token activity
   2. For busy pairs, increase the interval to avoid rate limiting
   3. Use a dedicated bot instance just for swap monitoring
   4. Consider using a premium RPC provider for higher throughput

## Backing up bot configurations
The application automatically saves your settings to a local file. For manual backups, you can copy your settings file from the application directory.

## Data Storage

The application stores historical data locally:
- Price history for supported tokens
- Liquidity reserves history
- Configuration settings

Database maintenance is automatic with cleanup of old records after 30 days.",

                ["about"] = @"# RadX Price Bot

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
Thanks to the RadX community for testing and feedback.",

                ["license"] = @"# License

## MIT License

Copyright (c) 2025 RadX Development Team

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the ""Software""), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED ""AS IS"", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE."
            };
        }


        private bool MatchContentToSection(string content)
        {
            if (content.Contains("# RadX Price Bot Documentation"))
            {
                var button = new Button { Tag = "getting-started" };
                NavButton_Click(button, new RoutedEventArgs(Button.ClickEvent, button));
                return true;
            }
            else if (content.Contains("# Frequently Asked Questions"))
            {
                var button = new Button { Tag = "faq-general" };
                NavButton_Click(button, new RoutedEventArgs(Button.ClickEvent, button));
                return true;
            }
            else if (content.Contains("# RadX Price Bot\n\nVersion"))
            {
                var button = new Button { Tag = "about" };
                NavButton_Click(button, new RoutedEventArgs(Button.ClickEvent, button));
                return true;
            }
            return false;
        }


        private void DisplayCustomContent(string markdownContent)
        {
            // Convert markdown to FlowDocument
            var document = ConvertMarkdownToFlowDocument(markdownContent);
            ContentRichTextBox.Document = document;
        }

        private void NavButton_Click(object sender, RoutedEventArgs e)
        {
            string sectionTag = (e.Source as Button)?.Tag as string;
            if (string.IsNullOrEmpty(sectionTag) || !_contentSections.ContainsKey(sectionTag))
                return;

            // Update title based on section
            ContentTitleBlock.Text = sectionTag switch
            {
                "getting-started" => "Getting Started",
                "setup" => "Setup Instructions",
                "contracts" => "Contract Requirements",
                "configuration" => "Bot Configuration",
                "discord" => "Discord Integration",
                "faq-general" => "General Questions",
                "faq-technical" => "Technical Questions",
                "troubleshooting" => "Troubleshooting",
                "about" => "About RadX Price Bot",
                "technologies" => "Technologies Used",
                "license" => "License",
                _ => ContentTitleBlock.Text
            };

            DisplayCustomContent(_contentSections[sectionTag]);
        }

        private void SearchBox_KeyUp(object sender, KeyEventArgs e)
        {
            string searchText = SearchBox.Text.ToLower();
            if (string.IsNullOrWhiteSpace(searchText))
                return;

            foreach (var section in _contentSections)
            {
                if (section.Value.ToLower().Contains(searchText))
                {
                    NavButton_Click(null, new RoutedEventArgs { Source = new Button { Tag = section.Key } });
                    return;
                }
            }
        }

        private void VisitRepo_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "https://github.com/yourusername/radxpricebot",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not open the link: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private FlowDocument ConvertMarkdownToFlowDocument(string markdown)
        {
            var document = new FlowDocument();
            var lines = markdown.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

            Paragraph currentParagraph = null;
            bool inCodeBlock = false;
            bool inBulletList = false;
            var codeBlockLines = new List<string>();

            foreach (var line in lines)
            {
                // Headers
                if (line.StartsWith("# "))
                {
                    document.Blocks.Add(new Paragraph(new Run(line.Substring(2)))
                    {
                        FontSize = 24,
                        FontWeight = FontWeights.Bold,
                        Foreground = new SolidColorBrush(Color.FromRgb(92, 219, 240)),
                        Margin = new Thickness(0, 0, 0, 10)
                    });
                }
                else if (line.StartsWith("## "))
                {
                    document.Blocks.Add(new Paragraph(new Run(line.Substring(3)))
                    {
                        FontSize = 18,
                        FontWeight = FontWeights.SemiBold,
                        Foreground = new SolidColorBrush(Color.FromRgb(145, 245, 173)),
                        Margin = new Thickness(0, 10, 0, 5)
                    });
                }
                else if (line.StartsWith("### "))
                {
                    document.Blocks.Add(new Paragraph(new Run(line.Substring(4)))
                    {
                        FontSize = 16,
                        FontWeight = FontWeights.Medium,
                        Foreground = new SolidColorBrush(Color.FromRgb(145, 245, 173)),
                        Margin = new Thickness(0, 5, 0, 5)
                    });
                }
                // Fenced code blocks
                else if (line.StartsWith("```"))
                {
                    if (inCodeBlock)
                    {
                        // End code block
                        inCodeBlock = false;
                        var codeText = string.Join("\n", codeBlockLines);
                        var border = new Border
                        {
                            Background = new SolidColorBrush(Color.FromRgb(37, 37, 45)),
                            BorderBrush = new SolidColorBrush(Color.FromRgb(58, 58, 69)),
                            BorderThickness = new Thickness(1),
                            CornerRadius = new CornerRadius(4),
                            Padding = new Thickness(8),
                            Margin = new Thickness(0, 5, 0, 10),
                            Child = new TextBlock
                            {
                                Text = codeText,
                                FontFamily = new FontFamily("Consolas, Courier New, monospace"),
                                Foreground = new SolidColorBrush(Color.FromRgb(209, 213, 218)),
                                TextWrapping = TextWrapping.Wrap
                            }
                        };
                        document.Blocks.Add(new BlockUIContainer(border));
                        codeBlockLines.Clear();
                    }
                    else
                    {
                        // Start code block
                        inCodeBlock = true;
                        codeBlockLines.Clear();
                        if (currentParagraph != null)
                        {
                            document.Blocks.Add(currentParagraph);
                            currentParagraph = null;
                        }
                    }
                }
                else if (inCodeBlock)
                {
                    // Inside code block, collect lines
                    codeBlockLines.Add(line);
                }
                // Bullet lists
                else if (line.TrimStart().StartsWith("- "))
                {
                    if (!inBulletList)
                    {
                        inBulletList = true;
                        if (currentParagraph != null)
                        {
                            document.Blocks.Add(currentParagraph);
                            currentParagraph = null;
                        }
                    }

                    var bulletText = line.TrimStart().Substring(2);
                    var bulletPara = new Paragraph(new Run(bulletText)) { Margin = new Thickness(15, 0, 0, 5) };
                    bulletPara.Inlines.InsertBefore(bulletPara.Inlines.FirstInline,
                        new Run("• ") { FontWeight = FontWeights.Bold });
                    document.Blocks.Add(bulletPara);
                }
                // Numbered lists
                else if (System.Text.RegularExpressions.Regex.IsMatch(line.TrimStart(), @"^\d+\.\s"))
                {
                    if (!inBulletList)
                    {
                        inBulletList = true;
                        if (currentParagraph != null)
                        {
                            document.Blocks.Add(currentParagraph);
                            currentParagraph = null;
                        }
                    }

                    var match = System.Text.RegularExpressions.Regex.Match(line.TrimStart(), @"^(\d+)\.\s(.+)");
                    if (match.Success)
                    {
                        var number = match.Groups[1].Value;
                        var text = match.Groups[2].Value;
                        var numPara = new Paragraph(new Run(text)) { Margin = new Thickness(15, 0, 0, 5) };
                        numPara.Inlines.InsertBefore(numPara.Inlines.FirstInline,
                            new Run($"{number}. ") { FontWeight = FontWeights.Bold });
                        document.Blocks.Add(numPara);
                    }
                }
                // Blank line
                else if (string.IsNullOrWhiteSpace(line))
                {
                    inBulletList = false;
                    if (currentParagraph != null)
                    {
                        document.Blocks.Add(currentParagraph);
                        currentParagraph = null;
                    }
                }
                // Regular text
                else
                {
                    inBulletList = false;
                    if (currentParagraph == null)
                        currentParagraph = new Paragraph();
                    else
                        currentParagraph.Inlines.Add(new LineBreak());

                    ProcessInlineFormatting(line, currentParagraph);
                }
            }

            if (currentParagraph != null && currentParagraph.Inlines.Count > 0)
                document.Blocks.Add(currentParagraph);

            return document;
        }

        private void ProcessInlineFormatting(string line, Paragraph paragraph)
        {
            int position = 0;

            while (position < line.Length)
            {
                int boldStart = line.IndexOf("**", position);
                if (boldStart >= 0)
                {
                    if (boldStart > position)
                        paragraph.Inlines.Add(new Run(line.Substring(position, boldStart - position)));
                    int boldEnd = line.IndexOf("**", boldStart + 2);
                    if (boldEnd >= 0)
                    {
                        string boldText = line.Substring(boldStart + 2, boldEnd - boldStart - 2);
                        paragraph.Inlines.Add(new Run(boldText) { FontWeight = FontWeights.Bold });
                        position = boldEnd + 2;
                        continue;
                    }
                    else
                    {
                        paragraph.Inlines.Add(new Run(line.Substring(position)));
                        break;
                    }
                }

                int italicStart = line.IndexOf("*", position);
                if (italicStart >= 0)
                {
                    if (italicStart > position)
                        paragraph.Inlines.Add(new Run(line.Substring(position, italicStart - position)));
                    int italicEnd = line.IndexOf("*", italicStart + 1);
                    if (italicEnd >= 0)
                    {
                        string italicText = line.Substring(italicStart + 1, italicEnd - italicStart - 1);
                        paragraph.Inlines.Add(new Run(italicText) { FontStyle = FontStyles.Italic });
                        position = italicEnd + 1;
                        continue;
                    }
                    else
                    {
                        paragraph.Inlines.Add(new Run(line.Substring(position)));
                        break;
                    }
                }

                int codeStart = line.IndexOf("`", position);
                if (codeStart >= 0)
                {
                    if (codeStart > position)
                        paragraph.Inlines.Add(new Run(line.Substring(position, codeStart - position)));
                    int codeEnd = line.IndexOf("`", codeStart + 1);
                    if (codeEnd >= 0)
                    {
                        string codeTxt = line.Substring(codeStart + 1, codeEnd - codeStart - 1);
                        var codeRun = new Run(codeTxt)
                        {
                            FontFamily = new FontFamily("Consolas, Courier New, monospace"),
                            Background = new SolidColorBrush(Color.FromRgb(37, 37, 45)),
                            Foreground = new SolidColorBrush(Color.FromRgb(209, 213, 218))
                        };
                        paragraph.Inlines.Add(codeRun);
                        position = codeEnd + 1;
                        continue;
                    }
                    else
                    {
                        paragraph.Inlines.Add(new Run(line.Substring(position)));
                        break;
                    }
                }

                int linkTextStart = line.IndexOf("[", position);
                if (linkTextStart >= 0)
                {
                    if (linkTextStart > position)
                        paragraph.Inlines.Add(new Run(line.Substring(position, linkTextStart - position)));
                    int linkTextEnd = line.IndexOf("]", linkTextStart);
                    if (linkTextEnd >= 0 && linkTextEnd + 1 < line.Length && line[linkTextEnd + 1] == '(')
                    {
                        int linkUrlEnd = line.IndexOf(")", linkTextEnd + 2);
                        if (linkUrlEnd >= 0)
                        {
                            string linkText = line.Substring(linkTextStart + 1, linkTextEnd - linkTextStart - 1);
                            string linkUrl = line.Substring(linkTextEnd + 2, linkUrlEnd - linkTextEnd - 2);
                            var hyperlink = new Hyperlink(new Run(linkText))
                            {
                                NavigateUri = new Uri(linkUrl),
                                Foreground = new SolidColorBrush(Color.FromRgb(92, 219, 240))
                            };
                            hyperlink.RequestNavigate += (s, e) =>
                            {
                                Process.Start(new ProcessStartInfo
                                {
                                    FileName = e.Uri.AbsoluteUri,
                                    UseShellExecute = true
                                });
                                e.Handled = true;
                            };
                            paragraph.Inlines.Add(hyperlink);
                            position = linkUrlEnd + 1;
                            continue;
                        }
                    }
                }

                // Fallback: plain text
                paragraph.Inlines.Add(new Run(line.Substring(position)));
                break;
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
