using System;
using System.Globalization;
using System.Windows.Data;

namespace SelectML.Client.Converters
{
    public class WidthAdjustmentConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double width)
            {
                // Adjust for Expander padding and toggle button width
                // Typically toggle button is ~20-30px, plus padding/margins
                return Math.Max(0, width - 40);
            }
            return 0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
