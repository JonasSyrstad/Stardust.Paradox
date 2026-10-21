namespace Stardust.Paradox.GremlinStudio.Core.Completion;

/// <summary>
/// Traversal direction relative to the current vertex.
/// </summary>
public enum GremlinDirection
{
    Out,
    In,
    Both
}

/// <summary>
/// The kind of traverser at a point in a traversal.
/// </summary>
public enum GremlinElementKind
{
    /// <summary>The kind could not be determined.</summary>
    Unknown,
    Vertex,
    Edge,
    /// <summary>A property traverser, produced by <c>properties()</c>.</summary>
    Property,
    /// <summary>A non-element value such as a property value, map, count or path.</summary>
    Value
}

/// <summary>
/// The traverser state at a point in a traversal. <see cref="Labels"/> is <c>null</c> when any label is possible.
/// <see cref="Owner"/> is set for property traversers and describes the owning element.
/// <see cref="OtherEnd"/> tells <c>otherV()</c> which end of an edge leads away from the previous vertex.
/// </summary>
public sealed record GremlinElementState(
    GremlinElementKind Kind,
    IReadOnlySet<string>? Labels = null,
    GremlinElementState? Owner = null,
    GremlinDirection OtherEnd = GremlinDirection.Both)
{
    public static GremlinElementState Unknown { get; } = new(GremlinElementKind.Unknown);
    public static GremlinElementState Value { get; } = new(GremlinElementKind.Value);
    public static GremlinElementState AnyVertex { get; } = new(GremlinElementKind.Vertex);
    public static GremlinElementState AnyEdge { get; } = new(GremlinElementKind.Edge);
}

/// <summary>
/// An observed edge between two vertex labels: <c>(OutVertexLabel)-[EdgeLabel]-&gt;(InVertexLabel)</c>.
/// </summary>
public sealed record GremlinEdgeConnection(string EdgeLabel, string OutVertexLabel, string InVertexLabel);

/// <summary>
/// Sampled per-label property keys and edge topology used to build a <see cref="GremlinCompletionSchema"/>.
/// </summary>
public sealed record GremlinSchemaSample(
    IReadOnlyDictionary<string, IReadOnlyCollection<string>> VertexPropertyKeys,
    IReadOnlyDictionary<string, IReadOnlyCollection<string>> EdgePropertyKeys,
    IReadOnlyList<GremlinEdgeConnection> EdgeConnections)
{
    public static GremlinSchemaSample Empty { get; } = new(
        new Dictionary<string, IReadOnlyCollection<string>>(),
        new Dictionary<string, IReadOnlyCollection<string>>(),
        []);
}

/// <summary>
/// Schema information used for context-aware editor completion: labels, property keys per label
/// and how edge labels connect vertex labels.
/// </summary>
public sealed class GremlinCompletionSchema
{
    private static readonly IReadOnlyDictionary<string, IReadOnlyCollection<string>> NoKeys =
        new Dictionary<string, IReadOnlyCollection<string>>();

    private readonly IReadOnlyDictionary<string, IReadOnlyCollection<string>> _vertexKeys;
    private readonly IReadOnlyDictionary<string, IReadOnlyCollection<string>> _edgeKeys;
    private readonly IReadOnlyList<string> _allVertexKeys;
    private readonly IReadOnlyList<string> _allEdgeKeys;
    private readonly HashSet<string> _topologyVertexLabels;
    private readonly HashSet<string> _topologyEdgeLabels;

    /// <summary>
    /// An empty schema (no statistics loaded).
    /// </summary>
    public static GremlinCompletionSchema Empty { get; } = new([], []);

    public GremlinCompletionSchema(
        IEnumerable<(string Label, long Count)> vertexLabels,
        IEnumerable<(string Label, long Count)> edgeLabels,
        IReadOnlyDictionary<string, IReadOnlyCollection<string>>? vertexPropertyKeys = null,
        IReadOnlyDictionary<string, IReadOnlyCollection<string>>? edgePropertyKeys = null,
        IEnumerable<GremlinEdgeConnection>? edgeConnections = null)
    {
        ArgumentNullException.ThrowIfNull(vertexLabels);
        ArgumentNullException.ThrowIfNull(edgeLabels);

        VertexLabels = ToCountMap(vertexLabels);
        EdgeLabels = ToCountMap(edgeLabels);
        _vertexKeys = vertexPropertyKeys ?? NoKeys;
        _edgeKeys = edgePropertyKeys ?? NoKeys;
        EdgeConnections = edgeConnections?.Distinct().ToList() ?? [];
        _topologyVertexLabels = EdgeConnections.SelectMany(c => new[] { c.OutVertexLabel, c.InVertexLabel }).ToHashSet(StringComparer.Ordinal);
        _topologyEdgeLabels = EdgeConnections.Select(c => c.EdgeLabel).ToHashSet(StringComparer.Ordinal);
        _allVertexKeys = Sorted(_vertexKeys.Values.SelectMany(k => k));
        _allEdgeKeys = Sorted(_edgeKeys.Values.SelectMany(k => k));
    }

    /// <summary>Vertex labels with their element counts.</summary>
    public IReadOnlyDictionary<string, long> VertexLabels { get; }

    /// <summary>Edge labels with their element counts.</summary>
    public IReadOnlyDictionary<string, long> EdgeLabels { get; }

    /// <summary>Observed edge connections between vertex labels.</summary>
    public IReadOnlyList<GremlinEdgeConnection> EdgeConnections { get; }

    /// <summary>
    /// Gets property keys that are valid for elements in the given state.
    /// Falls back to all keys of the element kind when labels are unknown or not sampled.
    /// </summary>
    public IReadOnlyList<string> GetPropertyKeys(GremlinElementState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        return state.Kind switch
        {
            GremlinElementKind.Vertex => KeysFor(state.Labels, _vertexKeys, _allVertexKeys),
            GremlinElementKind.Edge => KeysFor(state.Labels, _edgeKeys, _allEdgeKeys),
            GremlinElementKind.Value => [],
            GremlinElementKind.Property => state.Owner is { } owner ? GetPropertyKeys(owner) : [],
            _ => Sorted(_allVertexKeys.Concat(_allEdgeKeys))
        };
    }

    /// <summary>
    /// Gets edge labels incident to the given vertex labels in the given direction.
    /// Returns <c>null</c> when the answer cannot be determined from the schema (unknown labels or labels
    /// absent from the sampled topology); returns an empty set when the labels are known to have no such edges.
    /// </summary>
    public IReadOnlySet<string>? GetIncidentEdgeLabels(IReadOnlySet<string>? vertexLabels, GremlinDirection direction)
    {
        if (vertexLabels == null || vertexLabels.Count == 0 || !vertexLabels.All(_topologyVertexLabels.Contains)) return null;
        return EdgeConnections
            .Where(c => Matches(c, vertexLabels, direction))
            .Select(c => c.EdgeLabel)
            .ToHashSet(StringComparer.Ordinal);
    }

    /// <summary>
    /// Gets vertex labels reached from <paramref name="vertexLabels"/> over the given edge labels.
    /// Returns <c>null</c> when the answer cannot be determined from the schema.
    /// </summary>
    public IReadOnlySet<string>? GetAdjacentVertexLabels(
        IReadOnlySet<string>? vertexLabels,
        IReadOnlyCollection<string> edgeLabels,
        GremlinDirection direction)
    {
        ArgumentNullException.ThrowIfNull(edgeLabels);
        if (EdgeConnections.Count == 0) return null;
        if (vertexLabels != null && !vertexLabels.All(_topologyVertexLabels.Contains)) return null;
        if (!edgeLabels.All(_topologyEdgeLabels.Contains)) return null;

        var result = new HashSet<string>(StringComparer.Ordinal);
        foreach (var c in EdgeConnections)
        {
            if (edgeLabels.Count > 0 && !edgeLabels.Contains(c.EdgeLabel)) continue;
            if (direction is GremlinDirection.Out or GremlinDirection.Both
                && (vertexLabels == null || vertexLabels.Contains(c.OutVertexLabel)))
            {
                result.Add(c.InVertexLabel);
            }
            if (direction is GremlinDirection.In or GremlinDirection.Both
                && (vertexLabels == null || vertexLabels.Contains(c.InVertexLabel)))
            {
                result.Add(c.OutVertexLabel);
            }
        }
        return result;
    }

    /// <summary>
    /// Gets the vertex labels at the given end of the given edge labels.
    /// <see cref="GremlinDirection.Out"/> is the source (outV), <see cref="GremlinDirection.In"/> the target (inV).
    /// Returns <c>null</c> when the answer cannot be determined from the schema.
    /// </summary>
    public IReadOnlySet<string>? GetEdgeEndpointLabels(IReadOnlySet<string>? edgeLabels, GremlinDirection end)
    {
        if (EdgeConnections.Count == 0) return null;
        if (edgeLabels != null && !edgeLabels.All(_topologyEdgeLabels.Contains)) return null;

        var result = new HashSet<string>(StringComparer.Ordinal);
        foreach (var c in EdgeConnections)
        {
            if (edgeLabels != null && !edgeLabels.Contains(c.EdgeLabel)) continue;
            if (end is GremlinDirection.Out or GremlinDirection.Both) result.Add(c.OutVertexLabel);
            if (end is GremlinDirection.In or GremlinDirection.Both) result.Add(c.InVertexLabel);
        }
        return result;
    }

    private static bool Matches(GremlinEdgeConnection c, IReadOnlySet<string> vertexLabels, GremlinDirection direction) =>
        direction switch
        {
            GremlinDirection.Out => vertexLabels.Contains(c.OutVertexLabel),
            GremlinDirection.In => vertexLabels.Contains(c.InVertexLabel),
            _ => vertexLabels.Contains(c.OutVertexLabel) || vertexLabels.Contains(c.InVertexLabel)
        };

    private static IReadOnlyList<string> KeysFor(
        IReadOnlySet<string>? labels,
        IReadOnlyDictionary<string, IReadOnlyCollection<string>> keysByLabel,
        IReadOnlyList<string> allKeys)
    {
        if (labels == null || labels.Count == 0) return allKeys;

        var result = new HashSet<string>(StringComparer.Ordinal);
        foreach (var label in labels)
        {
            // A label that was never sampled could have any key; don't hide anything.
            if (!keysByLabel.TryGetValue(label, out var keys)) return allKeys;
            result.UnionWith(keys);
        }
        return Sorted(result);
    }

    private static IReadOnlyDictionary<string, long> ToCountMap(IEnumerable<(string Label, long Count)> labels)
    {
        var map = new Dictionary<string, long>(StringComparer.Ordinal);
        foreach (var (label, count) in labels)
        {
            if (!string.IsNullOrEmpty(label)) map[label] = count;
        }
        return map;
    }

    private static IReadOnlyList<string> Sorted(IEnumerable<string> keys) =>
        keys.Where(k => !string.IsNullOrEmpty(k))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(k => k, StringComparer.OrdinalIgnoreCase)
            .ToList();
}
