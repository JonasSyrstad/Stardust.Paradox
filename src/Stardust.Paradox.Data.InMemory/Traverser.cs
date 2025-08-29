using System;
using System.Collections.Generic;
using System.Linq;

namespace Stardust.Paradox.Data.InMemory;

/// <summary>
/// Enhanced traverser class based on Apache TinkerPop's Traverser implementation
/// Carries data through the traversal pipeline with proper state management
/// </summary>
public class Traverser
{
    /// <summary>
    /// The actual value being traversed
    /// </summary>
    public dynamic Value { get; set; }
    
    /// <summary>
    /// The bulk/weight of this traverser (for optimization)
    /// </summary>
    public long Bulk { get; set; } = 1;
    
    /// <summary>
    /// The path taken by this traverser through the graph
    /// </summary>
    public List<dynamic> Path { get; set; }
    
    /// <summary>
    /// Sack values for computation during traversal
    /// </summary>
    public Dictionary<string, object> Sack { get; set; }
    
    /// <summary>
    /// Side effects collected during traversal
    /// </summary>
    public Dictionary<string, object> SideEffects { get; set; }
    
    /// <summary>
    /// Loops counter for repeat steps
    /// </summary>
    public Dictionary<string, int> Loops { get; set; }
    
    /// <summary>
    /// Tags for labeled steps
    /// </summary>
    public Dictionary<string, dynamic> Tags { get; set; }

    public Traverser(dynamic value)
    {
        Value = value;
        Path = new List<dynamic>();
        Sack = new Dictionary<string, object>();
        SideEffects = new Dictionary<string, object>();
        Loops = new Dictionary<string, int>();
        Tags = new Dictionary<string, dynamic>();
    }

    /// <summary>
    /// Create a copy of this traverser (split operation)
    /// </summary>
    public Traverser Split()
    {
        return new Traverser(Value)
        {
            Bulk = this.Bulk,
            Path = new List<dynamic>(this.Path),
            Sack = new Dictionary<string, object>(this.Sack),
            SideEffects = new Dictionary<string, object>(this.SideEffects),
            Loops = new Dictionary<string, int>(this.Loops),
            Tags = new Dictionary<string, dynamic>(this.Tags)
        };
    }

    /// <summary>
    /// Add a step to the traversal path
    /// </summary>
    public void AddToPath(dynamic step)
    {
        Path.Add(step);
    }

    /// <summary>
    /// Add a labeled step to the path
    /// </summary>
    public void AddToPath(dynamic step, string label)
    {
        Path.Add(step);
        if (!string.IsNullOrEmpty(label))
        {
            Tags[label] = step;
        }
    }

    /// <summary>
    /// Get the current path as a list
    /// </summary>
    public List<dynamic> GetPath()
    {
        return new List<dynamic>(Path);
    }

    /// <summary>
    /// Get a tagged value by label
    /// </summary>
    public T GetTagged<T>(string label)
    {
        if (Tags.TryGetValue(label, out var value))
        {
            if (value is T typedValue)
                return typedValue;
                
            try
            {
                return (T)Convert.ChangeType(value, typeof(T));
            }
            catch
            {
                return default(T);
            }
        }
        return default(T);
    }

    /// <summary>
    /// Set a sack value
    /// </summary>
    public void SetSack(string key, object value)
    {
        Sack[key] = value;
    }

    /// <summary>
    /// Get a sack value
    /// </summary>
    public T GetSack<T>(string key, T defaultValue = default)
    {
        if (Sack.TryGetValue(key, out var value))
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
    /// Increment loop counter for a given label
    /// </summary>
    public int IncrementLoops(string label)
    {
        if (!Loops.ContainsKey(label))
        {
            Loops[label] = 0;
        }
        return ++Loops[label];
    }

    /// <summary>
    /// Get loop count for a given label
    /// </summary>
    public int GetLoops(string label)
    {
        return Loops.TryGetValue(label, out var count) ? count : 0;
    }

    /// <summary>
    /// Reset loop counter for a given label
    /// </summary>
    public void ResetLoops(string label)
    {
        Loops[label] = 0;
    }

    /// <summary>
    /// Add a side effect
    /// </summary>
    public void AddSideEffect(string key, object value)
    {
        SideEffects[key] = value;
    }

    /// <summary>
    /// Get a side effect value
    /// </summary>
    public T GetSideEffect<T>(string key, T defaultValue = default)
    {
        if (SideEffects.TryGetValue(key, out var value))
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
    /// Get the value as a specific type
    /// </summary>
    public T Get<T>()
    {
        if (Value is T typedValue)
            return typedValue;
            
        try
        {
            return (T)Convert.ChangeType(Value, typeof(T));
        }
        catch
        {
            return default(T);
        }
    }

    /// <summary>
    /// Check if this traverser has a path
    /// </summary>
    public bool HasPath => Path.Any();

    /// <summary>
    /// Check if this traverser has tags
    /// </summary>
    public bool HasTags => Tags.Any();

    /// <summary>
    /// Get all tag labels
    /// </summary>
    public IEnumerable<string> GetTagLabels()
    {
        return Tags.Keys;
    }

    /// <summary>
    /// Merge another traverser into this one (combine bulk)
    /// </summary>
    public void Merge(Traverser other)
    {
        if (other != null && Equals(Value, other.Value))
        {
            Bulk += other.Bulk;
        }
    }

    /// <summary>
    /// Create a new traverser with a different value but same metadata
    /// </summary>
    public Traverser WithValue(dynamic newValue)
    {
        var newTraverser = Split();
        newTraverser.Value = newValue;
        return newTraverser;
    }

    public override string ToString()
    {
        var pathStr = Path.Any() ? $"[{string.Join(" -> ", Path)}]" : "";
        var bulkStr = Bulk > 1 ? $"×{Bulk}" : "";
        return $"Traverser[{Value}]{pathStr}{bulkStr}";
    }

    public override bool Equals(object obj)
    {
        if (obj is Traverser other)
        {
            return Equals(Value, other.Value);
        }
        return false;
    }

    public override int GetHashCode()
    {
        return Value?.GetHashCode() ?? 0;
    }
}