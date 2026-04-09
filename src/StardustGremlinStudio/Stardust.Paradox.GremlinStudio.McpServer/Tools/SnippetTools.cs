using System.ComponentModel;
using ModelContextProtocol.Server;
using Stardust.Paradox.GremlinStudio.Core.Connections;
using Stardust.Paradox.GremlinStudio.Core.History;
using Stardust.Paradox.GremlinStudio.Core.Snippets;

namespace Stardust.Paradox.GremlinStudio.McpServer.Tools;

/// <summary>
/// MCP tools for managing saved query snippets and query history.
/// </summary>
[McpServerToolType]
public sealed class SnippetTools
{
    private readonly IQuerySnippetStore _snippetStore;
    private readonly IQueryHistoryService _historyService;
    private readonly IGremlinConnectionStore _connectionStore;

    public SnippetTools(IQuerySnippetStore snippetStore, IQueryHistoryService historyService, IGremlinConnectionStore connectionStore)
    {
        _snippetStore = snippetStore;
        _historyService = historyService;
        _connectionStore = connectionStore;
    }

    [McpServerTool, Description(
        "Lists all saved Gremlin query snippets in Gremlin Studio. " +
        "Snippets are reusable named queries that users have saved for quick access.")]
    public async Task<IReadOnlyList<SnippetSummary>> ListSnippets()
    {
        var snippets = await _snippetStore.GetAllAsync().ConfigureAwait(false);
        return snippets.Select(s => new SnippetSummary
        {
            Id = s.Id,
            Name = s.Name,
            Query = s.Query,
            Tags = s.Tags.ToList(),
            CreatedAt = s.CreatedAt,
            LastModifiedAt = s.LastModifiedAt
        }).ToList();
    }

    [McpServerTool, Description(
        "Gets a specific saved query snippet by name or ID. " +
        "Returns the full snippet including query text and tags.")]
    public async Task<SnippetSummary?> GetSnippet(
        [Description("The snippet name or ID to look up.")] string nameOrId)
    {
        var snippets = await _snippetStore.GetAllAsync().ConfigureAwait(false);
        var match = snippets.FirstOrDefault(s =>
            s.Name.Equals(nameOrId, StringComparison.OrdinalIgnoreCase) ||
            s.Id.Equals(nameOrId, StringComparison.OrdinalIgnoreCase));

        if (match is null)
        {
            return null;
        }

        return new SnippetSummary
        {
            Id = match.Id,
            Name = match.Name,
            Query = match.Query,
            Tags = match.Tags.ToList(),
            CreatedAt = match.CreatedAt,
            LastModifiedAt = match.LastModifiedAt
        };
    }

    [McpServerTool, Description(
        "Lists recent query history from Gremlin Studio. " +
        "Shows previously executed queries with their connection context and execution timestamps. " +
        "Optionally filter by connection name or ID.")]
    public async Task<IReadOnlyList<HistoryItemSummary>> ListQueryHistory(
        [Description("Optional connection name or ID to filter history. Leave empty for all history.")] string? connectionNameOrId = null)
    {
        await _historyService.LoadAsync().ConfigureAwait(false);

        string? connectionId = null;
        if (!string.IsNullOrWhiteSpace(connectionNameOrId))
        {
            // Resolve connection name to ID so the history filter matches correctly.
            var connections = await _connectionStore.GetAllConnectionsAsync().ConfigureAwait(false);
            var match = connections.FirstOrDefault(c =>
                c.Id.Equals(connectionNameOrId, StringComparison.OrdinalIgnoreCase) ||
                c.Name.Equals(connectionNameOrId, StringComparison.OrdinalIgnoreCase));

            connectionId = match?.Id ?? connectionNameOrId;
        }

        var history = _historyService.GetHistory(connectionId);
        return history.Select(h => new HistoryItemSummary
        {
            Id = h.Id,
            Query = h.Query,
            ConnectionId = h.ConnectionId,
            LastExecuted = h.LastExecuted,
            IsPinned = h.IsPinned,
            DisplayName = h.DisplayName
        }).ToList();
    }
}

/// <summary>
/// Summary representation of a query snippet.
/// </summary>
public sealed class SnippetSummary
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Query { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new();
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset LastModifiedAt { get; set; }
}

/// <summary>
/// Summary representation of a query history item.
/// </summary>
public sealed class HistoryItemSummary
{
    public string Id { get; set; } = string.Empty;
    public string Query { get; set; } = string.Empty;
    public string? ConnectionId { get; set; }
    public DateTime LastExecuted { get; set; }
    public bool IsPinned { get; set; }
    public string? DisplayName { get; set; }
}
