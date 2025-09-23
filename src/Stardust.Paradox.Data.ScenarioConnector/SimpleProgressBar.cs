using System;

namespace Stardust.Paradox.Data.ScenarioConnector;

/// <summary>
/// Simple ASCII-only progress bar for maximum compatibility
/// </summary>
public class SimpleProgressBar : IDisposable
{
    private readonly int _total;
    private readonly string _description;
    private readonly int _barWidth;
    private int _current;
    private bool _disposed;
    private readonly object _lock = new object();

    public SimpleProgressBar(int total, string description = "Progress", int barWidth = 50)
    {
        _total = total;
        _description = description;
        _barWidth = barWidth;
        _current = 0;
        
        // Initialize the progress bar
        UpdateDisplay();
    }

    /// <summary>
    /// Update progress by incrementing current value
    /// </summary>
    public void Increment(string? currentItem = null)
    {
        lock (_lock)
        {
            if (_disposed) return;
            
            _current++;
            if (_current > _total) _current = _total;
            
            UpdateDisplay(currentItem);
        }
    }

    /// <summary>
    /// Set progress to a specific value
    /// </summary>
    public void SetProgress(int current, string? currentItem = null)
    {
        lock (_lock)
        {
            if (_disposed) return;
            
            _current = Math.Min(current, _total);
            UpdateDisplay(currentItem);
        }
    }

    /// <summary>
    /// Complete the progress bar
    /// </summary>
    public void Complete(string? message = null)
    {
        lock (_lock)
        {
            if (_disposed) return;
            
            _current = _total;
            UpdateDisplay(message ?? "Complete!");
            Console.WriteLine(); // Move to next line
        }
    }

    private void UpdateDisplay(string? currentItem = null)
    {
        if (_disposed) return;
        
        var percentage = _total == 0 ? 100 : (_current * 100.0) / _total;
        var filled = (int)((_current * (double)_barWidth) / _total);
        
        // Use ASCII characters that work everywhere
        var bar = new string('=', filled) + new string('.', _barWidth - filled);
        
        var itemText = string.IsNullOrEmpty(currentItem) ? "" : $" | {currentItem}";
        var progressText = $"\r{_description}: [{bar}] {percentage:F1}% ({_current}/{_total}){itemText}";
        
        // Truncate if too long for console (with safety check)
        try
        {
            var consoleWidth = Console.WindowWidth;
            if (progressText.Length > consoleWidth - 1)
            {
                var maxItemLength = consoleWidth - 1 - progressText.Length + (currentItem?.Length ?? 0);
                if (maxItemLength > 10 && !string.IsNullOrEmpty(currentItem))
                {
                    var truncatedItem = currentItem.Length > maxItemLength 
                        ? currentItem.Substring(0, maxItemLength - 3) + "..."
                        : currentItem;
                    itemText = $" | {truncatedItem}";
                    progressText = $"\r{_description}: [{bar}] {percentage:F1}% ({_current}/{_total}){itemText}";
                }
                else
                {
                    itemText = "";
                    progressText = $"\r{_description}: [{bar}] {percentage:F1}% ({_current}/{_total})";
                }
            }
        }
        catch
        {
            // If console width is not available, use a safe fallback
            itemText = "";
            progressText = $"\r{_description}: [{bar}] {percentage:F1}% ({_current}/{_total})";
        }
        
        Console.Write(progressText);
    }

    public void Dispose()
    {
        lock (_lock)
        {
            if (!_disposed)
            {
                Complete();
                _disposed = true;
            }
        }
    }
}

/// <summary>
/// Factory for creating simple progress bars
/// </summary>
public static class SimpleProgressBarFactory
{
    /// <summary>
    /// Create a simple ASCII progress bar
    /// </summary>
    public static SimpleProgressBar Create(int total, string description)
    {
        return new SimpleProgressBar(total, description, 40);
    }

    /// <summary>
    /// Create a progress bar for vertex processing
    /// </summary>
    public static SimpleProgressBar CreateVertexProgress(int vertexCount)
    {
        return new SimpleProgressBar(vertexCount, "Processing Vertices", 40);
    }

    /// <summary>
    /// Create a progress bar for edge processing
    /// </summary>
    public static SimpleProgressBar CreateEdgeProgress(int edgeCount)
    {
        return new SimpleProgressBar(edgeCount, "Processing Edges", 40);
    }

    /// <summary>
    /// Create a progress bar for property enhancement
    /// </summary>
    public static SimpleProgressBar CreatePropertyProgress(int itemCount)
    {
        return new SimpleProgressBar(itemCount, "Enhancing Properties", 40);
    }

    /// <summary>
    /// Create a progress bar for file operations
    /// </summary>
    public static SimpleProgressBar CreateFileProgress(int fileCount)
    {
        return new SimpleProgressBar(fileCount, "Saving Files", 40);
    }
}