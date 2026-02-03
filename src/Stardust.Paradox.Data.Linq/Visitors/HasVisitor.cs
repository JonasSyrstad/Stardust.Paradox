using System;
using System.Linq.Expressions;

namespace Stardust.Paradox.Data.Linq.Visitors
{
    /// <summary>
    /// Visitor for Has() filtering step
    /// </summary>
    public class HasVisitor : GraphTraversalVisitorBase
    {
     public override string MethodName => "Has";

        public override Expression Visit(MethodCallExpression node, IVisitorContext context)
        {
     // Visit source
       context.VisitExpression(node.Arguments[0]);

        // Get the predicate lambda from the arguments
           var predicate = (LambdaExpression)StripQuotes(node.Arguments[1]);
           
    // Translate the predicate to Gremlin has() filter
       var parameterName = predicate.Parameters[0].Name;
            var filterGremlin = context.TranslatePredicate(predicate.Body, parameterName);

       if (!string.IsNullOrEmpty(filterGremlin))
  {
            context.GremlinQuery.Append(filterGremlin);
         }

// Element type stays the same
   if (node.Method.IsGenericMethod)
            {
       var genericArgs = node.Method.GetGenericArguments();
      if (genericArgs.Length >= 1)
       {
     context.ElementType = genericArgs[0];
          }
            }

            return node;
        }
    }
}
