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
        {
            // Seed built-in snippets on first run
            var builtIn = GetBuiltInSnippets();
            await SaveAsync(builtIn, cancellationToken).ConfigureAwait(false);
            return builtIn;
        }

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

    private static List<QuerySnippet> GetBuiltInSnippets()
    {
        var now = DateTimeOffset.UtcNow;
        return new List<QuerySnippet>
        {
            // Traversal
            new(Guid.NewGuid().ToString(), "Get all vertices", "g.V().limit(25)", new[] { "traversal", "vertex" }, now, now),
            new(Guid.NewGuid().ToString(), "Get all edges", "g.E().limit(25)", new[] { "traversal", "edge" }, now, now),
            new(Guid.NewGuid().ToString(), "Get vertex by id", "g.V('vertex-id')", new[] { "traversal", "vertex" }, now, now),
            new(Guid.NewGuid().ToString(), "Get vertices by label", "g.V().hasLabel('label')", new[] { "traversal", "filter" }, now, now),
            new(Guid.NewGuid().ToString(), "Filter by property", "g.V().has('propertyName', 'value')", new[] { "traversal", "filter" }, now, now),
            new(Guid.NewGuid().ToString(), "Outgoing neighbors", "g.V('vertex-id').out()", new[] { "traversal", "navigation" }, now, now),
            new(Guid.NewGuid().ToString(), "Incoming neighbors", "g.V('vertex-id').in()", new[] { "traversal", "navigation" }, now, now),
            new(Guid.NewGuid().ToString(), "Both directions", "g.V('vertex-id').both()", new[] { "traversal", "navigation" }, now, now),
            new(Guid.NewGuid().ToString(), "Outgoing edges", "g.V('vertex-id').outE()", new[] { "traversal", "edge" }, now, now),
            new(Guid.NewGuid().ToString(), "Path traversal", "g.V('vertex-id').out().out().path()", new[] { "traversal", "path" }, now, now),

            // CRUD
            new(Guid.NewGuid().ToString(), "Add vertex", "g.addV('label').property('id', 'my-id').property('name', 'value')", new[] { "crud", "create" }, now, now),
            new(Guid.NewGuid().ToString(), "Add edge", "g.V('from-id').addE('edge-label').to(g.V('to-id'))", new[] { "crud", "create" }, now, now),
            new(Guid.NewGuid().ToString(), "Update property", "g.V('vertex-id').property('key', 'new-value')", new[] { "crud", "update" }, now, now),
            new(Guid.NewGuid().ToString(), "Drop vertex", "g.V('vertex-id').drop()", new[] { "crud", "delete" }, now, now),
            new(Guid.NewGuid().ToString(), "Drop edge", "g.E('edge-id').drop()", new[] { "crud", "delete" }, now, now),

            // Aggregation
            new(Guid.NewGuid().ToString(), "Count all vertices", "g.V().count()", new[] { "aggregation", "count" }, now, now),
            new(Guid.NewGuid().ToString(), "Count all edges", "g.E().count()", new[] { "aggregation", "count" }, now, now),
            new(Guid.NewGuid().ToString(), "Group by label", "g.V().groupCount().by(label)", new[] { "aggregation", "group" }, now, now),
            new(Guid.NewGuid().ToString(), "Edge label distribution", "g.E().groupCount().by(label)", new[] { "aggregation", "group" }, now, now),
            new(Guid.NewGuid().ToString(), "Value map", "g.V().hasLabel('label').valueMap(true).limit(10)", new[] { "aggregation", "properties" }, now, now),

            // Schema / discovery
            new(Guid.NewGuid().ToString(), "All vertex labels", "g.V().label().dedup()", new[] { "schema", "discovery" }, now, now),
            new(Guid.NewGuid().ToString(), "All edge labels", "g.E().label().dedup()", new[] { "schema", "discovery" }, now, now),
            new(Guid.NewGuid().ToString(), "Property keys for label", "g.V().hasLabel('label').properties().key().dedup()", new[] { "schema", "discovery" }, now, now),
            new(Guid.NewGuid().ToString(), "Edge connectivity", "g.E().project('label','from','to').by(label).by(outV().label()).by(inV().label()).dedup()", new[] { "schema", "discovery" }, now, now),

            // Admin / bulk
            new(Guid.NewGuid().ToString(), "Drop all vertices (DANGER)", "g.V().drop()", new[] { "admin", "danger" }, now, now),
            new(Guid.NewGuid().ToString(), "Drop by label (DANGER)", "g.V().hasLabel('label').drop()", new[] { "admin", "danger" }, now, now),
            new(Guid.NewGuid().ToString(), "Execution profile", "g.V().hasLabel('label').limit(10).executionProfile()", new[] { "admin", "performance" }, now, now),
        };
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
