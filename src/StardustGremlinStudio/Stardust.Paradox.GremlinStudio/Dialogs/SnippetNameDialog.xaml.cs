using System.Windows;
using System.Windows.Input;

namespace Stardust.Paradox.GremlinStudio.Dialogs;

/// <summary>
/// Dialog for entering a snippet name when saving a query as a snippet.
/// </summary>
public partial class SnippetNameDialog : Window
{
    public SnippetNameDialog()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            SnippetNameTextBox.Focus();
            SnippetNameTextBox.SelectAll();
        };
    }

    /// <summary>
    /// The snippet name entered by the user. Null if cancelled.
    /// </summary>
    public string? SnippetName { get; private set; }

    /// <summary>
    /// Sets the initial suggested name.
    /// </summary>
    public void SetSuggestedName(string name)
    {
        SnippetNameTextBox.Text = name;
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        var name = SnippetNameTextBox.Text?.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            SnippetNameTextBox.Focus();
            return;
        }

        SnippetName = name;
        DialogResult = true;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void SnippetNameTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            SaveButton_Click(sender, e);
        }
    }
}
