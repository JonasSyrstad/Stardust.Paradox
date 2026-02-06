using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Stardust.Paradox.GremlinStudio.ViewModels;

/// <summary>
/// Represents a node in the result tree view.
/// </summary>
public class TreeNodeViewModel : INotifyPropertyChanged
{
    public string Icon { get; set; } = "[.]";
    public string Name { get; set; } = string.Empty;
    public string? Value { get; set; }
    public string FontWeight { get; set; } = "Normal";
    public List<TreeNodeViewModel> Children { get; set; } = new();
    
    /// <summary>
    /// Gets whether this node has children (used to show expand/collapse button).
    /// </summary>
    public bool HasChildren => Children.Count > 0;

    private bool _isExpanded;
    /// <summary>
    /// Gets or sets whether this node is expanded.
    /// </summary>
    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (_isExpanded != value)
            {
                _isExpanded = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ExpandCollapseIcon));
                OnPropertyChanged(nameof(ExpandCollapseTooltip));
            }
        }
    }

    /// <summary>
    /// Gets the icon for the expand/collapse button.
    /// </summary>
    public string ExpandCollapseIcon => IsExpanded ? "−" : "+";

    /// <summary>
    /// Gets the tooltip for the expand/collapse button.
    /// </summary>
    public string ExpandCollapseTooltip => IsExpanded ? "Collapse all children" : "Expand all children";

    /// <summary>
    /// Toggles the expand/collapse state for this node and all its descendants.
    /// </summary>
    public void ToggleExpandCollapseAll()
    {
        bool newState = !IsExpanded;
        SetExpandedRecursive(newState);
    }

    /// <summary>
    /// Sets the expanded state recursively for this node and all descendants.
    /// </summary>
    private void SetExpandedRecursive(bool expanded)
    {
        IsExpanded = expanded;
        foreach (var child in Children)
        {
            child.SetExpandedRecursive(expanded);
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public static TreeNodeViewModel FromObject(string name, object? value)
    {
        var node = new TreeNodeViewModel { Name = name };

        if (value is null)
        {
            node.Value = "null";
            node.Icon = "[o]";
            return node;
        }

        // Handle Newtonsoft.Json types
        if (value is Newtonsoft.Json.Linq.JObject jObj)
        {
            node.Icon = "{.}";
            node.FontWeight = "SemiBold";
            foreach (var prop in jObj.Properties())
            {
                node.Children.Add(FromObject(prop.Name, prop.Value));
            }
        }
        else if (value is Newtonsoft.Json.Linq.JArray jArr)
        {
            node.Icon = "[#]";
            node.FontWeight = "SemiBold";
            for (int i = 0; i < jArr.Count; i++)
            {
                node.Children.Add(FromObject($"[{i}]", jArr[i]));
            }
        }
        else if (value is Newtonsoft.Json.Linq.JValue jVal)
        {
            var innerValue = jVal.Value;
            if (innerValue is null)
            {
                node.Value = "null";
                node.Icon = "[o]";
            }
            else if (innerValue is string s)
            {
                node.Value = $"\"{s}\"";
                node.Icon = "[S]";
            }
            else if (innerValue is bool b)
            {
                node.Value = b.ToString().ToLower();
                node.Icon = b ? "[+]" : "[-]";
            }
            else if (innerValue is long or int or double or float or decimal)
            {
                node.Value = innerValue.ToString();
                node.Icon = "[N]";
            }
            else
            {
                node.Value = innerValue.ToString();
                node.Icon = "[.]";
            }
        }
        else if (value is IDictionary<string, object> dict)
        {
            node.Icon = "{.}";
            node.FontWeight = "SemiBold";
            foreach (var kvp in dict)
            {
                node.Children.Add(FromObject(kvp.Key, kvp.Value));
            }
        }
        else if (value is IList<object> list)
        {
            node.Icon = "[#]";
            node.FontWeight = "SemiBold";
            for (int i = 0; i < list.Count; i++)
            {
                node.Children.Add(FromObject($"[{i}]", list[i]));
            }
        }
        else if (value is string str)
        {
            node.Value = $"\"{str}\"";
            node.Icon = "[S]";
        }
        else if (value is bool bv)
        {
            node.Value = bv.ToString().ToLower();
            node.Icon = bv ? "[+]" : "[-]";
        }
        else if (value is int or long or double or float or decimal)
        {
            node.Value = value.ToString();
            node.Icon = "[N]";
        }
        else
        {
            node.Value = value.ToString();
            node.Icon = "[.]";
        }

        return node;
    }
}
