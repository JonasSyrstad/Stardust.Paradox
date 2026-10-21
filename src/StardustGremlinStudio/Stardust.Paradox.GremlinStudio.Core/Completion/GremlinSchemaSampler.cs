using System.Text.Json;
using Microsoft.Extensions.Logging;
using Stardust.Paradox.Data;
using Stardust.Paradox.GremlinStudio.Core.Execution;

namespace Stardust.Paradox.GremlinStudio.Core.Completion;

/// <summary>
/// Samples per-label property keys and edge topology for schema-aware editor completion.
/// Each label is sampled separately so that dominant labels cannot crowd out rare ones.
/// </summary>
public static class GremlinSchemaSampler
{
    /// <summary>Maximum number of elements sampled per label.</summary>
    public const int SamplePerLabel = 200;

    /// <summary>Maximum number of labels sampled per element kind.</summary>
    public const int MaxLabels = 100;

    /// <summary>Builds the query sampling property keys of a vertex label.</summary>
    public static string VertexKeysQuery(string label) =>
        $"g.V().hasLabel('{Escape(label)}').limit({SamplePerLabel}).valueMap()";

    /// <summary>Builds the query sampling property keys of an edge label.</summary>
    public static string EdgeKeysQuery(string label) =>
        $"g.E().hasLabel('{Escape(label)}').limit({SamplePerLabel}).valueMap()";

    /// <summary>Builds the query sampling the endpoint vertex labels of an edge label.</summary>
    public static string EdgeEndpointsQuery(string label) =>
        $"g.E().hasLabel('{Escape(label)}').limit({SamplePerLabel}).project('o','i').by(outV().label()).by(inV().label()).dedup()";

    /// <summary>Builds the query sampling the vertex labels at one end (<c>outV</c> or <c>inV</c>) of an edge label.</summary>
    public static string EdgeEndQuery(string label, string end) =>
        $"g.E().hasLabel('{Escape(label)}').limit({SamplePerLabel}).{end}().label().dedup()";

    /// <summary>
    /// Samples keys and topology for the given labels. Failures are logged and produce partial results.
    /// </summary>
    public static async Task<GremlinSchemaSample> SampleAsync(
        IGremlinQueryExecutor executor,
        IGremlinLanguageConnector connector,
        IEnumerable<string> vertexLabels,
        IEnumerable<string> edgeLabels,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(executor);
        ArgumentNullException.ThrowIfNull(connector);
        ArgumentNullException.ThrowIfNull(vertexLabels);
        ArgumentNullException.ThrowIfNull(edgeLabels);
        ArgumentNullException.ThrowIfNull(logger);

        var vertexKeys = new Dictionary<string, IReadOnlyCollection<string>>(StringComparer.Ordinal);
        var edgeKeys = new Dictionary<string, IReadOnlyCollection<string>>(StringComparer.Ordinal);
        var connections = new HashSet<GremlinEdgeConnection>();

        foreach (var label in vertexLabels.Distinct(StringComparer.Ordinal).Take(MaxLabels))
        {
            if (await RunAsync(executor, connector, VertexKeysQuery(label), logger, cancellationToken).ConfigureAwait(false) is { } json)
            {
                vertexKeys[label] = ParseKeys(json);
            }
        }

        foreach (var label in edgeLabels.Distinct(StringComparer.Ordinal).Take(MaxLabels))
        {
            if (await RunAsync(executor, connector, EdgeKeysQuery(label), logger, cancellationToken).ConfigureAwait(false) is { } keysJson)
            {
                edgeKeys[label] = ParseKeys(keysJson);
            }

            var endpoints = ParseEndpoints(await RunAsync(executor, connector, EdgeEndpointsQuery(label), logger, cancellationToken).ConfigureAwait(false));
            if (endpoints.Count == 0)
            {
                // Providers without project().by(traversal) support: pair every observed source with every observed target.
                var outLabels = ParseKeys(await RunAsync(executor, connector, EdgeEndQuery(label, "outV"), logger, cancellationToken).ConfigureAwait(false));
                var inLabels = ParseKeys(await RunAsync(executor, connector, EdgeEndQuery(label, "inV"), logger, cancellationToken).ConfigureAwait(false));
                endpoints = outLabels.SelectMany(o => inLabels.Select(i => (o, i))).ToList();
            }

            foreach (var (outLabel, inLabel) in endpoints)
            {
                connections.Add(new GremlinEdgeConnection(label, outLabel, inLabel));
            }
        }

        return new GremlinSchemaSample(vertexKeys, edgeKeys, connections.ToList());
    }

    /// <summary>
    /// Parses the result of a keys query: a JSON array of <c>valueMap()</c> objects (whose names are the keys)
    /// or of plain key strings.
    /// </summary>
    public static IReadOnlyCollection<string> ParseKeys(string? json)
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var row in Rows(json))
        {
            if (row.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in row.EnumerateObject())
                {
                    if (property.Name.Length > 0 && !IsElementToken(property.Name)) keys.Add(property.Name);
                }
            }
            else if (row.ValueKind == JsonValueKind.String && row.GetString() is { Length: > 0 } key)
            {
                keys.Add(key);
            }
        }
        return keys;
    }

    /// <summary>Parses the result of an endpoints query (a JSON array of <c>{o, i}</c> objects).</summary>
    public static IReadOnlyList<(string Out, string In)> ParseEndpoints(string? json) =>
        Rows(json)
            .Where(e => e.ValueKind == JsonValueKind.Object)
            .Select(e => (Out: GetString(e, "o"), In: GetString(e, "i")))
            .Where(p => p.Out != null && p.In != null)
            .Select(p => (p.Out!, p.In!))
            .Distinct()
            .ToList();

    private static async Task<string?> RunAsync(IGremlinQueryExecutor executor, IGremlinLanguageConnector connector, string query, ILogger logger, CancellationToken cancellationToken)
    {
        try
        {
            var result = await executor.ExecuteAsync(connector, query, cancellationToken: cancellationToken).ConfigureAwait(false);
            if (result.IsSuccess) return result.ResultJson;
            logger.LogWarning("Completion schema sampling failed for {Query}: {Error}", query, result.ErrorMessage);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Completion schema sampling failed for {Query}", query);
        }
        return null;
    }

    private static List<JsonElement> Rows(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.ValueKind == JsonValueKind.Array
                ? doc.RootElement.EnumerateArray().Select(e => e.Clone()).ToList()
                : [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static string? GetString(JsonElement row, string name) =>
        row.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    // Some providers (InMemory) store id/label as regular properties; they are element tokens, not keys.
    private static bool IsElementToken(string key) => key is "id" or "label";

    private static string Escape(string label) => label.Replace("\\", "\\\\").Replace("'", "\\'");
}
