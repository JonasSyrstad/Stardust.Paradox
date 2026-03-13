using System.Collections.ObjectModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Stardust.Paradox.GremlinStudio.Core.Connections;
using Stardust.Paradox.GremlinStudio.Core.Variables;

namespace Stardust.Paradox.GremlinStudio.ViewModels;

/// <summary>
/// ViewModel for the variable set editor dialog. Provides a key-value editor
/// for global variables and per-connection overrides.
/// </summary>
public partial class VariableSetDialogViewModel : ObservableObject
{
    private readonly QueryVariableSet? _existingSet;

    public VariableSetDialogViewModel(
        IReadOnlyList<GremlinConnectionMetadata> connections,
        QueryVariableSet? existingSet = null)
    {
        _existingSet = existingSet;
        AvailableConnections = connections;

        _setName = existingSet?.Name ?? string.Empty;

        // Load global variables
        GlobalVariables = ParseVariables(existingSet?.Json);

        // Load connection tabs
        ConnectionTabs = new ObservableCollection<ConnectionVariableTab>();
        if (existingSet?.ConnectionVariables != null)
        {
            foreach (var (connId, json) in existingSet.ConnectionVariables)
            {
                var conn = connections.FirstOrDefault(c => c.Id == connId);
                var tab = new ConnectionVariableTab(
                    connId,
                    conn?.Name ?? connId,
                    ParseVariables(json));
                ConnectionTabs.Add(tab);
            }
        }

        UpdateJsonPreview();
    }

    public event Action<bool>? RequestClose;

    /// <summary>
    /// Whether this dialog is editing an existing variable set (vs. creating new).
    /// </summary>
    public bool IsEditing => _existingSet != null;

    /// <summary>
    /// Dialog title reflecting create vs. edit mode.
    /// </summary>
    public string DialogTitle => IsEditing ? "Edit Variable Set" : "New Variable Set";

    /// <summary>
    /// All available connections for scoping variables.
    /// </summary>
    public IReadOnlyList<GremlinConnectionMetadata> AvailableConnections { get; }

    [ObservableProperty]
    private string _setName;

    /// <summary>
    /// Global variables that apply to all connections.
    /// </summary>
    public ObservableCollection<VariableEntry> GlobalVariables { get; }

    /// <summary>
    /// Per-connection variable override tabs.
    /// </summary>
    public ObservableCollection<ConnectionVariableTab> ConnectionTabs { get; }

    /// <summary>
    /// The connection selected in the "Add Connection" dropdown.
    /// </summary>
    [ObservableProperty]
    private GremlinConnectionMetadata? _selectedConnectionToAdd;

    /// <summary>
    /// The currently selected connection tab for editing.
    /// </summary>
    [ObservableProperty]
    private ConnectionVariableTab? _selectedConnectionTab;

    /// <summary>
    /// Live JSON preview of the merged variables.
    /// </summary>
    [ObservableProperty]
    private string _jsonPreview = string.Empty;

    /// <summary>
    /// Validation error message (empty when valid).
    /// </summary>
    [ObservableProperty]
    private string _errorMessage = string.Empty;

    /// <summary>
    /// Whether an error is present.
    /// </summary>
    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    partial void OnErrorMessageChanged(string value) =>
        OnPropertyChanged(nameof(HasError));

    #region Global Variable Commands

    [RelayCommand]
    private void AddGlobalVariable()
    {
        var entry = new VariableEntry();
        entry.PropertyChanged += (_, _) => UpdateJsonPreview();
        GlobalVariables.Add(entry);
        UpdateJsonPreview();
    }

    [RelayCommand]
    private void RemoveGlobalVariable(VariableEntry? entry)
    {
        if (entry == null) return;
        GlobalVariables.Remove(entry);
        UpdateJsonPreview();
    }

    #endregion

    #region Connection Tab Commands

    /// <summary>
    /// Adds a connection-specific override tab for the selected connection.
    /// </summary>
    [RelayCommand]
    private void AddConnectionTab()
    {
        if (SelectedConnectionToAdd == null) return;

        // Don't add duplicate tabs
        if (ConnectionTabs.Any(t => t.ConnectionId == SelectedConnectionToAdd.Id))
        {
            ErrorMessage = $"Connection '{SelectedConnectionToAdd.Name}' already has a tab";
            return;
        }

        var tab = new ConnectionVariableTab(
            SelectedConnectionToAdd.Id,
            SelectedConnectionToAdd.Name,
            new ObservableCollection<VariableEntry>());
        ConnectionTabs.Add(tab);
        SelectedConnectionTab = tab;
        SelectedConnectionToAdd = null;
        ErrorMessage = string.Empty;
        UpdateJsonPreview();
    }

    /// <summary>
    /// Removes a connection override tab.
    /// </summary>
    [RelayCommand]
    private void RemoveConnectionTab(ConnectionVariableTab? tab)
    {
        if (tab == null) return;
        ConnectionTabs.Remove(tab);
        if (SelectedConnectionTab == tab)
            SelectedConnectionTab = ConnectionTabs.FirstOrDefault();
        UpdateJsonPreview();
    }

    [RelayCommand]
    private void AddConnectionVariable(ConnectionVariableTab? tab)
    {
        if (tab == null) return;
        var entry = new VariableEntry();
        entry.PropertyChanged += (_, _) => UpdateJsonPreview();
        tab.Variables.Add(entry);
        UpdateJsonPreview();
    }

    [RelayCommand]
    private void RemoveConnectionVariable(VariableEntry? entry)
    {
        if (entry == null || SelectedConnectionTab == null) return;
        SelectedConnectionTab.Variables.Remove(entry);
        UpdateJsonPreview();
    }

    #endregion

    #region Save / Cancel

    [RelayCommand]
    private void Save()
    {
        if (string.IsNullOrWhiteSpace(SetName))
        {
            ErrorMessage = "Variable set name is required";
            return;
        }

        // Validate that keys are non-empty where values exist
        var invalidGlobal = GlobalVariables.Any(v => string.IsNullOrWhiteSpace(v.Key) && !string.IsNullOrWhiteSpace(v.Value));
        if (invalidGlobal)
        {
            ErrorMessage = "All variables must have a key name";
            return;
        }

        ErrorMessage = string.Empty;
        RequestClose?.Invoke(true);
    }

    [RelayCommand]
    private void Cancel()
    {
        RequestClose?.Invoke(false);
    }

    #endregion

    /// <summary>
    /// Builds the final <see cref="QueryVariableSet"/> from the dialog state.
    /// </summary>
    public QueryVariableSet BuildResult()
    {
        var globalJson = VariablesToJson(GlobalVariables);

        Dictionary<string, string>? connectionVars = null;
        if (ConnectionTabs.Count > 0)
        {
            connectionVars = new Dictionary<string, string>();
            foreach (var tab in ConnectionTabs)
            {
                var json = VariablesToJson(tab.Variables);
                if (json != "{}")
                {
                    connectionVars[tab.ConnectionId] = json;
                }
            }

            if (connectionVars.Count == 0)
                connectionVars = null;
        }

        var id = _existingSet?.Id ?? Guid.NewGuid().ToString();
        return new QueryVariableSet(
            id,
            SetName.Trim(),
            globalJson,
            _existingSet?.CreatedAt ?? DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            connectionVars);
    }

    private void UpdateJsonPreview()
    {
        try
        {
            var globalJson = VariablesToJson(GlobalVariables);

            if (ConnectionTabs.Count == 0)
            {
                JsonPreview = FormatJson(globalJson);
                return;
            }

            // Show a combined preview
            var preview = new Dictionary<string, object>
            {
                ["_global"] = JsonDocument.Parse(globalJson).RootElement.Clone()
            };

            foreach (var tab in ConnectionTabs)
            {
                var connJson = VariablesToJson(tab.Variables);
                preview[$"_{tab.ConnectionName}"] = JsonDocument.Parse(connJson).RootElement.Clone();
            }

            JsonPreview = FormatJson(JsonSerializer.Serialize(preview));
        }
        catch
        {
            JsonPreview = "{ }";
        }
    }

    private static ObservableCollection<VariableEntry> ParseVariables(string? json)
    {
        var entries = new ObservableCollection<VariableEntry>();
        if (string.IsNullOrWhiteSpace(json))
            return entries;

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in doc.RootElement.EnumerateObject())
                {
                    var value = prop.Value.ValueKind == JsonValueKind.String
                        ? prop.Value.GetString() ?? string.Empty
                        : prop.Value.GetRawText();

                    entries.Add(new VariableEntry { Key = prop.Name, Value = value });
                }
            }
        }
        catch
        {
            // If JSON is invalid, return empty
        }

        return entries;
    }

    private static string VariablesToJson(ObservableCollection<VariableEntry> variables)
    {
        var dict = new Dictionary<string, object>();
        foreach (var v in variables.Where(v => !string.IsNullOrWhiteSpace(v.Key)))
        {
            // Try to preserve type (number, bool) for cleaner JSON
            if (bool.TryParse(v.Value, out var boolVal))
                dict[v.Key] = boolVal;
            else if (long.TryParse(v.Value, out var longVal))
                dict[v.Key] = longVal;
            else if (double.TryParse(v.Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var doubleVal)
                     && v.Value.Contains('.'))
                dict[v.Key] = doubleVal;
            else
                dict[v.Key] = v.Value ?? string.Empty;
        }

        return JsonSerializer.Serialize(dict);
    }

    private static string FormatJson(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            return JsonSerializer.Serialize(doc, new JsonSerializerOptions { WriteIndented = true });
        }
        catch
        {
            return json;
        }
    }
}

/// <summary>
/// A single key-value variable entry in the editor.
/// </summary>
public partial class VariableEntry : ObservableObject
{
    [ObservableProperty]
    private string _key = string.Empty;

    [ObservableProperty]
    private string _value = string.Empty;
}

/// <summary>
/// Represents a connection-specific variable override tab.
/// </summary>
public sealed class ConnectionVariableTab
{
    public ConnectionVariableTab(
        string connectionId,
        string connectionName,
        ObservableCollection<VariableEntry> variables)
    {
        ConnectionId = connectionId;
        ConnectionName = connectionName;
        Variables = variables;
    }

    public string ConnectionId { get; }
    public string ConnectionName { get; }
    public ObservableCollection<VariableEntry> Variables { get; }
}
