using System.Windows;
using System.Windows.Controls;

namespace Stardust.Paradox.GremlinStudio.Dialogs;

/// <summary>
/// Theme-aware replacement for <see cref="MessageBox"/> using the same chrome as the other dialogs.
/// </summary>
public partial class ThemedMessageBox : Window
{
    private MessageBoxResult _result;
    private readonly MessageBoxResult _closeResult;

    private ThemedMessageBox(string message, string title, MessageBoxButton buttons, MessageBoxImage image)
    {
        InitializeComponent();
        Title = title;
        MessageText.Text = message;
        TitleIcon.Text = "\uE7F4";

        (MessageIcon.Text, var brushKey) = image switch
        {
            MessageBoxImage.Error => ("\uEA39", "StatusErrorBrush"),
            MessageBoxImage.Warning => ("\uE7BA", "StatusWarningBrush"),
            MessageBoxImage.Question => ("\uE9CE", "StatusInfoBrush"),
            MessageBoxImage.Information => ("\uE946", "StatusInfoBrush"),
            _ => (string.Empty, "StatusInfoBrush")
        };
        MessageIcon.SetResourceReference(TextBlock.ForegroundProperty, brushKey);
        if (string.IsNullOrEmpty(MessageIcon.Text)) MessageIcon.Visibility = Visibility.Collapsed;

        switch (buttons)
        {
            case MessageBoxButton.OK:
                AddButton("OK", MessageBoxResult.OK, isDefault: true, isCancel: true);
                _closeResult = MessageBoxResult.OK;
                break;
            case MessageBoxButton.OKCancel:
                AddButton("OK", MessageBoxResult.OK, isDefault: true);
                AddButton("Cancel", MessageBoxResult.Cancel, isCancel: true);
                _closeResult = MessageBoxResult.Cancel;
                break;
            case MessageBoxButton.YesNo:
                AddButton("Yes", MessageBoxResult.Yes, isDefault: true);
                AddButton("No", MessageBoxResult.No, isCancel: true);
                _closeResult = MessageBoxResult.No;
                break;
            default:
                AddButton("Yes", MessageBoxResult.Yes, isDefault: true);
                AddButton("No", MessageBoxResult.No);
                AddButton("Cancel", MessageBoxResult.Cancel, isCancel: true);
                _closeResult = MessageBoxResult.Cancel;
                break;
        }

        _result = _closeResult;
    }

    /// <summary>
    /// Shows a themed message box owned by the active application window.
    /// </summary>
    public static MessageBoxResult Show(string message, string title,
        MessageBoxButton buttons = MessageBoxButton.OK, MessageBoxImage image = MessageBoxImage.None)
    {
        var dialog = new ThemedMessageBox(message, title, buttons, image);
        var owner = Application.Current?.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
                    ?? Application.Current?.MainWindow;
        if (owner != null && owner != dialog && owner.IsVisible)
        {
            dialog.Owner = owner;
        }
        else
        {
            dialog.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }

        dialog.ShowDialog();
        return dialog._result;
    }

    private void AddButton(string text, MessageBoxResult result, bool isDefault = false, bool isCancel = false)
    {
        var button = new Button
        {
            Content = text,
            MinWidth = 80,
            Padding = new Thickness(8, 6, 8, 6),
            Margin = new Thickness(8, 0, 0, 0),
            IsDefault = isDefault,
            IsCancel = isCancel
        };
        if (isDefault)
        {
            button.SetResourceReference(BackgroundProperty, "AccentBrush");
            button.Foreground = System.Windows.Media.Brushes.White;
            Loaded += (_, _) => button.Focus();
        }

        button.Click += (_, _) =>
        {
            _result = result;
            DialogResult = result is MessageBoxResult.OK or MessageBoxResult.Yes;
        };
        ButtonPanel.Children.Add(button);
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        _result = _closeResult;
        DialogResult = false;
    }
}
