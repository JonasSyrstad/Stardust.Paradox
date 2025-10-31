using System.Linq.Expressions;

namespace Stardust.Paradox.Data.Linq.Visitors
{
    /// <summary>
    /// Visitor for OutV() graph traversal step
    /// </summary>
    public class OutVVisitor : GraphTraversalVisitorBase
    {
        public override string MethodName => "OutV";

        public override Expression Visit(MethodCallExpression node, IVisitorContext context)
        {
            context.VisitExpression(node.Arguments[0]);
            context.GremlinQuery.Append(".outV()");

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