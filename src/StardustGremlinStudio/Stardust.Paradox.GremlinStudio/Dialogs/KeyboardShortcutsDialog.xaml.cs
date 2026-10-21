using System.Windows;
using System.Windows.Input;

namespace Stardust.Paradox.GremlinStudio.Dialogs;

/// <summary>
/// Themed dialog showing keyboard shortcuts.
/// </summary>
public partial class KeyboardShortcutsDialog : Window
{
    public KeyboardShortcutsDialog()
    {
        InitializeComponent();
        
        // Allow Escape to close the dialog
        PreviewKeyDown += (s, e) =>
        {
            if (e.Key == Key.Escape)
            {
                Close();
                e.Handled = true;
            }
        };
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
    
    /// <summary>
    /// Allow dragging the window from anywhere.
    /// </summary>
    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        
        // Only allow dragging if left mouse button is pressed
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            try
            {
                DragMove();
            }
            catch
            {
                // DragMove can throw if mouse button is released during the operation
            }
        }
    }
}
