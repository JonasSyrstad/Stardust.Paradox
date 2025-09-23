using System;

namespace Stardust.Paradox.Data.ScenarioConnector;

/// <summary>
/// Consolidated progress bar with multiple sub-progress indicators fixed at the top of the console
/// </summary>
public class ConsolidatedProgressBar : IDisposable
{
    private readonly object _lock = new object();
    private static readonly object _globalLock = new object();
    private static ConsolidatedProgressBar? _activeProgressBar;
    private static int _savedCursorTop;
    private static int _savedCursorLeft;
    private static bool _positionSaved = false;
    
    // Progress tracking
    private int _vertexTotal;
    private int _vertexCurrent;
    private string _vertexCurrentItem = "";
    
    private int _edgeTotal;
    private int _edgeCurrent;
    private string _edgeCurrentItem = "";
    
    private int _fileTotal;
    private int _fileCurrent;
    private string _fileCurrentItem = "";
    
    private readonly int _barWidth;
    private bool _disposed;

    public ConsolidatedProgressBar(int vertexTotal, int edgeTotal, int fileTotal, int barWidth = 35)
    {
        _vertexTotal = vertexTotal;
        _edgeTotal = edgeTotal;
        _fileTotal = fileTotal;
        _barWidth = barWidth;
        
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
                    
                    // Clear the first 5 lines for our progress bars
                    for (int i = 0; i < 5; i++)
                    {
                        Console.SetCursorPosition(0, i);
                        ClearLine();
                    }
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
            Console.Write(new string(' ', 120)); // Fallback width
        }
    }

    /// <summary>
    /// Update vertex processing progress
    /// </summary>
    public void UpdateVertexProgress(int current, string? currentItem = null)
    {
        lock (_lock)
        {
            if (_disposed) return;
            
            _vertexCurrent = Math.Min(current, _vertexTotal);
            _vertexCurrentItem = currentItem ?? "";
            UpdateDisplay();
        }
    }

    /// <summary>
    /// Increment vertex processing progress
    /// </summary>
    public void IncrementVertexProgress(string? currentItem = null)
    {
        lock (_lock)
        {
            if (_disposed) return;
            
            _vertexCurrent = Math.Min(_vertexCurrent + 1, _vertexTotal);
            _vertexCurrentItem = currentItem ?? "";
            UpdateDisplay();
        }
    }

    /// <summary>
    /// Update edge processing progress
    /// </summary>
    public void UpdateEdgeProgress(int current, string? currentItem = null)
    {
        lock (_lock)
        {
            if (_disposed) return;
            
            _edgeCurrent = Math.Min(current, _edgeTotal);
            _edgeCurrentItem = currentItem ?? "";
            UpdateDisplay();
        }
    }

    /// <summary>
    /// Increment edge processing progress
    /// </summary>
    public void IncrementEdgeProgress(string? currentItem = null)
    {
        lock (_lock)
        {
            if (_disposed) return;
            
            _edgeCurrent = Math.Min(_edgeCurrent + 1, _edgeTotal);
            _edgeCurrentItem = currentItem ?? "";
            UpdateDisplay();
        }
    }

    /// <summary>
    /// Update file save progress
    /// </summary>
    public void UpdateFileProgress(int current, string? currentItem = null)
    {
        lock (_lock)
        {
            if (_disposed) return;
            
            _fileCurrent = Math.Min(current, _fileTotal);
            _fileCurrentItem = currentItem ?? "";
            UpdateDisplay();
        }
    }

    /// <summary>
    /// Increment file save progress
    /// </summary>
    public void IncrementFileProgress(string? currentItem = null)
    {
        lock (_lock)
        {
            if (_disposed) return;
            
            _fileCurrent = Math.Min(_fileCurrent + 1, _fileTotal);
            _fileCurrentItem = currentItem ?? "";
            UpdateDisplay();
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

    private void CompleteInternal(string? message = null)
    {
        if (_disposed) return;
        
        // Set all progress to complete
        _vertexCurrent = _vertexTotal;
        _edgeCurrent = _edgeTotal;
        _fileCurrent = _fileTotal;
        
        UpdateDisplay();
        
        // Small delay to show completion
        System.Threading.Thread.Sleep(1000);
        
        lock (_globalLock)
        {
            if (_activeProgressBar == this)
            {
                try
                {
                    if (_positionSaved)
                    {
                        // Clear progress bar lines
                        for (int i = 0; i < 5; i++)
                        {
                            Console.SetCursorPosition(0, i);
                            ClearLine();
                        }
                        
                        // Restore cursor to a safe position
                        var newTop = Math.Max(_savedCursorTop + 5, 5);
                        Console.SetCursorPosition(0, Math.Min(newTop, Console.BufferHeight - 1));
                        
                        if (!string.IsNullOrEmpty(message))
                        {
                            Console.WriteLine(message);
                        }
                    }
                    else
                    {
                        if (!string.IsNullOrEmpty(message))
                        {
                            Console.WriteLine(message);
                        }
                    }
                }
                catch
                {
                    if (!string.IsNullOrEmpty(message))
                    {
                        Console.WriteLine(message);
                    }
                }
                
                _activeProgressBar = null;
                _positionSaved = false;
            }
        }
    }

    private void UpdateDisplay()
    {
        if (_disposed) return;
        
        lock (_globalLock)
        {
            if (_activeProgressBar != this) return;
            
            try
            {
                if (_positionSaved)
                {
                    // Save current position
                    var currentLeft = Console.CursorLeft;
                    var currentTop = Console.CursorTop;
                    
                    // Display at fixed position (top lines)
                    DisplayProgressBars();
                    
                    // Restore cursor
                    Console.SetCursorPosition(currentLeft, currentTop);
                }
                else
                {
                    // Inline mode fallback
                    DisplayProgressBarsInline();
                }
            }
            catch
            {
                // Ultimate fallback - just show basic progress
                var totalItems = _vertexTotal + _edgeTotal + _fileTotal;
                var completedItems = _vertexCurrent + _edgeCurrent + _fileCurrent;
                var percentage = totalItems == 0 ? 100 : (completedItems * 100.0) / totalItems;
                Console.Write($"\rOverall Progress: {percentage:F1}% ({completedItems}/{totalItems})");
            }
        }
    }

    private void DisplayProgressBars()
    {
        // Line 0: Overall Progress
        var totalItems = _vertexTotal + _edgeTotal + _fileTotal;
        var completedItems = _vertexCurrent + _edgeCurrent + _fileCurrent;
        var overallPercentage = totalItems == 0 ? 100 : (completedItems * 100.0) / totalItems;
        var overallFilled = (int)((completedItems * (double)_barWidth) / Math.Max(totalItems, 1));
        var overallBar = new string('=', overallFilled) + new string('.', _barWidth - overallFilled);
        
        Console.SetCursorPosition(0, 0);
        var overallText = $"Overall Progress: [{overallBar}] {overallPercentage:F1}% ({completedItems}/{totalItems})";
        Console.Write(overallText.PadRight(GetConsoleWidth()));

        // Line 1: Vertex Progress
        var vertexPercentage = _vertexTotal == 0 ? 100 : (_vertexCurrent * 100.0) / _vertexTotal;
        var vertexFilled = (int)((_vertexCurrent * (double)_barWidth) / Math.Max(_vertexTotal, 1));
        var vertexBar = new string('=', vertexFilled) + new string('.', _barWidth - vertexFilled);
        
        Console.SetCursorPosition(0, 1);
        var vertexText = $"Vertices: [{vertexBar}] {vertexPercentage:F1}% ({_vertexCurrent}/{_vertexTotal})";
        if (!string.IsNullOrEmpty(_vertexCurrentItem))
        {
            vertexText += $" | {TruncateItem(_vertexCurrentItem, 20)}";
        }
        Console.Write(vertexText.PadRight(GetConsoleWidth()));

        // Line 2: Edge Progress
        var edgePercentage = _edgeTotal == 0 ? 100 : (_edgeCurrent * 100.0) / _edgeTotal;
        var edgeFilled = (int)((_edgeCurrent * (double)_barWidth) / Math.Max(_edgeTotal, 1));
        var edgeBar = new string('=', edgeFilled) + new string('.', _barWidth - edgeFilled);
        
        Console.SetCursorPosition(0, 2);
        var edgeText = $"Edges:    [{edgeBar}] {edgePercentage:F1}% ({_edgeCurrent}/{_edgeTotal})";
        if (!string.IsNullOrEmpty(_edgeCurrentItem))
        {
            edgeText += $" | {TruncateItem(_edgeCurrentItem, 20)}";
        }
        Console.Write(edgeText.PadRight(GetConsoleWidth()));

        // Line 3: File Progress
        var filePercentage = _fileTotal == 0 ? 100 : (_fileCurrent * 100.0) / _fileTotal;
        var fileFilled = (int)((_fileCurrent * (double)_barWidth) / Math.Max(_fileTotal, 1));
        var fileBar = new string('=', fileFilled) + new string('.', _barWidth - fileFilled);
        
        Console.SetCursorPosition(0, 3);
        var fileText = $"Files:    [{fileBar}] {filePercentage:F1}% ({_fileCurrent}/{_fileTotal})";
        if (!string.IsNullOrEmpty(_fileCurrentItem))
        {
            fileText += $" | {TruncateItem(_fileCurrentItem, 20)}";
        }
        Console.Write(fileText.PadRight(GetConsoleWidth()));

        // Line 4: Empty separator line
        Console.SetCursorPosition(0, 4);
        Console.Write(new string(' ', GetConsoleWidth()));
    }

    private void DisplayProgressBarsInline()
    {
        var totalItems = _vertexTotal + _edgeTotal + _fileTotal;
        var completedItems = _vertexCurrent + _edgeCurrent + _fileCurrent;
        var overallPercentage = totalItems == 0 ? 100 : (completedItems * 100.0) / totalItems;
        
        Console.Write($"\rProgress: V:{_vertexCurrent}/{_vertexTotal} E:{_edgeCurrent}/{_edgeTotal} F:{_fileCurrent}/{_fileTotal} Overall:{overallPercentage:F1}%");
    }

    private string TruncateItem(string item, int maxLength)
    {
        if (string.IsNullOrEmpty(item) || item.Length <= maxLength)
            return item;
        
        return item.Length > maxLength - 3 
            ? item.Substring(0, maxLength - 3) + "..." 
            : item;
    }

    private int GetConsoleWidth()
    {
        try
        {
            return Console.WindowWidth - 1;
        }
        catch
        {
            return 119; // Fallback width
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            if (!_disposed)
            {
                CompleteInternal("Export completed successfully!");
                _disposed = true;
            }
        }
    }
}

/// <summary>
/// Factory for creating consolidated progress bars
/// </summary>
public static class ConsolidatedProgressBarFactory
{
    /// <summary>
    /// Create a consolidated progress bar for scenario export
    /// </summary>
    public static ConsolidatedProgressBar CreateScenarioExportProgress(int vertexCount, int edgeCount, int fileCount = 2)
    {
        return new ConsolidatedProgressBar(vertexCount, edgeCount, fileCount);
    }

    /// <summary>
    /// Create a consolidated progress bar with custom totals
    /// </summary>
    public static ConsolidatedProgressBar Create(int vertexTotal, int edgeTotal, int fileTotal)
    {
        return new ConsolidatedProgressBar(vertexTotal, edgeTotal, fileTotal);
    }
}