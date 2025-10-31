using System.Linq.Expressions;

namespace Stardust.Paradox.Data.Linq.Visitors
{
    /// <summary>
    /// Visitor for InV() graph traversal step
    /// </summary>
    public class InVVisitor : GraphTraversalVisitorBase
    {
        public override string MethodName => "InV";

        public override Expression Visit(MethodCallExpression node, IVisitorContext context)
        {
            context.VisitExpression(node.Arguments[0]);
            context.GremlinQuery.Append(".inV()");

            if (node.Method.IsGenericMethod)
            {
                var genericArgs = node.Method.GetGenericArguments();
                if (genericArgs.Length >= 2)
                {
                    context.ElementType = genericArgs[1]; // TVertex
                }
            }

            return node;
        }
    }
}