using System.Linq.Expressions;

namespace Stardust.Paradox.Data.Linq.Visitors
{
    /// <summary>
    /// Visitor for OutE() graph traversal step
    /// </summary>
    public class OutEVisitor : GraphTraversalVisitorBase
    {
        public override string MethodName => "OutE";

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

            // For edge-type-only overload (OutE<TEdge>()), we cannot resolve the label here
            // because we don't have access to the CodeGenerator.EdgeLables dictionary
            // The label must come from attributes on the edge type itself or be omitted

            // Append the Gremlin step
            if (!string.IsNullOrEmpty(edgeLabel))
            {
                context.GremlinQuery.Append($".outE('{edgeLabel}')");
            }
            else
            {
                context.GremlinQuery.Append(".outE()");
            }

            // Update element type to edge type  
            // Note: For generic methods, GetGenericArguments() is called directly on MethodInfo
            // This is different from Type.GetGenericArguments() which ReflectionCache handles
            if (node.Method.IsGenericMethod)
            {
                var genericArgs = node.Method.GetGenericArguments();
                if (genericArgs.Length == 3)
                {
                    // OutE<TSource, TTarget, TEdge> - use TEdge
                    context.ElementType = genericArgs[2];
                }
                else if (genericArgs.Length == 1)
                {
                    // OutE<TEdge> - use TEdge
                    context.ElementType = genericArgs[0];
                }
                // For 2 type params OutE<TSource, TTarget>, the return type is IEdge<TTarget>
                // which will be handled by the query provider
            }

            return node;
        }
    }
}