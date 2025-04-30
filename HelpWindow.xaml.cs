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

RadX Price Bot is a tool that allows you to display token prices from decentralized exchanges on Discord.

This application helps you set up and run a Discord bot that monitors token prices and other metrics 
from blockchain networks that implement Uniswap V2-style contracts.

## Quick Start

1. Create a Discord bot application
2. Configure your RPC endpoint and contract addresses
3. Start the bot to monitor your selected token pairs
4. Access real-time price updates directly in your Discord server",

                ["setup"] = @"# Setup Instructions

Follow these steps to set up your RadX Price Bot:

1. Create a Discord application and bot at https://discord.com/developers/applications
   - Click 'New Application' and give it a name
   - Go to the 'Bot' tab and click 'Add Bot'
   - Under the bot settings, enable 'Server Members Intent' and 'Message Content Intent'
   - Copy the bot token for later use

2. Add the bot to your Discord server
   - Go to OAuth2 > URL Generator
   - Select 'bot' scope and permissions: 'Change Nickname', 'Send Messages', 'Embed Links', 'Read Messages'
   - Open the generated URL and select your server

3. Enter your bot token and guild ID in the settings
   - Guild ID can be found by right-clicking your server name and selecting 'Copy ID'
     (Developer Mode must be enabled in Discord settings)

4. Configure the RPC endpoint and contract addresses
   - RPC URL: Obtain from providers like Infura, Alchemy, or your own node
   - Factory Address: The DEX factory contract address
   - Router Address: The DEX router contract address

5. Start the bot by clicking the 'Start' button",

                ["contracts"] = @"# Contract Requirements

The bot uses standard Uniswap V2 interfaces to interact with decentralized exchanges.

## Required Interfaces

### Router
- `getAmountsOut(uint amountIn, address[] memory path)` - Calculates output amount for a swap
- `factory()` - Returns the factory address

### Factory
- `getPair(address tokenA, address tokenB)` - Returns pair address for two tokens

### Pair
- `getReserves()` - Returns token reserves in the pool
- `token0()` - Returns address of token0
- `token1()` - Returns address of token1

### ERC20 Token
- `symbol()` - Returns token symbol
- `name()` - Returns token name
- `decimals()` - Returns token decimals
- `totalSupply()` - Returns total supply of the token

## Blockchain Compatibility

The bot works with any EVM-compatible network that implements these standard interfaces, including:
- Ethereum
- Binance Smart Chain
- Polygon
- Arbitrum
- Optimism
- Avalanche C-Chain
- And many others",

                ["configuration"] = @"# Bot Configuration

## Status Types

The bot supports different types of status displays:

- **Price**: Shows the current token price
  - Format: `$RADX: $0.12345 (+2.5%)`

- **Reserves**: Shows the liquidity pool reserves
  - Format: `LP: 1.2M RADX | 500K USDC`

- **Market Cap**: Shows the token market capitalization
  - Format: `Market Cap: $12.5M`

- **Custom**: Use your own status text
  - Any custom text you specify

## Multiple Bot Instances

You can run multiple bot instances to monitor different token pairs:

1. Go to the 'Multi-Bot' tab
2. Click 'Add Bot' to create a new configuration
3. Configure the bot with a name and token pair
4. Start each bot individually or use 'Start All'",

                ["discord"] = @"# Discord Integration

## Bot Presence

The bot will update its presence status with the price information according to your selected status type.

## Commands

The bot supports the following commands:

- `!price` - Shows the current token price
- `!stats` - Shows detailed token statistics
- `!lp` - Shows liquidity pool information
- `!help` - Shows available commands

## Embed Messages

You can send rich embed messages with token information to any channel:

1. Enter the Channel ID in the Discord Integration panel
2. Select the information you want to include
3. Click 'Send to Discord' to post the embed

## Customization

You can customize the bot's:
- Nickname
- Update interval
- Status format
- Embed colors and fields",

                ["faq-general"] = @"# General Questions

## What is the RadX Price Bot?
The RadX Price Bot is a Discord bot that displays token prices and other metrics in real-time. It connects to blockchain networks to fetch data from decentralized exchanges and shows this information in your Discord server.

## How do I invite the bot to my server?
You need to host the bot yourself using this application and your own Discord bot token. This gives you full control over the bot's functionality and data sources.

## Is the bot free to use?
Yes, the RadX Price Bot is completely free and open-source. You may need to pay for your own RPC endpoint if you exceed free tier limits from providers.

## Can I customize what the bot displays?
Yes, you can customize the bot's nickname, status message, update frequency, and the token pairs it monitors.

## Do I need coding knowledge to use this?
No coding knowledge is required. The application provides a user-friendly interface to configure and run the bot.",

                ["faq-technical"] = @"# Technical Questions

## What blockchain networks does this support?
Any EVM-compatible network that implements Uniswap V2-style contracts, including:
- Ethereum
- Binance Smart Chain
- Polygon
- Arbitrum
- Optimism
- Avalanche C-Chain
- Many others

## Why can't I see any pairs?
Make sure you've entered the correct factory contract address and RPC URL. The factory address should be the Uniswap V2 compatible factory for the DEX you're trying to use.

## How do I find contract addresses?
You can find contract addresses on blockchain explorers like Etherscan or from the DEX's documentation. For popular DEXes:
- Uniswap V2 Factory: 0x5C69bEe701ef814a2B6a3EDD4B1652CB9cc5aA6f
- SushiSwap Factory: 0xC0AEe478e3658e2610c5F7A4A2E1777cE9e4f2Ac
- PancakeSwap Factory: 0xcA143Ce32Fe78f1f7019d7d551a6402fC5350c73

## What RPC providers do you recommend?
- Infura.io
- Alchemy.com
- QuickNode.com
- Your own Ethereum/blockchain node

## How often does the bot update prices?
By default, the bot updates every 30 seconds. You can configure this interval in the settings to be as frequent as every 5 seconds.",

                ["troubleshooting"] = @"# Troubleshooting

## The bot is not responding
Check that:
- Your bot token and guild ID are correct
- The bot has proper permissions in your Discord server
- You've enabled required intents in the Discord developer portal
- Your internet connection is stable

## Price information is incorrect
Ensure you've:
- Selected the correct LP pair
- Configured the path correctly (token order matters)
- Entered the right router contract address
- Connected to a reliable and up-to-date RPC endpoint

## I'm getting RPC errors
Your RPC endpoint may be:
- Rate-limited (exceeding allowed calls)
- Temporarily down for maintenance
- Unreliable or slow

Solutions:
- Switch to a different RPC provider
- Upgrade to a paid plan with higher limits
- Increase the update interval to reduce API calls

## The bot keeps disconnecting
This could be due to:
- Discord's rate limiting
- Internet connectivity issues
- Memory leaks in long-running instances

Try:
- Increasing the update interval
- Restarting the application periodically
- Running the bot on a more reliable connection",

                ["about"] = @"# About RadX Price Bot

RadX Price Bot is a Discord bot for displaying token prices and metrics from decentralized exchanges.

Version 1.0.0
© 2025 Your Name

This application allows you to create and manage Discord bots that track cryptocurrency prices
and other metrics directly from blockchain data sources. It's designed to be user-friendly
while providing powerful features for crypto communities.

## Features

- Real-time token price monitoring
- Support for multiple blockchain networks
- Customizable Discord presence
- Rich embed messages with token analytics
- Multi-bot management for different token pairs
- Direct blockchain data querying without relying on third-party APIs",

                ["technologies"] = @"# Technologies Used

RadX Price Bot is built using modern software technologies:

## Core Technologies
- C# / .NET 9
- WPF for the user interface
- Nethereum for blockchain interactions
- Discord.NET for Discord API integration

## Blockchain Integration
- Web3 provider integration
- Smart contract ABIs for DEX interactions
- ERC20 standard implementations
- Gas-efficient read-only calls

## UX/UI
- Modern WPF styling
- Responsive design patterns
- Multi-threading for non-blocking UI
- Application state persistence

## Data Management
- JSON configuration storage
- Secure token management
- Local caching for performance optimization
- Exception handling and logging",

                ["license"] = @"# License

## MIT License

Copyright (c) 2025 Your Name

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
SOFTWARE.

## Acknowledgments

Thanks to the Nethereum and Discord.NET teams for their excellent libraries."
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
