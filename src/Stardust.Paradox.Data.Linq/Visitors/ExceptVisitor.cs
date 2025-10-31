using System;
using System.Linq.Expressions;

namespace Stardust.Paradox.Data.Linq.Visitors
{
    /// <summary>
    /// Visitor for handling Except method calls
    /// </summary>
    public class ExceptVisitor : ExpressionVisitorBase
    {
        public override string MethodName => "Except";

        public override Expression Visit(MethodCallExpression node, IVisitorContext context)
        {
            // Except removes elements from first sequence that exist in second
            // Note: This is complex in Gremlin and requires special handling
            throw new NotSupportedException("Except operation requires complex query combination");
        }
    }
}