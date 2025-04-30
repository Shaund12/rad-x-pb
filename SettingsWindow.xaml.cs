// SettingsWindow.xaml.cs
using System;
using System.Windows;
using System.Windows.Controls;
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
