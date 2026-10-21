using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;

namespace Stardust.Paradox.GremlinStudio.ViewModels;

/// <summary>
/// Represents a node in the result tree view.
/// </summary>
public class TreeNodeViewModel : INotifyPropertyChanged
{
    private const int BatchSize = 50; // Nodes to process before yielding to UI
    
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
    
    private bool _isProcessing;
    /// <summary>
    /// Gets whether an expand/collapse operation is in progress.
    /// </summary>
    public bool IsProcessing
    {
        get => _isProcessing;
        private set
        {
            if (_isProcessing != value)
            {
                _isProcessing = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ExpandCollapseIcon));
            }
        }
    }

    /// <summary>
    /// Gets the icon for the expand/collapse button.
    /// </summary>
    public string ExpandCollapseIcon => IsProcessing ? "⏳" : (IsExpanded ? "−" : "+");

    /// <summary>
    /// Gets the tooltip for the expand/collapse button.
    /// </summary>
    public string ExpandCollapseTooltip => IsExpanded ? "Collapse all children" : "Expand all children";

    /// <summary>
    /// Toggles the expand/collapse state for this node and all its descendants.
    /// Runs asynchronously to keep UI responsive.
    /// </summary>
    public void ToggleExpandCollapseAll()
    {
        if (IsProcessing)
            return;
            
        bool newState = !IsExpanded;
        _ = SetExpandedAllAsync(newState);
    }

    /// <summary>
    /// Asynchronously sets the expanded state for this node and all descendants.
    /// Uses iterative processing with batching to avoid stack overflow and UI freezing.
    /// </summary>
    public async Task SetExpandedAllAsync(bool expanded, CancellationToken cancellationToken = default)
    {
        if (IsProcessing)
            return;

        IsProcessing = true;
        
        try
        {
            // Use iterative approach with stack to avoid stack overflow on deep trees
            var nodesToProcess = new Stack<TreeNodeViewModel>();
            nodesToProcess.Push(this);
            
            int processedCount = 0;
            
            while (nodesToProcess.Count > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                var node = nodesToProcess.Pop();
                
                // Set expanded state directly (minimal notifications)
                if (node._isExpanded != expanded)
                {
                    node._isExpanded = expanded;
                    node.OnPropertyChanged(nameof(IsExpanded));
                }
                
                processedCount++;
                
                // Add children to stack (reverse order to maintain traversal order)
                for (int i = node.Children.Count - 1; i >= 0; i--)
                {
                    nodesToProcess.Push(node.Children[i]);
                }
                
                // Yield to UI thread periodically to keep it responsive
                if (processedCount % BatchSize == 0 && nodesToProcess.Count > 0)
                {
                    await Task.Delay(1, cancellationToken).ConfigureAwait(true);
                }
            }
            
            // Update button state after all nodes processed
            OnPropertyChanged(nameof(ExpandCollapseIcon));
            OnPropertyChanged(nameof(ExpandCollapseTooltip));
        }
        finally
        {
            IsProcessing = false;
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
