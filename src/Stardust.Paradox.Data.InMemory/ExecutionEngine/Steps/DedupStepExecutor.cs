using Stardust.Paradox.Data.Annotations.Annotations;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Stardust.Paradox.Data.InMemory.ExecutionEngine.Steps
{
    /// <summary>
    /// Executes the dedup() step which removes duplicate values while preserving order.
    /// 
    /// TinkerPop Equivalence Semantics:
    /// - Uses EQUIVALENCE (not equality) for comparison
    /// - Key differences from equality:
    ///   * Type matters: 1 (int) is NOT equivalent to 1.0 (double)
    ///   * NaN IS equivalent to NaN (opposite of equality where NaN != NaN)
    ///   * null IS equivalent to null
    /// 
    /// Behavior:
    /// - Removes duplicate traversers based on their values
    /// - Preserves the order of first occurrence
    /// - Supports by() modulator for custom equivalence keys
    /// 
    /// Example:
    /// g.V().values('name').dedup() - returns unique names in order of first occurrence
    /// g.V().order().by('age').dedup() - returns unique vertices ordered by age
    /// g.inject(1, 1.0).dedup() - returns both (type-sensitive equivalence)
    /// </summary>
    [UsedImplicitly]
    public class DedupStepExecutor : StepExecutorBase
    {
        public DedupStepExecutor(Core.InMemoryGraphDatabase database) : base(database)
        {
        }

        public override string StepName => "dedup";

        public override string StepDescription => 
            "Removes duplicate values using TinkerPop equivalence semantics. " +
            "Type-sensitive: 1 (int) != 1.0 (double). NaN == NaN for equivalence.";

 public override void Execute(TinkerGraphStep step, TinkerTraversalContext context)
        {
        // Use an order-preserving deduplication that maintains the first occurrence
            var seen = new HashSet<object>(new ValueEqualityComparer());
     var dedupedTraversers = new List<Traverser>();

            var allByArguments = context.GetMetadata<List<List<object>>>("all_by_arguments");
            var hasBy = allByArguments != null && allByArguments.Any() && allByArguments[0].Any();
            var byKey = hasBy ? allByArguments[0][0]?.ToString() : null;

        foreach (var traverser in context.Traversers)
{
       // Get the value to use as the deduplication key
  var keyValue = traverser.Value;

                if (hasBy && !string.IsNullOrEmpty(byKey))
                {
                    if (byKey.Equals("label", StringComparison.OrdinalIgnoreCase))
                    {
                        keyValue = ExtractLabel(traverser.Value) ?? "unknown";
                    }
                    else
                    {
                        var properties = ExtractProperties(traverser.Value);
                        object propVal = null;
                        if (properties != null && properties.TryGetValue(byKey, out propVal))
                        {
                            keyValue = propVal;
                        }
                        else
                        {
                            keyValue = null;
                        }
                    }
                }
    
      if (!seen.Contains(keyValue))
 {
      seen.Add(keyValue);
           dedupedTraversers.Add(traverser.Split());
    }
  // If we've seen this value before, skip it (preserving order of first occurrence)
    }

            context.Traversers.Clear();
     context.Traversers.AddRange(dedupedTraversers);

            if (hasBy)
            {
                context.RemoveMetadata("all_by_arguments");
            }
        }

        /// <summary>
        /// Custom equality comparer for deduplication using TinkerPop EQUIVALENCE semantics.
        /// 
        /// Key differences from equality:
        /// - Type-sensitive: 1 (int) != 1.0 (double)
        /// - NaN == NaN for equivalence purposes
        /// - null == null
        /// </summary>
        private class ValueEqualityComparer : IEqualityComparer<object>
        {
            public new bool Equals(object x, object y)
            {
                if (ReferenceEquals(x, y)) return true;
                if (x == null && y == null) return true;
                if (x == null || y == null) return false;
                
                // Handle NaN - NaN IS equivalent to NaN in TinkerPop equivalence
                if (IsNaN(x) && IsNaN(y)) return true;
                
                // Check for type equivalence first - different types are NOT equivalent
                // This is the key difference from equality where 1 == 1.0
                if (!AreTypesEquivalent(x, y)) return false;
                
                // Try to get ID from graph elements for comparison
                var xId = GetElementId(x);
                var yId = GetElementId(y);
                
                if (xId != null && yId != null)
                {
                    return string.Equals(xId, yId, StringComparison.Ordinal);
                }
                
                // For numeric types, compare values
                if (IsNumeric(x) && IsNumeric(y))
                {
                    return CompareNumeric(x, y);
                }
                
                // For strings, use ordinal comparison
                if (x is string xStr && y is string yStr)
                {
                    return string.Equals(xStr, yStr, StringComparison.Ordinal);
                }
                
                // For other types, use Equals
                return x.Equals(y);
            }

            public int GetHashCode(object obj)
            {
                if (obj == null) return 0;
                
                // Handle NaN consistently
                if (IsNaN(obj)) return "NaN".GetHashCode();
                
                // Try to get ID for hash code
                var id = GetElementId(obj);
                if (id != null)
                {
                    return id.GetHashCode();
                }
                
                // Include type in hash for type-sensitive equivalence
                var typeHash = obj.GetType().GetHashCode();
                var valueHash = obj.GetHashCode();
                
                return typeHash ^ valueHash;
            }
            
            private bool IsNaN(object obj)
            {
                if (obj is double d) return double.IsNaN(d);
                if (obj is float f) return float.IsNaN(f);
                return false;
            }
            
            private bool AreTypesEquivalent(object x, object y)
            {
                var xType = x.GetType();
                var yType = y.GetType();
                
                // Exact type match
                if (xType == yType) return true;
                
                // Graph elements - compare by element type
                if (x is Core.InMemoryVertex && y is Core.InMemoryVertex) return true;
                if (x is Core.InMemoryEdge && y is Core.InMemoryEdge) return true;
                if (x is Core.GremlinResponseObject && y is Core.GremlinResponseObject) return true;
                
                // For strict equivalence, don't allow numeric type promotion
                // 1 (int) is NOT equivalent to 1.0 (double)
                return false;
            }
            
            private bool IsNumeric(object obj)
            {
                return obj is int || obj is long || obj is double || 
                       obj is float || obj is decimal || obj is short ||
                       obj is byte || obj is sbyte || obj is ushort ||
                       obj is uint || obj is ulong;
            }
            
            private bool CompareNumeric(object x, object y)
            {
                // For equivalence within same type, compare values
                try
                {
                    var xVal = Convert.ToDouble(x);
                    var yVal = Convert.ToDouble(y);
                    
                    // Handle -0.0 == 0.0
                    if (xVal == 0.0 && yVal == 0.0) return true;
                    
                    return xVal == yVal;
                }
                catch
                {
                    return false;
                }
            }

  private string GetElementId(object obj)
        {
    if (obj == null) return null;
    
      try
     {
      // Check for Core types
     if (obj is Core.InMemoryVertex vertex)
       {
  return vertex.Id;
         }
        
      if (obj is Core.InMemoryEdge edge)
     {
        return edge.Id;
   }
     
           // Check for GremlinResponseObject
      if (obj is Core.GremlinResponseObject gremlinObj)
          {
    return gremlinObj.id?.ToString();
 }
        
// Try dynamic property access
                dynamic dynObj = obj;
    var id = dynObj.id;
       return id?.ToString();
          }
     catch
       {
    return null;
                }
        }
        }
    }
}
