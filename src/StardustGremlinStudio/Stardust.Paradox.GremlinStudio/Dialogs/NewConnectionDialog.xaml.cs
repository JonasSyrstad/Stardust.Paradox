using System.Windows;
using Stardust.Paradox.GremlinStudio.ViewModels;

namespace Stardust.Paradox.GremlinStudio.Dialogs;

/// <summary>
/// Dialog for creating a new connection with wizard-style guidance.
/// </summary>
public partial class NewConnectionDialog : Window
{
    public NewConnectionDialog(NewConnectionDialogViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        viewModel.RequestClose += (result) =>
        {
            DialogResult = result;
            Close();
        };

        // Wire up password box (can't bind directly)
        PasswordBox.PasswordChanged += (s, e) =>
        {
            viewModel.Password = PasswordBox.Password;
        };
    }

    /// <summary>
    /// Gets the result settings if dialog was confirmed.
    /// </summary>
    public NewConnectionDialogViewModel? ResultViewModel => 
        DialogResult == true ? DataContext as NewConnectionDialogViewModel : null;

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
