using Stardust.Paradox.Data.Annotations.Annotations;
using System.Collections.Generic;
using System.Linq;

namespace Stardust.Paradox.Data.InMemory.ExecutionEngine.Steps
{
    /// <summary>
    /// Executes the dedup() step which removes duplicate values while preserving order.
 /// 
  /// Behavior:
    /// - Removes duplicate traversers based on their values
    /// - Preserves the order of first occurrence
    /// - Compatible with TinkerPop semantics
    /// 
    /// Example:
    /// g.V().values('name').dedup() - returns unique names in order of first occurrence
    /// g.V().order().by('age').dedup() - returns unique vertices ordered by age
    /// </summary>
    [UsedImplicitly]
    public class DedupStepExecutor : StepExecutorBase
    {
     public DedupStepExecutor(Core.InMemoryGraphDatabase database) : base(database)
{
        }

        public override string StepName => "dedup";

        public override string StepDescription => 
            "Removes duplicate values from the traversal stream while preserving order. " +
  "Keeps only unique values based on their representation, maintaining the order of first occurrence.";

 public override void Execute(TinkerGraphStep step, TinkerTraversalContext context)
        {
        // Use an order-preserving deduplication that maintains the first occurrence
            var seen = new HashSet<object>(new ValueEqualityComparer());
     var dedupedTraversers = new List<Traverser>();

        foreach (var traverser in context.Traversers)
{
       // Get the value to use as the deduplication key
  var keyValue = traverser.Value;
    
      if (!seen.Contains(keyValue))
 {
      seen.Add(keyValue);
           dedupedTraversers.Add(traverser.Split());
    }
  // If we've seen this value before, skip it (preserving order of first occurrence)
    }

            context.Traversers.Clear();
     context.Traversers.AddRange(dedupedTraversers);
        }

        /// <summary>
        /// Custom equality comparer for deduplication that handles various object types
        /// </summary>
  private class ValueEqualityComparer : IEqualityComparer<object>
        {
        public new bool Equals(object x, object y)
  {
    if (ReferenceEquals(x, y)) return true;
        if (x == null || y == null) return false;
             
                // Try to get ID from graph elements for comparison
           var xId = GetElementId(x);
     var yId = GetElementId(y);
     
         if (xId != null && yId != null)
            {
            return string.Equals(xId, yId, System.StringComparison.Ordinal);
          }
       
    // For non-graph elements, compare string representations
       var xStr = x.ToString();
       var yStr = y.ToString();
      
         return string.Equals(xStr, yStr, System.StringComparison.Ordinal);
      }

public int GetHashCode(object obj)
     {
    if (obj == null) return 0;
                
   // Try to get ID for hash code
        var id = GetElementId(obj);
           if (id != null)
    {
      return id.GetHashCode();
        }
                
 return obj.ToString()?.GetHashCode() ?? 0;
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
