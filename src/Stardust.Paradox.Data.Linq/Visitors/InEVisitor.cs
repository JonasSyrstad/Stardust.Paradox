using System.Linq.Expressions;

namespace Stardust.Paradox.Data.Linq.Visitors
{
    /// <summary>
    /// Visitor for InE() graph traversal step
    /// </summary>
    public class InEVisitor : GraphTraversalVisitorBase
    {
        public override string MethodName => "InE";

        public override Expression Visit(MethodCallExpression node, IVisitorContext context)
        {
            // Visit source
            context.VisitExpression(node.Arguments[0]);

            // Extract edge label
            var lambda = (LambdaExpression)StripQuotes(node.Arguments[1]);
            var edgeLabel = GetEdgeLabelFromExpression(lambda.Body);
 
            if (!string.IsNullOrEmpty(edgeLabel))
            {
                context.GremlinQuery.Append($".inE('{edgeLabel}')");
            }
            else
            {
                context.GremlinQuery.Append(".inE()");
            }

            // Update element type to edge type
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