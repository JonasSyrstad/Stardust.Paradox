using System;
using Stardust.Paradox.Data.InMemory.Core;
using System.Collections.Generic;

namespace Stardust.Paradox.Data.InMemory.Scenarios;

/// <summary>
/// Definition for creating edges in scenarios 
/// </summary>
public class ScenarioEdgeDefinition
{
    public string Id { get; set; } 
    public string Label { get; set; }
    public string OutVertexId { get; set; }
    public string InVertexId { get; set; }
    public KeyValuePair<string, object>[] Properties { get; set; }

    public ScenarioEdgeDefinition(string id, string label, string outVertexId, string inVertexId, params KeyValuePair<string, object>[] properties)
    {
        Id = id;
        Label = label;
        OutVertexId = outVertexId;
        InVertexId = inVertexId;
        Properties = properties;
    }

    public ScenarioEdgeDefinition(string label, string outVertexId, string inVertexId, params KeyValuePair<string, object>[] properties)
        : this(null, label, outVertexId, inVertexId, properties)
    {
    }
}

[Obsolete("Use ScenarioEdgeDefinition", false)]
public class InMemoryEdgeDefinition:ScenarioEdgeDefinition
{
    public InMemoryEdgeDefinition(string id, string label, string outVertexId, string inVertexId, params KeyValuePair<string, object>[] properties) : base(id, label, outVertexId, inVertexId, properties)
    {
    }

    public InMemoryEdgeDefinition(string label, string outVertexId, string inVertexId, params KeyValuePair<string, object>[] properties) : base(label, outVertexId, inVertexId, properties)
    {
    }
}
