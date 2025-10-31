using System;
using System.Collections.Concurrent;
using System.Reflection;
using Stardust.Paradox.Data.Annotations;
using Stardust.Paradox.Data.Internals;
using Stardust.Particles;

namespace Stardust.Paradox.Data.Infrastructure
{
    /// <summary>
    /// Performance-optimized property transfer utility with caching optimizations.
    /// </summary>
    internal static class PropertyTransferOptimizer
    {
        // Optimized cache keys using ValueTuple instead of string concatenation
        private static readonly ConcurrentDictionary<(Type, string), PropertyInfo> _propertyInfoCache = 
            new ConcurrentDictionary<(Type, string), PropertyInfo>(
      concurrencyLevel: Environment.ProcessorCount * 2,
        capacity: 256);

    private static readonly ConcurrentDictionary<(Type, string), Action<object, object>> _setterCache = 
       new ConcurrentDictionary<(Type, string), Action<object, object>>(
       concurrencyLevel: Environment.ProcessorCount * 2,
                capacity: 256);

        private static readonly ConcurrentDictionary<(Type, string), Func<object, object>> _getterCache = 
   new ConcurrentDictionary<(Type, string), Func<object, object>>(
           concurrencyLevel: Environment.ProcessorCount * 2,
                capacity: 256);

    private static readonly ConcurrentDictionary<(Type, string), bool> _inlineSerializationCache = 
  new ConcurrentDictionary<(Type, string), bool>(
       concurrencyLevel: Environment.ProcessorCount * 2,
 capacity: 128);

        /// <summary>
        /// Gets cached PropertyInfo using optimized tuple key.
      /// </summary>
        public static PropertyInfo GetCachedPropertyInfo(Type itemType, string key)
        {
   var cacheKey = (itemType, key);
       if (!_propertyInfoCache.TryGetValue(cacheKey, out var prop))
 {
          prop = itemType.GetProperty(key,
        BindingFlags.Public | BindingFlags.FlattenHierarchy | BindingFlags.Instance);
    _propertyInfoCache.TryAdd(cacheKey, prop);
          }
            return prop;
        }

        /// <summary>
        /// Gets cached property setter using optimized tuple key.
      /// </summary>
        public static Action<object, object> GetCachedSetter(Type itemType, string key, PropertyInfo prop)
        {
 var cacheKey = (itemType, key);
            if (!_setterCache.TryGetValue(cacheKey, out var action))
     {
     action = ExpressionCompiler.CreatePropertySetter(prop);
 _setterCache.TryAdd(cacheKey, action);
         }
     return action;
        }

        /// <summary>
        /// Gets cached property getter using optimized tuple key.
        /// </summary>
     public static Func<object, object> GetCachedGetter(Type itemType, string key, PropertyInfo prop)
        {
var cacheKey = (itemType, key);
    if (!_getterCache.TryGetValue(cacheKey, out var action))
     {
    action = ExpressionCompiler.CreatePropertyGetter(prop);
 _getterCache.TryAdd(cacheKey, action);
   }
return action;
        }

        /// <summary>
     /// Checks if property has InlineSerializationAttribute (cached).
        /// </summary>
      public static bool HasInlineSerialization(Type itemType, string key, PropertyInfo prop)
   {
      var cacheKey = (itemType, key);
      if (!_inlineSerializationCache.TryGetValue(cacheKey, out var hasAttr))
            {
         hasAttr = prop?.GetCustomAttribute<InlineSerializationAttribute>() != null;
       _inlineSerializationCache.TryAdd(cacheKey, hasAttr);
     }
        return hasAttr;
        }

     /// <summary>
        /// Gets the value of a property using cached getter.
        /// </summary>
        public static object GetPropertyValue(object item, Type itemType, string key)
        {
            var prop = GetCachedPropertyInfo(itemType, key);
          if (prop == null) return null;

         var getter = GetCachedGetter(itemType, key, prop);
        return getter.Invoke(item);
        }
    }
}
