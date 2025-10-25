using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using Stardust.Paradox.Data.Annotations;

namespace Stardust.Paradox.Data.Linq
{
    /// <summary>
    /// Fluent graph traversal API for building Gremlin queries
    /// </summary>
    /// <typeparam name="T">The vertex or edge type</typeparam>
    public class GraphTraversal<T> where T : IGraphEntity
    {
   private readonly List<string> _steps = new List<string>();

   public GraphTraversal()
        {
            // Constructor allows object creation for tests
        }

    internal GraphTraversal(List<string> steps)
        {
        _steps = new List<string>(steps);
        }

  internal void AddStep(string step)
        {
  _steps.Add(step);
        }

        internal List<string> GetSteps()
      {
            return new List<string>(_steps);
        }

        public string ToGremlinQuery()
        {
  if (_steps.Count == 0)
        return "";

       return string.Join(".", _steps);
        }
    }

    /// <summary>
    /// Extension methods for GraphTraversal
    /// </summary>
    public static class GraphTraversalLambdaExtensions
    {
     public static string ToGremlinQuery<T>(this GraphTraversal<T> traversal) where T : IGraphEntity
        {
            return traversal.ToGremlinQuery();
        }

        // Lambda-based traversal methods
        public static GraphTraversal<T> OutE<T>(this GraphTraversal<T> traversal, Expression<Func<T, object>> edgeProperty) where T : IGraphEntity
      {
          var newTraversal = new GraphTraversal<T>(traversal.GetSteps());
         var propertyName = ExtractPropertyName(edgeProperty);
            newTraversal.AddStep($"outE('{propertyName}')");
         return newTraversal;
        }

        public static GraphTraversal<T> InE<T>(this GraphTraversal<T> traversal, Expression<Func<T, object>> edgeProperty) where T : IGraphEntity
        {
          var newTraversal = new GraphTraversal<T>(traversal.GetSteps());
            var propertyName = ExtractPropertyName(edgeProperty);
            newTraversal.AddStep($"inE('{propertyName}')");
         return newTraversal;
  }

        public static GraphTraversal<T> Out<T>(this GraphTraversal<T> traversal, Expression<Func<T, object>> edgeProperty) where T : IGraphEntity
        {
     var newTraversal = new GraphTraversal<T>(traversal.GetSteps());
            var propertyName = ExtractPropertyName(edgeProperty);
    newTraversal.AddStep($"out('{propertyName}')");
         return newTraversal;
        }

        public static GraphTraversal<T> In<T>(this GraphTraversal<T> traversal, Expression<Func<T, object>> edgeProperty) where T : IGraphEntity
        {
      var newTraversal = new GraphTraversal<T>(traversal.GetSteps());
            var propertyName = ExtractPropertyName(edgeProperty);
            newTraversal.AddStep($"in('{propertyName}')");
  return newTraversal;
 }

   private static string ExtractPropertyName<T>(Expression<Func<T, object>> expression)
        {
      if (expression.Body is MemberExpression memberExpr)
       {
  return ToCamelCase(memberExpr.Member.Name);
            }
            else if (expression.Body is UnaryExpression unaryExpr && unaryExpr.Operand is MemberExpression operandMemberExpr)
            {
            return ToCamelCase(operandMemberExpr.Member.Name);
            }

  throw new ArgumentException("Expression must be a property accessor", nameof(expression));
        }

        private static string ToCamelCase(string name)
        {
     if (string.IsNullOrEmpty(name) || name.Length == 0)
        return name;
return char.ToLowerInvariant(name[0]) + name.Substring(1);
    }
    }

 /// <summary>
    /// Extension methods for entity traversal
    /// </summary>
    public static class GraphTraversalEntityExtensions
    {
        // Basic filtering
        public static GraphTraversal<T> Has<T>(this GraphTraversal<T> traversal, string key, object value) where T : IGraphEntity
        {
    var newTraversal = new GraphTraversal<T>(traversal.GetSteps());
  var formattedValue = FormatValue(value);
            newTraversal.AddStep($"has('{key}', {formattedValue})");
     return newTraversal;
        }

        public static GraphTraversal<T> Has<T>(this GraphTraversal<T> traversal, string key) where T : IGraphEntity
        {
     var newTraversal = new GraphTraversal<T>(traversal.GetSteps());
            newTraversal.AddStep($"has('{key}')");
            return newTraversal;
        }

  public static GraphTraversal<T> Has<T>(this GraphTraversal<T> traversal, Expression<Func<T, bool>> predicate) where T : IGraphEntity
        {
        var newTraversal = new GraphTraversal<T>(traversal.GetSteps());
            var gremlinPredicate = ParseBooleanExpression(predicate);
          newTraversal.AddStep(gremlinPredicate);
   return newTraversal;
        }

        public static GraphTraversal<T> HasLabel<T>(this GraphTraversal<T> traversal, params string[] labels) where T : IGraphEntity
      {
     var newTraversal = new GraphTraversal<T>(traversal.GetSteps());
            var labelList = string.Join("', '", labels);
            newTraversal.AddStep($"hasLabel('{labelList}')");
 return newTraversal;
        }

        public static GraphTraversal<T> HasId<T>(this GraphTraversal<T> traversal, params string[] ids) where T : IGraphEntity
      {
    var newTraversal = new GraphTraversal<T>(traversal.GetSteps());
      var idList = string.Join("', '", ids);
 newTraversal.AddStep($"hasId('{idList}')");
         return newTraversal;
        }

        private static string FormatValue(object value)
     {
        if (value == null) return "null";
if (value is string s) return $"'{s.Replace("'", "\\'")}'";
          if (value is bool b) return b.ToString().ToLowerInvariant();
            if (value is decimal d)
     {
 // Format decimal without trailing zeros
              var formatted = d.ToString(System.Globalization.CultureInfo.InvariantCulture);
       return formatted;
      }
      if (value is double db)
     {
           // Format double without trailing zeros
          var formatted = db.ToString(System.Globalization.CultureInfo.InvariantCulture);
         return formatted;
        }
            if (value is float f)
            {
     // Format float without trailing zeros
             var formatted = f.ToString(System.Globalization.CultureInfo.InvariantCulture);
      return formatted;
       }
     if (value is GremlinPredicate p) return p.PredicateString;
            return value.ToString();
    }

   // Vertex traversals
        public static GraphTraversal<T> Out<T>(this GraphTraversal<T> traversal, params string[] labels) where T : IGraphEntity
        {
            var newTraversal = new GraphTraversal<T>(traversal.GetSteps());
       if (labels.Length == 0)
{
  newTraversal.AddStep("out()");
            }
   else if (labels.Length == 1)
            {
           newTraversal.AddStep($"out('{labels[0]}')");
            }
            else
        {
       var labelList = string.Join("', '", labels);
          newTraversal.AddStep($"out('{labelList}')");
   }
   return newTraversal;
        }

        public static GraphTraversal<T> In<T>(this GraphTraversal<T> traversal, params string[] labels) where T : IGraphEntity
        {
         var newTraversal = new GraphTraversal<T>(traversal.GetSteps());
            if (labels.Length == 0)
   {
  newTraversal.AddStep("in()");
 }
            else if (labels.Length == 1)
            {
     newTraversal.AddStep($"in('{labels[0]}')");
            }
         else
            {
     var labelList = string.Join("', '", labels);
      newTraversal.AddStep($"in('{labelList}')");
            }
         return newTraversal;
      }

        public static GraphTraversal<T> Both<T>(this GraphTraversal<T> traversal, params string[] labels) where T : IGraphEntity
     {
            var newTraversal = new GraphTraversal<T>(traversal.GetSteps());
            if (labels.Length == 0)
     {
       newTraversal.AddStep("both()");
}
   else if (labels.Length == 1)
        {
      newTraversal.AddStep($"both('{labels[0]}')");
   }
            else
    {
      var labelList = string.Join("', '", labels);
  newTraversal.AddStep($"both('{labelList}')");
}
   return newTraversal;
     }

    // Edge traversals
        public static GraphTraversal<T> OutE<T>(this GraphTraversal<T> traversal, params string[] labels) where T : IGraphEntity
        {
     var newTraversal = new GraphTraversal<T>(traversal.GetSteps());
         if (labels.Length == 0)
            {
  newTraversal.AddStep("outE()");
            }
         else if (labels.Length == 1)
            {
     newTraversal.AddStep($"outE('{labels[0]}')");
     }
          else
          {
    var labelList = string.Join("', '", labels);
      newTraversal.AddStep($"outE('{labelList}')");
            }
        return newTraversal;
     }

      public static GraphTraversal<T> InE<T>(this GraphTraversal<T> traversal, params string[] labels) where T : IGraphEntity
        {
            var newTraversal = new GraphTraversal<T>(traversal.GetSteps());
            if (labels.Length == 0)
      {
        newTraversal.AddStep("inE()");
            }
     else if (labels.Length == 1)
{
    newTraversal.AddStep($"inE('{labels[0]}')");
            }
  else
   {
     var labelList = string.Join("', '", labels);
     newTraversal.AddStep($"inE('{labelList}')");
    }
            return newTraversal;
        }

        public static GraphTraversal<T> BothE<T>(this GraphTraversal<T> traversal, params string[] labels) where T : IGraphEntity
        {
            var newTraversal = new GraphTraversal<T>(traversal.GetSteps());
            if (labels.Length == 0)
            {
  newTraversal.AddStep("bothE()");
            }
            else if (labels.Length == 1)
            {
  newTraversal.AddStep($"bothE('{labels[0]}')");
  }
  else
    {
          var labelList = string.Join("', '", labels);
            newTraversal.AddStep($"bothE('{labelList}')");
        }
     return newTraversal;
        }

    // Vertex from edge
        public static GraphTraversal<T> OutV<T>(this GraphTraversal<T> traversal) where T : IGraphEntity
        {
            var newTraversal = new GraphTraversal<T>(traversal.GetSteps());
    newTraversal.AddStep("outV()");
return newTraversal;
    }

        public static GraphTraversal<T> InV<T>(this GraphTraversal<T> traversal) where T : IGraphEntity
        {
            var newTraversal = new GraphTraversal<T>(traversal.GetSteps());
          newTraversal.AddStep("inV()");
     return newTraversal;
        }

      public static GraphTraversal<T> BothV<T>(this GraphTraversal<T> traversal) where T : IGraphEntity
        {
            var newTraversal = new GraphTraversal<T>(traversal.GetSteps());
            newTraversal.AddStep("bothV()");
     return newTraversal;
     }

        public static GraphTraversal<T> OtherV<T>(this GraphTraversal<T> traversal) where T : IGraphEntity
        {
 var newTraversal = new GraphTraversal<T>(traversal.GetSteps());
  newTraversal.AddStep("otherV()");
    return newTraversal;
        }

        // Filtering
        public static GraphTraversal<T> Where<T>(this GraphTraversal<T> traversal, Expression<Func<T, bool>> predicate) where T : IGraphEntity
        {
            var newTraversal = new GraphTraversal<T>(traversal.GetSteps());
      var gremlinPredicate = ParseBooleanExpression(predicate);
     newTraversal.AddStep($"where({gremlinPredicate})");
         return newTraversal;
        }

        public static GraphTraversal<T> Where<T>(this GraphTraversal<T> traversal, Func<object, object> predicate) where T : IGraphEntity
       => throw new NotImplementedException();

        public static GraphTraversal<T> Not<T>(this GraphTraversal<T> traversal, Func<GraphTraversal<T>, GraphTraversal<T>> traversalFunc) where T : IGraphEntity
        {
     var newTraversal = new GraphTraversal<T>(traversal.GetSteps());
        var innerTraversal = new GraphTraversal<T>();
       var result = traversalFunc(innerTraversal);
         var innerQuery = result.ToGremlinQuery();

         newTraversal.AddStep($"not({innerQuery})");
  return newTraversal;
        }

   // Logical operations
        public static GraphTraversal<T> And<T>(this GraphTraversal<T> traversal, params Func<GraphTraversal<T>, GraphTraversal<T>>[] traversals) where T : IGraphEntity
   {
            var newTraversal = new GraphTraversal<T>(traversal.GetSteps());
         var conditions = new List<string>();

            foreach (var func in traversals)
 {
         var innerTraversal = new GraphTraversal<T>();
      var result = func(innerTraversal);
                conditions.Add(result.ToGremlinQuery());
            }

    var andClause = string.Join(", ", conditions);
    newTraversal.AddStep($"and({andClause})");
         return newTraversal;
        }

        public static GraphTraversal<T> Or<T>(this GraphTraversal<T> traversal, params Func<GraphTraversal<T>, GraphTraversal<T>>[] traversals) where T : IGraphEntity
        {
var newTraversal = new GraphTraversal<T>(traversal.GetSteps());
       var conditions = new List<string>();

            foreach (var func in traversals)
        {
    var innerTraversal = new GraphTraversal<T>();
         var result = func(innerTraversal);
        conditions.Add(result.ToGremlinQuery());
            }

            var orClause = string.Join(", ", conditions);
       newTraversal.AddStep($"or({orClause})");
 return newTraversal;
        }

        // Labeling and selection
      public static GraphTraversal<T> As<T>(this GraphTraversal<T> traversal, string label) where T : IGraphEntity
 {
            var newTraversal = new GraphTraversal<T>(traversal.GetSteps());
          newTraversal.AddStep($"as('{label}')");
     return newTraversal;
  }

        public static GraphTraversal<T> Select<T>(this GraphTraversal<T> traversal, params string[] labels) where T : IGraphEntity
        {
            var newTraversal = new GraphTraversal<T>(traversal.GetSteps());
            var labelList = string.Join("', '", labels);
   newTraversal.AddStep($"select('{labelList}')");
            return newTraversal;
     }

        // Deduplication
        public static GraphTraversal<T> Dedup<T>(this GraphTraversal<T> traversal) where T : IGraphEntity
   {
  var newTraversal = new GraphTraversal<T>(traversal.GetSteps());
      newTraversal.AddStep("dedup()");
          return newTraversal;
        }

        public static GraphTraversal<T> DedupBy<T>(this GraphTraversal<T> traversal, string property) where T : IGraphEntity
        {
  var newTraversal = new GraphTraversal<T>(traversal.GetSteps());
         newTraversal.AddStep("dedup()");
 newTraversal.AddStep($"by('{property}')");
        return newTraversal;
        }

      // Limiting
        public static GraphTraversal<T> Limit<T>(this GraphTraversal<T> traversal, long count) where T : IGraphEntity
        {
        var newTraversal = new GraphTraversal<T>(traversal.GetSteps());
            newTraversal.AddStep($"limit({count})");
            return newTraversal;
        }

        public static GraphTraversal<T> Skip<T>(this GraphTraversal<T> traversal, long count) where T : IGraphEntity
  {
            var newTraversal = new GraphTraversal<T>(traversal.GetSteps());
          newTraversal.AddStep($"skip({count})");
            return newTraversal;
        }

    // Helper methods for expression parsing
        private static string ParseBooleanExpression<T>(Expression<Func<T, bool>> predicate)
        {
          if (predicate.Body is BinaryExpression binaryExpr)
     {
      return ParseBinaryExpression(binaryExpr);
            }
   else if (predicate.Body is ConstantExpression)
            {
        throw new ArgumentException("Lambda expression must contain a property comparison");
            }

         throw new ArgumentException("Unsupported expression type");
  }

    private static string ParseBinaryExpression(BinaryExpression expr)
        {
            // Extract left side (property)
        string propertyName = null;
          object value = null;

        if (expr.Left is MemberExpression memberExpr)
      {
       propertyName = ToCamelCase(memberExpr.Member.Name);
  }
            else if (expr.Left is UnaryExpression unaryExpr && unaryExpr.Operand is MemberExpression operandMemberExpr)
        {
        propertyName = ToCamelCase(operandMemberExpr.Member.Name);
            }

            // Extract right side (value)
            if (expr.Right is ConstantExpression constantExpr)
            {
                value = constantExpr.Value;
         }
   else if (expr.Right is MemberExpression rightMemberExpr)
    {
// Handle variable references
       value = EvaluateExpression(rightMemberExpr);
    }
        else if (expr.Right is UnaryExpression rightUnaryExpr)
    {
        value = EvaluateExpression(rightUnaryExpr);
            }

       if (propertyName == null)
        {
    throw new ArgumentException("Could not extract property name from expression");
  }

     // Build Gremlin predicate based on operator
            switch (expr.NodeType)
   {
                case ExpressionType.Equal:
        return $"has('{propertyName}', {FormatValue(value)})";
       case ExpressionType.NotEqual:
         return $"has('{propertyName}', P.neq({FormatValue(value)}))";
    case ExpressionType.GreaterThan:
    return $"has('{propertyName}', P.gt({FormatValue(value)}))";
    case ExpressionType.GreaterThanOrEqual:
       return $"has('{propertyName}', P.gte({FormatValue(value)}))";
      case ExpressionType.LessThan:
  return $"has('{propertyName}', P.lt({FormatValue(value)}))";
       case ExpressionType.LessThanOrEqual:
      return $"has('{propertyName}', P.lte({FormatValue(value)}))";
     default:
        throw new NotSupportedException($"Operator {expr.NodeType} is not supported");
    }
        }

        private static object EvaluateExpression(Expression expression)
        {
      var lambda = Expression.Lambda(expression);
     var compiled = lambda.Compile();
            return compiled.DynamicInvoke();
        }

        private static string ToCamelCase(string name)
        {
 if (string.IsNullOrEmpty(name) || name.Length == 0)
       return name;
return char.ToLowerInvariant(name[0]) + name.Substring(1);
        }
    }

    /// <summary>
    /// Predicate builder for graph traversals
    /// </summary>
    public static class P
    {
        public static GremlinPredicate Gt(object value) => new GremlinPredicate("P.gt", value);
        public static GremlinPredicate Lt(object value) => new GremlinPredicate("P.lt", value);
    public static GremlinPredicate Gte(object value) => new GremlinPredicate("P.gte", value);
        public static GremlinPredicate Lte(object value) => new GremlinPredicate("P.lte", value);
        public static GremlinPredicate Eq(object value) => new GremlinPredicate("P.eq", value);
   public static GremlinPredicate Neq(object value) => new GremlinPredicate("P.neq", value);
        public static GremlinPredicate Within(params object[] values) => new GremlinPredicate("P.within", values);
        public static GremlinPredicate Without(params object[] values) => new GremlinPredicate("P.without", values);
        public static GremlinPredicate Between(object start, object end) => new GremlinPredicate("P.between", start, end);
 public static GremlinPredicate Inside(object start, object end) => new GremlinPredicate("P.inside", start, end);
        public static GremlinPredicate Outside(object start, object end) => new GremlinPredicate("P.outside", start, end);
    }

    /// <summary>
    /// Represents a Gremlin predicate
    /// </summary>
    public class GremlinPredicate
    {
        private readonly string _predicateName;
        private readonly object[] _values;

        public GremlinPredicate(string predicateName, params object[] values)
        {
        _predicateName = predicateName;
            _values = values;
        }

        public string PredicateString
        {
      get
    {
      if (_values.Length == 0)
              return $"{_predicateName}()";

            if (_values.Length == 1)
           {
 var val = FormatValue(_values[0]);
         return $"{_predicateName}({val})";
                }

          var formattedValues = string.Join(", ", Array.ConvertAll(_values, FormatValue));
         return $"{_predicateName}({formattedValues})";
            }
 }

        private string FormatValue(object value)
  {
          if (value == null) return "null";
  if (value is string s) return $"'{s.Replace("'", "\\'")}'";
            if (value is bool b) return b.ToString().ToLowerInvariant();
       if (value is decimal d)
   {
        // Format decimal without trailing zeros
   return d.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
   if (value is double db)
{
              // Format double without trailing zeros
    return db.ToString(System.Globalization.CultureInfo.InvariantCulture);
     }
         if (value is float f)
{
         // Format float without trailing zeros
    return f.ToString(System.Globalization.CultureInfo.InvariantCulture);
            }
return value.ToString();
        }
    }
}
