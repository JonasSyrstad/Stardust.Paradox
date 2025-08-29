using System.Collections.Generic;

namespace Stardust.Paradox.Data.InMemory.Scenarios;

/// <summary>
/// Definition for creating vertices in scenarios
/// </summary>
public class InMemoryVertexDefinition
{
    public string Id { get; set; }
    public string Label { get; set; }
    public KeyValuePair<string, object>[] Properties { get; set; }

    public InMemoryVertexDefinition(string id, string label, params KeyValuePair<string, object>[] properties)
    {
        Id = id;
        Label = label;
        Properties = properties;
    }
}