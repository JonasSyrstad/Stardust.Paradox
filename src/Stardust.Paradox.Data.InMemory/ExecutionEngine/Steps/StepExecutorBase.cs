using System;
using System.Collections.Generic;
using System.Linq;
using Stardust.Paradox.Data.InMemory.Core;

namespace Stardust.Paradox.Data.InMemory.ExecutionEngine.Steps
{
    /// <summary>
    /// Base class for step executors providing common utility methods
    /// for extracting data from graph elements.
    /// Enhanced to handle TinkerPop and CosmosDB property formats.
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
        /// Extract ID from various data structures - improved for TinkerPop compliance
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
        /// Extract label from various data structures - improved for TinkerPop compliance
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
        /// Extract type from various data structures - improved for TinkerPop compliance
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
                try
                {
                    var type = value.type;
                    if (type != null)
                        return type.ToString();
                }
                catch
                {
                    // Continue to fallback logic
                }
                    
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
        /// Extract properties from various data structures - enhanced for TinkerPop and CosmosDB compliance
        /// Based on Apache TinkerGraph property handling patterns
        /// </summary>
        protected Dictionary<string, object> ExtractProperties(dynamic value)
        {
            if (value == null) return new Dictionary<string, object>();
            
            try
            {
                // Handle GremlinResponseObject (the actual response type)
                if (value is GremlinResponseObject responseObj)
                {
                    var properties = new Dictionary<string, object>();
                    
                    try
                    {
                        // Get the raw properties object
                        var rawProperties = responseObj.Get<object>("properties");
                        
                        if (rawProperties == null)
                            return properties;
                        
                        // Handle the exact CosmosDB format: Dictionary<string, List<Dictionary<string, object>>>
                        // This is the multi-value property format used by CosmosDB
                        if (rawProperties is Dictionary<string, List<Dictionary<string, object>>> cosmosPropsDict)
                        {
                            foreach (var kvp in cosmosPropsDict)
                            {
                                if (kvp.Value != null && kvp.Value.Count > 0)
                                {
                                    var firstProp = kvp.Value[0];
                                    if (firstProp != null && firstProp.TryGetValue("value", out var val))
                                    {
                                        properties[kvp.Key] = NormalizePropertyValue(val);
                                    }
                                }
                            }
                        }
                        // Handle standard dictionary format: Dictionary<string, object>
                        else if (rawProperties is Dictionary<string, object> propsDict)
                        {
                            foreach (var kvp in propsDict)
                            {
                                var normalizedValue = ExtractPropertyValue(kvp.Value);
                                if (normalizedValue != null)
                                {
                                    properties[kvp.Key] = normalizedValue;
                                }
                            }
                        }
                        // Handle list-based property format
                        else if (rawProperties is IEnumerable<dynamic> propsList)
                        {
                            foreach (var prop in propsList)
                            {
                                try
                                {
                                    if (prop is IDictionary<string, object> propDict)
                                    {
                                        if (propDict.TryGetValue("key", out var key) && 
                                            propDict.TryGetValue("value", out var val))
                                        {
                                            properties[key.ToString()] = NormalizePropertyValue(val);
                                        }
                                    }
                                }
                                catch
                                {
                                    // Skip malformed property
                                    continue;
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
                                // Try to convert to dictionary
                                if (dynamicProps is IDictionary<string, object> propsDictionary)
                                {
                                    foreach (var kvp in propsDictionary)
                                    {
                                        var normalizedValue = ExtractPropertyValue(kvp.Value);
                                        if (normalizedValue != null)
                                        {
                                            properties[kvp.Key] = normalizedValue;
                                        }
                                    }
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
                            var result = new Dictionary<string, object>();
                            foreach (var kvp in props)
                            {
                                var normalizedValue = ExtractPropertyValue(kvp.Value);
                                if (normalizedValue != null)
                                {
                                    result[kvp.Key] = normalizedValue;
                                }
                            }
                            return result;
                        }
                    }
                    
                    // If no explicit properties key, treat the whole dict as properties
                    // but exclude special keys (TinkerPop reserved keys)
                    var resultDict = new Dictionary<string, object>();
                    var reservedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase) 
                    { 
                        "id", "label", "type", "inV", "outV", "inVLabel", "outVLabel", "properties" 
                    };
                    
                    foreach (var kvp in dict)
                    {
                        if (!reservedKeys.Contains(kvp.Key))
                        {
                            var normalizedValue = ExtractPropertyValue(kvp.Value);
                            if (normalizedValue != null)
                            {
                                resultDict[kvp.Key] = normalizedValue;
                            }
                        }
                    }
                    return resultDict;
                }
                
                // Handle dynamic object with properties
                try
                {
                    var dynamicProps = value.properties;
                    if (dynamicProps != null)
                    {
                        if (dynamicProps is IDictionary<string, object> propsDict)
                        {
                            var result = new Dictionary<string, object>();
                            foreach (var kvp in propsDict)
                            {
                                var normalizedValue = ExtractPropertyValue(kvp.Value);
                                if (normalizedValue != null)
                                {
                                    result[kvp.Key] = normalizedValue;
                                }
                            }
                            return result;
                        }
                        
                        // Handle DynamicProperties
                        if (dynamicProps is DynamicProperties dynProps)
                        {
                            return dynProps.GetProperties();
                        }
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
        /// Extract a single property value from various formats
        /// Handles TinkerPop multi-value properties and CosmosDB formats
        /// </summary>
        private object ExtractPropertyValue(object value)
        {
            if (value == null)
                return null;

            // Handle CosmosDB-style property format: [{"value": actualValue}]
            if (value is List<object> list && list.Count > 0)
            {
                var first = list[0];
                if (first is Dictionary<string, object> firstDict && firstDict.TryGetValue("value", out var val))
                {
                    return NormalizePropertyValue(val);
                }
                // If not in expected format, return first value
                return NormalizePropertyValue(first);
            }
            
            // Handle IEnumerable<dynamic> (common in Gremlin.Net)
            if (value is IEnumerable<dynamic> enumerable)
            {
                var first = enumerable.FirstOrDefault();
                if (first != null)
                {
                    if (first is IDictionary<string, object> firstDict && firstDict.TryGetValue("value", out var val))
                    {
                        return NormalizePropertyValue(val);
                    }
                    return NormalizePropertyValue(first);
                }
            }
            
            // Handle dictionary with value key
            if (value is IDictionary<string, object> dict && dict.TryGetValue("value", out var dictVal))
            {
                return NormalizePropertyValue(dictVal);
            }
            
            // Return value as-is, normalized
            return NormalizePropertyValue(value);
        }

        /// <summary>
        /// Normalize property values to standard types
        /// Handles type conversions similar to Apache TinkerGraph
        /// </summary>
        private object NormalizePropertyValue(object value)
        {
  if (value == null)
           return null;

            // Handle string representations of numbers (common in JSON)
            if (value is string strValue)
            {
 // Try to parse as different numeric types
    if (int.TryParse(strValue, out int intVal))
          return intVal;
     if (long.TryParse(strValue, out long longVal))
  return longVal;
       if (double.TryParse(strValue, out double doubleVal))
        return doubleVal;
      if (bool.TryParse(strValue, out bool boolVal))
        return boolVal;
      
return strValue;
            }

       // Handle numeric types - preserve decimal type for better precision
            if (value is decimal)
                return value; // Keep decimal as decimal
     
            if (value is int || value is long || value is short || value is byte)
     return Convert.ToInt64(value);
     
  if (value is float || value is double)
   return Convert.ToDouble(value);
     
            // Handle boolean
        if (value is bool)
        return value;
    
  // Handle DateTime/DateTimeOffset
            if (value is DateTime || value is DateTimeOffset)
           return value;
         
     // Return as-is for other types
            return value;
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
 
    // Try to convert as a last resort
            try
          {
                result = Convert.ToDouble(value);
  return true;
    }
      catch
        {
      return false;
    }
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

        #region Predicate Evaluation Helpers

        /// <summary>
        /// Evaluate a logical condition string (like "has('age', gt(25))")
        /// </summary>
        protected bool EvaluateLogicalCondition(Traverser traverser, string conditionStr)
        {
            if (string.IsNullOrWhiteSpace(conditionStr))
                return false;

            // Parse the condition string
            conditionStr = conditionStr.Trim();

            // Handle has() conditions
            if (conditionStr.StartsWith("has("))
            {
                return EvaluateHasCondition(traverser, conditionStr);
            }

            return false;
        }

        /// <summary>
        /// Evaluate a has() condition
        /// </summary>
        private bool EvaluateHasCondition(Traverser traverser, string conditionStr)
        {
            // Extract content between has( and )
            var content = ExtractBetweenParentheses(conditionStr, "has");
            if (string.IsNullOrEmpty(content))
                return false;

            var parts = SplitConditionArguments(content);
            if (parts.Count == 0)
                return false;

            var propertyKey = parts[0].Trim().Trim('\'', '"');

            if (parts.Count == 1)
            {
                // has('property') - check if property exists
                var properties = ExtractProperties(traverser.Value);
                return properties != null && properties.ContainsKey(propertyKey);
            }
            else if (parts.Count == 2)
            {
                // has('property', value) or has('property', predicate)
                var valueOrPredicate = parts[1].Trim();
                var properties = ExtractProperties(traverser.Value);
                
                if (properties == null || !properties.ContainsKey(propertyKey))
                    return false;

                var actualValue = properties[propertyKey];

                // Check if it's a predicate
                if (IsPredicate(valueOrPredicate))
                {
                    return EvaluatePredicate(actualValue, valueOrPredicate);
                }
                else
                {
                    // Direct value comparison
                    var expectedValue = valueOrPredicate.Trim('\'', '"');
                    return CompareValues(actualValue, expectedValue);
                }
            }

            return false;
        }

        /// <summary>
        /// Check if a string is a predicate (gt, lt, within, etc.)
        /// </summary>
        private bool IsPredicate(string value)
        {
            return value.StartsWith("gt(") || value.StartsWith("gte(") ||
                   value.StartsWith("lt(") || value.StartsWith("lte(") ||
                   value.StartsWith("eq(") || value.StartsWith("neq(") ||
                   value.StartsWith("within(") || value.StartsWith("without(");
        }

        /// <summary>
        /// Evaluate a predicate against a value
        /// </summary>
      protected bool EvaluatePredicate(object actualValue, string predicate)
        {
   // Normalize predicate - ensure it has closing parenthesis
    if (!predicate.EndsWith(")"))
        predicate += ")";

    // Handle comparison predicates
     if (predicate.StartsWith("gt("))
      {
       var threshold = ExtractPredicateValue(predicate, "gt");
         return CompareNumeric(actualValue, threshold, (a, b) => a > b);
       }
else if (predicate.StartsWith("gte("))
         {
var threshold = ExtractPredicateValue(predicate, "gte");
   return CompareNumeric(actualValue, threshold, (a, b) => a >= b);
    }
            else if (predicate.StartsWith("lt("))
   {
  var threshold = ExtractPredicateValue(predicate, "lt");
    return CompareNumeric(actualValue, threshold, (a, b) => a < b);
   }
    else if (predicate.StartsWith("lte("))
            {
        var threshold = ExtractPredicateValue(predicate, "lte");
      return CompareNumeric(actualValue, threshold, (a, b) => a <= b);
          }
  else if (predicate.StartsWith("eq("))
      {
      var expected = ExtractPredicateValue(predicate, "eq");
     return CompareValues(actualValue, expected);
         }
            else if (predicate.StartsWith("neq("))
    {
         var expected = ExtractPredicateValue(predicate, "neq");
        return !CompareValues(actualValue, expected);
            }
else if (predicate.StartsWith("within("))
     {
   var values = ExtractWithinValues(predicate, "within");
        return values.Any(v => CompareValues(actualValue, v));
  }
     else if (predicate.StartsWith("without("))
     {
    var values = ExtractWithinValues(predicate, "without");
       return !values.Any(v => CompareValues(actualValue, v));
  }
            // Handle string predicates
      else if (predicate.StartsWith("containing("))
      {
       var searchValue = ExtractPredicateValue(predicate, "containing");
       var actualStr = actualValue?.ToString() ?? "";
      return actualStr.IndexOf(searchValue, StringComparison.OrdinalIgnoreCase) >= 0;
            }
   else if (predicate.StartsWith("notContaining("))
 {
   var searchValue = ExtractPredicateValue(predicate, "notContaining");
       var actualStr = actualValue?.ToString() ?? "";
            return actualStr.IndexOf(searchValue, StringComparison.OrdinalIgnoreCase) < 0;
        }
            else if (predicate.StartsWith("startingWith("))
 {
     var searchValue = ExtractPredicateValue(predicate, "startingWith");
         var actualStr = actualValue?.ToString() ?? "";
return actualStr.StartsWith(searchValue, StringComparison.OrdinalIgnoreCase);
 }
  else if (predicate.StartsWith("notStartingWith("))
     {
   var searchValue = ExtractPredicateValue(predicate, "notStartingWith");
     var actualStr = actualValue?.ToString() ?? "";
        return !actualStr.StartsWith(searchValue, StringComparison.OrdinalIgnoreCase);
 }
else if (predicate.StartsWith("endingWith("))
        {
  var searchValue = ExtractPredicateValue(predicate, "endingWith");
       var actualStr = actualValue?.ToString() ?? "";
          return actualStr.EndsWith(searchValue, StringComparison.OrdinalIgnoreCase);
 }
        else if (predicate.StartsWith("notEndingWith("))
         {
    var searchValue = ExtractPredicateValue(predicate, "notEndingWith");
     var actualStr = actualValue?.ToString() ?? "";
   return !actualStr.EndsWith(searchValue, StringComparison.OrdinalIgnoreCase);
      }

            return false;
}

        /// <summary>
      /// Compare two values with type coercion
        /// </summary>
   private bool CompareValues(object actual, string expected)
        {
            if (actual == null && expected == null)
       return true;
  if (actual == null || expected == null)
                return false;

            // Try boolean comparison
        if (bool.TryParse(expected, out bool expectedBool))
       {
         if (actual is bool actualBool)
           return actualBool == expectedBool;
 if (bool.TryParse(actual.ToString(), out bool parsedBool))
           return parsedBool == expectedBool;
            }

// Try integer comparison first (before decimal)
            if (int.TryParse(expected, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out int expectedInt))
   {
       // Check if the expected value is actually an integer (no decimal point)
       if (!expected.Contains(".") && !expected.Contains(","))
     {
          if (actual is int actualInt)
   return actualInt == expectedInt;
          
           if (actual is long actualLong)
     return actualLong == expectedInt;
      
      if (actual is short actualShort)
   return actualShort == expectedInt;
      
                if (actual is byte actualByte)
   return actualByte == expectedInt;
    }
            }

// Try decimal comparison for decimal numbers
      if (decimal.TryParse(expected, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out decimal expectedDecimal))
         {
         // If actual is decimal, compare directly
           if (actual is decimal actualDecimal)
return actualDecimal == expectedDecimal;
                
    // Try to convert actual to decimal
   if (actual is double actualDouble)
             {
   // Convert double to decimal for comparison
         try
     {
      var actualAsDecimal = Convert.ToDecimal(actualDouble);
      return actualAsDecimal == expectedDecimal;
          }
  catch
      {
  // Fallback to double comparison if conversion fails
       }
  }
        
     if (actual is float actualFloat)
    {
        try
             {
     var actualAsDecimal = Convert.ToDecimal(actualFloat);
         return actualAsDecimal == expectedDecimal;
     }
         catch
      {
      // Fallback to double comparison if conversion fails
             }
  }
                
        if (actual is int actualIntForDecimal)
        return actualIntForDecimal == expectedDecimal;
 
     if (actual is long actualLongForDecimal)
 return actualLongForDecimal == expectedDecimal;
 }

            // Try numeric comparison as fallback (for floating point without decimal points)
            if (double.TryParse(expected, out double expectedNum))
  {
            if (TryConvertToDouble(actual, out double actualNum))
     {
          // For equality comparison, use exact equality
   return actualNum == expectedNum;
          }
   }

          // String comparison - CASE-SENSITIVE to match CosmosDB behavior
          // But only for actual string-to-string comparisons
          if (actual is string || expected is string)
      {
     return actual.ToString().Equals(expected, StringComparison.OrdinalIgnoreCase);
          }
  
  // For other types, use default equality
return actual.Equals(expected);
        }

        /// <summary>
        /// Compare numeric values using a comparison function
        /// </summary>
        private bool CompareNumeric(object actualValue, string thresholdStr, Func<double, double, bool> comparison)
        {
  // First try to parse as decimal for better precision
       if (decimal.TryParse(thresholdStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out decimal thresholdDecimal))
            {
    // If actual value is decimal, compare as decimals
         if (actualValue is decimal actualDecimal)
          {
   // Convert comparison to work with decimal
   var thresholdAsDouble = (double)thresholdDecimal;
     var actualAsDouble = (double)actualDecimal;
 return comparison(actualAsDouble, thresholdAsDouble);
              }
            
     // Otherwise convert to double
                if (TryConvertToDouble(actualValue, out double actualDouble))
       {
     var thresholdAsDouble = (double)thresholdDecimal;
         return comparison(actualDouble, thresholdAsDouble);
  }
            }
      
         // Fallback to original double parsing
            if (double.TryParse(thresholdStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double threshold))
            {
                if (TryConvertToDouble(actualValue, out double actual))
  {
        return comparison(actual, threshold);
    }
            }
       
         return false;
        }

        /// <summary>
        /// Extract value from a predicate like "gt(25)"
        /// </summary>
    private string ExtractPredicateValue(string predicate, string predicateName)
        {
            var content = ExtractBetweenParentheses(predicate, predicateName);
            return content?.Trim().Trim('\'', '"') ?? "";
        }

        /// <summary>
        /// Extract multiple values from within() or without() predicate
        /// </summary>
        private List<string> ExtractWithinValues(string predicate, string predicateName)
        {
            var content = ExtractBetweenParentheses(predicate, predicateName);
            if (string.IsNullOrEmpty(content))
                return new List<string>();

            var values = new List<string>();
            var current = "";
            var inQuotes = false;
            var quoteChar = '\0';

            foreach (char c in content)
            {
                if (!inQuotes && (c == '\'' || c == '"'))
                {
                    inQuotes = true;
                    quoteChar = c;
                }
                else if (inQuotes && c == quoteChar)
                {
                    inQuotes = false;
                }
                else if (!inQuotes && c == ',')
                {
                    if (!string.IsNullOrWhiteSpace(current))
                    {
                        values.Add(current.Trim().Trim('\'', '"'));
                        current = "";
                    }
                }
                else if (c != '\'' && c != '"')
                {
                    current += c;
                }
            }

            if (!string.IsNullOrWhiteSpace(current))
            {
                values.Add(current.Trim().Trim('\'', '"'));
            }

            return values;
        }

        /// <summary>
        /// Extract content between parentheses for a given prefix
        /// </summary>
        private string ExtractBetweenParentheses(string text, string prefix)
        {
            var start = prefix.Length + 1; // Skip "prefix("
            if (start >= text.Length)
                return "";

            var depth = 1;
            var end = start;

            for (int i = start; i < text.Length && depth > 0; i++)
            {
                if (text[i] == '(')
                    depth++;
                else if (text[i] == ')')
                {
                    depth--;
                    if (depth == 0)
                    {
                        end = i;
                        break;
                    }
                }
            }

            if (depth == 0 && end > start)
            {
                return text.Substring(start, end - start);
            }

            // If no closing found, take rest of string
            return text.Substring(start).TrimEnd(')');
        }

        /// <summary>
        /// Split condition arguments respecting quotes and parentheses
        /// </summary>
        protected List<string> SplitConditionArguments(string content)
        {
            var parts = new List<string>();
            var current = "";
            var inQuotes = false;
            var quoteChar = '\0';
            var parenDepth = 0;

            foreach (char c in content)
            {
                if (!inQuotes && (c == '\'' || c == '"'))
                {
                    inQuotes = true;
                    quoteChar = c;
                    current += c;
                }
                else if (inQuotes && c == quoteChar)
                {
                    inQuotes = false;
                    current += c;
                }
                else if (!inQuotes && c == '(')
                {
                    parenDepth++;
                    current += c;
                }
                else if (!inQuotes && c == ')')
                {
                    parenDepth--;
                    current += c;
                }
                else if (!inQuotes && parenDepth == 0 && c == ',')
                {
                    if (!string.IsNullOrWhiteSpace(current))
                    {
                        parts.Add(current.Trim());
                        current = "";
                    }
                }
                else
                {
                    current += c;
                }
            }

            if (!string.IsNullOrWhiteSpace(current))
            {
                parts.Add(current.Trim());
            }

            return parts;
        }

        #endregion
    }
}
