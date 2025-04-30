// SettingsWindow.xaml.cs
using System;
using System.Windows;
using System.Windows.Controls;
using System.ComponentModel;  // Add this for CancelEventArgs
using RadXPriceBot.Services;

namespace RadXPriceBot
{
    public partial class SettingsWindow : Window
    {
        private readonly BotSettings _settings;

        public SettingsWindow(BotSettings settings)
        {
            InitializeComponent();
            _settings = settings;
            LoadSettingsToUI();
            
            // Subscribe to the Closing event
            this.Closing += SettingsWindow_Closing;
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
            BotTokenTextBox.Text = _settings.BotToken;
            GuildIdTextBox.Text = _settings.GuildId;
            RpcUrlTextBox.Text = _settings.RpcUrl;
            SwapRouterAddressTextBox.Text = _settings.SwapRouterAddress;
            FactoryAddressTextBox.Text = _settings.FactoryAddress;
            BotNicknameTextBox.Text = _settings.BotNickname;
            CustomStatusTextBox.Text = _settings.CustomStatus;
            MinimizeOnCloseCheckBox.IsChecked = _settings.MinimizeOnClose; // Add this line

            foreach (ComboBoxItem item in StatusTypeComboBox.Items)
            {
                if (item.Content.ToString() == _settings.StatusType)
                {
                    StatusTypeComboBox.SelectedItem = item;
                    break;
                }
            }

            if (StatusTypeComboBox.SelectedIndex == -1 && StatusTypeComboBox.Items.Count > 0)
            {
                StatusTypeComboBox.SelectedIndex = 0;
            }
        }

        private void SaveSettingsFromUI()
        {
            _settings.BotToken = BotTokenTextBox.Text.Trim();
            _settings.GuildId = GuildIdTextBox.Text.Trim();
            _settings.RpcUrl = RpcUrlTextBox.Text.Trim();
            _settings.SwapRouterAddress = SwapRouterAddressTextBox.Text.Trim();
            _settings.FactoryAddress = FactoryAddressTextBox.Text.Trim();
            _settings.BotNickname = BotNicknameTextBox.Text.Trim();
            _settings.CustomStatus = CustomStatusTextBox.Text.Trim();
            _settings.StatusType = (StatusTypeComboBox.SelectedItem as ComboBoxItem)?.Content as string ?? "Price";
            _settings.MinimizeOnClose = MinimizeOnCloseCheckBox.IsChecked ?? true; // Add this line

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
            DialogResult = false;
            Close();
        }
    }
}
