using System.Linq.Expressions;

namespace Stardust.Paradox.Data.Linq.Visitors
{
    /// <summary>
    /// Visitor for Out() graph traversal step
    /// </summary>
    public class OutVisitor : GraphTraversalVisitorBase
    {
        public override string MethodName => "Out";

        public override Expression Visit(MethodCallExpression node, IVisitorContext context)
        {
            // Visit source
            context.VisitExpression(node.Arguments[0]);

            // Extract edge label from lambda
            var lambda = (LambdaExpression)StripQuotes(node.Arguments[1]);
            var edgeLabel = GetEdgeLabelFromExpression(lambda.Body);
      
            if (!string.IsNullOrEmpty(edgeLabel))
            {
                context.GremlinQuery.Append($".out('{edgeLabel}')");
            }
            else
            {
                context.GremlinQuery.Append(".out()");
            }

            // Update element type - for Out<TSource, TTarget>, we want TTarget (index 1)
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