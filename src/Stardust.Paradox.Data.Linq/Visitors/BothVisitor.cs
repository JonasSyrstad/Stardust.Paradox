using System.Linq.Expressions;

namespace Stardust.Paradox.Data.Linq.Visitors
{
    /// <summary>
    /// Visitor for Both() graph traversal step
    /// </summary>
    public class BothVisitor : GraphTraversalVisitorBase
    {
        public override string MethodName => "Both";

        public override Expression Visit(MethodCallExpression node, IVisitorContext context)
        {
            context.VisitExpression(node.Arguments[0]);

            var lambda = (LambdaExpression)StripQuotes(node.Arguments[1]);
            var edgeLabel = GetEdgeLabelFromExpression(lambda.Body);
    
            if (!string.IsNullOrEmpty(edgeLabel))
            {
                context.GremlinQuery.Append($".both('{edgeLabel}')");
            }
            else
            {
                context.GremlinQuery.Append(".both()");
            }

            // Update element type - for Both<TSource, TTarget>, we want TTarget (index 1)
            if (node.Method.IsGenericMethod)
            {
                var genericArgs = node.Method.GetGenericArguments();
                if (genericArgs.Length >= 2)
                {
                    context.ElementType = genericArgs[1]; // TTarget
                }
            }

            return node;
        }
    }
}