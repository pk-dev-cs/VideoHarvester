using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace VideoHarvester.Converters;

public class DependencyStatusColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isAvailable)
        {
            return isAvailable ? new SolidColorBrush(Color.FromRgb(34, 197, 94)) : new SolidColorBrush(Color.FromRgb(239, 68, 68)); // Green : Red
        }
        return new SolidColorBrush(Color.FromRgb(156, 163, 175)); // Gray
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
