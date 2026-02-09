using System.Globalization;
using System.Windows.Data;
using Stardust.Paradox.GremlinStudio.Core.Export;

namespace Stardust.Paradox.GremlinStudio.Converters;

/// <summary>
/// Converts ScenarioExportFormat to syntax highlighting mode string.
/// </summary>
public class ExportFormatToSyntaxModeConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is ScenarioExportFormat format)
        {
            return format switch
            {
                ScenarioExportFormat.Json => "json",
                ScenarioExportFormat.CSharp => "csharp",
                _ => ""
            };
        }
        return "";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
