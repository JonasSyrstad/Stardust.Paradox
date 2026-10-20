using System.Globalization;
using System.Windows.Data;

namespace Stardust.Paradox.GremlinStudio.Converters;

/// <summary>
/// Returns the first whitespace-separated term of a string (trailing ':' removed),
/// used to group connections by name prefix at render time.
/// </summary>
[ValueConversion(typeof(string), typeof(string))]
public sealed class FirstTermGroupConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string text || string.IsNullOrWhiteSpace(text))
            return "(Unnamed)";

        var trimmed = text.Trim();
        var index = trimmed.IndexOfAny([' ', '\t']);
        var term = index < 0 ? trimmed : trimmed[..index];
        term = term.TrimEnd(':');
        return term.Length == 0 ? "(Unnamed)" : term;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
