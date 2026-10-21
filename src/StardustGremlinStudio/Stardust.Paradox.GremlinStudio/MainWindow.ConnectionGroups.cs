using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Stardust.Paradox.GremlinStudio.Core.Storage;

namespace Stardust.Paradox.GremlinStudio;

/// <summary>
/// Handles expand/collapse state of the connection groups in the Connections list.
/// Collapsed group names are persisted so the state survives re-rendering and restarts.
/// </summary>
public partial class MainWindow
{
    private HashSet<string>? _collapsedConnectionGroups;
    private readonly List<Expander> _connectionGroupExpanders = new();
    private bool _isApplyingGroupState;

    private HashSet<string> CollapsedConnectionGroups => _collapsedConnectionGroups ??= LoadCollapsedConnectionGroups();

    private static HashSet<string> LoadCollapsedConnectionGroups()
    {
        try
        {
            var path = AppDataPaths.CollapsedConnectionGroupsFilePath;
            if (!File.Exists(path))
                return new HashSet<string>(StringComparer.Ordinal);

            return new HashSet<string>(
                File.ReadAllLines(path).Where(l => !string.IsNullOrWhiteSpace(l)),
                StringComparer.Ordinal);
        }
        catch (IOException)
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }
    }

    private void SaveCollapsedConnectionGroups()
    {
        try
        {
            File.WriteAllLines(AppDataPaths.CollapsedConnectionGroupsFilePath, CollapsedConnectionGroups.OrderBy(n => n));
        }
        catch (IOException ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to save connection group state: {ex.Message}");
        }
    }

    private static string? GetGroupName(Expander expander)
        => (expander.DataContext as CollectionViewGroup)?.Name?.ToString();

    private void ConnectionGroupExpander_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is not Expander expander)
            return;

        if (!_connectionGroupExpanders.Contains(expander))
            _connectionGroupExpanders.Add(expander);

        var name = GetGroupName(expander);
        _isApplyingGroupState = true;
        expander.IsExpanded = name is null || !CollapsedConnectionGroups.Contains(name);
        _isApplyingGroupState = false;
    }

    private void ConnectionGroupExpander_Unloaded(object sender, RoutedEventArgs e)
    {
        if (sender is Expander expander)
            _connectionGroupExpanders.Remove(expander);
    }

    private void ConnectionGroupExpander_StateChanged(object sender, RoutedEventArgs e)
    {
        if (_isApplyingGroupState || sender is not Expander expander || !ReferenceEquals(e.OriginalSource, expander))
            return;

        var name = GetGroupName(expander);
        if (name is null)
            return;

        var changed = expander.IsExpanded
            ? CollapsedConnectionGroups.Remove(name)
            : CollapsedConnectionGroups.Add(name);

        if (changed)
            SaveCollapsedConnectionGroups();
    }

    private void ExpandAllConnectionGroups_Click(object sender, RoutedEventArgs e) => SetAllConnectionGroups(true);

    private void CollapseAllConnectionGroups_Click(object sender, RoutedEventArgs e) => SetAllConnectionGroups(false);

    private void SetAllConnectionGroups(bool expanded)
    {
        _isApplyingGroupState = true;
        try
        {
            foreach (var expander in _connectionGroupExpanders)
            {
                expander.IsExpanded = expanded;
                var name = GetGroupName(expander);
                if (name is null)
                    continue;
                if (expanded)
                    CollapsedConnectionGroups.Remove(name);
                else
                    CollapsedConnectionGroups.Add(name);
            }
        }
        finally
        {
            _isApplyingGroupState = false;
        }

        SaveCollapsedConnectionGroups();
    }
}
