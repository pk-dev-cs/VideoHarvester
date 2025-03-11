using System;
using System.Globalization;
using System.Windows.Data;

namespace VideoHarvester;

public class WidthAdjustmentConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is double originalWidth)
        {
            return originalWidth - 30; // Subtract 10 pixels
        }
        return value;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException(); // One-way binding only
    }
}
