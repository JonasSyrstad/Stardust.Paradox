using System;
using System.Linq;
using System.Linq.Expressions;

namespace Stardust.Paradox.Data.Linq.Visitors
{
    /// <summary>
    /// Visitor for SelectByLabels() step (multiple labels)
    /// </summary>
    public class SelectByLabelsVisitor : GraphTraversalVisitorBase
    {
        public override string MethodName => "SelectByLabels";

        public override Expression Visit(MethodCallExpression node, IVisitorContext context)
        {
            // Visit source
            context.VisitExpression(node.Arguments[0]);

            // Get the labels array from the constant argument
            var labels = (string[])((ConstantExpression)node.Arguments[1]).Value;
       
            if (labels == null || labels.Length == 0)
                throw new ArgumentException("At least one label is required");
         
            var labelList = string.Join(", ", labels.Select(l => $"'{l}'"));
            context.GremlinQuery.Append($".select({labelList})");

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