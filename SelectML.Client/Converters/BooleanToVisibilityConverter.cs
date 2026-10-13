using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SelectML.Client.Converters
{
    public class BooleanToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                bool invert = parameter is string p && p.Equals("Inverse", StringComparison.OrdinalIgnoreCase);
                bool visible = invert ? !boolValue : boolValue;
                return visible ? Visibility.Visible : Visibility.Collapsed;
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool isVisible = value is Visibility visibility && visibility == Visibility.Visible;
            bool invert = parameter is string p && p.Equals("Inverse", StringComparison.OrdinalIgnoreCase);
            return invert ? !isVisible : isVisible;
        }
    }
}
