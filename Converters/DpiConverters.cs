// Converters/DpiConverters.cs
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace RadXPriceBot.Converters
{
    public class DpiValueConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double dpiScale)
            {
                return $"{(int)(dpiScale * 100)}%";
            }
            return "100%";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string dpiString && dpiString.EndsWith("%"))
            {
                if (int.TryParse(dpiString.TrimEnd('%'), out int dpiValue))
                {
                    return dpiValue / 100.0;
                }
            }
            return 1.0;
        }
    }
}
