using System.Text.Json;
using Microsoft.Extensions.Logging;
using Stardust.Paradox.GremlinStudio.Core.Storage;

namespace Stardust.Paradox.GremlinStudio.Core.Variables;

public sealed class FileQueryVariableStore : IQueryVariableStore
{
    private readonly ILogger<FileQueryVariableStore> _logger;
    private readonly string _filePath;
    private readonly SemaphoreSlim _fileLock = new(1, 1);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public FileQueryVariableStore(ILogger<FileQueryVariableStore> logger)
    {
        _logger = logger;
        _filePath = AppDataPaths.VariablesFilePath;
    }

    public async Task<IReadOnlyList<QueryVariableSet>> GetAllAsync(CancellationToken cancellationToken = default)
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

    public async Task<QueryVariableSet?> GetAsync(string id, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(id))
            return null;

        var all = await GetAllAsync(cancellationToken).ConfigureAwait(false);
        return all.FirstOrDefault(s => string.Equals(s.Id, id, StringComparison.Ordinal));
    }

    public async Task SaveAsync(QueryVariableSet variables, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(variables);

        await _fileLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var items = (await LoadAsync(cancellationToken).ConfigureAwait(false)).ToList();
            var index = items.FindIndex(s => string.Equals(s.Id, variables.Id, StringComparison.Ordinal));

            if (index >= 0)
                items[index] = variables;
            else
                items.Add(variables);

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

    private async Task<IReadOnlyList<QueryVariableSet>> LoadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_filePath))
            return Array.Empty<QueryVariableSet>();

        try
        {
            var json = await File.ReadAllTextAsync(_filePath, cancellationToken).ConfigureAwait(false);
            return JsonSerializer.Deserialize<List<QueryVariableSet>>(json, JsonOptions) ?? new();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load variables from '{FilePath}'", _filePath);
            return Array.Empty<QueryVariableSet>();
        }
    }

    private async Task SaveAsync(List<QueryVariableSet> variables, CancellationToken cancellationToken)
    {
        try
        {
            var json = JsonSerializer.Serialize(variables, JsonOptions);
            await File.WriteAllTextAsync(_filePath, json, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save variables to '{FilePath}'", _filePath);
            throw;
        }
    }
}
