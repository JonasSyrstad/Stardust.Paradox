using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace Stardust.Paradox.Data.Linq
{
    /// <summary>
    /// Handles LINQ set operations (Concat, Union, Except, Intersect) by executing both queries
    /// separately and performing the set operation in memory
    /// </summary>
    internal static class SetOperationsHandler
    {
        /// <summary>
      /// Determines if the expression contains a set operation
        /// </summary>
  public static bool IsSetOperation(Expression expression)
        {
if (expression is MethodCallExpression methodCall)
{
     if (methodCall.Method.DeclaringType == typeof(Queryable) ||
       methodCall.Method.DeclaringType == typeof(Enumerable))
    {
                 return methodCall.Method.Name == "Concat" ||
        methodCall.Method.Name == "Union" ||
       methodCall.Method.Name == "Except" ||
methodCall.Method.Name == "Intersect";
                }
            }
            return false;
        }

        /// <summary>
   /// Executes a set operation by running both queries and combining results in memory
        /// </summary>
        public static object ExecuteSetOperation(MethodCallExpression setOpExpression, IQueryProvider provider)
        {
            var operationName = setOpExpression.Method.Name;
        
            // Get the two source sequences
        var firstSource = setOpExpression.Arguments[0];
     var secondSource = setOpExpression.Arguments[1];
            
         // Execute both queries
       var firstResults = ExecuteQuery(firstSource, provider);
         var secondResults = ExecuteQuery(secondSource, provider);
            
            // Get the element type
         var elementType = setOpExpression.Type.GetGenericArguments()[0];

            // Perform the appropriate set operation
   switch (operationName)
        {
       case "Concat":
        return PerformConcat(firstResults, secondResults, elementType);
    case "Union":
  return PerformUnion(firstResults, secondResults, elementType);
     case "Except":
          return PerformExcept(firstResults, secondResults, elementType);
                case "Intersect":
        return PerformIntersect(firstResults, secondResults, elementType);
            default:
           throw new NotSupportedException($"Set operation {operationName} is not supported");
  }
        }

        private static object ExecuteQuery(Expression expression, IQueryProvider provider)
        {
            // Check if this is a constant query (already evaluated)
         if (expression is ConstantExpression constant)
  {
    return constant.Value;
          }
            
        // Create a query and execute it
 var elementType = expression.Type.GetGenericArguments()[0];
    
   // Use the generic CreateQuery<T> method
   var createQueryMethod = typeof(IQueryProvider)
   .GetMethods()
      .Where(m => m.Name == "CreateQuery" && m.IsGenericMethod)
       .Single()
     .MakeGenericMethod(elementType);
        
   var query = createQueryMethod.Invoke(provider, new object[] { expression });
          
            // Convert to list
            var toListMethod = typeof(Enumerable).GetMethod("ToList").MakeGenericMethod(elementType);
    return toListMethod.Invoke(null, new[] { query });
        }

        private static object PerformConcat(object first, object second, Type elementType)
        {
// Concat: combines without removing duplicates
            var concatMethod = typeof(Enumerable).GetMethod("Concat").MakeGenericMethod(elementType);
            var result = concatMethod.Invoke(null, new[] { first, second });
     
 // Convert to List
var toListMethod = typeof(Enumerable).GetMethod("ToList").MakeGenericMethod(elementType);
 return toListMethod.Invoke(null, new[] { result });
        }

     private static object PerformUnion(object first, object second, Type elementType)
     {
     // Union: combines and removes duplicates
      var unionMethod = typeof(Enumerable)
         .GetMethods()
        .Where(m => m.Name == "Union" && m.GetParameters().Length == 2)
        .Single()
       .MakeGenericMethod(elementType);
  var result = unionMethod.Invoke(null, new[] { first, second });
        
       // Convert to List
    var toListMethod = typeof(Enumerable).GetMethod("ToList").MakeGenericMethod(elementType);
            return toListMethod.Invoke(null, new[] { result });
  }

   private static object PerformExcept(object first, object second, Type elementType)
    {
   // Except: removes elements from first that exist in second
      var exceptMethod = typeof(Enumerable)
      .GetMethods()
        .Where(m => m.Name == "Except" && m.GetParameters().Length == 2)
     .Single()
           .MakeGenericMethod(elementType);
var result = exceptMethod.Invoke(null, new[] { first, second });
          
  // Convert to List
   var toListMethod = typeof(Enumerable).GetMethod("ToList").MakeGenericMethod(elementType);
      return toListMethod.Invoke(null, new[] { result });
    }

    private static object PerformIntersect(object first, object second, Type elementType)
       {
 // Intersect: finds common elements
        var intersectMethod = typeof(Enumerable)
    .GetMethods()
      .Where(m => m.Name == "Intersect" && m.GetParameters().Length == 2)
       .Single()
        .MakeGenericMethod(elementType);
      var result = intersectMethod.Invoke(null, new[] { first, second });
     
      // Convert to List
   var toListMethod = typeof(Enumerable).GetMethod("ToList").MakeGenericMethod(elementType);
   return toListMethod.Invoke(null, new[] { result });
    }
    }
}
