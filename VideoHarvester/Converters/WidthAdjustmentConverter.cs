using System.Globalization;
using System.Windows.Data;

namespace VideoHarvester.Converters;

public class WidthAdjustmentConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is double originalWidth)
            return originalWidth - 40;

        return value;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException(); // One-way binding only
    }
}
