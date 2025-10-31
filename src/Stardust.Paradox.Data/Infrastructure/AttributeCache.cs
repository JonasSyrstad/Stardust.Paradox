using System;
using System.Collections.Concurrent;
using System.Reflection;
using Stardust.Paradox.Data.Annotations;

namespace Stardust.Paradox.Data.Infrastructure
{
  /// <summary>
    /// High-performance cache for attribute lookups to avoid repeated reflection calls.
  /// </summary>
 internal static class AttributeCache
    {
  private static readonly ConcurrentDictionary<(Type, MemberInfo), EagerAttribute> _eagerAttributeCache =
     new ConcurrentDictionary<(Type, MemberInfo), EagerAttribute>(
         concurrencyLevel: Environment.ProcessorCount * 2,
        capacity: 128);

    private static readonly ConcurrentDictionary<(Type, MemberInfo), InlineSerializationAttribute> _inlineSerializationCache =
          new ConcurrentDictionary<(Type, MemberInfo), InlineSerializationAttribute>(
      concurrencyLevel: Environment.ProcessorCount * 2,
     capacity: 128);

        private static readonly ConcurrentDictionary<(Type, MemberInfo), EdgeLabelAttribute> _edgeLabelCache =
  new ConcurrentDictionary<(Type, MemberInfo), EdgeLabelAttribute>(
   concurrencyLevel: Environment.ProcessorCount * 2,
            capacity: 128);

        private static readonly ConcurrentDictionary<(Type, MemberInfo), ReverseEdgeLabelAttribute> _reverseEdgeLabelCache =
            new ConcurrentDictionary<(Type, MemberInfo), ReverseEdgeLabelAttribute>(
                concurrencyLevel: Environment.ProcessorCount * 2,
     capacity: 128);

   private static readonly ConcurrentDictionary<(Type, MemberInfo), GremlinQueryAttribute> _gremlinQueryCache =
     new ConcurrentDictionary<(Type, MemberInfo), GremlinQueryAttribute>(
      concurrencyLevel: Environment.ProcessorCount * 2,
         capacity: 128);

        private static readonly ConcurrentDictionary<(Type, MemberInfo), ToWayEdgeLabelAttribute> _toWayEdgeLabelCache =
        new ConcurrentDictionary<(Type, MemberInfo), ToWayEdgeLabelAttribute>(
              concurrencyLevel: Environment.ProcessorCount * 2,
             capacity: 128);

 /// <summary>
        /// Gets the EagerAttribute for the specified entity type and member (cached).
 /// </summary>
        public static EagerAttribute GetEagerAttribute(Type entityType, MemberInfo member)
        {
            return _eagerAttributeCache.GetOrAdd((entityType, member), 
   key => key.Item2.GetCustomAttribute<EagerAttribute>());
        }

/// <summary>
        /// Gets the InlineSerializationAttribute for the specified entity type and member (cached).
   /// </summary>
    public static InlineSerializationAttribute GetInlineSerializationAttribute(Type entityType, MemberInfo member)
        {
          return _inlineSerializationCache.GetOrAdd((entityType, member), 
         key => key.Item2.GetCustomAttribute<InlineSerializationAttribute>());
        }

        /// <summary>
   /// Gets the EdgeLabelAttribute for the specified entity type and member (cached).
        /// </summary>
        public static EdgeLabelAttribute GetEdgeLabelAttribute(Type entityType, MemberInfo member)
        {
  return _edgeLabelCache.GetOrAdd((entityType, member), 
        key => key.Item2.GetCustomAttribute<EdgeLabelAttribute>());
   }

        /// <summary>
        /// Gets the ReverseEdgeLabelAttribute for the specified entity type and member (cached).
        /// </summary>
        public static ReverseEdgeLabelAttribute GetReverseEdgeLabelAttribute(Type entityType, MemberInfo member)
      {
       return _reverseEdgeLabelCache.GetOrAdd((entityType, member), 
     key => key.Item2.GetCustomAttribute<ReverseEdgeLabelAttribute>());
     }

        /// <summary>
        /// Gets the GremlinQueryAttribute for the specified entity type and member (cached).
        /// </summary>
        public static GremlinQueryAttribute GetGremlinQueryAttribute(Type entityType, MemberInfo member)
        {
  return _gremlinQueryCache.GetOrAdd((entityType, member), 
 key => key.Item2.GetCustomAttribute<GremlinQueryAttribute>());
        }

        /// <summary>
      /// Gets the ToWayEdgeLabelAttribute for the specified entity type and member (cached).
        /// </summary>
        public static ToWayEdgeLabelAttribute GetToWayEdgeLabelAttribute(Type entityType, MemberInfo member)
        {
            return _toWayEdgeLabelCache.GetOrAdd((entityType, member), 
       key => key.Item2.GetCustomAttribute<ToWayEdgeLabelAttribute>());
        }

        /// <summary>
    /// Clears all caches (useful for testing or when reloading entities).
        /// </summary>
        public static void ClearAll()
        {
    _eagerAttributeCache.Clear();
         _inlineSerializationCache.Clear();
            _edgeLabelCache.Clear();
       _reverseEdgeLabelCache.Clear();
      _gremlinQueryCache.Clear();
        _toWayEdgeLabelCache.Clear();
        }
    }
}
