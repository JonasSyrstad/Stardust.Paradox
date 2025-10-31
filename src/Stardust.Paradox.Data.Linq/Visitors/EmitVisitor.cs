using System.Linq.Expressions;

namespace Stardust.Paradox.Data.Linq.Visitors
{
    /// <summary>
    /// Visitor for Emit() graph traversal steps
    /// </summary>
    public class EmitVisitor : GraphTraversalVisitorBase
    {
        public override string MethodName => "Emit";

        public override Expression Visit(MethodCallExpression node, IVisitorContext context)
        {
            // Visit source
            context.VisitExpression(node.Arguments[0]);

            // Check if this is the parameterless Emit() or Emit(predicate)
            if (node.Arguments.Count == 1)
            {
                // Parameterless emit
                context.GremlinQuery.Append(".emit()");
            }
            else if (node.Arguments.Count == 2)
            {
                // Emit with predicate
                var lambda = (LambdaExpression)StripQuotes(node.Arguments[1]);

                // Translate the predicate - this returns something like ".has('age', gt(30))"
                var predicateGremlin = context.TranslatePredicate(lambda.Body, lambda.Parameters[0].Name);
 
                // Remove the leading dot from the predicate
                if (!string.IsNullOrEmpty(predicateGremlin) && predicateGremlin.StartsWith("."))
                {
                    predicateGremlin = predicateGremlin.Substring(1);
                }
 
                context.GremlinQuery.Append($".emit({predicateGremlin})");
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