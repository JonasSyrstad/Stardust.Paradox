namespace Stardust.Paradox.GremlinStudio.Core.Schema;

/// <summary>
/// Represents a discovered graph schema containing all vertex labels, edge labels, and their properties.
/// </summary>
public class GraphSchema
{
    /// <summary>
    /// The discovered vertex labels and their properties.
    /// </summary>
    public List<SchemaVertexLabel> VertexLabels { get; set; } = new();

    /// <summary>
    /// The discovered edge labels and their properties.
    /// </summary>
    public List<SchemaEdgeLabel> EdgeLabels { get; set; } = new();

    /// <summary>
    /// When the schema was discovered.
    /// </summary>
    public DateTime DiscoveredAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// The connection name used for discovery.
    /// </summary>
    public string? SourceConnection { get; set; }

    /// <summary>
    /// Total vertex count sampled during discovery.
    /// </summary>
    public int VertexSampleCount { get; set; }

    /// <summary>
    /// Total edge count sampled during discovery.
    /// </summary>
    public int EdgeSampleCount { get; set; }
}

/// <summary>
/// Represents a vertex label in the graph schema.
/// </summary>
public class SchemaVertexLabel
{
    /// <summary>
    /// The vertex label name (e.g., "person", "product").
    /// </summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>
    /// The properties discovered on vertices with this label.
    /// </summary>
    public List<SchemaProperty> Properties { get; set; } = new();

    /// <summary>
    /// Count of vertices with this label (from sample).
    /// </summary>
    public int Count { get; set; }

    /// <summary>
    /// Outgoing edge labels from this vertex type.
    /// Key: edge label, Value: target vertex label.
    /// </summary>
    public List<SchemaEdgeConnection> OutgoingEdges { get; set; } = new();

    /// <summary>
    /// Incoming edge labels to this vertex type.
    /// Key: edge label, Value: source vertex label.
    /// </summary>
    public List<SchemaEdgeConnection> IncomingEdges { get; set; } = new();

    /// <summary>
    /// Generates a C#-safe interface name from the label.
    /// </summary>
    public string InterfaceName => "I" + ToPascalCase(Label);

    private static string ToPascalCase(string text)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        var words = text.Split(new[] { '_', '-', ' ' }, StringSplitOptions.RemoveEmptyEntries);
        return string.Concat(words.Select(w => char.ToUpperInvariant(w[0]) + w.Substring(1).ToLowerInvariant()));
    }
}

/// <summary>
/// Represents an edge label in the graph schema.
/// </summary>
public class SchemaEdgeLabel
{
    /// <summary>
    /// The edge label name (e.g., "knows", "purchased").
    /// </summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>
    /// The properties discovered on edges with this label.
    /// </summary>
    public List<SchemaProperty> Properties { get; set; } = new();

    /// <summary>
    /// Count of edges with this label (from sample).
    /// </summary>
    public int Count { get; set; }

    /// <summary>
    /// Source vertex labels for this edge type.
    /// </summary>
    public List<string> SourceLabels { get; set; } = new();

    /// <summary>
    /// Target vertex labels for this edge type.
    /// </summary>
    public List<string> TargetLabels { get; set; } = new();

    /// <summary>
    /// Generates a C#-safe interface name from the label.
    /// </summary>
    public string InterfaceName => "I" + ToPascalCase(Label) + "Edge";

    private static string ToPascalCase(string text)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        var words = text.Split(new[] { '_', '-', ' ' }, StringSplitOptions.RemoveEmptyEntries);
        return string.Concat(words.Select(w => char.ToUpperInvariant(w[0]) + w.Substring(1).ToLowerInvariant()));
    }
}

/// <summary>
/// Represents a connection between vertex types via an edge.
/// </summary>
public class SchemaEdgeConnection
{
    /// <summary>
    /// The edge label.
    /// </summary>
    public string EdgeLabel { get; set; } = string.Empty;

    /// <summary>
    /// The connected vertex label (target for outgoing, source for incoming).
    /// </summary>
    public string VertexLabel { get; set; } = string.Empty;

    /// <summary>
    /// Whether multiple edges of this type can exist.
    /// </summary>
    public bool IsCollection { get; set; } = true;
}

/// <summary>
/// Represents a property in the graph schema.
/// </summary>
public class SchemaProperty
{
    /// <summary>
    /// The property key name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The inferred CLR type for this property.
    /// </summary>
    public string InferredType { get; set; } = "string";

    /// <summary>
    /// Whether this property appears to be nullable (not present on all instances).
    /// </summary>
    public bool IsNullable { get; set; }

    /// <summary>
    /// Whether this property appears to be a collection.
    /// </summary>
    public bool IsCollection { get; set; }

    /// <summary>
    /// Sample values found for this property (for reference).
    /// </summary>
    public List<string> SampleValues { get; set; } = new();

    /// <summary>
    /// Number of instances that have this property.
    /// </summary>
    public int OccurrenceCount { get; set; }

    /// <summary>
    /// Generates a C#-safe property name.
    /// </summary>
    public string PropertyName => ToPascalCase(Name);

    /// <summary>
    /// Gets the C# type declaration including nullability.
    /// </summary>
    public string CSharpType
    {
        get
        {
            var type = InferredType;
            if (IsCollection)
                type = $"ICollection<{type}>";
            if (IsNullable && !IsCollection)
                type += "?";
            return type;
        }
    }

    private static string ToPascalCase(string text)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        // Handle special cases like "id" -> "Id"
        if (text.Equals("id", StringComparison.OrdinalIgnoreCase))
            return "Id";

        var words = text.Split(new[] { '_', '-', ' ' }, StringSplitOptions.RemoveEmptyEntries);
        return string.Concat(words.Select(w => char.ToUpperInvariant(w[0]) + w.Substring(1).ToLowerInvariant()));
    }
}
