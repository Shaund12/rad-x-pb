using System;
using System.Collections.Generic;
using System.Windows;
using RadXPriceBot.Services;

namespace RadXPriceBot
{
    public partial class PairDetailsWindow : Window
    {
        public PairDetailsWindow()
        {
            InitializeComponent();
        }

        public void DisplayPairDetails(string botName, PairInfo pair, Dictionary<string, decimal> metrics)
        {
            if (pair == null || metrics == null)
            {
                MessageBox.Show("No pair data available.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                this.Close();
                return;
            }

            // Set the window title
            this.Title = $"{pair.Name} Details - {botName}";

            // Set pair name
            PairNameTextBlock.Text = $"{pair.Token0.Symbol}/{pair.Token1.Symbol} - {botName}";
            
            // Update last updated timestamp
            LastUpdatedTextBlock.Text = $"Last Updated: {DateTime.Now:HH:mm:ss}";

            // Price Information
            CurrentPriceTextBlock.Text = $"{metrics["Price"]:N6} {pair.Token1.Symbol}";
            
            if (metrics.ContainsKey("PriceUsd") && metrics["PriceUsd"] > 0)
                UsdPriceTextBlock.Text = $"${metrics["PriceUsd"]:N4}";
            else
                UsdPriceTextBlock.Text = "Not available";

            decimal reversePrice = metrics["Price"] > 0 ? 1 / metrics["Price"] : 0;
            ReversePriceTextBlock.Text = $"{reversePrice:N6} {pair.Token0.Symbol}";

            // Market Information
            MarketCapTextBlock.Text = $"${metrics["MarketCap"]:N2}";

            if (metrics.ContainsKey("Volume24h"))
                VolumeTextBlock.Text = $"${metrics["Volume24h"]:N2}";
            else
                VolumeTextBlock.Text = $"~${metrics["Liquidity"] * 0.1m:N2} (est.)";

            if (metrics.ContainsKey("HolderCount") && metrics["HolderCount"] > 0)
                HolderCountTextBlock.Text = $"{metrics["HolderCount"]:N0}";
            else
                HolderCountTextBlock.Text = "Not available";

            // Liquidity Information
            TotalLiquidityTextBlock.Text = $"${metrics["Liquidity"]:N2}";

            // Reserve Information
            Reserve0LabelTextBlock.Text = $"{pair.Token0.Symbol} Reserve:";
            Reserve1LabelTextBlock.Text = $"{pair.Token1.Symbol} Reserve:";
            Reserve0TextBlock.Text = $"{metrics["Reserve0"]:N2} {pair.Token0.Symbol}";
            Reserve1TextBlock.Text = $"{metrics["Reserve1"]:N2} {pair.Token1.Symbol}";

            // Token Supply Information
            if (metrics.ContainsKey("TotalSupply"))
                TotalSupplyTextBlock.Text = $"{metrics["TotalSupply"]:N0} {pair.Token0.Symbol}";
            else
                TotalSupplyTextBlock.Text = "Not available";

            if (metrics.ContainsKey("CirculatingSupply"))
                CirculatingSupplyTextBlock.Text = $"{metrics["CirculatingSupply"]:N0} {pair.Token0.Symbol}";
            else
                CirculatingSupplyTextBlock.Text = "Not available";

            // Contract Addresses
            Token0LabelTextBlock.Text = $"{pair.Token0.Symbol} Address:";
            Token1LabelTextBlock.Text = $"{pair.Token1.Symbol} Address:";
            PairAddressTextBox.Text = pair.Address;
            Token0AddressTextBox.Text = pair.Token0.Address;
            Token1AddressTextBox.Text = pair.Token1.Address;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
