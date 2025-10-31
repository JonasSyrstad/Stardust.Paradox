using System.Linq.Expressions;

namespace Stardust.Paradox.Data.Linq.Visitors
{
    /// <summary>
    /// Visitor for BothE() graph traversal step
    /// </summary>
    public class BothEVisitor : GraphTraversalVisitorBase
    {
        public override string MethodName => "BothE";

        public override Expression Visit(MethodCallExpression node, IVisitorContext context)
        {
            context.VisitExpression(node.Arguments[0]);

            var lambda = (LambdaExpression)StripQuotes(node.Arguments[1]);
            var edgeLabel = GetEdgeLabelFromExpression(lambda.Body);
            
            if (!string.IsNullOrEmpty(edgeLabel))
            {
                context.GremlinQuery.Append($".bothE('{edgeLabel}')");
            }
            else
            {
                context.GremlinQuery.Append(".bothE()");
            }

            if (node.Method.IsGenericMethod)
            {
                var genericArgs = node.Method.GetGenericArguments();
                if (genericArgs.Length >= 3)
                {
                    context.ElementType = genericArgs[2]; // TEdge
                }
            }

            return node;
        }
    }
}