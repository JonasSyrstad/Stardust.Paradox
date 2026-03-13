using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows.Data;

namespace Stardust.Paradox.GremlinStudio.Converters;

/// <summary>
/// Collapses a multiline Gremlin query into a compact single line by removing
/// line breaks and the surrounding whitespace between steps.
/// </summary>
[ValueConversion(typeof(string), typeof(string))]
public sealed class SingleLineConverter : IValueConverter
{
    // Matches whitespace that spans across a line break (including any surrounding spaces/tabs).
    private static readonly Regex LineBreakWhitespace =
        new(@"[ \t]*[\r\n]+[ \t]*", RegexOptions.Compiled);

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string text)
            return value;

        return LineBreakWhitespace.Replace(text, string.Empty);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
