using System.Text.Json;
using Microsoft.Extensions.Logging;
using Stardust.Paradox.GremlinStudio.Core.Storage;

namespace Stardust.Paradox.GremlinStudio.Core.Snippets;

public sealed class FileQuerySnippetStore : IQuerySnippetStore
{
    private readonly ILogger<FileQuerySnippetStore> _logger;
    private readonly string _filePath;
    private readonly SemaphoreSlim _fileLock = new(1, 1);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public FileQuerySnippetStore(ILogger<FileQuerySnippetStore> logger)
    {
        _logger = logger;
        _filePath = AppDataPaths.SnippetsFilePath;
    }

    public async Task<IReadOnlyList<QuerySnippet>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await _fileLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await LoadAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _fileLock.Release();
        }
    }

    public async Task<QuerySnippet?> GetAsync(string id, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(id))
            return null;

        var all = await GetAllAsync(cancellationToken).ConfigureAwait(false);
        return all.FirstOrDefault(s => string.Equals(s.Id, id, StringComparison.Ordinal));
    }

    public async Task SaveAsync(QuerySnippet snippet, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snippet);

        await _fileLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var items = (await LoadAsync(cancellationToken).ConfigureAwait(false)).ToList();
            var index = items.FindIndex(s => string.Equals(s.Id, snippet.Id, StringComparison.Ordinal));

            if (index >= 0)
            {
                items[index] = snippet;
            }
            else
            {
                items.Add(snippet);
            }

            await SaveAsync(items, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _fileLock.Release();
        }
    }

    public async Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(id))
            return false;

        await _fileLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var items = (await LoadAsync(cancellationToken).ConfigureAwait(false)).ToList();
            var removed = items.RemoveAll(s => string.Equals(s.Id, id, StringComparison.Ordinal));
            if (removed == 0)
                return false;

            await SaveAsync(items, cancellationToken).ConfigureAwait(false);
            return true;
        }
        finally
        {
            _fileLock.Release();
        }
    }

    private async Task<IReadOnlyList<QuerySnippet>> LoadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_filePath))
            return Array.Empty<QuerySnippet>();

        try
        {
            var json = await File.ReadAllTextAsync(_filePath, cancellationToken).ConfigureAwait(false);
            return JsonSerializer.Deserialize<List<QuerySnippet>>(json, JsonOptions) ?? new();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load snippets from '{FilePath}'", _filePath);
            return Array.Empty<QuerySnippet>();
        }
    }

    private async Task SaveAsync(List<QuerySnippet> snippets, CancellationToken cancellationToken)
    {
        try
        {
            var json = JsonSerializer.Serialize(snippets, JsonOptions);
            await File.WriteAllTextAsync(_filePath, json, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save snippets to '{FilePath}'", _filePath);
            throw;
        }
    }
}
