using System.Windows;
using System.Windows.Input;
using Stardust.Paradox.GremlinStudio.ViewModels;

namespace Stardust.Paradox.GremlinStudio.Dialogs;

/// <summary>
/// Dialog for creating and editing variable sets with per-connection overrides.
/// </summary>
public partial class VariableSetDialog : Window
{
    public VariableSetDialog(VariableSetDialogViewModel viewModel)
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
    /// Gets the result view model if the dialog was confirmed.
    /// </summary>
    public VariableSetDialogViewModel? ResultViewModel =>
        DialogResult == true ? DataContext as VariableSetDialogViewModel : null;

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void ConnectionTab_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement element && element.DataContext is ConnectionVariableTab tab)
        {
            var vm = DataContext as VariableSetDialogViewModel;
            if (vm != null)
            {
                vm.SelectedConnectionTab = tab;
            }
        }
    }
}
