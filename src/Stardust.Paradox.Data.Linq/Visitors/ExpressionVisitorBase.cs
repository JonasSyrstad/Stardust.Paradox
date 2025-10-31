using System;
using System.Linq;
using System.Linq.Expressions;

namespace Stardust.Paradox.Data.Linq.Visitors
{
    /// <summary>
    /// Base class for expression visitors providing common functionality
    /// </summary>
    public abstract class ExpressionVisitorBase : IExpressionVisitor
    {
    /// <inheritdoc />
        public abstract string MethodName { get; }

        /// <inheritdoc />
        public virtual int Priority => 100;

        /// <inheritdoc />
      public virtual bool CanVisit(MethodCallExpression node, IVisitorContext context)
        {
         if (node.Method.DeclaringType != typeof(Queryable) &&
node.Method.DeclaringType != typeof(Enumerable))
            {
  return false;
            }

     return node.Method.Name == MethodName;
        }

   /// <inheritdoc />
        public abstract Expression Visit(MethodCallExpression node, IVisitorContext context);

   /// <summary>
        /// Helper method to visit the source expression
        /// </summary>
     protected void VisitSource(MethodCallExpression node, IVisitorContext context)
{
      context.VisitExpression(node.Arguments[0]);
        }

     /// <summary>
 /// Helper method to extract and translate a predicate lambda
   /// </summary>
    protected string ExtractAndTranslatePredicate(MethodCallExpression node, IVisitorContext context, int argumentIndex = 1)
        {
      if (node.Arguments.Count <= argumentIndex)
       {
     return string.Empty;
     }

        var lambda = (LambdaExpression)context.StripQuotes(node.Arguments[argumentIndex]);
      return context.TranslatePredicate(lambda.Body, lambda.Parameters[0].Name);
        }

        /// <summary>
    /// Helper method to get a property name from a lambda selector
      /// </summary>
      protected string GetPropertyNameFromSelector(MethodCallExpression node, IVisitorContext context, int argumentIndex = 1)
        {
         if (node.Arguments.Count <= argumentIndex)
  {
       return null;
      }

       var lambda = (LambdaExpression)context.StripQuotes(node.Arguments[argumentIndex]);
      return context.GetPropertyName(lambda.Body);
        }
    }
}
