using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace VideoHarvester.Converters;

public class StatusColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string status && status == "Downloaded")
            return Brushes.Green;

        return Brushes.Yellow; // Default color
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}
