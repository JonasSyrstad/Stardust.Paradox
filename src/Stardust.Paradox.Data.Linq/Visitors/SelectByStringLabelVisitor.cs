using System;
using System.Linq;
using System.Linq.Expressions;

namespace Stardust.Paradox.Data.Linq.Visitors
{
    /// <summary>
  /// Visitor for Select<TResult>(string label) step that selects a previously labeled step
 /// </summary>
    public class SelectByStringLabelVisitor : GraphTraversalVisitorBase
    {
        public override string MethodName => "Select";

    public override int Priority => 90; // Higher priority than SelectVisitor

        public override bool CanVisit(MethodCallExpression node, IVisitorContext context)
     {
            // Check if it's a Select method with a string label parameter
    return node.Method.Name == "Select" &&
       node.Method.DeclaringType == typeof(GraphSetLinqExtensions) &&
      node.Method.IsGenericMethod &&
           node.Method.GetGenericArguments().Length == 1 &&
         node.Arguments.Count == 2 &&
                node.Arguments[1] is ConstantExpression constExpr &&
         constExpr.Type == typeof(string);
        }

        public override Expression Visit(MethodCallExpression node, IVisitorContext context)
        {
  // Visit source
            context.VisitExpression(node.Arguments[0]);

            // Get the label from the constant string argument
            var label = (string)((ConstantExpression)node.Arguments[1]).Value;
            
      if (string.IsNullOrWhiteSpace(label))
      throw new ArgumentException("Label cannot be empty");

            context.GremlinQuery.Append($".select('{label}')");

            // Update element type to TResult
       if (node.Method.IsGenericMethod)
       {
            var genericArgs = node.Method.GetGenericArguments();
              if (genericArgs.Length >= 1)
        {
              context.ElementType = genericArgs[0]; // TResult
}
  }

            return node;
        }
    }
}
