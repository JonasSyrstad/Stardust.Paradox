using System;
using System.Linq.Expressions;

namespace Stardust.Paradox.Data.Linq.Visitors
{
    /// <summary>
    /// Visitor for handling Intersect method calls
    /// </summary>
    public class IntersectVisitor : ExpressionVisitorBase
    {
        public override string MethodName => "Intersect";

        public override Expression Visit(MethodCallExpression node, IVisitorContext context)
        {
            // Intersect finds common elements between two sequences
            // Note: This is complex in Gremlin and requires special handling
            throw new NotSupportedException("Intersect operation requires complex query combination");
        }
    }
}