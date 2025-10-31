using System;
using System.Linq.Expressions;
using System.Reflection;
using Stardust.Paradox.Data.Annotations;

namespace Stardust.Paradox.Data.Linq.Visitors
{
    /// <summary>
    /// Base class for graph traversal visitors
    /// </summary>
    public abstract class GraphTraversalVisitorBase : IExpressionVisitor
    {
        public abstract string MethodName { get; }

        public virtual int Priority => 100;

        public virtual bool CanVisit(MethodCallExpression node, IVisitorContext context)
        {
            return node.Method.DeclaringType == typeof(GraphTraversalExtensions) &&
                node.Method.Name == MethodName;
        }

        public abstract Expression Visit(MethodCallExpression node, IVisitorContext context);

    protected string GetEdgeLabelFromExpression(Expression expression)
        {
       // Extract property name from p => p.Companies
            if (expression is MemberExpression memberExpr)
            {
           var propertyInfo = memberExpr.Member as PropertyInfo;
    if (propertyInfo != null)
 {
                 // Get the entity type from the member expression
         // For "p => p.Skills", memberExpr.Expression is the parameter "p"
 // We need to get the actual type of the parameter
            Type entityType = null;
              
                 if (memberExpr.Expression is ParameterExpression paramExpr)
          {
          // Get the parameter's type (e.g., IPerson)
       entityType = paramExpr.Type;
      Console.WriteLine($"[GraphTraversalVisitorBase] Extracted entityType from parameter: {entityType?.Name}");
          }
      else if (memberExpr.Expression != null)
                {
           // Fallback to the expression's type
 entityType = memberExpr.Expression.Type;
        Console.WriteLine($"[GraphTraversalVisitorBase] Extracted entityType from expression: {entityType?.Name}");
   }

      if (entityType != null)
    {
        // Try to get edge label from fluent configuration or attributes
    var edgeLabel = EdgeLabelResolver.GetAnyEdgeLabel(entityType, propertyInfo);
       Console.WriteLine($"[GraphTraversalVisitorBase] EdgeLabelResolver returned: '{edgeLabel}' for {entityType.Name}.{propertyInfo.Name}");
     if (!string.IsNullOrEmpty(edgeLabel))
             return edgeLabel;
              }
}

         // Fallback: Convert property name to camelCase for edge label
         var fallback = ToCamelCase(memberExpr.Member.Name);
        Console.WriteLine($"[GraphTraversalVisitorBase] Falling back to camelCase property name: '{fallback}'");
 return fallback;
          }

          return null;
 }

        private string ToCamelCase(string name)
   {
            if (string.IsNullOrEmpty(name) || name.Length == 0)
     return name;
            return char.ToLowerInvariant(name[0]) + name.Substring(1);
        }

      protected Expression StripQuotes(Expression expression)
        {
            while (expression.NodeType == ExpressionType.Quote)
   {
       expression = ((UnaryExpression)expression).Operand;
}
            return expression;
      }
    }
}
