using System;
using System.Linq.Expressions;
using System.Reflection;
using Stardust.Particles;

namespace Stardust.Paradox.Data.Infrastructure
{
    /// <summary>
    /// Compiles property access expressions for high-performance reflection operations.
    /// </summary>
    internal static class ExpressionCompiler
    {
        /// <summary>
     /// Creates a compiled property setter delegate.
     /// </summary>
      public static Action<object, object> CreatePropertySetter(PropertyInfo property)
   {
            try
    {
       var valueParameter = Expression.Parameter(typeof(object), "value");
        var instanceParameter = Expression.Parameter(typeof(object), "target");
    var member = Expression.Property(Expression.Convert(instanceParameter, property.DeclaringType), property);
    var assign = Expression.Assign(member, Expression.Convert(valueParameter, property.PropertyType));
         var lambda = Expression.Lambda<Action<object, object>>(
  Expression.Convert(assign, typeof(object)), 
    instanceParameter, 
   valueParameter);
   return lambda.Compile();
            }
 catch (Exception ex)
      {
      Logging.Exception(ex);
     throw;
          }
     }

        /// <summary>
        /// Creates a compiled property getter delegate.
        /// </summary>
        public static Func<object, object> CreatePropertyGetter(PropertyInfo property)
   {
            try
         {
      var instanceParameter = Expression.Parameter(typeof(object), "target");
                var member = Expression.Property(Expression.Convert(instanceParameter, property.DeclaringType), property);
       var lambda = Expression.Lambda<Func<object, object>>(
       Expression.Convert(member, typeof(object)), 
                 instanceParameter);
 return lambda.Compile();
            }
            catch (Exception ex)
   {
     Logging.Exception(ex);
       throw;
            }
        }
    }
}
