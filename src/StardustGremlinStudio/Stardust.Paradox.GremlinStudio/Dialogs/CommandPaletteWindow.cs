using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Stardust.Paradox.GremlinStudio.Dialogs;

/// <summary>
/// A command shown in the command palette.
/// </summary>
public sealed record CommandPaletteEntry(string Title, ICommand Command);

/// <summary>
/// Command palette with type-to-filter.
/// </summary>
public sealed class CommandPaletteWindow : Window
{
    private readonly IReadOnlyList<CommandPaletteEntry> _entries;
    private readonly TextBox _search;
    private readonly ListBox _list;

    public CommandPaletteWindow(IReadOnlyList<CommandPaletteEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        _entries = entries;

        Title = "Command Palette";
        Width = 520;
        Height = 420;
        WindowStyle = WindowStyle.ToolWindow;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        SetResourceReference(BackgroundProperty, "PrimaryBackgroundBrush");

        _search = new TextBox { Margin = new Thickness(8), Padding = new Thickness(6, 4, 6, 4), FontSize = 14 };
        _search.SetResourceReference(BackgroundProperty, "SecondaryBackgroundBrush");
        _search.SetResourceReference(ForegroundProperty, "PrimaryForegroundBrush");
        _search.TextChanged += (_, _) => ApplyFilter();
        _search.PreviewKeyDown += Search_PreviewKeyDown;

        _list = new ListBox { Margin = new Thickness(8, 0, 8, 8), DisplayMemberPath = nameof(CommandPaletteEntry.Title) };
        _list.SetResourceReference(BackgroundProperty, "PrimaryBackgroundBrush");
        _list.SetResourceReference(ForegroundProperty, "PrimaryForegroundBrush");
        _list.MouseDoubleClick += (_, _) => Accept();
        _list.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                Accept();
                e.Handled = true;
            }
        };

        var panel = new DockPanel();
        DockPanel.SetDock(_search, Dock.Top);
        panel.Children.Add(_search);
        panel.Children.Add(_list);
        Content = panel;

        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                DialogResult = false;
                e.Handled = true;
            }
        };
        Loaded += (_, _) => _search.Focus();
        ApplyFilter();
    }

    /// <summary>
    /// Gets the entry chosen by the user.
    /// </summary>
    public CommandPaletteEntry? SelectedEntry { get; private set; }

    private void ApplyFilter()
    {
        var terms = _search.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        _list.ItemsSource = _entries
            .Where(e => terms.All(t => e.Title.Contains(t, StringComparison.OrdinalIgnoreCase)))
            .ToList();
        _list.SelectedIndex = _list.Items.Count > 0 ? 0 : -1;
    }

    private void Search_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Down when _list.Items.Count > 0:
                _list.SelectedIndex = Math.Min(_list.SelectedIndex + 1, _list.Items.Count - 1);
                _list.ScrollIntoView(_list.SelectedItem);
                e.Handled = true;
                break;
            case Key.Up when _list.Items.Count > 0:
                _list.SelectedIndex = Math.Max(_list.SelectedIndex - 1, 0);
                _list.ScrollIntoView(_list.SelectedItem);
                e.Handled = true;
                break;
            case Key.Enter:
                Accept();
                e.Handled = true;
                break;
        }
    }

    private void Accept()
    {
        if (_list.SelectedItem is not CommandPaletteEntry entry) return;
        SelectedEntry = entry;
        DialogResult = true;
    }
}
