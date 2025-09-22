using System;
using System.Collections.Generic;

namespace Stardust.Paradox.Data.InMemory.Core;

/// <summary>
/// Enhanced edge definition for scenario data
/// </summary>
public class InMemoryEdgeDefinition
{
    public string Label { get; }
    public string OutVertexId { get; }
    public string InVertexId { get; }
    public string Id { get; }
    public Dictionary<string, object> Properties { get; }

    public InMemoryEdgeDefinition(string label, string outVertexId, string inVertexId, string id = null, Dictionary<string, object> properties = null)
    {
        Label = label ?? throw new ArgumentNullException(nameof(label));
        OutVertexId = outVertexId ?? throw new ArgumentNullException(nameof(outVertexId));
        InVertexId = inVertexId ?? throw new ArgumentNullException(nameof(inVertexId));
        Id = id ?? Guid.NewGuid().ToString();
        Properties = properties ?? new Dictionary<string, object>();
    }

    public InMemoryEdge ToEdge()
    {
        var edge = new InMemoryEdge(Id, Label, OutVertexId, InVertexId);
        foreach (var prop in Properties)
        {
            edge.SetProperty(prop.Key, prop.Value);
        }
        return edge;
    }
}
