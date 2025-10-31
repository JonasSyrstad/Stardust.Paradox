using System.Linq.Expressions;

namespace Stardust.Paradox.Data.Linq.Visitors
{
    /// <summary>
    /// Visitor for Path() step
    /// </summary>
    public class PathVisitor : GraphTraversalVisitorBase
    {
        public override string MethodName => "Path";

        public override Expression Visit(MethodCallExpression node, IVisitorContext context)
        {
            // Visit source
            context.VisitExpression(node.Arguments[0]);

            context.GremlinQuery.Append(".path()");

// Path returns IGraphPath type
            // We change the element type to reflect this, though for query string generation this doesn't matter
            return node;
        }
    }
}