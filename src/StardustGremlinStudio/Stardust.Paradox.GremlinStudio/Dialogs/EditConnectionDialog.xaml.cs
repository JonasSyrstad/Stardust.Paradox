using System.Windows;
using Stardust.Paradox.GremlinStudio.ViewModels;

namespace Stardust.Paradox.GremlinStudio.Dialogs;

/// <summary>
/// Dialog for editing an existing connection's settings.
/// </summary>
public partial class EditConnectionDialog : Window
{
    public EditConnectionDialog(EditConnectionDialogViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        viewModel.RequestClose += (result) =>
        {
            DialogResult = result;
            Close();
        };
    }

    /// <summary>
    /// Gets the result view model if dialog was confirmed.
    /// </summary>
    public EditConnectionDialogViewModel? ResultViewModel =>
        DialogResult == true ? DataContext as EditConnectionDialogViewModel : null;

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
