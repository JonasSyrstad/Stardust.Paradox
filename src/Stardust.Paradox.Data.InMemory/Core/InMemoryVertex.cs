using System;
using System.Collections.Generic;
using System.Linq;

namespace Stardust.Paradox.Data.InMemory.Core;

/// <summary>
/// In-memory vertex implementation inspired by Apache TinkerPop's TinkerGraph
/// </summary>
public class InMemoryVertex
{
    public string Id { get; }
    public string Label { get; }
    public Dictionary<string, object> Properties { get; }

    public InMemoryVertex(string id, string label)
    {
        Id = id ?? throw new ArgumentNullException(nameof(id));
        Label = label ?? throw new ArgumentNullException(nameof(label));
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
        response.Set("type", "vertex");

        // Convert properties to proper Gremlin format
        var propertyDict = new Dictionary<string, List<Dictionary<string, object>>>();
        foreach (var prop in Properties)
        {
            propertyDict[prop.Key] = new List<Dictionary<string, object>>
            {
                new Dictionary<string, object>
                {
                    ["id"] = $"{Id}-{prop.Key}",
                    ["value"] = prop.Value
                }
            };
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
    /// Check if vertex has a property
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
    /// Create a copy of this vertex
    /// </summary>
    public InMemoryVertex Clone()
    {
        var clone = new InMemoryVertex(Id, Label);
        foreach (var prop in Properties)
        {
            clone.SetProperty(prop.Key, prop.Value);
        }
        return clone;
    }

    public override string ToString()
    {
        var props = string.Join(", ", Properties.Select(p => $"{p.Key}={p.Value}"));
        return $"v[{Id}]:{Label}" + (Properties.Any() ? $" {{{props}}}" : "");
    }

    public override bool Equals(object obj)
    {
        if (obj is InMemoryVertex other)
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
