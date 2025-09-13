using System.Collections.Generic;

namespace Stardust.Paradox.Data.InMemory.Scenarios;

/// <summary>
/// Definition for creating vertices in scenarios
/// </summary>
public class ScenarioVertexDefinition
{
    public string Id { get; set; }
    public string Label { get; set; }
    public KeyValuePair<string, object>[] Properties { get; set; }

    public ScenarioVertexDefinition(string id, string label, params KeyValuePair<string, object>[] properties)
    {
        Id = id;
        Label = label;
        Properties = properties;
    }
}

[System.Obsolete("Use ScenarioVertexDefinition instead",false)]
public class InMemoryVertexDefinition: ScenarioVertexDefinition
{
    public InMemoryVertexDefinition(string id, string label, params KeyValuePair<string, object>[] properties) : base(id, label, properties)
    {
    }
}