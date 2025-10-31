using System;
using System.Linq.Expressions;

namespace Stardust.Paradox.Data.Linq.Visitors
{
    /// <summary>
    /// Visitor for handling Concat method calls
    /// </summary>
    public class ConcatVisitor : ExpressionVisitorBase
    {
   public override string MethodName => "Concat";

        public override Expression Visit(MethodCallExpression node, IVisitorContext context)
        {
       // Concat combines two sequences without removing duplicates
      // In Gremlin, this can be done with union() step
       // Note: This is a simplified implementation that may need enhancement
      throw new NotSupportedException("Concat operation requires special handling - use Union for combining queries");
      }
    }
}
