using System;
using System.Collections.Generic;
using System.Linq;

namespace Stardust.Paradox.Data.InMemory.Core;

/// <summary>
/// Vertex property implementation matching TinkerPop's structure
/// </summary>
public class InMemoryVertexProperty
{
    public string Key { get; }
    public object Value { get; set; }
    public string Id { get; }
    public Dictionary<string, object> Properties { get; }

    public InMemoryVertexProperty(string key, object value, string id = null)
    {
        Key = key ?? throw new ArgumentNullException(nameof(key));
        Value = value;
        Id = id ?? Guid.NewGuid().ToString();
        Properties = new Dictionary<string, object>();
    }

    public dynamic ToGremlinResponse()
    {
        var response = new Dictionary<string, object>
        {
            ["id"] = Id,
            ["key"] = Key,
            ["value"] = Value
        };

        if (Properties.Any())
        {
            response["properties"] = Properties;
        }

        return response;
    }
}
