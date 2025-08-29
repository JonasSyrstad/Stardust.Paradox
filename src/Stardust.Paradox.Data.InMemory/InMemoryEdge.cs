using System;
using System.Collections.Generic;
using System.Linq;

namespace Stardust.Paradox.Data.InMemory;

/// <summary>
/// In-memory edge implementation inspired by Apache TinkerPop's TinkerGraph
/// </summary>
public class InMemoryEdge
{
    public string Id { get; }
    public string Label { get; }
    public string OutVertexId { get; }
    public string InVertexId { get; }
    public Dictionary<string, object> Properties { get; }

    public InMemoryEdge(string id, string label, string outVertexId, string inVertexId)
    {
        Id = id ?? throw new ArgumentNullException(nameof(id));
        Label = label ?? throw new ArgumentNullException(nameof(label));
        OutVertexId = outVertexId ?? throw new ArgumentNullException(nameof(outVertexId));
        InVertexId = inVertexId ?? throw new ArgumentNullException(nameof(inVertexId));
        Properties = new Dictionary<string, object>();
    }

    /// <summary>
    /// Convert to Gremlin response format compatible with Paradox framework
    /// </summary>
    public dynamic ToGremlinResponse()
    {
        var response = new GremlinResponseObject();
        response.Set("id", Id);
        response.Set("label", Label);
        response.Set("type", "edge");
        response.Set("inV", InVertexId);
        response.Set("outV", OutVertexId);
        
        // Convert properties to proper Gremlin format for edges (flat dictionary)
        var propertyDict = new Dictionary<string, object>();
        foreach (var prop in Properties)
        {
            propertyDict[prop.Key] = prop.Value;
        }
        response.Set("properties", propertyDict);
        
        return response;
    }

    /// <summary>
    /// Add or update a property
    /// </summary>
    public void SetProperty(string key, object value)
    {
        if (string.IsNullOrEmpty(key))
            throw new ArgumentException("Property key cannot be null or empty", nameof(key));
            
        Properties[key] = value;
    }

    /// <summary>
    /// Get a property value
    /// </summary>
    public T GetProperty<T>(string key, T defaultValue = default)
    {
        if (Properties.TryGetValue(key, out var value))
        {
            if (value is T typedValue)
                return typedValue;
                
            try
            {
                return (T)Convert.ChangeType(value, typeof(T));
            }
            catch
            {
                return defaultValue;
            }
        }
        return defaultValue;
    }

    /// <summary>
    /// Check if edge has a property
    /// </summary>
    public bool HasProperty(string key)
    {
        return Properties.ContainsKey(key);
    }

    /// <summary>
    /// Remove a property
    /// </summary>
    public bool RemoveProperty(string key)
    {
        return Properties.Remove(key);
    }

    /// <summary>
    /// Get all property keys
    /// </summary>
    public IEnumerable<string> GetPropertyKeys()
    {
        return Properties.Keys;
    }

    /// <summary>
    /// Get all property values
    /// </summary>
    public IEnumerable<object> GetPropertyValues()
    {
        return Properties.Values;
    }

    /// <summary>
    /// Create a copy of this edge
    /// </summary>
    public InMemoryEdge Clone()
    {
        var clone = new InMemoryEdge(Id, Label, OutVertexId, InVertexId);
        foreach (var prop in Properties)
        {
            clone.SetProperty(prop.Key, prop.Value);
        }
        return clone;
    }

    /// <summary>
    /// Get the other vertex ID (opposite of the given vertex ID)
    /// </summary>
    public string GetOtherVertexId(string vertexId)
    {
        if (vertexId == OutVertexId)
            return InVertexId;
        if (vertexId == InVertexId)
            return OutVertexId;
        throw new ArgumentException($"Vertex {vertexId} is not connected to this edge");
    }

    /// <summary>
    /// Check if this edge connects the given vertices
    /// </summary>
    public bool ConnectsVertices(string vertexId1, string vertexId2)
    {
        return (OutVertexId == vertexId1 && InVertexId == vertexId2) ||
               (OutVertexId == vertexId2 && InVertexId == vertexId1);
    }

    /// <summary>
    /// Check if this edge is incident to the given vertex
    /// </summary>
    public bool IsIncidentTo(string vertexId)
    {
        return OutVertexId == vertexId || InVertexId == vertexId;
    }

    public override string ToString()
    {
        var props = string.Join(", ", Properties.Select(p => $"{p.Key}={p.Value}"));
        return $"e[{Id}][{OutVertexId}-{Label}->{InVertexId}]" + (Properties.Any() ? $" {{{props}}}" : "");
    }

    public override bool Equals(object obj)
    {
        if (obj is InMemoryEdge other)
        {
            return Id.Equals(other.Id);
        }
        return false;
    }

    public override int GetHashCode()
    {
        return Id.GetHashCode();
    }
}