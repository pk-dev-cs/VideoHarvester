using System.Globalization;
using System.Windows.Data;
using System.Windows;

namespace VideoHarvester.Converters;

public class ButtonVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is string status && status == "Downloaded" ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}
