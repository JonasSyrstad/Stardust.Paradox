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
       // Check for attributes on the property
       var edgeLabelAttr = propertyInfo.GetCustomAttribute<EdgeLabelAttribute>();
     if (edgeLabelAttr != null)
          return edgeLabelAttr.Label;
 
        var reverseLabelAttr = propertyInfo.GetCustomAttribute<ReverseEdgeLabelAttribute>();
              if (reverseLabelAttr != null)
    return reverseLabelAttr.ReverseLabel;
}
      
                // Fallback: Convert property name to camelCase for edge label
      return ToCamelCase(memberExpr.Member.Name);
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
