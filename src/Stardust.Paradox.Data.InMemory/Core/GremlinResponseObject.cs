using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using Newtonsoft.Json.Linq;
using Stardust.Paradox.Data.InMemory.Extensions;

namespace Stardust.Paradox.Data.InMemory.Core;

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
    
    public JObject properties 
    { 
        get 
        {
            var rawProperties = Get<object>("properties");
            
            // Handle different property formats
            if (rawProperties is JObject jObject)
            {
                return jObject;
            }
            
            // Handle the exact CosmosDB format: Dictionary<string, List<Dictionary<string, object>>>
            if (rawProperties is Dictionary<string, List<Dictionary<string, object>>> cosmosPropsDict)
            {
                // Check if we're being called from LoadProperties method (GraphContextBase ORM layer)
                var stackTrace = Environment.StackTrace;
                if (stackTrace.Contains("LoadProperties") || stackTrace.Contains("GetVertexById"))
                {
                    // Return Cosmos DB format for ORM layer
                    var jObj = new JObject();
                    
                    foreach (var kvp in cosmosPropsDict)
                    {
                        if (kvp.Value != null && kvp.Value.Count > 0)
                        {
                            // Create arrays that can be deserialized to Property[]
                            var propertyArray = kvp.Value.Select(propDict => new
                            {
                                id = propDict.GetValueOrDefault("id"),
                                key = kvp.Key,
                                value = propDict.GetValueOrDefault("value")
                            }).ToArray();
                            
                            jObj[kvp.Key] = JArray.FromObject(propertyArray);
                        }
                    }
                    
                    return jObj;
                }
                else
                {
                    // Return simple format for direct property access
                    var jObj = new JObject();
                    
                    foreach (var kvp in cosmosPropsDict)
                    {
                        if (kvp.Value != null && kvp.Value.Count > 0)
                        {
                            var firstProp = kvp.Value[0];
                            if (firstProp.TryGetValue("value", out var val))
                            {
                                jObj[kvp.Key] = JToken.FromObject(val);
                            }
                        }
                    }
                    
                    return jObj;
                }
            }
            
            // Convert Dictionary to JObject
            if (rawProperties is Dictionary<string, object> dictProps)
            {
                var jObj = new JObject();
                
                foreach (var kvp in dictProps)
                {
                    if (kvp.Value is List<object> list && list.Count > 0)
                    {
                        var first = list[0];
                        if (first is Dictionary<string, object> firstDict && firstDict.TryGetValue("value", out var val))
                        {
                            jObj[kvp.Key] = JToken.FromObject(val);
                        }
                        else
                        {
                            jObj[kvp.Key] = JToken.FromObject(first);
                        }
                    }
                    else if (kvp.Value is IEnumerable<object> enumerable)
                    {
                        var first = enumerable.FirstOrDefault();
                        if (first is Dictionary<string, object> firstDict && firstDict.TryGetValue("value", out var val))
                        {
                            jObj[kvp.Key] = JToken.FromObject(val);
                        }
                        else
                        {
                            jObj[kvp.Key] = JToken.FromObject(first);
                        }
                    }
                    else
                    {
                        jObj[kvp.Key] = JToken.FromObject(kvp.Value);
                    }
                }
                
                return jObj;
            }
            
            // Handle DynamicProperties for backward compatibility
            if (rawProperties is DynamicProperties dynamicProps)
            {
                var jObj = new JObject();
                var props = dynamicProps.GetProperties();
                
                foreach (var kvp in props)
                {
                    jObj[kvp.Key] = JToken.FromObject(kvp.Value);
                }
                
                return jObj;
            }
            
            // Fallback to empty JObject
            return new JObject();
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
