using System;
using System.Collections.Generic;

namespace Stardust.Paradox.Data.InMemory;

/// <summary>
/// Enhanced vertex definition for scenario data - now properly aligned with TinkerPop structure
/// </summary>
public class InMemoryVertexDefinition
{
    public string Id { get; }
    public string Label { get; }
    public Dictionary<string, object> Properties { get; }

    public InMemoryVertexDefinition(string id, string label, Dictionary<string, object> properties = null)
    {
        Id = id ?? throw new ArgumentNullException(nameof(id));
        Label = label ?? throw new ArgumentNullException(nameof(label));
        Properties = properties ?? new Dictionary<string, object>();
    }

    /// <summary>
    /// Create an InMemoryVertexDefinition with builder pattern
    /// </summary>
    public static InMemoryVertexDefinition Create(string id, string label)
    {
        return new InMemoryVertexDefinition(id, label);
    }

    /// <summary>
    /// Add a property to this vertex definition
    /// </summary>
    public InMemoryVertexDefinition WithProperty(string key, object value)
    {
        if (string.IsNullOrEmpty(key))
            throw new ArgumentException("Property key cannot be null or empty", nameof(key));
            
        Properties[key] = value;
        return this;
    }

    /// <summary>
    /// Add multiple properties to this vertex definition
    /// </summary>
    public InMemoryVertexDefinition WithProperties(Dictionary<string, object> properties)
    {
        if (properties != null)
        {
            foreach (var prop in properties)
            {
                Properties[prop.Key] = prop.Value;
            }
        }
        return this;
    }

    /// <summary>
    /// Convert to InMemoryVertex instance
    /// </summary>
    public InMemoryVertex ToVertex()
    {
        var vertex = new InMemoryVertex(Id, Label);
        foreach (var prop in Properties)
        {
            vertex.SetProperty(prop.Key, prop.Value);
        }
        return vertex;
    }

    /// <summary>
    /// Create a copy of this vertex definition
    /// </summary>
    public InMemoryVertexDefinition Clone()
    {
        return new InMemoryVertexDefinition(Id, Label, new Dictionary<string, object>(Properties));
    }

    public override string ToString()
    {
        var propCount = Properties.Count;
        return $"VertexDef[{Id}]:{Label} ({propCount} properties)";
    }

    public override bool Equals(object obj)
    {
        if (obj is InMemoryVertexDefinition other)
        {
            return Id.Equals(other.Id) && Label.Equals(other.Label);
        }
        return false;
    }

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = 17;
            hash = hash * 23 + (Id?.GetHashCode() ?? 0);
            hash = hash * 23 + (Label?.GetHashCode() ?? 0);
            return hash;
        }
    }
}