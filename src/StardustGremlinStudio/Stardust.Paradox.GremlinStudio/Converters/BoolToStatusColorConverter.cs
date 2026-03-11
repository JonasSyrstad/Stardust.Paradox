using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Stardust.Paradox.GremlinStudio.Converters;

/// <summary>
/// Converts a boolean success value to a color brush (green for success, red for failure).
/// </summary>
public class BoolToStatusColorConverter : IValueConverter
{
    private static readonly SolidColorBrush SuccessBrush = new(Color.FromRgb(0x22, 0xCC, 0x22));
    private static readonly SolidColorBrush ErrorBrush = new(Color.FromRgb(0xFF, 0x44, 0x44));

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is true ? SuccessBrush : ErrorBrush;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
