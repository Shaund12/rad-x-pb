using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using RadXPriceBot.Services;

namespace RadXPriceBot.ViewModels
{
    public class BotStatusViewModel : INotifyPropertyChanged
    {
        private string _botId;
        private string _botName;
        private string _pairName;
        private string _currentPrice;
        private bool _isRunning;
        private string _status;
        private string _lastUpdated;
        private PairInfo _pairInfo;

        public string BotId
        {
            get => _botId;
            set
            {
                if (_botId != value)
                {
                    _botId = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public string BotName
        {
            get => _botName;
            set
            {
                if (_botName != value)
                {
                    _botName = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public string PairName
        {
            get => _pairName;
            set
            {
                if (_pairName != value)
                {
                    _pairName = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public string CurrentPrice
        {
            get => _currentPrice;
            set
            {
                if (_currentPrice != value)
                {
                    _currentPrice = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public bool IsRunning
        {
            get => _isRunning;
            set
            {
                if (_isRunning != value)
                {
                    _isRunning = value;
                    Status = value ? "Running" : "Stopped";
                    NotifyPropertyChanged();
                }
            }
        }

        public string Status
        {
            get => _status;
            set
            {
                if (_status != value)
                {
                    _status = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public string LastUpdated
        {
            get => _lastUpdated;
            set
            {
                if (_lastUpdated != value)
                {
                    _lastUpdated = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public PairInfo PairInfo
        {
            get => _pairInfo;
            set
            {
                if (_pairInfo != value)
                {
                    _pairInfo = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void NotifyPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
