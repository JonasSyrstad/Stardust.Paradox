using System.Linq.Expressions;

namespace Stardust.Paradox.Data.Linq.Visitors
{
    /// <summary>
    /// Visitor for In() graph traversal step
    /// </summary>
    public class InVisitor : GraphTraversalVisitorBase
    {
        public override string MethodName => "In";

        public override Expression Visit(MethodCallExpression node, IVisitorContext context)
        {
            // Visit source
            context.VisitExpression(node.Arguments[0]);

            // Extract edge label from lambda
            var lambda = (LambdaExpression)StripQuotes(node.Arguments[1]);
            var edgeLabel = GetEdgeLabelFromExpression(lambda.Body);
       
            if (!string.IsNullOrEmpty(edgeLabel))
            {
                context.GremlinQuery.Append($".in('{edgeLabel}')");
            }
            else
            {
                context.GremlinQuery.Append(".in()");
            }

            // Update element type - for In<TSource, TTarget>, we want TSource (index 0)
            if (node.Method.IsGenericMethod)
            {
                var genericArgs = node.Method.GetGenericArguments();
                if (genericArgs.Length >= 2)
                {
                    context.ElementType = genericArgs[0]; // TSource
                }
            }

            return node;
        }
    }
}