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

            string edgeLabel = null;

            // Check if this is the overload with lambda expression or edge-type-only
            if (node.Arguments.Count > 1)
            {
                // Has lambda expression - extract edge label from lambda
                var lambda = (LambdaExpression)StripQuotes(node.Arguments[1]);
                edgeLabel = GetEdgeLabelFromExpression(lambda.Body);
            }

            // Append the Gremlin step
            if (!string.IsNullOrEmpty(edgeLabel))
            {
                context.GremlinQuery.Append($".inE('{edgeLabel}')");
            }
            else
            {
                context.GremlinQuery.Append(".inE()");
            }

            // Update element type to edge type
            // Note: For generic methods, GetGenericArguments() is called directly on MethodInfo
            // This is different from Type.GetGenericArguments() which ReflectionCache handles
            if (node.Method.IsGenericMethod)
            {
                var genericArgs = node.Method.GetGenericArguments();
                if (genericArgs.Length == 3)
                {
                    // InE<TSource, TTarget, TEdge> - use TEdge
                    context.ElementType = genericArgs[2];
                }
                else if (genericArgs.Length == 1)
                {
                    // InE<TEdge> - use TEdge
                    context.ElementType = genericArgs[0];
                }
                // For 2 type params InE<TSource, TTarget>, the return type is IEdge<TSource>
                // which will be handled by the query provider
            }

            return node;
        }
    }
}