// Converters/Converters.cs
using RadXPriceBot.ViewModels;
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace RadXPriceBot.Converters
{
    public class BotRunningConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string botId && Application.Current.MainWindow.DataContext is MainViewModel vm)
            {
                return vm.IsBotConfigRunning(botId);
            }
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class BoolToVisibilityConverter : IValueConverter
    {
        public bool Inverted { get; set; } = false;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                if (Inverted)
                    return boolValue ? Visibility.Collapsed : Visibility.Visible;
                else
                    return boolValue ? Visibility.Visible : Visibility.Collapsed;
            }
            
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Visibility visibility)
            {
                if (Inverted)
                    return visibility != Visibility.Visible;
                else
                    return visibility == Visibility.Visible;
            }
            
            return false;
        }
    }

    public class BoolToVisibilityConverterInverted : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (bool)value ? Visibility.Collapsed : Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (Visibility)value == Visibility.Collapsed;
        }
    }

    public class IsCustomStatusConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string statusType)
            {
                return statusType.Equals("Custom", StringComparison.OrdinalIgnoreCase);
            }
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
