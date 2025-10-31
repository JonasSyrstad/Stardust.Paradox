using System.Linq.Expressions;

namespace Stardust.Paradox.Data.Linq.Visitors
{
    /// <summary>
    /// Interface for expression visitors that handle specific LINQ method calls
    /// and translate them to Gremlin query steps
    /// </summary>
    public interface IExpressionVisitor
    {
        /// <summary>
        /// Gets the name of the method this visitor handles (e.g., "Where", "Select", "OrderBy")
        /// </summary>
        string MethodName { get; }

        /// <summary>
        /// Gets the priority of this visitor. Lower values are processed first.
        /// Default is 100. Use lower values for specialized visitors that should
        /// override default behavior.
        /// </summary>
  int Priority { get; }

        /// <summary>
        /// Determines if this visitor can handle the given method call expression
        /// </summary>
        /// <param name="node">The method call expression to evaluate</param>
      /// <param name="context">The visitor context containing state and helper methods</param>
        /// <returns>True if this visitor can handle the expression, false otherwise</returns>
    bool CanVisit(MethodCallExpression node, IVisitorContext context);

      /// <summary>
      /// Visits the method call expression and updates the context accordingly
    /// </summary>
 /// <param name="node">The method call expression to visit</param>
  /// <param name="context">The visitor context to update</param>
        /// <returns>The processed expression</returns>
        Expression Visit(MethodCallExpression node, IVisitorContext context);
    }
}
