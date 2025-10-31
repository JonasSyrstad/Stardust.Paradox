using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Stardust.Paradox.Data.Linq.Infrastructure
{
    /// <summary>
    /// Provides a centralized, thread-safe cache for reflection operations.
    /// All reflection lookups should go through this class to avoid repeated expensive reflection calls.
    /// </summary>
    internal static class ReflectionCache
    {
  // Type-level caches
        private static readonly ConcurrentDictionary<Type, PropertyInfo[]> _propertyCache = new ConcurrentDictionary<Type, PropertyInfo[]>();
        private static readonly ConcurrentDictionary<Type, MethodInfo[]> _methodCache = new ConcurrentDictionary<Type, MethodInfo[]>();
        private static readonly ConcurrentDictionary<Type, FieldInfo[]> _fieldCache = new ConcurrentDictionary<Type, FieldInfo[]>();
        private static readonly ConcurrentDictionary<Type, MemberInfo[]> _memberCache = new ConcurrentDictionary<Type, MemberInfo[]>();
 private static readonly ConcurrentDictionary<Type, Type[]> _interfaceCache = new ConcurrentDictionary<Type, Type[]>();
private static readonly ConcurrentDictionary<Type, Type[]> _genericArgumentsCache = new ConcurrentDictionary<Type, Type[]>();

      // Member-specific caches (using composite keys)
  private static readonly ConcurrentDictionary<string, PropertyInfo> _namedPropertyCache = new ConcurrentDictionary<string, PropertyInfo>();
        private static readonly ConcurrentDictionary<string, MethodInfo> _namedMethodCache = new ConcurrentDictionary<string, MethodInfo>();
        private static readonly ConcurrentDictionary<string, FieldInfo> _namedFieldCache = new ConcurrentDictionary<string, FieldInfo>();

        // Attribute caches
        private static readonly ConcurrentDictionary<MemberInfo, ConcurrentDictionary<Type, Attribute>> _attributeCache = 
    new ConcurrentDictionary<MemberInfo, ConcurrentDictionary<Type, Attribute>>();
        private static readonly ConcurrentDictionary<MemberInfo, ConcurrentDictionary<Type, Attribute[]>> _attributesCache = 
      new ConcurrentDictionary<MemberInfo, ConcurrentDictionary<Type, Attribute[]>>();

        // Type info caches
        private static readonly ConcurrentDictionary<Type, bool> _isGenericTypeCache = new ConcurrentDictionary<Type, bool>();
        private static readonly ConcurrentDictionary<Type, bool> _isValueTypeCache = new ConcurrentDictionary<Type, bool>();
        private static readonly ConcurrentDictionary<Type, bool> _isClassCache = new ConcurrentDictionary<Type, bool>();
        private static readonly ConcurrentDictionary<Type, bool> _isEnumCache = new ConcurrentDictionary<Type, bool>();
 private static readonly ConcurrentDictionary<string, bool> _isAssignableFromCache = new ConcurrentDictionary<string, bool>();

  // Method signature cache (for generic method lookups)
    private static readonly ConcurrentDictionary<string, MethodInfo> _genericMethodCache = new ConcurrentDictionary<string, MethodInfo>();

        #region Type Property/Method/Field Lookups

     /// <summary>
    /// Gets all properties for a type with caching.
        /// </summary>
        public static PropertyInfo[] GetProperties(Type type)
        {
            if (type == null) return Array.Empty<PropertyInfo>();
            return _propertyCache.GetOrAdd(type, t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance));
        }

        /// <summary>
        /// Gets all methods for a type with caching.
        /// </summary>
        public static MethodInfo[] GetMethods(Type type)
        {
        if (type == null) return Array.Empty<MethodInfo>();
         return _methodCache.GetOrAdd(type, t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance));
        }

        /// <summary>
        /// Gets all fields for a type with caching.
      /// </summary>
  public static FieldInfo[] GetFields(Type type)
        {
if (type == null) return Array.Empty<FieldInfo>();
return _fieldCache.GetOrAdd(type, t => t.GetFields(BindingFlags.Public | BindingFlags.Instance));
        }

        /// <summary>
     /// Gets all members (properties, methods, fields) for a type with caching.
        /// </summary>
   public static MemberInfo[] GetMembers(Type type)
        {
        if (type == null) return Array.Empty<MemberInfo>();
    return _memberCache.GetOrAdd(type, t => t.GetMembers(BindingFlags.Public | BindingFlags.Instance));
        }

        /// <summary>
        /// Gets a specific property by name with caching.
 /// </summary>
      public static PropertyInfo GetProperty(Type type, string propertyName)
        {
            if (type == null || string.IsNullOrEmpty(propertyName)) return null;
    
       var key = $"{type.FullName}.{propertyName}";
       return _namedPropertyCache.GetOrAdd(key, _ => type.GetProperty(propertyName));
        }

    /// <summary>
        /// Gets a specific method by name with caching (first overload).
   /// </summary>
        public static MethodInfo GetMethod(Type type, string methodName)
        {
            if (type == null || string.IsNullOrEmpty(methodName)) return null;
         
          var key = $"{type.FullName}.{methodName}";
            return _namedMethodCache.GetOrAdd(key, _ => type.GetMethod(methodName));
        }

        /// <summary>
        /// Gets a specific method by name with parameter types for overload resolution.
        /// </summary>
 public static MethodInfo GetMethod(Type type, string methodName, Type[] parameterTypes)
     {
            if (type == null || string.IsNullOrEmpty(methodName)) return null;
          
  var key = $"{type.FullName}.{methodName}({string.Join(",", parameterTypes?.Select(t => t.FullName) ?? Array.Empty<string>())})";
      return _namedMethodCache.GetOrAdd(key, _ => type.GetMethod(methodName, parameterTypes ?? Type.EmptyTypes));
     }

     /// <summary>
   /// Gets a specific field by name with caching.
        /// </summary>
        public static FieldInfo GetField(Type type, string fieldName)
        {
       if (type == null || string.IsNullOrEmpty(fieldName)) return null;
    
          var key = $"{type.FullName}.{fieldName}";
 return _namedFieldCache.GetOrAdd(key, _ => type.GetField(fieldName));
     }

        #endregion

     #region Attribute Lookups

    /// <summary>
        /// Gets a custom attribute from a member with caching.
        /// </summary>
  public static T GetCustomAttribute<T>(MemberInfo member) where T : Attribute
   {
            if (member == null) return null;

            var memberAttributes = _attributeCache.GetOrAdd(member, _ => new ConcurrentDictionary<Type, Attribute>());
      return (T)memberAttributes.GetOrAdd(typeof(T), _ => member.GetCustomAttribute<T>());
        }

        /// <summary>
        /// Gets all custom attributes of a specific type from a member with caching.
        /// </summary>
   public static T[] GetCustomAttributes<T>(MemberInfo member) where T : Attribute
        {
    if (member == null) return Array.Empty<T>();

            var memberAttributes = _attributesCache.GetOrAdd(member, _ => new ConcurrentDictionary<Type, Attribute[]>());
       return (T[])memberAttributes.GetOrAdd(typeof(T), _ => member.GetCustomAttributes<T>().ToArray());
        }

        /// <summary>
/// Checks if a member has a specific attribute with caching.
        /// </summary>
        public static bool HasCustomAttribute<T>(MemberInfo member) where T : Attribute
        {
          return GetCustomAttribute<T>(member) != null;
        }

        #endregion

        #region Type Information

        /// <summary>
        /// Gets all interfaces implemented by a type with caching.
        /// </summary>
        public static Type[] GetInterfaces(Type type)
        {
            if (type == null) return Array.Empty<Type>();
         return _interfaceCache.GetOrAdd(type, t => t.GetInterfaces());
        }

        /// <summary>
        /// Gets generic type arguments with caching.
        /// </summary>
        public static Type[] GetGenericArguments(Type type)
        {
         if (type == null) return Array.Empty<Type>();
     return _genericArgumentsCache.GetOrAdd(type, t => t.GetGenericArguments());
        }

        /// <summary>
        /// Checks if a type is generic with caching.
        /// </summary>
        public static bool IsGenericType(Type type)
        {
            if (type == null) return false;
  return _isGenericTypeCache.GetOrAdd(type, t => t.IsGenericType);
        }

        /// <summary>
        /// Checks if a type is a value type with caching.
    /// </summary>
        public static bool IsValueType(Type type)
        {
          if (type == null) return false;
    return _isValueTypeCache.GetOrAdd(type, t => t.IsValueType);
        }

        /// <summary>
        /// Checks if a type is a class with caching.
   /// </summary>
        public static bool IsClass(Type type)
        {
            if (type == null) return false;
return _isClassCache.GetOrAdd(type, t => t.IsClass);
        }

        /// <summary>
        /// Checks if a type is an enum with caching.
        /// </summary>
        public static bool IsEnum(Type type)
        {
            if (type == null) return false;
   return _isEnumCache.GetOrAdd(type, t => t.IsEnum);
        }

   /// <summary>
/// Checks if a type is assignable from another type with caching.
        /// </summary>
        public static bool IsAssignableFrom(Type baseType, Type derivedType)
 {
            if (baseType == null || derivedType == null) return false;
            
   var key = $"{baseType.FullName}<-{derivedType.FullName}";
            return _isAssignableFromCache.GetOrAdd(key, _ => baseType.IsAssignableFrom(derivedType));
     }

        #endregion

   #region Generic Method Lookups

   /// <summary>
  /// Gets a generic method and makes it with specific type arguments, with caching.
        /// </summary>
  public static MethodInfo GetGenericMethod(Type type, string methodName, Type[] typeArguments, Type[] parameterTypes = null)
        {
      if (type == null || string.IsNullOrEmpty(methodName) || typeArguments == null || typeArguments.Length == 0)
           return null;

          var key = $"{type.FullName}.{methodName}<{string.Join(",", typeArguments.Select(t => t.FullName))}>" +
              $"({string.Join(",", parameterTypes?.Select(t => t.FullName) ?? Array.Empty<string>())})";

       return _genericMethodCache.GetOrAdd(key, _ =>
  {
   var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);
        var genericMethod = methods.FirstOrDefault(m =>
           {
          if (m.Name != methodName || !m.IsGenericMethodDefinition)
      return false;

           if (m.GetGenericArguments().Length != typeArguments.Length)
            return false;

             if (parameterTypes != null)
       {
  var methodParams = m.GetParameters();
       if (methodParams.Length != parameterTypes.Length)
     return false;
        }

      return true;
   });

         return genericMethod?.MakeGenericMethod(typeArguments);
            });
        }

        #endregion

  #region Cache Management

  /// <summary>
  /// Clears all caches. Use with caution - mainly for testing.
        /// </summary>
        public static void ClearAllCaches()
        {
            _propertyCache.Clear();
    _methodCache.Clear();
      _fieldCache.Clear();
            _memberCache.Clear();
  _interfaceCache.Clear();
_genericArgumentsCache.Clear();
    _namedPropertyCache.Clear();
     _namedMethodCache.Clear();
         _namedFieldCache.Clear();
 _attributeCache.Clear();
   _attributesCache.Clear();
    _isGenericTypeCache.Clear();
            _isValueTypeCache.Clear();
     _isClassCache.Clear();
          _isEnumCache.Clear();
     _isAssignableFromCache.Clear();
         _genericMethodCache.Clear();
  }

        /// <summary>
        /// Gets cache statistics for monitoring/debugging.
        /// </summary>
        public static Dictionary<string, int> GetCacheStatistics()
        {
            return new Dictionary<string, int>
   {
      { "PropertyCache", _propertyCache.Count },
    { "MethodCache", _methodCache.Count },
            { "FieldCache", _fieldCache.Count },
         { "MemberCache", _memberCache.Count },
     { "InterfaceCache", _interfaceCache.Count },
        { "GenericArgumentsCache", _genericArgumentsCache.Count },
                { "NamedPropertyCache", _namedPropertyCache.Count },
          { "NamedMethodCache", _namedMethodCache.Count },
     { "NamedFieldCache", _namedFieldCache.Count },
                { "AttributeCache", _attributeCache.Count },
       { "AttributesCache", _attributesCache.Count },
                { "GenericMethodCache", _genericMethodCache.Count },
       { "IsGenericTypeCache", _isGenericTypeCache.Count },
         { "IsValueTypeCache", _isValueTypeCache.Count },
{ "IsClassCache", _isClassCache.Count },
 { "IsEnumCache", _isEnumCache.Count },
       { "IsAssignableFromCache", _isAssignableFromCache.Count }
            };
        }

        #endregion
    }
}
