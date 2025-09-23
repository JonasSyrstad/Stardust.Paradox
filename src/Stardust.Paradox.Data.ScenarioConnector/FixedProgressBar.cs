using System;

namespace Stardust.Paradox.Data.ScenarioConnector;

/// <summary>
/// Fixed-position progress bar that appears at the top of the console
/// </summary>
public class FixedProgressBar : IDisposable
{
    private readonly int _total;
    private readonly string _description;
    private readonly int _barWidth;
    private int _current;
    private bool _disposed;
    private readonly object _lock = new object();
    private static readonly object _globalLock = new object();
    private static FixedProgressBar? _activeProgressBar;
    private static int _savedCursorTop;
    private static int _savedCursorLeft;
    private static bool _positionSaved = false;

    public FixedProgressBar(int total, string description = "Progress", int barWidth = 40)
    {
        _total = total;
        _description = description;
        _barWidth = Math.Min(barWidth, 50); // Limit width for better compatibility
        _current = 0;
        
        RegisterAsActive();
        UpdateDisplay();
    }

    private void RegisterAsActive()
    {
        lock (_globalLock)
        {
            // Complete any existing progress bar
            _activeProgressBar?.CompleteInternal();
            
            _activeProgressBar = this;
            
            // Save current cursor position
            if (!_positionSaved)
            {
                try
                {
                    _savedCursorLeft = Console.CursorLeft;
                    _savedCursorTop = Console.CursorTop;
                    _positionSaved = true;
                    
                    // Clear the first two lines for our progress bar
                    Console.SetCursorPosition(0, 0);
                    ClearLine();
                    Console.SetCursorPosition(0, 1);
                    ClearLine();
                    Console.SetCursorPosition(0, 0);
                }
                catch
                {
                    // If cursor positioning fails, fall back to inline mode
                    _positionSaved = false;
                }
            }
        }
    }

    private static void ClearLine()
    {
        try
        {
            Console.Write(new string(' ', Console.WindowWidth - 1));
        }
        catch
        {
            Console.Write(new string(' ', 80)); // Fallback width
        }
    }

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

    public void SetProgress(int current, string? currentItem = null)
    {
        lock (_lock)
        {
            if (_disposed) return;
            
            _current = Math.Min(current, _total);
            UpdateDisplay(currentItem);
        }
    }

    public void Complete(string? message = null)
    {
        lock (_lock)
        {
            if (_disposed) return;
            CompleteInternal(message);
        }
    }

    private void CompleteInternal(string? message = null)
    {
        if (_disposed) return;
        
        _current = _total;
        UpdateDisplay(message ?? "Complete!");
        
        // Small delay to show completion
        System.Threading.Thread.Sleep(500);
        
        lock (_globalLock)
        {
            if (_activeProgressBar == this)
            {
                try
                {
                    if (_positionSaved)
                    {
                        // Clear progress bar lines
                        Console.SetCursorPosition(0, 0);
                        ClearLine();
                        Console.SetCursorPosition(0, 1);
                        ClearLine();
                        
                        // Restore cursor to a safe position
                        var newTop = Math.Max(_savedCursorTop + 2, 2);
                        Console.SetCursorPosition(0, Math.Min(newTop, Console.BufferHeight - 1));
                    }
                    else
                    {
                        Console.WriteLine(); // Add line break for inline mode
                    }
                }
                catch
                {
                    Console.WriteLine(); // Fallback
                }
                
                _activeProgressBar = null;
                _positionSaved = false;
            }
        }
    }

    private void UpdateDisplay(string? currentItem = null)
    {
        if (_disposed) return;
        
        lock (_globalLock)
        {
            if (_activeProgressBar != this) return;
            
            var percentage = _total == 0 ? 100 : (_current * 100.0) / _total;
            var filled = (int)((_current * (double)_barWidth) / _total);
            
            // Use simple ASCII characters for maximum compatibility
            var progressBar = new string('=', filled) + new string('.', _barWidth - filled);
            
            // Format the display text
            var baseText = $"{_description}: [{progressBar}] {percentage:F1}% ({_current}/{_total})";
            
            // Add current item if provided and space allows
            var displayText = baseText;
            if (!string.IsNullOrEmpty(currentItem))
            {
                var itemText = $" | {currentItem}";
                try
                {
                    var maxWidth = Console.WindowWidth - 1;
                    if (baseText.Length + itemText.Length <= maxWidth)
                    {
                        displayText = baseText + itemText;
                    }
                    else if (baseText.Length < maxWidth - 10)
                    {
                        var maxItemLength = maxWidth - baseText.Length - 3; // Space for " | "
                        if (maxItemLength > 5)
                        {
                            var truncatedItem = currentItem.Length > maxItemLength - 3
                                ? currentItem.Substring(0, maxItemLength - 3) + "..."
                                : currentItem;
                            displayText = baseText + $" | {truncatedItem}";
                        }
                    }
                }
                catch
                {
                    // If we can't determine console width, stick with base text
                }
            }
            
            // Display the progress bar
            try
            {
                if (_positionSaved)
                {
                    // Save current position
                    var currentLeft = Console.CursorLeft;
                    var currentTop = Console.CursorTop;
                    
                    // Write to fixed position (top line)
                    Console.SetCursorPosition(0, 0);
                    Console.Write(displayText.PadRight(Math.Min(Console.WindowWidth - 1, 120)));
                    
                    // Restore cursor
                    Console.SetCursorPosition(currentLeft, currentTop);
                }
                else
                {
                    // Inline mode fallback
                    Console.Write($"\r{displayText}");
                }
            }
            catch
            {
                // Ultimate fallback
                Console.Write($"\r{baseText}");
            }
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            if (!_disposed)
            {
                CompleteInternal();
                _disposed = true;
            }
        }
    }
}

/// <summary>
/// Factory for creating fixed-position progress bars
/// </summary>
public static class FixedProgressBarFactory
{
    public static FixedProgressBar Create(int total, string description)
        => new FixedProgressBar(total, description, 40);

    public static FixedProgressBar CreateVertexProgress(int vertexCount)
        => new FixedProgressBar(vertexCount, "Processing Vertices", 40);

    public static FixedProgressBar CreateEdgeProgress(int edgeCount)
        => new FixedProgressBar(edgeCount, "Processing Edges", 40);

    public static FixedProgressBar CreatePropertyProgress(int itemCount)
        => new FixedProgressBar(itemCount, "Enhancing Properties", 40);

    public static FixedProgressBar CreateFileProgress(int fileCount)
        => new FixedProgressBar(fileCount, "Saving Files", 40);
}