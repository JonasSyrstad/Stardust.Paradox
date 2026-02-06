using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Stardust.Paradox.GremlinStudio.Core.History;

/// <summary>
/// File-based implementation of query history service.
/// </summary>
public class FileQueryHistoryService : IQueryHistoryService
{
    private readonly ILogger<FileQueryHistoryService> _logger;
    private readonly string _historyFilePath;
    private readonly List<QueryHistoryItem> _history = new();
    private readonly int _maxHistoryCount;

    public FileQueryHistoryService(ILogger<FileQueryHistoryService> logger, int maxHistoryCount = 50)
    {
        _logger = logger;
        _maxHistoryCount = maxHistoryCount;
        
        var appDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "GremlinStudio");
        Directory.CreateDirectory(appDataPath);
        _historyFilePath = Path.Combine(appDataPath, "query-history.json");
    }

    public IReadOnlyList<QueryHistoryItem> GetHistory()
    {
        return _history
            .OrderByDescending(h => h.IsPinned)
            .ThenByDescending(h => h.LastExecuted)
            .ToList();
    }

    public void AddQuery(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return;

        var normalizedQuery = query.Trim();
        
        // Check if query already exists
        var existing = _history.FirstOrDefault(h => 
            string.Equals(h.Query, normalizedQuery, StringComparison.OrdinalIgnoreCase));
        
        if (existing != null)
        {
            existing.LastExecuted = DateTime.UtcNow;
        }
        else
        {
            _history.Add(new QueryHistoryItem
            {
                Query = normalizedQuery,
                LastExecuted = DateTime.UtcNow
            });

            // Remove oldest non-pinned entries if over limit
            TrimHistory();
        }
    }

    private void TrimHistory()
    {
        var unpinnedCount = _history.Count(h => !h.IsPinned);
        if (unpinnedCount <= _maxHistoryCount)
            return;

        var toRemove = _history
            .Where(h => !h.IsPinned)
            .OrderBy(h => h.LastExecuted)
            .Take(unpinnedCount - _maxHistoryCount)
            .ToList();

        foreach (var item in toRemove)
        {
            _history.Remove(item);
        }
    }

    public void SetPinned(string id, bool isPinned)
    {
        var item = _history.FirstOrDefault(h => h.Id == id);
        if (item != null)
        {
            item.IsPinned = isPinned;
        }
    }

    public void RemoveQuery(string id)
    {
        var item = _history.FirstOrDefault(h => h.Id == id);
        if (item != null)
        {
            _history.Remove(item);
        }
    }

    public void ClearUnpinned()
    {
        _history.RemoveAll(h => !h.IsPinned);
    }

    public async Task SaveAsync()
    {
        try
        {
            var json = JsonSerializer.Serialize(_history, new JsonSerializerOptions 
            { 
                WriteIndented = true 
            });
            await File.WriteAllTextAsync(_historyFilePath, json);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to save query history");
        }
    }

    public async Task LoadAsync()
    {
        try
        {
            if (!File.Exists(_historyFilePath))
                return;

            var json = await File.ReadAllTextAsync(_historyFilePath);
            var items = JsonSerializer.Deserialize<List<QueryHistoryItem>>(json);
            
            if (items != null)
            {
                _history.Clear();
                _history.AddRange(items);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load query history");
        }
    }
}
