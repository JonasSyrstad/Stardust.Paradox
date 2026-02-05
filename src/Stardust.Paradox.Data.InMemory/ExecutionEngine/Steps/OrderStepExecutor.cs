using Stardust.Paradox.Data.Annotations.Annotations;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Stardust.Paradox.Data.InMemory.ExecutionEngine.Steps
{
    /// <summary>
    /// Executes the order() step which sorts results.
    /// 
    /// Behavior:
    /// - Orders results by their string representation (default)
    /// - Can be modified with .by() modulators for property-based ordering
    /// - Supports multiple .by() modulators for secondary sorting (ThenBy)
  /// 
    /// Example:
    /// g.V().values('name').order() - returns names in sorted order
    /// g.V().order().by('age', incr) - orders vertices by age ascending
    /// g.V().order().by('age', decr) - orders vertices by age descending
    /// g.V().order().by('city', incr).by('age', incr) - orders by city, then by age
    /// </summary>
    [UsedImplicitly]
    public class OrderStepExecutor : StepExecutorBase
    {
        public OrderStepExecutor(Core.InMemoryGraphDatabase database) : base(database)
   {
        }

        public override string StepName => "order";

        public override string StepDescription => 
      "Sorts results in the traversal stream. " +
     "By default orders by string representation of values. " +
      "Can be modified with .by() modulator. " +
   "Supports multiple .by() modulators for multi-level sorting.";

        public override void Execute(TinkerGraphStep step, TinkerTraversalContext context)
  {
       // Check for .by() modulator arguments from metadata (set by TinkerGraphQueryExecutor lookahead)
   var allByArguments = context.GetMetadata<List<List<object>>>("all_by_arguments");
   
          // Extract all by modulator configurations
   var sortConfigurations = new List<(string PropertyName, bool Descending)>();
         
        if (allByArguments != null && allByArguments.Any())
       {
      foreach (var byArguments in allByArguments)
         {
          if (byArguments.Any())
           {
        // Extract property name from .by() step
            var propertyName = byArguments[0]?.ToString();
        
  // Check for ordering type (incr/decr)
   var orderType = "incr";
        if (byArguments.Count > 1)
           {
       orderType = byArguments[1]?.ToString()?.ToLower() ?? "incr";
}
      
         sortConfigurations.Add((propertyName, orderType == "decr"));
          }
   }
             
      // Clear the metadata after using it
        context.RemoveMetadata("all_by_arguments");
            }

    // Order the traversers
    IEnumerable<Traverser> ordered;
      
   if (sortConfigurations.Any())
         {
                // Apply multi-level sorting using the sort configurations
           ordered = ApplyMultiLevelSort(context.Traversers, sortConfigurations);
         }
      else
  {
                // Default: order by string representation
   ordered = context.Traversers.OrderBy(t => t.Value?.ToString()).ToList();
   }

        context.Traversers.Clear();
    context.Traversers.AddRange(ordered);
        }

        /// <summary>
      /// Apply multi-level sorting based on multiple sort configurations
        /// </summary>
private IEnumerable<Traverser> ApplyMultiLevelSort(
            List<Traverser> traversers, 
   List<(string PropertyName, bool Descending)> sortConfigurations)
        {
  if (!sortConfigurations.Any())
 {
     return traversers;
            }

    // Start with the first sort configuration
            var firstSort = sortConfigurations[0];
        IOrderedEnumerable<Traverser> ordered;
   
if (firstSort.Descending)
            {
     ordered = traversers.OrderByDescending(t => GetPropertyValue(t, firstSort.PropertyName), new PropertyValueComparer());
    }
  else
       {
          ordered = traversers.OrderBy(t => GetPropertyValue(t, firstSort.PropertyName), new PropertyValueComparer());
}

     // Apply additional sorts (ThenBy)
            for (int i = 1; i < sortConfigurations.Count; i++)
      {
    var sortConfig = sortConfigurations[i];
         
if (sortConfig.Descending)
          {
       ordered = ordered.ThenByDescending(t => GetPropertyValue(t, sortConfig.PropertyName), new PropertyValueComparer());
                }
  else
        {
 ordered = ordered.ThenBy(t => GetPropertyValue(t, sortConfig.PropertyName), new PropertyValueComparer());
}
            }

     return ordered.ToList();
        }

        /// <summary>
/// Comparer that handles different property value types correctly
     /// </summary>
  private class PropertyValueComparer : IComparer<object>
        {
        /// <summary>
        /// Comparer that handles different property value types according to TinkerPop Orderability semantics.
        /// 
        /// Type Priority (from lowest to highest):
        /// 1. null, 2. Boolean, 3. Number, 4. Date, 5. String, 
        /// 6. Vertex, 7. Edge, 8. VertexProperty, 9. Property,
        /// 10. Path, 11. Set, 12. List, 13. Map, 14. Unknown
        /// 
        /// Within numerics: -Infinity < negative numbers < 0 < positive numbers < +Infinity < NaN
        /// </summary>
        public int Compare(object x, object y)
            {
                // Handle nulls - null is lowest priority
                if (x == null && y == null) return 0;
                if (x == null) return -1;
                if (y == null) return 1;

                // Get type priorities
                var xPriority = GetTypePriority(x);
                var yPriority = GetTypePriority(y);
                
                // If different types, order by type priority
                if (xPriority != yPriority)
                {
                    return xPriority.CompareTo(yPriority);
                }
                
                // Same type priority - compare within type
                return CompareWithinType(x, y, xPriority);
            }
            
            private int GetTypePriority(object value)
            {
                if (value == null) return 0;
                if (value is bool) return 1;
                if (IsNumeric(value)) return 2;
                if (value is DateTime || value is DateTimeOffset) return 3;
                if (value is string) return 4;
                if (value is Core.InMemoryVertex || 
                    (value is Core.GremlinResponseObject gro && gro.type == "vertex")) return 5;
                if (value is Core.InMemoryEdge ||
                    (value is Core.GremlinResponseObject gre && gre.type == "edge")) return 6;
                if (value is System.Collections.Generic.ISet<object>) return 10;
                if (value is System.Collections.IList) return 11;
                if (value is System.Collections.IDictionary) return 12;
                return 99; // Unknown
            }
            
            private int CompareWithinType(object x, object y, int typePriority)
            {
                switch (typePriority)
                {
                    case 1: // Boolean: FALSE < TRUE
                        return ((bool)x).CompareTo((bool)y);
                        
                    case 2: // Numeric with NaN handling
                        return CompareNumeric(x, y);
                        
                    case 3: // DateTime
                        return CompareDateTime(x, y);
                        
                    case 4: // String: lexicographic
                        return string.Compare(x.ToString(), y.ToString(), StringComparison.Ordinal);
                        
                    case 5: // Vertex: by id
                    case 6: // Edge: by id
                        return CompareById(x, y);
                        
                    default:
                        return string.Compare(x.ToString(), y.ToString(), StringComparison.Ordinal);
                }
            }
            
            private int CompareNumeric(object x, object y)
            {
                var xNum = Convert.ToDouble(x);
                var yNum = Convert.ToDouble(y);
                
                // Handle NaN - NaN is greater than all other numbers including +Infinity
                var xIsNaN = double.IsNaN(xNum);
                var yIsNaN = double.IsNaN(yNum);
                
                if (xIsNaN && yIsNaN) return 0;
                if (xIsNaN) return 1; // NaN is greatest
                if (yIsNaN) return -1;
                
                // Handle infinity
                var xIsPosInf = double.IsPositiveInfinity(xNum);
                var yIsPosInf = double.IsPositiveInfinity(yNum);
                var xIsNegInf = double.IsNegativeInfinity(xNum);
                var yIsNegInf = double.IsNegativeInfinity(yNum);
                
                if (xIsNegInf && yIsNegInf) return 0;
                if (xIsNegInf) return -1;
                if (yIsNegInf) return 1;
                
                if (xIsPosInf && yIsPosInf) return 0;
                if (xIsPosInf) return 1;
                if (yIsPosInf) return -1;
                
                return xNum.CompareTo(yNum);
            }
            
            private int CompareDateTime(object x, object y)
            {
                DateTime xDt, yDt;
                
                if (x is DateTime xDateTime) xDt = xDateTime;
                else if (x is DateTimeOffset xDto) xDt = xDto.DateTime;
                else xDt = DateTime.MinValue;
                
                if (y is DateTime yDateTime) yDt = yDateTime;
                else if (y is DateTimeOffset yDto) yDt = yDto.DateTime;
                else yDt = DateTime.MinValue;
                
                return xDt.CompareTo(yDt);
            }
            
            private int CompareById(object x, object y)
            {
                var xId = GetId(x);
                var yId = GetId(y);
                return string.Compare(xId, yId, StringComparison.Ordinal);
            }
            
            private string GetId(object value)
            {
                if (value is Core.InMemoryVertex v) return v.Id;
                if (value is Core.InMemoryEdge e) return e.Id;
                if (value is Core.GremlinResponseObject gro) return gro.id?.ToString();
                return value.ToString();
            }

   private bool IsNumeric(object value)
           {
     return value is int || value is long || value is float || value is double || value is decimal;
      }
  }

      private object GetPropertyValue(Traverser traverser, string propertyName)
        {
         if (traverser.Value is Core.InMemoryVertex vertex)
   {
                // Try exact match first
   if (vertex.Properties.TryGetValue(propertyName, out var value))
  {
            return value;
        }
           
        // Try case-insensitive match
 var key = vertex.Properties.Keys.FirstOrDefault(k => 
    string.Equals(k, propertyName, StringComparison.OrdinalIgnoreCase));
    if (key != null)
          {
       return vertex.Properties[key];
         }
        }
      else if (traverser.Value is Core.InMemoryEdge edge)
   {
      // Try exact match first
    if (edge.Properties.TryGetValue(propertyName, out var value))
      {
 return value;
            }
  
     // Try case-insensitive match
       var key = edge.Properties.Keys.FirstOrDefault(k => 
        string.Equals(k, propertyName, StringComparison.OrdinalIgnoreCase));
          if (key != null)
   {
       return edge.Properties[key];
        }
  }
      else if (traverser.Value is Core.GremlinResponseObject gremlinObj)
     {
     // Handle GremlinResponseObject (used by InMemory connector)
         var props = gremlinObj.properties;
          if (props != null)
     {
    // Try to find the property (case-insensitive)
      var propKey = props.Properties().Select(p => p.Name).FirstOrDefault(k => 
 string.Equals(k, propertyName, StringComparison.OrdinalIgnoreCase));
     
  if (propKey != null)
        {
    var propValue = props[propKey];
        
     // Handle JToken
    if (propValue is Newtonsoft.Json.Linq.JToken jToken)
        {
 // Extract the actual value from JToken
          return jToken.ToObject<object>();
    }

       return propValue;
    }
       }
     }
  else if (traverser.Value is System.Collections.Generic.IDictionary<string, object> dict)
          {
    // First, check if this is a flat dictionary from valueMap (most common case)
      // Try case-insensitive property name lookup directly
      var propKey = dict.Keys.FirstOrDefault(k => 
      string.Equals(k, propertyName, StringComparison.OrdinalIgnoreCase));
  
     if (propKey != null)
 {
         var propValue = dict[propKey];
       
   // Handle property values that might be in TinkerPop format (list with value)
       if (propValue is System.Collections.IList list && list.Count > 0)
            {
     var firstItem = list[0];
       if (firstItem is System.Collections.Generic.IDictionary<string, object> itemDict && 
        itemDict.TryGetValue("value", out var val))
         {
       return val;
                }
        return firstItem;
       }
   
     return propValue;
      }
  
      // Fall back: check for nested "properties" key (TinkerPop format)
    if (dict.TryGetValue("properties", out var props) && props is System.Collections.Generic.IDictionary<string, object> propsDict)
      {
      // Try to find the property (case-insensitive)
    propKey = propsDict.Keys.FirstOrDefault(k => 
        string.Equals(k, propertyName, StringComparison.OrdinalIgnoreCase));
     
  if (propKey != null)
    {
 var propValue = propsDict[propKey];
   
           // Handle both formats: List<Dictionary> (TinkerPop) or direct value
    if (propValue is System.Collections.IList list && list.Count > 0)
     {
  var firstItem = list[0];
if (firstItem is System.Collections.Generic.IDictionary<string, object> itemDict && 
  itemDict.TryGetValue("value", out var val))
           {
        return val;
   }
          return firstItem;
 }
       
  return propValue;
             }
     }
        }
        
            return null;
      }
    }
}
