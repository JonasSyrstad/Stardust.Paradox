using System.Windows;
using System.Windows.Controls;

namespace Stardust.Paradox.GremlinStudio.Controls;

/// <summary>
/// Attached property to enable binding to PasswordBox.Password.
/// PasswordBox does not support binding by default for security reasons.
/// </summary>
public static class PasswordBoxHelper
{
    public static readonly DependencyProperty BoundPasswordProperty =
        DependencyProperty.RegisterAttached(
            "BoundPassword",
            typeof(string),
            typeof(PasswordBoxHelper),
            new FrameworkPropertyMetadata(
                string.Empty,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnBoundPasswordChanged));

    public static readonly DependencyProperty IsUpdatingProperty =
        DependencyProperty.RegisterAttached(
            "IsUpdating",
            typeof(bool),
            typeof(PasswordBoxHelper),
            new PropertyMetadata(false));

    public static string GetBoundPassword(DependencyObject obj)
    {
        return (string)obj.GetValue(BoundPasswordProperty);
    }

    public static void SetBoundPassword(DependencyObject obj, string value)
    {
        obj.SetValue(BoundPasswordProperty, value);
    }

    private static bool GetIsUpdating(DependencyObject obj)
    {
        return (bool)obj.GetValue(IsUpdatingProperty);
    }

    private static void SetIsUpdating(DependencyObject obj, bool value)
    {
        obj.SetValue(IsUpdatingProperty, value);
    }

    private static void OnBoundPasswordChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not PasswordBox passwordBox)
        {
            return;
        }

        // Attach the handler only once
        passwordBox.PasswordChanged -= PasswordBox_PasswordChanged;
        passwordBox.PasswordChanged += PasswordBox_PasswordChanged;

        if (GetIsUpdating(passwordBox))
        {
            return;
        }

        passwordBox.Password = (string)e.NewValue ?? string.Empty;
    }

    private static void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (sender is not PasswordBox passwordBox)
        {
            return;
        }

        SetIsUpdating(passwordBox, true);
        SetBoundPassword(passwordBox, passwordBox.Password);
        SetIsUpdating(passwordBox, false);
    }
}
