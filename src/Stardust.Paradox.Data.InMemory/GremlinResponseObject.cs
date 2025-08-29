using System.Collections.Generic;
using System.Dynamic;
using System.Linq;

namespace Stardust.Paradox.Data.InMemory;

/// <summary>
/// Enhanced ExpandoObject for better dynamic property access
/// </summary>
public class GremlinResponseObject : DynamicObject
{
    private readonly Dictionary<string, object> _data = new Dictionary<string, object>();

    public void Set(string name, object value)
    {
        _data[name] = value;
    }

    public T Get<T>(string name)
    {
        return _data.TryGetValue(name, out var value) ? (T)value : default(T);
    }

    public override bool TryGetMember(GetMemberBinder binder, out object result)
    {
        if (_data.TryGetValue(binder.Name, out result))
        {
            return true;
        }
        result = null;
        return false;
    }

    public override bool TrySetMember(SetMemberBinder binder, object value)
    {
        _data[binder.Name] = value;
        return true;
    }

    public override IEnumerable<string> GetDynamicMemberNames()
    {
        return _data.Keys;
    }

    // Add explicit properties for common access patterns
    public object id => Get<object>("id");
    public object label => Get<object>("label");
    public object type => Get<object>("type");
    public object outV => Get<object>("outV");
    public object inV => Get<object>("inV");
    
    public DynamicProperties properties 
    { 
        get 
        {
            var rawProperties = Get<object>("properties");
            
            // Handle different property formats
            if (rawProperties is DynamicProperties dynamicProps)
            {
                return dynamicProps;
            }
            
            // Handle the exact CosmosDB format: Dictionary<string, List<Dictionary<string, object>>>
            if (rawProperties is Dictionary<string, List<Dictionary<string, object>>> cosmosPropsDict)
            {
                var flatProps = new Dictionary<string, object>();
                
                foreach (var kvp in cosmosPropsDict)
                {
                    if (kvp.Value != null && kvp.Value.Count > 0)
                    {
                        var firstProp = kvp.Value[0];
                        if (firstProp.TryGetValue("value", out var val))
                        {
                            flatProps[kvp.Key] = val;
                        }
                    }
                }
                
                return new DynamicProperties(flatProps);
            }
            
            // Handle CosmosDB-style property format: Dictionary<string, List<Dictionary<string, object>>>
            if (rawProperties is Dictionary<string, object> dictProps)
            {
                var flatProps = new Dictionary<string, object>();
                
                foreach (var kvp in dictProps)
                {
                    if (kvp.Value is List<object> list && list.Count > 0)
                    {
                        var first = list[0];
                        if (first is Dictionary<string, object> firstDict && firstDict.TryGetValue("value", out var val))
                        {
                            flatProps[kvp.Key] = val;
                        }
                        else
                        {
                            flatProps[kvp.Key] = first;
                        }
                    }
                    else if (kvp.Value is IEnumerable<object> enumerable)
                    {
                        var first = enumerable.FirstOrDefault();
                        if (first is Dictionary<string, object> firstDict && firstDict.TryGetValue("value", out var val))
                        {
                            flatProps[kvp.Key] = val;
                        }
                        else
                        {
                            flatProps[kvp.Key] = first;
                        }
                    }
                    else
                    {
                        flatProps[kvp.Key] = kvp.Value;
                    }
                }
                
                return new DynamicProperties(flatProps);
            }
            
            // Fallback to empty properties
            return new DynamicProperties(new Dictionary<string, object>());
        }
    }

    public override string ToString()
    {
        var pairs = new List<string>();
        foreach (var kvp in _data)
        {
            pairs.Add($"[{kvp.Key}, {kvp.Value}]");
        }
        return "{" + string.Join(", ", pairs) + "}";
    }
}