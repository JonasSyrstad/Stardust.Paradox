using System;
using System.Collections.Concurrent;
using System.Reflection;
using Stardust.Paradox.Data.Annotations;
using Stardust.Paradox.Data.Linq.Infrastructure;

namespace Stardust.Paradox.Data.Linq
{
    /// <summary>
    /// Centralizes edge label resolution logic for LINQ translation with optimized tuple-based caching.
    /// Checks attributes to determine edge labels.
    /// </summary>
    internal static class EdgeLabelResolver
    {
        // Optimized caches using ValueTuple keys (zero allocation)
        private static readonly ConcurrentDictionary<(Type, string), string> _inEdgeLabelCache = 
   new ConcurrentDictionary<(Type, string), string>(
        concurrencyLevel: Environment.ProcessorCount * 2,
  capacity: 512);
         
    private static readonly ConcurrentDictionary<(Type, string), string> _outEdgeLabelCache = 
         new ConcurrentDictionary<(Type, string), string>(
     concurrencyLevel: Environment.ProcessorCount * 2,
      capacity: 512);
                
 private static readonly ConcurrentDictionary<(Type, string), string> _anyEdgeLabelCache = 
            new ConcurrentDictionary<(Type, string), string>(
              concurrencyLevel: Environment.ProcessorCount * 2,
capacity: 512);

        /// <summary>
        /// Creates a tuple cache key from type and member (zero allocation).
  /// </summary>
        private static (Type, string) GetCacheKey(Type entityType, MemberInfo member)
      {
            return (entityType, member?.Name);
    }

 /// <summary>
    /// Gets the edge label for an In-edge (incoming edge) from attributes.
        /// This is used when traversing from the current vertex to vertices connected via incoming edges.
    /// </summary>
        /// <param name="entityType">The entity type that contains the navigation property</param>
  /// <param name="member">The navigation property member</param>
        /// <returns>The edge label if found, otherwise null</returns>
 public static string GetInEdgeLabel(Type entityType, MemberInfo member)
    {
            if (entityType == null || member == null)
     return null;

    var cacheKey = GetCacheKey(entityType, member);
            return _inEdgeLabelCache.GetOrAdd(cacheKey, _ =>
          {
      // Check attributes using cached reflection
     return ReflectionCache.GetCustomAttribute<EdgeLabelAttribute>(member)?.Label ??
               ReflectionCache.GetCustomAttribute<InLabelAttribute>(member)?.Label;
    });
        }

        /// <summary>
        /// Gets the reverse edge label for an Out-edge (outgoing edge) from attributes.
        /// This is used when traversing from the current vertex to vertices connected via outgoing edges.
 /// </summary>
        /// <param name="entityType">The entity type that contains the navigation property</param>
        /// <param name="member">The navigation property member</param>
        /// <returns>The reverse edge label if found, otherwise null</returns>
        public static string GetOutEdgeLabel(Type entityType, MemberInfo member)
      {
       if (entityType == null || member == null)
            return null;

  var cacheKey = GetCacheKey(entityType, member);
     return _outEdgeLabelCache.GetOrAdd(cacheKey, _ =>
       {
     // Check attributes using cached reflection
         return ReflectionCache.GetCustomAttribute<ReverseEdgeLabelAttribute>(member)?.ReverseLabel ??
       ReflectionCache.GetCustomAttribute<OutLabelAttribute>(member)?.ReverseLabel;
      });
        }

        /// <summary>
        /// Gets any edge label (In or Out) for a navigation property.
        /// This method checks both directions and returns the first label found.
        /// Useful when the direction is unknown or either direction is acceptable.
        /// </summary>
        /// <param name="entityType">The entity type that contains the navigation property</param>
        /// <param name="member">The navigation property member</param>
 /// <returns>The edge label if found (In or Out), otherwise null</returns>
   public static string GetAnyEdgeLabel(Type entityType, MemberInfo member)
        {
            if (entityType == null || member == null)
     return null;

 var cacheKey = GetCacheKey(entityType, member);
            return _anyEdgeLabelCache.GetOrAdd(cacheKey, _ =>
            {
   // Try In-edge label first
         var inLabel = GetInEdgeLabel(entityType, member);
   if (!string.IsNullOrEmpty(inLabel))
 return inLabel;

    // Try Out-edge label
       var outLabel = GetOutEdgeLabel(entityType, member);
       if (!string.IsNullOrEmpty(outLabel))
 return outLabel;

        return null;
            });
        }

   /// <summary>
        /// Checks if a member has any edge label defined (attribute).
      /// </summary>
     /// <param name="entityType">The entity type that contains the navigation property</param>
        /// <param name="member">The navigation property member</param>
        /// <returns>True if any edge label is defined, false otherwise</returns>
     public static bool HasEdgeLabel(Type entityType, MemberInfo member)
    {
  return !string.IsNullOrEmpty(GetAnyEdgeLabel(entityType, member));
        }

     /// <summary>
        /// Clears all cached edge labels. Use with caution - mainly for testing.
        /// </summary>
    public static void ClearCache()
  {
     _inEdgeLabelCache.Clear();
            _outEdgeLabelCache.Clear();
     _anyEdgeLabelCache.Clear();
        }
    }
}
