using System.ComponentModel;
using ModelContextProtocol.Server;
using Stardust.Paradox.GremlinStudio.Core.Connections;
using Stardust.Paradox.GremlinStudio.Core.Schema;

namespace Stardust.Paradox.GremlinStudio.McpServer.Tools;

/// <summary>
/// MCP tools for discovering and exporting graph database schemas.
/// </summary>
[McpServerToolType]
public sealed class SchemaTools
{
    private readonly IGremlinConnectionStore _connectionStore;
    private readonly IGremlinConnectorFactory _connectorFactory;
    private readonly ISchemaDiscoveryService _schemaDiscovery;
    private readonly ISchemaExportService _schemaExport;

    public SchemaTools(
        IGremlinConnectionStore connectionStore,
        IGremlinConnectorFactory connectorFactory,
        ISchemaDiscoveryService schemaDiscovery,
        ISchemaExportService schemaExport)
    {
        _connectionStore = connectionStore;
        _connectorFactory = connectorFactory;
        _schemaDiscovery = schemaDiscovery;
        _schemaExport = schemaExport;
    }

    [McpServerTool, Description(
        "Discovers the graph schema for a saved connection. " +
        "Returns vertex labels, edge labels, their properties, and relationships. " +
        "Useful for understanding the graph structure before writing queries.")]
    public async Task<SchemaToolResult> DiscoverGraphSchema(
        [Description("The name or ID of the saved connection to discover schema for.")] string connectionName,
        [Description("Maximum number of vertices/edges to sample per label for property discovery. Default: 50.")] int sampleSize = 50)
    {
        if (string.IsNullOrWhiteSpace(connectionName))
        {
            return SchemaToolResult.Error("Connection name is required.");
        }

        var connections = await _connectionStore.GetAllConnectionsAsync().ConfigureAwait(false);
        var match = connections.FirstOrDefault(c =>
            c.Name.Equals(connectionName, StringComparison.OrdinalIgnoreCase) ||
            c.Id.Equals(connectionName, StringComparison.OrdinalIgnoreCase));

        if (match is null)
        {
            var available = string.Join(", ", connections.Select(c => $"'{c.Name}'"));
            return SchemaToolResult.Error(
                $"Connection '{connectionName}' not found. Available: {available}");
        }

        var settings = await _connectionStore.GetConnectionWithSecretAsync(match.Id).ConfigureAwait(false);
        if (settings is null)
        {
            return SchemaToolResult.Error($"Could not load credentials for '{match.Name}'.");
        }

        var connector = _connectorFactory.CreateConnector(settings);
        try
        {
            var schema = await _schemaDiscovery.DiscoverSchemaAsync(
                connector, sampleSize).ConfigureAwait(false);

            return new SchemaToolResult
            {
                IsSuccess = true,
                SchemaJson = _schemaExport.ExportToJson(schema),
                VertexLabelCount = schema.VertexLabels.Count,
                EdgeLabelCount = schema.EdgeLabels.Count,
                VertexLabels = schema.VertexLabels.Select(v => v.Label).ToList(),
                EdgeLabels = schema.EdgeLabels.Select(e => e.Label).ToList()
            };
        }
        catch (Exception ex)
        {
            return SchemaToolResult.Error($"Schema discovery failed: {ex.Message}");
        }
        finally
        {
            if (connector is IAsyncDisposable asyncDisposable)
            {
                await asyncDisposable.DisposeAsync().ConfigureAwait(false);
            }
            else if (connector is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
    }

    [McpServerTool, Description(
        "Exports the graph schema as C# entity interfaces compatible with Stardust.Paradox ORM. " +
        "Discovers the schema first, then generates interface code with vertex/edge annotations.")]
    public async Task<SchemaCodeExportResult> ExportSchemaAsCode(
        [Description("The name or ID of the saved connection.")] string connectionName,
        [Description("The C# namespace for the generated interfaces. Default: 'MyApp.Graph.Entities'.")] string namespaceName = "MyApp.Graph.Entities",
        [Description("The name for the generated GraphContext class. Default: 'MyGraphContext'.")] string contextClassName = "MyGraphContext")
    {
        if (string.IsNullOrWhiteSpace(connectionName))
        {
            return SchemaCodeExportResult.Error("Connection name is required.");
        }

        var connections = await _connectionStore.GetAllConnectionsAsync().ConfigureAwait(false);
        var match = connections.FirstOrDefault(c =>
            c.Name.Equals(connectionName, StringComparison.OrdinalIgnoreCase) ||
            c.Id.Equals(connectionName, StringComparison.OrdinalIgnoreCase));

        if (match is null)
        {
            var available = string.Join(", ", connections.Select(c => $"'{c.Name}'"));
            return SchemaCodeExportResult.Error(
                $"Connection '{connectionName}' not found. Available: {available}");
        }

        var settings = await _connectionStore.GetConnectionWithSecretAsync(match.Id).ConfigureAwait(false);
        if (settings is null)
        {
            return SchemaCodeExportResult.Error($"Could not load credentials for '{match.Name}'.");
        }

        var connector = _connectorFactory.CreateConnector(settings);
        try
        {
            var schema = await _schemaDiscovery.DiscoverSchemaAsync(connector).ConfigureAwait(false);
            var options = new SchemaExportOptions
            {
                Namespace = namespaceName,
                ContextClassName = contextClassName,
                GenerateTypedEdges = true,
                GenerateNavigationProperties = true,
                IncludeXmlDocumentation = true
            };

            return new SchemaCodeExportResult
            {
                IsSuccess = true,
                Code = _schemaExport.ExportToCode(schema, options),
                VertexLabelCount = schema.VertexLabels.Count,
                EdgeLabelCount = schema.EdgeLabels.Count
            };
        }
        catch (Exception ex)
        {
            return SchemaCodeExportResult.Error($"Schema export failed: {ex.Message}");
        }
        finally
        {
            if (connector is IAsyncDisposable asyncDisposable)
            {
                await asyncDisposable.DisposeAsync().ConfigureAwait(false);
            }
            else if (connector is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
    }
}

/// <summary>
/// Result of a schema discovery operation.
/// </summary>
public sealed class SchemaToolResult
{
    public bool IsSuccess { get; set; }
    public string? SchemaJson { get; set; }
    public int VertexLabelCount { get; set; }
    public int EdgeLabelCount { get; set; }
    public List<string> VertexLabels { get; set; } = new();
    public List<string> EdgeLabels { get; set; } = new();
    public string? ErrorMessage { get; set; }

    public static SchemaToolResult Error(string message) => new()
    {
        IsSuccess = false,
        ErrorMessage = message
    };
}

/// <summary>
/// Result of a schema-to-code export operation.
/// </summary>
public sealed class SchemaCodeExportResult
{
    public bool IsSuccess { get; set; }
    public string? Code { get; set; }
    public int VertexLabelCount { get; set; }
    public int EdgeLabelCount { get; set; }
    public string? ErrorMessage { get; set; }

    public static SchemaCodeExportResult Error(string message) => new()
    {
        IsSuccess = false,
        ErrorMessage = message
    };
}
