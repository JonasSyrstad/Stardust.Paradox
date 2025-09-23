using System;

namespace Stardust.Paradox.Data.ScenarioConnector;

/// <summary>
/// Simple console progress bar utility for scenario export operations with fixed positioning
/// </summary>
public class ProgressBar : IDisposable
{
    private readonly int _total;
    private readonly string _description;
    private readonly int _barWidth;
    private int _current;
    private bool _disposed;
    private readonly object _lock = new object();
    private static readonly object _globalLock = new object();
    private static ProgressBar? _activeProgressBar;
    private static int _originalCursorTop;
    private static bool _cursorPositionSaved = false;

    public ProgressBar(int total, string description = "Progress", int barWidth = 50)
    {
        _total = total;
        _description = description;
        _barWidth = barWidth;
        _current = 0;
        
        // Try to set console to support Unicode characters
        TrySetConsoleEncoding();
        
        // Register this progress bar as the active one
        RegisterAsActive();
        
        // Initialize the progress bar
        UpdateDisplay();
    }

    /// <summary>
    /// Register this progress bar as the active one
    /// </summary>
    private void RegisterAsActive()
    {
        lock (_globalLock)
        {
            // Complete any existing progress bar
            if (_activeProgressBar != null && _activeProgressBar != this)
            {
                _activeProgressBar.CompleteInternal();
            }
            
            _activeProgressBar = this;
            
            // Save cursor position and clear space for progress bar
            if (!_cursorPositionSaved)
            {
                try
                {
                    _originalCursorTop = Console.CursorTop;
                    _cursorPositionSaved = true;
                    
                    // Move to top of screen and clear lines for progress bar
                    Console.SetCursorPosition(0, 0);
                    Console.WriteLine(new string(' ', Console.WindowWidth - 1));
                    Console.WriteLine(new string(' ', Console.WindowWidth - 1));
                    Console.SetCursorPosition(0, 0);
                }
                catch
                {
                    // If cursor positioning fails, fall back to normal mode
                    _cursorPositionSaved = false;
                }
            }
        }
    }

    /// <summary>
    /// Try to set console encoding to support Unicode characters
    /// </summary>
    private static void TrySetConsoleEncoding()
    {
        try
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
        }
        catch
        {
            // If setting encoding fails, we'll fall back to ASCII characters
        }
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
            
            CompleteInternal(message);
        }
    }

    /// <summary>
    /// Internal completion method
    /// </summary>
    private void CompleteInternal(string? message = null)
    {
        if (_disposed) return;
        
        _current = _total;
        UpdateDisplay(message ?? "Complete!");
        
        lock (_globalLock)
        {
            if (_activeProgressBar == this)
            {
                // Clear the progress bar area
                if (_cursorPositionSaved)
                {
                    try
                    {
                        Console.SetCursorPosition(0, 0);
                        Console.WriteLine(new string(' ', Console.WindowWidth - 1));
                        Console.WriteLine(new string(' ', Console.WindowWidth - 1));
                        
                        // Restore cursor to original position plus offset for any output
                        var newPosition = Math.Max(_originalCursorTop + 2, 2);
                        if (newPosition < Console.BufferHeight)
                        {
                            Console.SetCursorPosition(0, newPosition);
                        }
                    }
                    catch
                    {
                        // If positioning fails, just add a newline
                        Console.WriteLine();
                    }
                }
                else
                {
                    Console.WriteLine(); // Move to next line
                }
                
                _activeProgressBar = null;
                _cursorPositionSaved = false;
            }
        }
    }

    private void UpdateDisplay(string? currentItem = null)
    {
        if (_disposed) return;
        
        lock (_globalLock)
        {
            // Only update if this is the active progress bar
            if (_activeProgressBar != this) return;
            
            var percentage = _total == 0 ? 100 : (_current * 100.0) / _total;
            var filled = (int)((_current * (double)_barWidth) / _total);
            
            // Choose progress bar characters based on console capabilities
            string bar = GetProgressBar(filled);
            
            var itemText = string.IsNullOrEmpty(currentItem) ? "" : $" | {currentItem}";
            var progressText = $"{_description}: [{bar}] {percentage:F1}% ({_current}/{_total}){itemText}";
            
            // Truncate if too long for console
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
                        progressText = $"{_description}: [{bar}] {percentage:F1}% ({_current}/{_total}){itemText}";
                    }
                    else
                    {
                        itemText = "";
                        progressText = $"{_description}: [{bar}] {percentage:F1}% ({_current}/{_total})";
                    }
                }
            }
            catch
            {
                // If console width is not available, use basic format
                itemText = "";
                progressText = $"{_description}: [{bar}] {percentage:F1}% ({_current}/{_total})";
            }
            
            // Display at fixed position (top of screen)
            if (_cursorPositionSaved)
            {
                try
                {
                    var savedCursorLeft = Console.CursorLeft;
                    var savedCursorTop = Console.CursorTop;
                    
                    Console.SetCursorPosition(0, 0);
                    Console.Write(progressText.PadRight(Console.WindowWidth - 1));
                    
                    // Restore cursor position
                    Console.SetCursorPosition(savedCursorLeft, savedCursorTop);
                }
                catch
                {
                    // If positioning fails, fall back to normal output
                    Console.Write($"\r{progressText}");
                }
            }
            else
            {
                Console.Write($"\r{progressText}");
            }
        }
    }

    /// <summary>
    /// Get progress bar string with appropriate characters
    /// </summary>
    private string GetProgressBar(int filled)
    {
        // Try different character sets in order of preference
        if (CanDisplayUnicode())
        {
            // Unicode block characters
            return new string('?', filled) + new string('?', _barWidth - filled);
        }
        else if (CanDisplayExtendedAscii())
        {
            // Extended ASCII characters
            return new string('?', filled) + new string('?', _barWidth - filled);
        }
        else
        {
            // Safe ASCII characters
            return new string('=', filled) + new string('.', _barWidth - filled);
        }
    }

    /// <summary>
    /// Test if console can display Unicode characters
    /// </summary>
    private static bool CanDisplayUnicode()
    {
        try
        {
            return Console.OutputEncoding.EncodingName.Contains("UTF") || 
                   Console.OutputEncoding == System.Text.Encoding.Unicode;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Test if console can display extended ASCII characters
    /// </summary>
    private static bool CanDisplayExtendedAscii()
    {
        try
        {
            return Console.OutputEncoding.CodePage == 437 || // CP437 (original PC)
                   Console.OutputEncoding.CodePage == 850 || // CP850 (Western European)
                   Console.OutputEncoding.CodePage == 1252;  // Windows-1252
        }
        catch
        {
            return false;
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
/// Utility class for creating different types of progress indicators
/// </summary>
public static class ProgressBarFactory
{
    /// <summary>
    /// Create a progress bar for vertex processing
    /// </summary>
    public static ProgressBar CreateVertexProgress(int vertexCount)
    {
        return new ProgressBar(vertexCount, "Processing Vertices", 40);
    }

    /// <summary>
    /// Create a progress bar for edge processing
    /// </summary>
    public static ProgressBar CreateEdgeProgress(int edgeCount)
    {
        return new ProgressBar(edgeCount, "Processing Edges", 40);
    }

    /// <summary>
    /// Create a progress bar for property enhancement
    /// </summary>
    public static ProgressBar CreatePropertyProgress(int itemCount)
    {
        return new ProgressBar(itemCount, "Enhancing Properties", 40);
    }

    /// <summary>
    /// Create a progress bar for file operations
    /// </summary>
    public static ProgressBar CreateFileProgress(int fileCount)
    {
        return new ProgressBar(fileCount, "Saving Files", 40);
    }

    /// <summary>
    /// Create a general progress bar with custom description
    /// </summary>
    public static ProgressBar Create(int total, string description)
    {
        return new ProgressBar(total, description, 40);
    }
}