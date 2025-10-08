using System;
using System.Collections.Generic;
using System.Linq;
using Stardust.Paradox.Data.InMemory.Core;

namespace Stardust.Paradox.Data.InMemory.ExecutionEngine.Steps
{
    /// <summary>
    /// Base class for step executors providing common utility methods
    /// for extracting data from graph elements.
    /// </summary>
    public abstract class StepExecutorBase : IStepExecutor
    {
        protected  InMemoryGraphDatabase Database;


        protected StepExecutorBase(InMemoryGraphDatabase database)
        {
            Database = database;
        }

        public abstract string StepName { get; }
        public abstract string StepDescription { get; }
        public abstract void Execute(TinkerGraphStep step, TinkerTraversalContext context);

        #region Utility Methods for Data Extraction

        /// <summary>
        /// Extract vertex ID from various data structures
        /// </summary>
        protected string ExtractVertexId(dynamic value)
        {
            return ExtractId(value);
        }

        /// <summary>
        /// Extract edge ID from various data structures  
        /// </summary>
        protected string ExtractEdgeId(dynamic value)
        {
            return ExtractId(value);
        }

        /// <summary>
        /// Extract ID from various data structures
        /// </summary>
        protected string ExtractId(dynamic value)
        {
            if (value == null) return null;
            
            try
            {
                // Handle GremlinResponseObject (the actual response type)
                if (value is GremlinResponseObject responseObj)
                {
                    return responseObj.id?.ToString();
                }
                
                // Handle dictionary format
                if (value is IDictionary<string, object> dict)
                {
                    if (dict.TryGetValue("id", out var id))
                    {
                        return id?.ToString();
                    }
                }
                
                // Handle dynamic object with id property
                try
                {
                    var dynamicId = value.id;
                    if (dynamicId != null)
                    {
                        return dynamicId.ToString();
                    }
                }
                catch
                {
                    // Ignore dynamic access errors
                }
                
                // Handle direct ID value
                if (value is string strValue)
                {
                    return strValue;
                }
                
                // Handle numeric ID
                if (value is int || value is long)
                {
                    return value.ToString();
                }
            }
            catch (Exception)
            {
                // If all else fails, return null
            }
            
            return null;
        }

        /// <summary>
        /// Extract label from various data structures
        /// </summary>
        protected string ExtractLabel(dynamic value)
        {
            if (value == null) return null;
            
            try
            {
                // Handle GremlinResponseObject (the actual response type)
                if (value is GremlinResponseObject responseObj)
                {
                    return responseObj.label?.ToString();
                }
                
                // Handle dictionary format
                if (value is IDictionary<string, object> dict)
                {
                    if (dict.TryGetValue("label", out var label))
                    {
                        return label?.ToString();
                    }
                }
                
                // Handle dynamic object with label property
                try
                {
                    var dynamicLabel = value.label;
                    if (dynamicLabel != null)
                    {
                        return dynamicLabel.ToString();
                    }
                }
                catch
                {
                    // Ignore dynamic access errors
                }
            }
            catch (Exception)
            {
                // If all else fails, return null
            }
            
            return null;
        }

        /// <summary>
        /// Extract type from various data structures  
        /// </summary>
        protected string ExtractType(dynamic value)
        {
            if (value == null) return null;
            
            try
            {
                // Check for direct type property
                if (value is IDictionary<string, object> dict)
                {
                    if (dict.ContainsKey("type"))
                        return dict["type"]?.ToString();
                }
                
                // Try dynamic property access
                var type = value.type;
                if (type != null)
                    return type.ToString();
                    
                // Check if it's likely a vertex or edge based on structure
                if (value is IDictionary<string, object> dictCheck)
                {
                    if (dictCheck.ContainsKey("outV") || dictCheck.ContainsKey("inV"))
                        return "edge";
                    if (dictCheck.ContainsKey("properties") || dictCheck.ContainsKey("label"))
                        return "vertex";
                }
                
                // Default to vertex for backward compatibility
                return "vertex";
            }
            catch
            {
                return "vertex"; // Default fallback
            }
        }

        /// <summary>
        /// Extract properties from various data structures
        /// </summary>
        protected Dictionary<string, object> ExtractProperties(dynamic value)
        {
            if (value == null) return new Dictionary<string, object>();
            
            try
            {
                // Handle GremlinResponseObject (the actual response type)
                if (value is GremlinResponseObject responseObj)
                {
                    // Access the underlying properties data directly
                    var properties = new Dictionary<string, object>();
                    
                    try
                    {
                        // Get the raw properties object
                        var rawProperties = responseObj.Get<object>("properties");
                        
                        // Handle the exact CosmosDB format: Dictionary<string, List<Dictionary<string, object>>>
                        if (rawProperties is Dictionary<string, List<Dictionary<string, object>>> cosmosPropsDict)
                        {
                            foreach (var kvp in cosmosPropsDict)
                            {
                                if (kvp.Value != null && kvp.Value.Count > 0)
                                {
                                    var firstProp = kvp.Value[0];
                                    if (firstProp.TryGetValue("value", out var val))
                                    {
                                        properties[kvp.Key] = val;
                                    }
                                }
                            }
                        }
                        // Handle other possible formats
                        else if (rawProperties is Dictionary<string, object> propsDict)
                        {
                            foreach (var kvp in propsDict)
                            {
                                // Handle CosmosDB-style property format: [{"value": actualValue}]
                                if (kvp.Value is List<object> list && list.Count > 0)
                                {
                                    var first = list[0];
                                    if (first is Dictionary<string, object> firstDict && firstDict.TryGetValue("value", out var val))
                                    {
                                        properties[kvp.Key] = val;
                                    }
                                    else
                                    {
                                        properties[kvp.Key] = first;
                                    }
                                }
                                else if (kvp.Value is IEnumerable<dynamic> enumerable)
                                {
                                    var first = enumerable.FirstOrDefault();
                                    if (first is IDictionary<string, object> firstDict && firstDict.TryGetValue("value", out var val))
                                    {
                                        properties[kvp.Key] = val;
                                    }
                                    else
                                    {
                                        properties[kvp.Key] = first;
                                    }
                                }
                                else
                                {
                                    properties[kvp.Key] = kvp.Value;
                                }
                            }
                        }
                    }
                    catch (Exception)
                    {
                        // If property access fails, try to access properties dynamically
                        try
                        {
                            dynamic dynamicResponse = responseObj;
                            var dynamicProps = dynamicResponse.properties;
                            if (dynamicProps != null)
                            {
                                // Handle the DynamicProperties case
                                if (dynamicProps is DynamicProperties dynProps)
                                {
                                    return dynProps.GetProperties();
                                }
                            }
                        }
                        catch
                        {
                            // Fallback to empty dictionary
                        }
                    }
                    
                    return properties;
                }
                
                // Handle dictionary format
                if (value is IDictionary<string, object> dict)
                {
                    if (dict.TryGetValue("properties", out var propsObj))
                    {
                        if (propsObj is IDictionary<string, object> props)
                        {
                            return new Dictionary<string, object>(props);
                        }
                    }
                    
                    // If no explicit properties key, treat the whole dict as properties
                    // but exclude special keys
                    var result = new Dictionary<string, object>();
                    foreach (var kvp in dict)
                    {
                        if (kvp.Key != "id" && kvp.Key != "label" && kvp.Key != "type")
                        {
                            result[kvp.Key] = kvp.Value;
                        }
                    }
                    return result;
                }
                
                // Handle dynamic object with properties
                try
                {
                    var dynamicProps = value.properties;
                    if (dynamicProps is IDictionary<string, object> propsDict)
                    {
                        return new Dictionary<string, object>(propsDict);
                    }
                    
                    // Handle DynamicProperties
                    if (dynamicProps is DynamicProperties dynProps)
                    {
                        return dynProps.GetProperties();
                    }
                }
                catch
                {
                    // Ignore dynamic access errors
                }
            }
            catch (Exception)
            {
                // If all else fails, return empty dictionary
            }
            
            return new Dictionary<string, object>();
        }

        /// <summary>
        /// Helper method to convert various numeric types to double
        /// </summary>
        protected bool TryConvertToDouble(object value, out double result)
        {
            result = 0.0;
            
            if (value == null)
                return false;
                
            if (value is double d)
            {
                result = d;
                return true;
            }
            
            if (value is float f)
            {
                result = f;
                return true;
            }
            
            if (value is int i)
            {
                result = i;
                return true;
            }
            
            if (value is long l)
            {
                result = l;
                return true;
            }
            
            if (value is decimal dec)
            {
                result = (double)dec;
                return true;
            }
            
            if (value is string str && double.TryParse(str, out double parsed))
            {
                result = parsed;
                return true;
            }
            
            return false;
        }

        /// <summary>
        /// Helper method to convert various numeric types to long
        /// </summary>
        protected bool TryConvertToLong(object value, out long result)
        {
            result = 0L;
            
            if (value == null)
                return false;
                
            if (value is long l)
            {
                result = l;
                return true;
            }
            
            if (value is int i)
            {
                result = i;
                return true;
            }
            
            if (value is double d)
            {
                result = (long)d;
                return true;
            }
            
            if (value is float f)
            {
                result = (long)f;
                return true;
            }
            
            if (value is decimal dec)
            {
                result = (long)dec;
                return true;
            }
            
            if (value is string str && long.TryParse(str, out long parsed))
            {
                result = parsed;
                return true;
            }
            
            return false;
        }

        /// <summary>
        /// Safely convert an object to integer
        /// </summary>
        protected bool TryConvertToInt(object value, out int result)
        {
            result = 0;
            
            if (value == null)
                return false;
            
            if (value is int intValue)
            {
                result = intValue;
                return true;
            }
            
            if (value is long longValue && longValue >= int.MinValue && longValue <= int.MaxValue)
            {
                result = (int)longValue;
                return true;
            }
            
            if (value is double doubleValue && doubleValue >= int.MinValue && doubleValue <= int.MaxValue && doubleValue == Math.Floor(doubleValue))
            {
                result = (int)doubleValue;
                return true;
            }
            
            if (value is string stringValue && int.TryParse(stringValue, out int parsedValue))
            {
                result = parsedValue;
                return true;
            }
            
            return false;
        }

        #endregion
    }
}
