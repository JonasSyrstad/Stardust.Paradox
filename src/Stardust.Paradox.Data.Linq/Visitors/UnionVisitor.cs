using System;
using System.Linq.Expressions;

namespace Stardust.Paradox.Data.Linq.Visitors
{
    /// <summary>
    /// Visitor for handling Union method calls
    /// </summary>
    public class UnionVisitor : ExpressionVisitorBase
    {
        public override string MethodName => "Union";

        public override Expression Visit(MethodCallExpression node, IVisitorContext context)
        {
            // Union combines two sequences and removes duplicates
            // Note: This is a simplified implementation
            throw new NotSupportedException("Union operation requires combining two separate query contexts");
        }
    }
}