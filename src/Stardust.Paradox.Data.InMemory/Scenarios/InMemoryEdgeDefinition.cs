using System.Collections.Generic;

namespace Stardust.Paradox.Data.InMemory.Scenarios;

/// <summary>
/// Definition for creating edges in scenarios
/// </summary>
public class InMemoryEdgeDefinition
{
    public string Id { get; set; }
    public string Label { get; set; }
    public string OutVertexId { get; set; }
    public string InVertexId { get; set; }
    public KeyValuePair<string, object>[] Properties { get; set; }

    public InMemoryEdgeDefinition(string id, string label, string outVertexId, string inVertexId, params KeyValuePair<string, object>[] properties)
    {
        Id = id;
        Label = label;
        OutVertexId = outVertexId;
        InVertexId = inVertexId;
        Properties = properties;
    }

    public InMemoryEdgeDefinition(string label, string outVertexId, string inVertexId, params KeyValuePair<string, object>[] properties)
        : this(null, label, outVertexId, inVertexId, properties)
    {
    }
}