using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;

namespace Stardust.Paradox.Data.Linq
{
    /// <summary>
    /// Handles translation of GroupBy operations to Gremlin queries
    /// Supports GroupBy().Select() patterns with aggregations
 /// </summary>
    internal class GroupByTranslator
    {
        private readonly StringBuilder _gremlinQuery;

        public GroupByTranslator(StringBuilder gremlinQuery)
    {
      _gremlinQuery = gremlinQuery;
        }

        /// <summary>
     /// Translates a GroupBy followed by Select pattern
        /// Example: .GroupBy(p => p.City).Select(g => new { City = g.Key, Count = g.Count() })
        /// </summary>
        public void TranslateGroupByWithSelect(MethodCallExpression groupByNode, MethodCallExpression selectNode)
        {
            // Extract the grouping key from GroupBy
            var groupByKeySelector = (LambdaExpression)StripQuotes(groupByNode.Arguments[1]);
     var groupByKeyProperty = GetPropertyName(groupByKeySelector.Body);

            // Extract the Select projection lambda
            var selectLambda = (LambdaExpression)StripQuotes(selectNode.Arguments[1]);

        // Analyze the projection to determine what aggregations are needed
     var aggregationInfo = AnalyzeProjection(selectLambda);

if (aggregationInfo == null || string.IsNullOrEmpty(groupByKeyProperty))
  {
      throw new NotSupportedException(
                 "GroupBy requires a key selector and Select requires a projection with g.Key and aggregation functions like g.Count(), g.Sum(), g.Average(), g.Min(), or g.Max()");
            }

            // Build the Gremlin query based on the aggregation type
            BuildGroupByQuery(groupByKeyProperty, aggregationInfo);
        }

        /// <summary>
        /// Translates a simple GroupBy without Select
      /// </summary>
        public void TranslateGroupBy(MethodCallExpression node)
  {
      var keySelector = (LambdaExpression)StripQuotes(node.Arguments[1]);
            var keyProperty = GetPropertyName(keySelector.Body);

            if (!string.IsNullOrEmpty(keyProperty))
    {
          // For GroupBy without Select, we need to return IGrouping<TKey, TElement>
             // The Gremlin query needs to group and unfold to provide key-value pairs
         // where each value is a list of grouped elements
                _gremlinQuery.Append($".group().by('{ToCamelCase(keyProperty)}')");
    
    // Unfold the map so we get individual key-value pairs
             // Each pair will be converted to an IGrouping by the provider
     _gremlinQuery.Append(".unfold()");
 }
 else
    {
    throw new NotSupportedException("GroupBy requires a property-based key selector");
 }
        }

        private void BuildGroupByQuery(string keyProperty, AggregationInfo aggregationInfo)
        {
      // Start with group().by(key)
            _gremlinQuery.Append($".group().by('{ToCamelCase(keyProperty)}')");

          // Add the aggregation function
if (aggregationInfo.HasAggregation)
            {
            _gremlinQuery.Append(".by(");

      if (!string.IsNullOrEmpty(aggregationInfo.AggregateProperty))
       {
    // Property-based aggregation: fold().values('property').aggregateFunc()
         _gremlinQuery.Append($"fold().unfold().values('{ToCamelCase(aggregationInfo.AggregateProperty)}').{aggregationInfo.AggregateFunction}()");
            }
                else
  {
        // Count-based aggregation: count()
            _gremlinQuery.Append($"{aggregationInfo.AggregateFunction}()");
       }

  _gremlinQuery.Append(")");
 }

            // The result needs to be unfolded and projected
            // This converts the map to a list of key-value pairs
         _gremlinQuery.Append(".unfold()");
        }

    private AggregationInfo AnalyzeProjection(LambdaExpression selectLambda)
        {
       if (!(selectLambda.Body is NewExpression newExpr))
            {
           return null;
        }

            var info = new AggregationInfo();

            // Analyze each argument in the anonymous type constructor
   for (int i = 0; i < newExpr.Arguments.Count; i++)
{
   var arg = newExpr.Arguments[i];
      var memberName = newExpr.Members[i].Name;

     // Check if this is g.Key (property access on group parameter)
         if (arg is MemberExpression memberExpr && memberExpr.Member.Name == "Key")
       {
         info.KeyPropertyName = memberName;
   }
                // Check if this is an aggregate function call (g.Count(), g.Average(), etc.)
      else if (arg is MethodCallExpression methodCall)
       {
     var aggregation = ParseAggregation(methodCall);
            if (aggregation != null)
  {
         info.HasAggregation = true;
      info.AggregateFunction = aggregation.Function;
    info.AggregateProperty = aggregation.Property;
   info.AggregateResultName = memberName;

  // For now, support single aggregation
         // Multiple aggregations would require more complex query structure
      break;
     }
      }
     }

          return info.HasAggregation ? info : null;
    }

 private AggregationDetail ParseAggregation(MethodCallExpression methodCall)
        {
   var methodName = methodCall.Method.Name;

    switch (methodName)
  {
       case "Count":
        return new AggregationDetail { Function = "count" };

         case "Sum":
    return new AggregationDetail
        {
               Function = "sum",
              Property = ExtractPropertyFromAggregation(methodCall)
  };

    case "Average":
  return new AggregationDetail
  {
    Function = "mean",
        Property = ExtractPropertyFromAggregation(methodCall)
          };

                case "Min":
        return new AggregationDetail
           {
  Function = "min",
   Property = ExtractPropertyFromAggregation(methodCall)
 };

      case "Max":
  return new AggregationDetail
        {
       Function = "max",
   Property = ExtractPropertyFromAggregation(methodCall)
       };

     default:
    return null;
            }
        }

        private string ExtractPropertyFromAggregation(MethodCallExpression methodCall)
        {
            // Check if there's a lambda selector (e.g., g.Sum(p => p.Age))
            if (methodCall.Arguments.Count > 1)
   {
       var lambda = StripQuotes(methodCall.Arguments[1]) as LambdaExpression;
      if (lambda != null)
     {
     return GetPropertyName(lambda.Body);
      }
            }

   return null;
        }

  private string GetPropertyName(Expression expression)
        {
          if (expression is MemberExpression memberExpr)
      {
 return memberExpr.Member.Name;
            }
            else if (expression is UnaryExpression unaryExpr)
            {
   return GetPropertyName(unaryExpr.Operand);
}

            return null;
      }

   private string ToCamelCase(string name)
        {
  if (string.IsNullOrEmpty(name) || name.Length == 0)
         return name;
return char.ToLowerInvariant(name[0]) + name.Substring(1);
        }

        private static Expression StripQuotes(Expression expression)
      {
            while (expression.NodeType == ExpressionType.Quote)
     {
      expression = ((UnaryExpression)expression).Operand;
            }
         return expression;
 }

        private class AggregationInfo
 {
          public string KeyPropertyName { get; set; }
         public bool HasAggregation { get; set; }
            public string AggregateFunction { get; set; }
            public string AggregateProperty { get; set; }
       public string AggregateResultName { get; set; }
        }

        private class AggregationDetail
        {
          public string Function { get; set; }
       public string Property { get; set; }
        }
    }
}
