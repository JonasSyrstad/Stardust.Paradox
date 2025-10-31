using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;

namespace Stardust.Paradox.Data.Linq
{
    /// <summary>
    /// Evaluates complex projection expressions client-side for LINQ to Gremlin
    /// Handles conditional expressions, calculations, string manipulations, and nested types
    /// </summary>
    internal class ProjectionExpressionEvaluator
    {
        // Cache for generated proxy types to avoid recreating them
private static readonly ConcurrentDictionary<Type, Type> _proxyTypeCache = new ConcurrentDictionary<Type, Type>();

        /// <summary>
        /// Checks if a Select projection requires client-side evaluation
   /// </summary>
        public static bool RequiresClientSideEvaluation(LambdaExpression projection)
    {
   if (projection == null)
 return false;

      var body = projection.Body;

        // Simple member access (e.g., p => p.Name) - can be done server-side
     if (body is MemberExpression)
    return false;

   // Check for edge traversal patterns: p.OutE(...).As<T>() or p.InE(...).As<T>()
    // These should be translated to Gremlin server-side
     if (IsEdgeTraversalWithCast(body))
         return false;

// Anonymous type with only member expressions - can be done server-side
        if (body is NewExpression newExpr)
{
   // Check if all arguments are simple member expressions or edge traversals
return newExpr.Arguments.Any(arg => !IsSimpleMemberAccess(arg) && !IsEdgeTraversalWithCast(arg));
 }

 // Everything else requires client-side evaluation
   return true;
      }

        private static bool IsSimpleMemberAccess(Expression expr)
     {
            return expr is MemberExpression memberExpr &&
 memberExpr.Expression is ParameterExpression;
      }

        /// <summary>
        /// Checks if expression is an edge traversal pattern: p.OutE(...).Cast<T>() or p.InE(...).Cast<T>()
     /// </summary>
        private static bool IsEdgeTraversalWithCast(Expression expr)
        {
            // Pattern: p.OutE(...).Cast<T>() or p.InE(...).Cast<T>()
     if (expr is MethodCallExpression methodCall)
            {
   // Check if it's the Cast<T>() method
         if (methodCall.Method.Name == "Cast" && methodCall.Method.IsGenericMethod)
  {
            // Check if the object is an OutE or InE call result
   if (methodCall.Object != null)
           {
              // The object is the IEdgeTraversal returned by OutE/InE
// We need to check the arguments[0] to see if it's a MethodCallExpression for OutE/InE
 return true; // Any Cast call on an IEdgeTraversal is server-side
     }
       }
       // Also check for OutE/InE without Cast<T/>
                else if (methodCall.Method.Name == "OutE" || methodCall.Method.Name == "InE")
       {
// These methods return IEdgeTraversal which should be handled server-side
         return true;
        }
  }

            return false;
      }

        /// <summary>
        /// Creates a client-side projection function from a lambda expression
      /// </summary>
    public static Func<TSource, TResult> CreateProjectionFunction<TSource, TResult>(LambdaExpression projection)
     {
            // Compile the lambda expression to a delegate
     return (Func<TSource, TResult>)projection.Compile();
        }

   /// <summary>
/// Extracts property names needed for server-side data retrieval
        /// </summary>
        public static HashSet<string> ExtractRequiredProperties(LambdaExpression projection)
        {
        var properties = new HashSet<string>();
    ExtractPropertiesFromExpression(projection.Body, properties);
            return properties;
        }

        private static void ExtractPropertiesFromExpression(Expression expression, HashSet<string> properties)
        {
      switch (expression)
      {
       case MemberExpression memberExpr when memberExpr.Expression is ParameterExpression:
      // Direct property access like p.Name
             properties.Add(memberExpr.Member.Name);
         break;

   case NewExpression newExpr:
        // Anonymous type creation
    foreach (var arg in newExpr.Arguments)
 {
     ExtractPropertiesFromExpression(arg, properties);
         }
   break;

     case BinaryExpression binaryExpr:
          // Binary operations (arithmetic, comparisons, etc.)
         ExtractPropertiesFromExpression(binaryExpr.Left, properties);
         ExtractPropertiesFromExpression(binaryExpr.Right, properties);
  break;

      case ConditionalExpression conditionalExpr:
     // Ternary operator
        ExtractPropertiesFromExpression(conditionalExpr.Test, properties);
       ExtractPropertiesFromExpression(conditionalExpr.IfTrue, properties);
    ExtractPropertiesFromExpression(conditionalExpr.IfFalse, properties);
   break;

                case UnaryExpression unaryExpr:
  // Unary operations (negation, conversion, etc.)
    ExtractPropertiesFromExpression(unaryExpr.Operand, properties);
     break;

      case MethodCallExpression methodCallExpr:
                    // Method calls (string operations, etc.)
        if (methodCallExpr.Object != null)
           {
     ExtractPropertiesFromExpression(methodCallExpr.Object, properties);
     }
   foreach (var arg in methodCallExpr.Arguments)
   {
          ExtractPropertiesFromExpression(arg, properties);
           }
     break;

      case MemberExpression memberExpr:
             // Nested member access
   ExtractPropertiesFromExpression(memberExpr.Expression, properties);
        break;
            }
        }

      /// <summary>
        /// Gets the Gremlin query part for retrieving required properties
        /// </summary>
   public static string GetPropertyRetrievalQuery(HashSet<string> properties)
        {
            // Always use elementMap() to get all properties including id, label, etc.
       // This is required for proper entity reconstruction
            return ".elementMap()";
 }

        private static string ToCamelCase(string name)
      {
   if (string.IsNullOrEmpty(name) || name.Length == 0)
          return name;
            return char.ToLowerInvariant(name[0]) + name.Substring(1);
        }

        /// <summary>
 /// Applies a client-side projection to server-retrieved data
        /// </summary>
        public static object ApplyProjectionToServerData<TSource, TResult>(
 IEnumerable serverData,
            LambdaExpression projection,
       Type sourceType)
        {
   if (serverData == null)
return new List<TResult>();

    // Check if this is a SelectMany operation (TResult is IEnumerable)
  var resultIsEnumerable = typeof(System.Collections.IEnumerable).IsAssignableFrom(typeof(TResult)) &&
       typeof(TResult) != typeof(string);

     // For SelectMany, we need to extract the element type from the IEnumerable<T>
     Type flattenedElementType = null;
       if (resultIsEnumerable && typeof(TResult).IsGenericType)
 {
        var genericArgs = typeof(TResult).GetGenericArguments();
      if (genericArgs.Length > 0)
     {
            flattenedElementType = genericArgs[0];
   }
   }

    // Use object as the collection type for flattening, we'll cast later
       var projectedList = resultIsEnumerable && flattenedElementType != null
          ? (System.Collections.IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(flattenedElementType))
      : new List<TResult>();
    
       var compiledProjection = (Func<TSource, TResult>)projection.Compile();

      foreach (var serverItem in serverData)
      {
        // Convert server item (dictionary from elementMap) to source entity
    var entity = ConvertDictionaryToEntity<TSource>(serverItem, sourceType);

        // Apply the projection
   var projectedItem = compiledProjection(entity);
    
   if (resultIsEnumerable && projectedItem != null)
      {
 // For SelectMany, flatten the collection
      if (projectedItem is System.Collections.IEnumerable enumerable)
       {
            foreach (var item in enumerable)
       {
    projectedList.Add(item);
      }
       }
     }
  else
  {
           projectedList.Add(projectedItem);
           }
   }

 return projectedList;
        }

        /// <summary>
        /// Converts a dictionary from elementMap() to an entity instance
      /// </summary>
        private static T ConvertDictionaryToEntity<T>(object serverItem, Type entityType)
        {
 // If it's a dictionary (from elementMap/valueMap), create entity from it
            if (serverItem is Dictionary<string, object> dict)
            {
                // For interfaces, create a dynamic proxy
      if (entityType.IsInterface)
     {
      return CreateDynamicProxy<T>(dict, entityType);
      }

      // Try to find a parameterless constructor for classes
          var constructor = entityType.GetConstructors()
          .FirstOrDefault(c => c.GetParameters().Length == 0);

   if (constructor != null)
        {
           var instance = (T)constructor.Invoke(null);

            // Set properties using reflection
        foreach (var prop in entityType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
         {
             if (!prop.CanWrite)
       continue;

    var propName = ToCamelCase(prop.Name);
         if (dict.ContainsKey(propName))
       {
       var value = dict[propName];

        // Handle single-element arrays from Gremlin
           if (value is IList list && list.Count > 0 && !prop.PropertyType.IsArray)
   {
        value = list[0];
         }

          var convertedValue = ConvertValue(value, prop.PropertyType);
           prop.SetValue(instance, convertedValue);
       }
     }

         return instance;
       }
            }

     // Fallback: throw with helpful message
            throw new InvalidOperationException(
         $"Cannot convert server item of type {serverItem?.GetType().Name ?? "null"} to entity type {entityType.Name}. " +
$"Server item must be a Dictionary<string, object> from elementMap(), and entity type must have a parameterless constructor or be an interface.");
        }

        /// <summary>
  /// Creates a dynamic proxy for an interface type using Reflection.Emit
        /// </summary>
        private static T CreateDynamicProxy<T>(Dictionary<string, object> dict, Type interfaceType)
   {
        // Get or create the proxy type for this interface
          var proxyType = _proxyTypeCache.GetOrAdd(interfaceType, type => GenerateProxyType(type));

         // Create an instance of the proxy
  var instance = Activator.CreateInstance(proxyType);

            // Set property values from the dictionary
            foreach (var prop in GetAllProperties(interfaceType))
            {
       if (!prop.CanWrite)
              continue;

            var propName = ToCamelCase(prop.Name);
    if (dict.ContainsKey(propName))
  {
             var value = dict[propName];

      // Handle single-element arrays from Gremlin
if (value is IList list && list.Count > 0 && !prop.PropertyType.IsArray)
       {
     value = list[0];
     }

          var convertedValue = ConvertValue(value, prop.PropertyType);
     prop.SetValue(instance, convertedValue);
    }
            }

         return (T)instance;
        }

        /// <summary>
        /// Gets all properties from an interface and all its base interfaces
/// </summary>
   private static IEnumerable<PropertyInfo> GetAllProperties(Type interfaceType)
        {
      var properties = new HashSet<PropertyInfo>();

       // Get properties from the interface itself
 foreach (var prop in interfaceType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
   {
       properties.Add(prop);
 }

   // Get properties from all base interfaces
   foreach (var baseInterface in interfaceType.GetInterfaces())
            {
        foreach (var prop in baseInterface.GetProperties(BindingFlags.Public | BindingFlags.Instance))
       {
         properties.Add(prop);
    }
     }

            return properties;
        }

        /// <summary>
        /// Generates a dynamic proxy type that implements the specified interface and all its base interfaces
    /// </summary>
        private static Type GenerateProxyType(Type interfaceType)
        {
#if NETSTANDARD2_0 || NET6_0_OR_GREATER
         var assemblyName = new AssemblyName($"DynamicProxies_{Guid.NewGuid():N}");
  var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(
 assemblyName,
        AssemblyBuilderAccess.Run);
      var moduleBuilder = assemblyBuilder.DefineDynamicModule("ProxyModule");

      // Get all interfaces (including the target and all base interfaces)
 var allInterfaces = new List<Type> { interfaceType };
      allInterfaces.AddRange(interfaceType.GetInterfaces());

    var typeBuilder = moduleBuilder.DefineType(
$"{interfaceType.Name}Proxy_{Guid.NewGuid():N}",
TypeAttributes.Public | TypeAttributes.Class,
        null,
      allInterfaces.ToArray());

         // Get all unique properties from all interfaces
          var allProperties = new Dictionary<string, PropertyInfo>();
   foreach (var iface in allInterfaces)
     {
    foreach (var prop in iface.GetProperties(BindingFlags.Public | BindingFlags.Instance))
      {
       // Use property name as key to avoid duplicates
  if (!allProperties.ContainsKey(prop.Name))
     {
       allProperties[prop.Name] = prop;
      }
       }
 }

     // Get all unique events from all interfaces
      var allEvents = new Dictionary<string, EventInfo>();
    foreach (var iface in allInterfaces)
   {
    foreach (var evt in iface.GetEvents(BindingFlags.Public | BindingFlags.Instance))
       {
    if (!allEvents.ContainsKey(evt.Name))
  {
       allEvents[evt.Name] = evt;
     }
         }
        }

    // Get all unique methods from all interfaces (excluding property getters/setters and event add/remove)
  var allMethods = new Dictionary<string, MethodInfo>();
    foreach (var iface in allInterfaces)
    {
 foreach (var method in iface.GetMethods(BindingFlags.Public | BindingFlags.Instance))
 {
            // Skip special methods (property getters/setters, event add/remove)
            if (method.IsSpecialName)
       continue;

            // Use method signature as key to avoid duplicates
         var methodKey = $"{method.Name}_{string.Join("_", method.GetParameters().Select(p => p.ParameterType.Name))}";
    if (!allMethods.ContainsKey(methodKey))
            {
                allMethods[methodKey] = method;
            }
        }
    }

      // Implement each unique property with a backing field
      foreach (var prop in allProperties.Values)
  {
          // Define backing field
    var fieldBuilder = typeBuilder.DefineField(
     $"_{prop.Name}",
 prop.PropertyType,
        FieldAttributes.Private);

 // Define property
     var propertyBuilder = typeBuilder.DefineProperty(
  prop.Name,
          PropertyAttributes.HasDefault,
  prop.PropertyType,
         null);

       // Implement getter if the property has one
     if (prop.GetMethod != null)
  {
            var getterBuilder = typeBuilder.DefineMethod(
            $"get_{prop.Name}",
MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.SpecialName | MethodAttributes.HideBySig | MethodAttributes.NewSlot | MethodAttributes.Final,
  prop.PropertyType,
      Type.EmptyTypes);

 var getterIL = getterBuilder.GetILGenerator();
    getterIL.Emit(OpCodes.Ldarg_0);
         getterIL.Emit(OpCodes.Ldfld, fieldBuilder);
        getterIL.Emit(OpCodes.Ret);

     propertyBuilder.SetGetMethod(getterBuilder);
    }

    // Implement setter if the property has one
    if (prop.SetMethod != null)
       {
       var setterBuilder = typeBuilder.DefineMethod(
       $"set_{prop.Name}",
      MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.SpecialName | MethodAttributes.HideBySig | MethodAttributes.NewSlot | MethodAttributes.Final,
       null,
  new[] { prop.PropertyType });

            var setterIL = setterBuilder.GetILGenerator();
        setterIL.Emit(OpCodes.Ldarg_0);
          setterIL.Emit(OpCodes.Ldarg_1);
        setterIL.Emit(OpCodes.Stfld, fieldBuilder);
      setterIL.Emit(OpCodes.Ret);

    propertyBuilder.SetSetMethod(setterBuilder);
   }
 }

       // Implement each unique event with a backing field
      foreach (var evt in allEvents.Values)
     {
    // Define backing field for the event delegate
    var eventFieldBuilder = typeBuilder.DefineField(
         $"_{evt.Name}",
evt.EventHandlerType,
FieldAttributes.Private);

         // Define the event
    var eventBuilder = typeBuilder.DefineEvent(
        evt.Name,
    EventAttributes.None,
         evt.EventHandlerType);

  // Implement add method
     if (evt.AddMethod != null)
    {
            var addBuilder = typeBuilder.DefineMethod(
   $"add_{evt.Name}",
  MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.SpecialName | MethodAttributes.HideBySig | MethodAttributes.NewSlot | MethodAttributes.Final,
   null,
  new[] { evt.EventHandlerType });

      var addIL = addBuilder.GetILGenerator();
   // Implement as: _field = (HandlerType)Delegate.Combine(_field, value);
   addIL.Emit(OpCodes.Ldarg_0);
           addIL.Emit(OpCodes.Ldarg_0);
     addIL.Emit(OpCodes.Ldfld, eventFieldBuilder);
     addIL.Emit(OpCodes.Ldarg_1);
       addIL.Emit(OpCodes.Call, typeof(Delegate).GetMethod("Combine", new[] { typeof(Delegate), typeof(Delegate) }));
  addIL.Emit(OpCodes.Castclass, evt.EventHandlerType);
   addIL.Emit(OpCodes.Stfld, eventFieldBuilder);
    addIL.Emit(OpCodes.Ret);

         eventBuilder.SetAddOnMethod(addBuilder);
      }

         // Implement remove method
        if (evt.RemoveMethod != null)
      {
      var removeBuilder = typeBuilder.DefineMethod(
         $"remove_{evt.Name}",
         MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.SpecialName | MethodAttributes.HideBySig | MethodAttributes.NewSlot | MethodAttributes.Final,
 null,
   new[] { evt.EventHandlerType });

       var removeIL = removeBuilder.GetILGenerator();
      // Implement as: _field = (HandlerType)Delegate.Remove(_field, value);
 removeIL.Emit(OpCodes.Ldarg_0);
   removeIL.Emit(OpCodes.Ldarg_0);
    removeIL.Emit(OpCodes.Ldfld, eventFieldBuilder);
    removeIL.Emit(OpCodes.Ldarg_1);
     removeIL.Emit(OpCodes.Call, typeof(Delegate).GetMethod("Remove", new[] { typeof(Delegate), typeof(Delegate) }));
     removeIL.Emit(OpCodes.Castclass, evt.EventHandlerType);
removeIL.Emit(OpCodes.Stfld, eventFieldBuilder);
   removeIL.Emit(OpCodes.Ret);

      eventBuilder.SetRemoveOnMethod(removeBuilder);
     }
       }

    // Implement each unique method with a default implementation
    foreach (var method in allMethods.Values)
    {
    var parameters = method.GetParameters();
        var parameterTypes = parameters.Select(p => p.ParameterType).ToArray();

        var methodBuilder = typeBuilder.DefineMethod(
            method.Name,
            MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.HideBySig | MethodAttributes.NewSlot | MethodAttributes.Final,
      method.ReturnType,
        parameterTypes);

        var methodIL = methodBuilder.GetILGenerator();

        // Implement a default return value based on return type
        if (method.ReturnType == typeof(void))
        {
      // For void methods, just return
            methodIL.Emit(OpCodes.Ret);
  }
      else if (method.ReturnType.IsGenericType && method.ReturnType.GetGenericTypeDefinition() == typeof(System.Threading.Tasks.Task<>))
        {
 // For Task<T>, return Task.FromResult(default(T))
 var taskResultType = method.ReturnType.GetGenericArguments()[0];
            var fromResultMethod = typeof(System.Threading.Tasks.Task)
            .GetMethod("FromResult", BindingFlags.Public | BindingFlags.Static)
  .MakeGenericMethod(taskResultType);

     // Load default value for T
  if (taskResultType.IsValueType)
    {
                var local = methodIL.DeclareLocal(taskResultType);
                methodIL.Emit(OpCodes.Ldloca_S, local);
    methodIL.Emit(OpCodes.Initobj, taskResultType);
     methodIL.Emit(OpCodes.Ldloc_0);
       }
      else
          {
         methodIL.Emit(OpCodes.Ldnull);
     }

            // Call Task.FromResult
         methodIL.Emit(OpCodes.Call, fromResultMethod);
        methodIL.Emit(OpCodes.Ret);
        }
        else if (method.ReturnType == typeof(System.Threading.Tasks.Task))
        {
    // For Task, return Task.CompletedTask
          var completedTaskProperty = typeof(System.Threading.Tasks.Task)
                .GetProperty("CompletedTask", BindingFlags.Public | BindingFlags.Static);
            methodIL.Emit(OpCodes.Call, completedTaskProperty.GetMethod);
            methodIL.Emit(OpCodes.Ret);
        }
 else if (method.ReturnType.IsValueType)
        {
// For value types, load default value
        var local = methodIL.DeclareLocal(method.ReturnType);
         methodIL.Emit(OpCodes.Ldloca_S, local);
     methodIL.Emit(OpCodes.Initobj, method.ReturnType);
            methodIL.Emit(OpCodes.Ldloc_0);
            methodIL.Emit(OpCodes.Ret);
     }
        else
        {
     // For reference types, return null
            methodIL.Emit(OpCodes.Ldnull);
            methodIL.Emit(OpCodes.Ret);
        }
    }

#if NET6_0_OR_GREATER
     return typeBuilder.CreateType();
#else
    return typeBuilder.CreateTypeInfo().AsType();
#endif
#else
         throw new PlatformNotSupportedException("Dynamic proxy generation is not supported on this platform.");
#endif
        }

   /// <summary>
    /// Converts a value to the target type
    /// </summary>
        private static object ConvertValue(object value, Type targetType)
        {
          if (value == null)
      return GetDefaultValue(targetType);

  // Handle nullable types
  if (targetType.IsGenericType && targetType.GetGenericTypeDefinition() == typeof(Nullable<>))
   {
     var underlyingType = Nullable.GetUnderlyingType(targetType);
            value = ConvertValue(value, underlyingType);
                return Activator.CreateInstance(targetType, value);
          }

  // If value is already the target type, return it
       if (value.GetType() == targetType)
      return value;

       // Handle string specially
            if (targetType == typeof(string))
    return value.ToString();

            // Handle bool from long (Gremlin often returns numbers for booleans)
  if (targetType == typeof(bool))
  {
        if (value is long longValue)
         return longValue > 0;
if (value is int intValue)
            return intValue > 0;
    if (value is bool boolValue)
      return boolValue;
 }

            // Use Convert.ChangeType for numeric conversions
      try
    {
     return Convert.ChangeType(value, targetType);
   }
   catch
   {
        // If conversion fails, return default
       return GetDefaultValue(targetType);
            }
        }

 private static object GetDefaultValue(Type type)
        {
         if (type.IsValueType)
         return Activator.CreateInstance(type);
            return null;
        }
    }
}
