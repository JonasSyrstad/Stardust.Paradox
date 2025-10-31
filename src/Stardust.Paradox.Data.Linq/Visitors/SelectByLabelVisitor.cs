using System;
using System.Linq.Expressions;

namespace Stardust.Paradox.Data.Linq.Visitors
{
    /// <summary>
    /// Visitor for SelectByLabel() step
    /// </summary>
    public class SelectByLabelVisitor : GraphTraversalVisitorBase
    {
        public override string MethodName => "SelectByLabel";

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
                if (genericArgs.Length >= 2)
                {
                    context.ElementType = genericArgs[1]; // TResult
                }
            }

            return node;
        }
    }
}