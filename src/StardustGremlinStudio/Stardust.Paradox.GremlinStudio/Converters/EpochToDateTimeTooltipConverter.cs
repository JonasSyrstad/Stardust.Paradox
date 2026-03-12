using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace Stardust.Paradox.GremlinStudio.Converters;

/// <summary>
/// Converts epoch timestamp values (seconds or milliseconds) to a themed
/// tooltip panel matching the <c>EpochTooltipProvider</c> visual style.
/// Returns null for non-epoch values unless ConverterParameter is "fallback",
/// in which case the raw value is returned to preserve existing tooltip behavior.
/// </summary>
public sealed class EpochToDateTimeTooltipConverter : IValueConverter
{
    private const long MinEpochSeconds = 1_000_000_000;
    private const long EpochSecondsThreshold = 4_102_444_800;

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var str = value?.ToString();
        if (string.IsNullOrWhiteSpace(str))
        {
            return parameter as string == "fallback" ? str : null;
        }

        var epochTooltip = TryFormatEpoch(str);
        if (epochTooltip != null)
        {
            return BuildTooltipContent(str, epochTooltip);
        }

        return parameter as string == "fallback" ? str : null;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return Binding.DoNothing;
    }

    internal static string? TryFormatEpoch(string value)
    {
        if (!long.TryParse(value, out var epoch) || epoch < MinEpochSeconds)
        {
            return null;
        }

        try
        {
            DateTimeOffset dto;
            if (epoch > EpochSecondsThreshold)
            {
                dto = DateTimeOffset.FromUnixTimeMilliseconds(epoch);
                if (dto.Year < 2000 || dto.Year > 2100)
                {
                    return null;
                }
            }
            else
            {
                dto = DateTimeOffset.FromUnixTimeSeconds(epoch);
            }

            return $"\u2192 {dto:yyyy-MM-dd HH:mm:ss} UTC";
        }
        catch
        {
            return null;
        }
    }

    private static UIElement BuildTooltipContent(string epochValue, string formattedDate)
    {
        var panel = new StackPanel();

        var accentBrush = GetBrush("AccentBrush");
        var primaryFg = GetBrush("PrimaryForegroundBrush");
        var secondaryFg = GetBrush("SecondaryForegroundBrush");

        panel.Children.Add(new TextBlock
        {
            Text = "Epoch Timestamp",
            FontSize = 10,
            Foreground = accentBrush,
            Margin = new Thickness(0, 0, 0, 4)
        });

        panel.Children.Add(new TextBlock
        {
            Text = epochValue,
            FontFamily = new FontFamily("Consolas"),
            FontWeight = FontWeights.SemiBold,
            FontSize = 13,
            Foreground = primaryFg,
            Margin = new Thickness(0, 0, 0, 4)
        });

        panel.Children.Add(new TextBlock
        {
            Text = formattedDate,
            FontSize = 12,
            Foreground = secondaryFg
        });

        return panel;
    }

    private static Brush GetBrush(string resourceKey)
    {
        return Application.Current.TryFindResource(resourceKey) as Brush ?? Brushes.Transparent;
    }
}
