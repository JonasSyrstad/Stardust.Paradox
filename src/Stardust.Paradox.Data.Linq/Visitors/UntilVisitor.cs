using System.Linq.Expressions;

namespace Stardust.Paradox.Data.Linq.Visitors
{
    /// <summary>
    /// Visitor for Until() graph traversal step
    /// </summary>
    public class UntilVisitor : GraphTraversalVisitorBase
    {
        public override string MethodName => "Until";

        public override Expression Visit(MethodCallExpression node, IVisitorContext context)
        {
            // Visit source
            context.VisitExpression(node.Arguments[0]);

            // Extract the predicate lambda
            var lambda = (LambdaExpression)StripQuotes(node.Arguments[1]);

            // Translate the predicate - this returns something like ".has('age', gt(50))"
            var predicateGremlin = context.TranslatePredicate(lambda.Body, lambda.Parameters[0].Name);
         
            // Remove the leading dot from the predicate
            if (!string.IsNullOrEmpty(predicateGremlin) && predicateGremlin.StartsWith("."))
            {
                predicateGremlin = predicateGremlin.Substring(1);
            }
     
            context.GremlinQuery.Append($".until({predicateGremlin})");

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