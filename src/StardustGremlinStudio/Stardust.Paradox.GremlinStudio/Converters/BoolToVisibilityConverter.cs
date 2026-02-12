using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Stardust.Paradox.GremlinStudio.Converters;

/// <summary>
/// Converts a boolean value to a Visibility value.
/// </summary>
public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool b)
        {
            return b ? Visibility.Visible : Visibility.Collapsed;
        }
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is Visibility v)
        {
            return v == Visibility.Visible;
        }
        return false;
    }
}

/// <summary>
/// Inverts a boolean value.
/// </summary>
public class InverseBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool b)
        {
            return !b;
        }
        return true;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool b)
        {
            return !b;
        }
        return false;
    }
}


/// <summary>
/// Converts a boolean value to an inverse Visibility value.
/// </summary>
public class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool b)
        {
            return b ? Visibility.Collapsed : Visibility.Visible;
        }
        return Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is Visibility v)
        {
            return v != Visibility.Visible;
        }
        return true;
    }
}

/// <summary>
/// Converts null/empty string to Collapsed, otherwise Visible.
/// </summary>
public class NullToCollapsedConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is null || (value is string s && string.IsNullOrEmpty(s)))
        {
            return Visibility.Collapsed;
        }
        return Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts boolean to edge color (highlighted when selected).
/// </summary>
public class BoolToEdgeColorConverter : IValueConverter
{
    private static readonly System.Windows.Media.SolidColorBrush NormalColor = 
        new(System.Windows.Media.Color.FromRgb(170, 170, 170)); // #AAAAAA
    private static readonly System.Windows.Media.SolidColorBrush SelectedColor = 
        new(System.Windows.Media.Color.FromRgb(0, 170, 255)); // #00AAFF

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is true ? SelectedColor : NormalColor;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}



/// <summary>
/// Converts boolean to edge stroke thickness (thicker when selected).
/// </summary>
public class BoolToEdgeThicknessConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is true ? 1.5 : 0.8;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts boolean to edge label background (highlighted when selected).
/// </summary>
public class BoolToLabelBackgroundConverter : IValueConverter
{
    private static readonly System.Windows.Media.SolidColorBrush NormalBackground = 
        new(System.Windows.Media.Color.FromArgb(0xDD, 0x2D, 0x2D, 0x30)); // #DD2D2D30
    private static readonly System.Windows.Media.SolidColorBrush SelectedBackground = 
        new(System.Windows.Media.Color.FromArgb(0xEE, 0x00, 0x60, 0xA0)); // Blue highlight

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is true ? SelectedBackground : NormalBackground;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts boolean isPinned to tooltip text.
/// </summary>
public class BoolToPinTooltipConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is true ? "Unpin query" : "Pin query";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Compares two objects for equality. Used to determine if a tab is selected.
/// </summary>
public class EqualityConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 2)
            return false;
        
        return Equals(values[0], values[1]);
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts a progress value (0-100) to a width based on a maximum width parameter.
/// </summary>
public class ProgressToWidthConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int progress && parameter is string maxWidthStr && double.TryParse(maxWidthStr, out var maxWidth))
        {
            return progress / 100.0 * maxWidth;
        }
        if (value is int p)
        {
            return p; // Return progress directly as width if no parameter
        }
        return 0.0;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
