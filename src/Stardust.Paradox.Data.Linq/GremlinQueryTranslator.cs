using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;

namespace Stardust.Paradox.Data.Linq
{
    /// <summary>
    /// Translates LINQ expression trees to Gremlin query strings
    /// </summary>
  internal class GremlinQueryTranslator : ExpressionVisitor
    {
    private readonly string _label;
     private readonly StringBuilder _gremlinQuery;
     private readonly Stack<string> _contextStack;
    private Type _elementType;
      private bool _isCountQuery;
        private bool _isLongCountQuery;
    private bool _isAnyQuery;
    private bool _isFirstQuery;
    private bool _isSingleQuery;
        private bool _useFirstOrDefault;
     private bool _useSingleOrDefault;
     private bool _isDistinctQuery;
    private bool _isAllQuery;
    private bool _isSumQuery;
        private bool _isAverageQuery;
   private bool _isMinQuery;
  private bool _isMaxQuery;
private bool _isGroupByQuery;
      private bool _requiresClientSideProjection;
     private LambdaExpression _clientSideProjection;

  public Type ElementType => _elementType;
  public bool IsGroupByQuery => _isGroupByQuery;
        public bool IsSingleQuery => _isSingleQuery;
  public bool UseSingleOrDefault => _useSingleOrDefault;
    public bool IsFirstQuery => _isFirstQuery;
  public bool UseFirstOrDefault => _useFirstOrDefault;
        public bool IsAllQuery => _isAllQuery;
     public bool RequiresClientSideProjection => _requiresClientSideProjection;
        public LambdaExpression ClientSideProjection => _clientSideProjection;

        public GremlinQueryTranslator(string label)
        {
    _label = label;
    _gremlinQuery = new StringBuilder();
    _contextStack = new Stack<string>();
   }

  public string Translate(Expression expression)
        {
        _gremlinQuery.Clear();
    _gremlinQuery.Append($"g.V().hasLabel('{_label}')");
            
Visit(expression);
    
         // Add terminal steps
     if (_isCountQuery || _isLongCountQuery)
   {
      _gremlinQuery.Append(".count()");
    }
    else if (_isAnyQuery)
       {
         _gremlinQuery.Append(".limit(1).count()");
     }
       else if (_isFirstQuery)
     {
    _gremlinQuery.Append(".limit(1)");
   }
         else if (_isSingleQuery)
  {
    _gremlinQuery.Append(".limit(2)"); // Get 2 to verify single
   }
else if (_isDistinctQuery)
{
           _gremlinQuery.Append(".dedup()");
    }
 else if (_isAllQuery)
    {
      // All query already has .count() added in VisitAll
  // No additional terminal step needed
         }
  else if (_isSumQuery)
     {
   _gremlinQuery.Append(".sum()");
            }
    else if (_isAverageQuery)
         {
  _gremlinQuery.Append(".mean()");
            }
    else if (_isMinQuery)
  {
     _gremlinQuery.Append(".min()");
 }
    else if (_isMaxQuery)
    {
           _gremlinQuery.Append(".max()");
   }

      return _gremlinQuery.ToString();
        }

  protected override Expression VisitMethodCall(MethodCallExpression node)
      {
        if (node.Method.DeclaringType == typeof(Queryable) ||
           node.Method.DeclaringType == typeof(Enumerable))
     {
     switch (node.Method.Name)
{
case "Where":
   return VisitWhere(node);
      case "Select":
      return VisitSelect(node);
   case "SelectMany":
  return VisitSelectMany(node);
           case "Count":
            return VisitCount(node);
       case "LongCount":
 return VisitLongCount(node);
       case "Sum":
         return VisitSum(node);
case "Average":
   return VisitAverage(node);
 case "Min":
    return VisitMin(node);
         case "Max":
   return VisitMax(node);
           case "Any":
       return VisitAny(node);
case "First":
            case "FirstOrDefault":
     return VisitFirst(node);
      case "Single":
   case "SingleOrDefault":
       return VisitSingle(node);
   case "OrderBy":
case "OrderByDescending":
   return VisitOrderBy(node);
        case "ThenBy":
           case "ThenByDescending":
       return VisitThenBy(node);
          case "Skip":
 return VisitSkip(node);
case "Take":
     return VisitTake(node);
   case "Distinct":
      return VisitDistinct(node);
   case "Concat":
         return VisitConcat(node);
     case "Union":
        return VisitUnion(node);
case "Except":
      return VisitExcept(node);
      case "Intersect":
  return VisitIntersect(node);
         case "GroupBy":
 return VisitGroupBy(node);
       case "All":
     return VisitAll(node);
 }
         }
    else if (node.Method.DeclaringType == typeof(GraphTraversalExtensions))
    {
    // Handle graph traversal operations
  switch (node.Method.Name)
       {
    case "Out":
             return VisitOut(node);
         case "In":
 return VisitIn(node);
     case "OutE":
  return VisitOutE(node);
    case "InE":
  return VisitInE(node);
     case "Both":
 return VisitBoth(node);
 case "BothE":
    return VisitBothE(node);
        case "OutV":
       return VisitOutV(node);
    case "InV":
         return VisitInV(node);
       case "BothV":
 return VisitBothV(node);
       case "OtherV":
            return VisitOtherV(node);
      }
   }

  return base.VisitMethodCall(node);
        }

   private Expression VisitWhere(MethodCallExpression node)
 {
   // Visit source
       Visit(node.Arguments[0]);

 // Extract lambda and build has() predicate
 var lambda = (LambdaExpression)StripQuotes(node.Arguments[1]);
            var predicate = TranslatePredicate(lambda.Body, lambda.Parameters[0].Name);
            
 if (!string.IsNullOrEmpty(predicate))
 {
   _gremlinQuery.Append(predicate);
   }

  return node;
        }

        private Expression VisitSelect(MethodCallExpression node)
        {
   // Check if source is GroupBy - if so, delegate to GroupByTranslator
       if (node.Arguments[0] is MethodCallExpression sourceCall &&
 sourceCall.Method.Name == "GroupBy" &&
         (sourceCall.Method.DeclaringType == typeof(Queryable) ||
   sourceCall.Method.DeclaringType == typeof(Enumerable)))
          {
 // Visit the GroupBy source first (this will also visit what's before GroupBy)
     Visit(sourceCall.Arguments[0]);
    
     // If the source triggered client-side projection, don't translate GroupBy or this Select
          // They will be handled by ChainedOperationsHandler
       if (_requiresClientSideProjection)
          {
       return node;
       }

   // Use GroupByTranslator to handle the complex pattern
   var groupByTranslator = new GroupByTranslator(_gremlinQuery);
     groupByTranslator.TranslateGroupByWithSelect(sourceCall, node);
   
  _isGroupByQuery = true;
     return node;
}

     // Visit source
Visit(node.Arguments[0]);

// If visiting source triggered client-side projection, we're done
  // This Select and any subsequent operations will be handled client-side
    if (_requiresClientSideProjection)
   {
        return node;
      }

        // Extract lambda
   var lambda = (LambdaExpression)StripQuotes(node.Arguments[1]);
  
    // Check if this requires client-side evaluation
    if (ProjectionExpressionEvaluator.RequiresClientSideEvaluation(lambda))
 {
     // Mark for client-side projection
       _requiresClientSideProjection = true;
    _clientSideProjection = lambda;
       _elementType = lambda.ReturnType;
      
  // Extract required properties for server-side retrieval
       var requiredProperties = ProjectionExpressionEvaluator.ExtractRequiredProperties(lambda);
   var propertyQuery = ProjectionExpressionEvaluator.GetPropertyRetrievalQuery(requiredProperties);
    _gremlinQuery.Append(propertyQuery);
  
         return node;
         }
  
 // Build values() or valueMap() step for server-side projection
   if (lambda.Body is MemberExpression memberExpr)
  {
      var propertyName = ToCamelCase(memberExpr.Member.Name);
  _gremlinQuery.Append($".values('{propertyName}')");
     
  // For single property selection, the element type becomes the property type
     _elementType = memberExpr.Type;
  }
   else if (lambda.Body is NewExpression newExpr)
  {
    // Anonymous type projection
      var properties = newExpr.Arguments
 .OfType<MemberExpression>()
  .Select(m => $"'{ToCamelCase(m.Member.Name)}'")
.ToArray();
 
  if (properties.Length > 0)
   {
      _gremlinQuery.Append($".valueMap({string.Join(",", properties)})");
   
 // For anonymous type projection, the element type becomes the anonymous type
_elementType = newExpr.Type;
 }
  }

      return node;
   }

        private Expression VisitSelectMany(MethodCallExpression node)
        {
          // SelectMany flattens collections - requires client-side evaluation
    // because Gremlin doesn't have a direct equivalent
  
            // Visit source
         Visit(node.Arguments[0]);

   // Extract lambda
     var lambda = (LambdaExpression)StripQuotes(node.Arguments[1]);
      
   // Mark for client-side projection
 _requiresClientSideProjection = true;
          _clientSideProjection = lambda;
      
  // For SelectMany, the element type is the element inside the collection, not the collection itself
  // The lambda returns IEnumerable<T>, so we need to extract T
   if (lambda.ReturnType.IsGenericType)
      {
 var genericArgs = lambda.ReturnType.GetGenericArguments();
    if (genericArgs.Length > 0)
  {
    _elementType = genericArgs[0]; // Extract T from IEnumerable<T>
      }
  else
  {
    _elementType = lambda.ReturnType;
        }
  }
  else
 {
            _elementType = lambda.ReturnType;
      }
    
   // Check if there's a result selector (the third argument in some SelectMany overloads)
     // For now, we'll handle the simple case where there's just the collection selector
      
// Extract required properties for server-side retrieval
   var requiredProperties = ProjectionExpressionEvaluator.ExtractRequiredProperties(lambda);
   var propertyQuery = ProjectionExpressionEvaluator.GetPropertyRetrievalQuery(requiredProperties);
 _gremlinQuery.Append(propertyQuery);
     
  return node;
        }

        private Expression VisitCount(MethodCallExpression node)
    {
     _isCountQuery = true;

 // Visit source
            Visit(node.Arguments[0]);

    // If there's a predicate, add it
    if (node.Arguments.Count > 1)
  {
        var lambda = (LambdaExpression)StripQuotes(node.Arguments[1]);
       var predicate = TranslatePredicate(lambda.Body, lambda.Parameters[0].Name);
    
if (!string.IsNullOrEmpty(predicate))
   {
         _gremlinQuery.Append(predicate);
       }
            }

  return node;
    }

     private Expression VisitLongCount(MethodCallExpression node)
        {
          _isLongCountQuery = true;

 // Visit source
      Visit(node.Arguments[0]);

   // If there's a predicate, add it
     if (node.Arguments.Count > 1)
 {
        var lambda = (LambdaExpression)StripQuotes(node.Arguments[1]);
   var predicate = TranslatePredicate(lambda.Body, lambda.Parameters[0].Name);

    if (!string.IsNullOrEmpty(predicate))
                {
       _gremlinQuery.Append(predicate);
       }
            }

         return node;
}

        private Expression VisitSum(MethodCallExpression node)
        {
            _isSumQuery = true;
   
            // Visit source
            Visit(node.Arguments[0]);

            // If there's a lambda selector, extract property
     if (node.Arguments.Count > 1)
 {
         var lambda = (LambdaExpression)StripQuotes(node.Arguments[1]);
    var propertyName = GetPropertyName(lambda.Body);
                
           if (!string.IsNullOrEmpty(propertyName))
       {
          _gremlinQuery.Append($".values('{ToCamelCase(propertyName)}')");
       }
          }

            return node;
        }

        private Expression VisitAverage(MethodCallExpression node)
      {
_isAverageQuery = true;
  
// Visit source
 Visit(node.Arguments[0]);

 // If there's a lambda selector, extract property
  if (node.Arguments.Count > 1)
            {
              var lambda = (LambdaExpression)StripQuotes(node.Arguments[1]);
         var propertyName = GetPropertyName(lambda.Body);
            
       if (!string.IsNullOrEmpty(propertyName))
  {
               _gremlinQuery.Append($".values('{ToCamelCase(propertyName)}')");
    }
 }

  return node;
        }

   private Expression VisitMin(MethodCallExpression node)
  {
        _isMinQuery = true;
     
            // Visit source
            Visit(node.Arguments[0]);

     // If there's a lambda selector, extract property
     if (node.Arguments.Count > 1)
       {
     var lambda = (LambdaExpression)StripQuotes(node.Arguments[1]);
      var propertyName = GetPropertyName(lambda.Body);
       
    if (!string.IsNullOrEmpty(propertyName))
       {
    _gremlinQuery.Append($".values('{ToCamelCase(propertyName)}')");
       }
    }

   return node;
        }

    private Expression VisitMax(MethodCallExpression node)
      {
   _isMaxQuery = true;
  
            // Visit source
            Visit(node.Arguments[0]);

     // If there's a lambda selector, extract property
         if (node.Arguments.Count > 1)
     {
       var lambda = (LambdaExpression)StripQuotes(node.Arguments[1]);
      var propertyName = GetPropertyName(lambda.Body);
        
        if (!string.IsNullOrEmpty(propertyName))
          {
          _gremlinQuery.Append($".values('{ToCamelCase(propertyName)}')");
         }
    }

            return node;
    }

        private Expression VisitAny(MethodCallExpression node)
        {
            _isAnyQuery = true;
         
            // Visit source
            Visit(node.Arguments[0]);

            // If there's a predicate, add it
       if (node.Arguments.Count > 1)
            {
                var lambda = (LambdaExpression)StripQuotes(node.Arguments[1]);
           var predicate = TranslatePredicate(lambda.Body, lambda.Parameters[0].Name);
     
            if (!string.IsNullOrEmpty(predicate))
    {
            _gremlinQuery.Append(predicate);
      }
   }

       return node;
        }

        private Expression VisitFirst(MethodCallExpression node)
        {
            _isFirstQuery = true;
        _useFirstOrDefault = node.Method.Name == "FirstOrDefault";
            
  // Visit source
            Visit(node.Arguments[0]);

  // If there's a predicate, add it
    if (node.Arguments.Count > 1)
     {
          var lambda = (LambdaExpression)StripQuotes(node.Arguments[1]);
    var predicate = TranslatePredicate(lambda.Body, lambda.Parameters[0].Name);
             

     if (!string.IsNullOrEmpty(predicate))
         {
               _gremlinQuery.Append(predicate);
         }
   }

   return node;
   }

        private Expression VisitSingle(MethodCallExpression node)
        {
_isSingleQuery = true;
    _useSingleOrDefault = node.Method.Name == "SingleOrDefault";
          
 // Visit source
        Visit(node.Arguments[0]);

            // If there's a predicate, add it
    if (node.Arguments.Count > 1)
        {
    var lambda = (LambdaExpression)StripQuotes(node.Arguments[1]);
                var predicate = TranslatePredicate(lambda.Body, lambda.Parameters[0].Name);
         

      if (!string.IsNullOrEmpty(predicate))
       {
            _gremlinQuery.Append(predicate);
      }
 }

            return node;
        }

        private Expression VisitOrderBy(MethodCallExpression node)
        {
  // If we're already in client-side projection mode, don't translate OrderBy to Gremlin
      // It will be handled by the ChainedOperationsHandler
       if (_requiresClientSideProjection)
   {
    // Just visit the source and return - the OrderBy will be applied client-side
    Visit(node.Arguments[0]);
       return node;
 }

    // Visit source
         Visit(node.Arguments[0]);

      // Extract lambda
  var lambda = (LambdaExpression)StripQuotes(node.Arguments[1]);
         var propertyName = GetPropertyName(lambda.Body);
      
       var direction = node.Method.Name == "OrderByDescending" ? "decr" : "incr";
    _gremlinQuery.Append($".order().by('{ToCamelCase(propertyName)}', {direction})");

 return node;
    }

     private Expression VisitThenBy(MethodCallExpression node)
        {
    // If we're already in client-side projection mode, don't translate ThenBy to Gremlin
       // It will be handled by the ChainedOperationsHandler
  if (_requiresClientSideProjection)
{
    // Just visit the source and return - the ThenBy will be applied client-side
       Visit(node.Arguments[0]);
     return node;
      }

    // Visit source
    Visit(node.Arguments[0]);

    // Extract lambda
      var lambda = (LambdaExpression)StripQuotes(node.Arguments[1]);
       var propertyName = GetPropertyName(lambda.Body);
  
  var direction = node.Method.Name == "ThenByDescending" ? "decr" : "incr";
  _gremlinQuery.Append($".by('{ToCamelCase(propertyName)}', {direction})");

         return node;
   }

    private Expression VisitSkip(MethodCallExpression node)
        {
  // Visit source
            Visit(node.Arguments[0]);

      // Extract count and validate
      var count = (int)((ConstantExpression)node.Arguments[1]).Value;
    
            if (count < 0)
            {
         throw new ArgumentException("Skip count cannot be negative.", nameof(count));
            }
       
   _gremlinQuery.Append($".skip({count})");

      return node;
        }

private Expression VisitTake(MethodCallExpression node)
  {
   // Visit source
Visit(node.Arguments[0]);

     // Extract count and validate
  var count = (int)((ConstantExpression)node.Arguments[1]).Value;
     
          if (count < 0)
            {
                throw new ArgumentException("Take count cannot be negative.", nameof(count));
            }
   
   _gremlinQuery.Append($".limit({count})");

            return node;
  }

     private Expression VisitDistinct(MethodCallExpression node)
{
            _isDistinctQuery = true;
            
            // Visit source
            Visit(node.Arguments[0]);

            return node;
        }

   private Expression VisitConcat(MethodCallExpression node)
        {
            // Concat combines two sequences without removing duplicates
      // In Gremlin, this can be done with union() step
       // Note: This is a simplified implementation that may need enhancement
          throw new NotSupportedException("Concat operation requires special handling - use Union for combining queries");
        }

        private Expression VisitUnion(MethodCallExpression node)
        {
            // Union combines two sequences and removes duplicates
            // Note: This is a simplified implementation
          throw new NotSupportedException("Union operation requires combining two separate query contexts");
     }

        private Expression VisitExcept(MethodCallExpression node)
        {
            // Except removes elements from first sequence that exist in second
          // Note: This is complex in Gremlin and requires special handling
        throw new NotSupportedException("Except operation requires complex query combination");
        }

        private Expression VisitIntersect(MethodCallExpression node)
        {
// Intersect finds common elements between two sequences
            // Note: This is complex in Gremlin and requires special handling
  throw new NotSupportedException("Intersect operation requires complex query combination");
        }

        private Expression VisitGroupBy(MethodCallExpression node)
{
  // If we're already in client-side projection mode, don't try to translate GroupBy to Gremlin
      // It will be handled by the ChainedOperationsHandler
      if (_requiresClientSideProjection)
 {
     // Don't visit children - stop translation here
       // The client-side handler will take care of GroupBy
   return node;
   }

  // Visit source
       Visit(node.Arguments[0]);

// Check again after visiting source - if source triggered client-side projection,
      // don't translate this GroupBy
      if (_requiresClientSideProjection)
      {
    return node;
  }

          // Use GroupByTranslator for the GroupBy operation
    var groupByTranslator = new GroupByTranslator(_gremlinQuery);
    groupByTranslator.TranslateGroupBy(node);

         _isGroupByQuery = true;
     return node;
     }

        private Expression VisitAll(MethodCallExpression node)
        {
   _isAllQuery = true;
   
            // Visit source
    Visit(node.Arguments[0]);

      // For All, we need special handling:
          // LINQ All(predicate) returns true if ALL elements satisfy the predicate
        // In Gremlin, we need to check if count(filtered) == count(total)
            
            // Add predicate if present - this filters to matching items
            if (node.Arguments.Count > 1)
       {
      var lambda = (LambdaExpression)StripQuotes(node.Arguments[1]);
          var predicate = TranslatePredicate(lambda.Body, lambda.Parameters[0].Name);
 
if (!string.IsNullOrEmpty(predicate))
      {
     _gremlinQuery.Append(predicate);
      }
            }
      
            // Add count to count the filtered results
            // The provider will compare this to the total count
     _gremlinQuery.Append(".count()");

       return node;
        }

        // Graph traversal methods
        private Expression VisitOut(MethodCallExpression node)
        {
    // Visit source
Visit(node.Arguments[0]);

            // Extract edge label from lambda
            var lambda = (LambdaExpression)StripQuotes(node.Arguments[1]);
    var edgeLabel = GetEdgeLabelFromExpression(lambda.Body);
            
     if (!string.IsNullOrEmpty(edgeLabel))
   {
  _gremlinQuery.Append($".out('{edgeLabel}')");
       }
        else
            {
                _gremlinQuery.Append(".out()");

        }

            // Update element type - for Out<TSource, TTarget>, we want TTarget (index 1)
            if (node.Method.IsGenericMethod)
            {
     var genericArgs = node.Method.GetGenericArguments();
      if (genericArgs.Length >= 2)
      {
          _elementType = genericArgs[1]; // TTarget
   }
            }

          return node;
        }

        private Expression VisitIn(MethodCallExpression node)
  {
    // Visit source
       Visit(node.Arguments[0]);

   // Extract edge label from lambda
    var lambda = (LambdaExpression)StripQuotes(node.Arguments[1]);
      var edgeLabel = GetEdgeLabelFromExpression(lambda.Body);
         
            if (!string.IsNullOrEmpty(edgeLabel))
      {
                _gremlinQuery.Append($".in('{edgeLabel}')");
 }
            else
     {
       _gremlinQuery.Append(".in()");
            }

       // Update element type - for In<TSource, TTarget>, we want TSource (index 0)
     if (node.Method.IsGenericMethod)
    {
 var genericArgs = node.Method.GetGenericArguments();
     if (genericArgs.Length >= 2)
        {
        _elementType = genericArgs[0]; // TSource
        }
 }

            return node;
        }

        private Expression VisitOutE(MethodCallExpression node)
        {
// Visit source
            Visit(node.Arguments[0]);

  // Extract edge label
   var lambda = (LambdaExpression)StripQuotes(node.Arguments[1]);
            var edgeLabel = GetEdgeLabelFromExpression(lambda.Body);
            
            if (!string.IsNullOrEmpty(edgeLabel))
         {
          _gremlinQuery.Append($".outE('{edgeLabel}')");
            }
            else
            {
        _gremlinQuery.Append(".outE()");
            }

      // Update element type to edge type
    if (node.Method.IsGenericMethod)
            {
     var genericArgs = node.Method.GetGenericArguments();
       if (genericArgs.Length >= 3)
       {
          _elementType = genericArgs[2]; // TEdge
     }
   }

            return node;
   }

      private Expression VisitInE(MethodCallExpression node)
   {
   // Visit source
            Visit(node.Arguments[0]);

            // Extract edge label
         var lambda = (LambdaExpression)StripQuotes(node.Arguments[1]);
       var edgeLabel = GetEdgeLabelFromExpression(lambda.Body);
      
            if (!string.IsNullOrEmpty(edgeLabel))
            {
       _gremlinQuery.Append($".inE('{edgeLabel}')");
            }
else
     {
       _gremlinQuery.Append(".inE()");
   }

      // Update element type to edge type
   if (node.Method.IsGenericMethod)
   {
                var genericArgs = node.Method.GetGenericArguments();
   if (genericArgs.Length >= 3)
    {
          _elementType = genericArgs[2]; // TEdge
        }
       }

            return node;
  }

 private Expression VisitBoth(MethodCallExpression node)
   {
            Visit(node.Arguments[0]);

      var lambda = (LambdaExpression)StripQuotes(node.Arguments[1]);
            var edgeLabel = GetEdgeLabelFromExpression(lambda.Body);
            
     if (!string.IsNullOrEmpty(edgeLabel))
            {
    _gremlinQuery.Append($".both('{edgeLabel}')");
            }
     else
            {
         _gremlinQuery.Append(".both()");
       }

        // Update element type - for Both<TSource, TTarget>, we want TTarget (index 1)
            if (node.Method.IsGenericMethod)
 {
      var genericArgs = node.Method.GetGenericArguments();
          if (genericArgs.Length >= 2)
            {
        _elementType = genericArgs[1]; // TTarget
           }
}

            return node;
        }

        private Expression VisitBothE(MethodCallExpression node)
  {
       Visit(node.Arguments[0]);

            var lambda = (LambdaExpression)StripQuotes(node.Arguments[1]);
   var edgeLabel = GetEdgeLabelFromExpression(lambda.Body);
    
            if (!string.IsNullOrEmpty(edgeLabel))
  {
         _gremlinQuery.Append($".bothE('{edgeLabel}')");
      }
            else
       {
    _gremlinQuery.Append(".bothE()");
            }

      if (node.Method.IsGenericMethod)
     {
                var genericArgs = node.Method.GetGenericArguments();
     if (genericArgs.Length >= 3)
                {
_elementType = genericArgs[2]; // TEdge
     }
            }

        return node;
        }

        private Expression VisitOutV(MethodCallExpression node)
        {
         Visit(node.Arguments[0]);
_gremlinQuery.Append(".outV()");

            if (node.Method.IsGenericMethod)
{
              var genericArgs = node.Method.GetGenericArguments();
        if (genericArgs.Length >= 2)
  {
           _elementType = genericArgs[1]; // TVertex
}
         }

 return node;
        }

        private Expression VisitInV(MethodCallExpression node)
        {
       Visit(node.Arguments[0]);
          _gremlinQuery.Append(".inV()");

            if (node.Method.IsGenericMethod)
            {
       var genericArgs = node.Method.GetGenericArguments();
      if (genericArgs.Length >= 2)
                {
          _elementType = genericArgs[1]; // TVertex
       }
       }

            return node;
        }

        private Expression VisitBothV(MethodCallExpression node)
        {
            Visit(node.Arguments[0]);
            _gremlinQuery.Append(".bothV()");

if (node.Method.IsGenericMethod)
            {
              var genericArgs = node.Method.GetGenericArguments();
  if (genericArgs.Length >= 2)
                {
           _elementType = genericArgs[1]; // TVertex
       }
 }

            return node;
        }

        private Expression VisitOtherV(MethodCallExpression node)
      {
          Visit(node.Arguments[0]);
   _gremlinQuery.Append(".otherV()");

            if (node.Method.IsGenericMethod)
{
     var genericArgs = node.Method.GetGenericArguments();
         if (genericArgs.Length >= 2)
          {
         _elementType = genericArgs[1]; // TVertex
        }
            }

         return node;
        }

        private string TranslatePredicate(Expression expression, string parameterName)
        {
    switch (expression.NodeType)
         {
    case ExpressionType.Constant:
    // Handle constant boolean values (e.g., Where(p => true) or Where(p => false))
 if (expression is ConstantExpression constantExpr && constantExpr.Type == typeof(bool))
 {
          var boolValue = (bool)constantExpr.Value;
     if (!boolValue)
    {
           // false: Filter out everything with a condition that never matches
       // Use an impossible condition
          return ".has('__impossible__', '__never__')";
          }
   // true: Don't add any filter (matches everything)
        return string.Empty;
      }
    return string.Empty;
    case ExpressionType.Equal:
   return TranslateEquality((BinaryExpression)expression, "eq");
    case ExpressionType.NotEqual:
  return TranslateEquality((BinaryExpression)expression, "neq");
          case ExpressionType.GreaterThan:
    return TranslateComparison((BinaryExpression)expression, "gt");
case ExpressionType.GreaterThanOrEqual:
       return TranslateComparison((BinaryExpression)expression, "gte");
        case ExpressionType.LessThan:
        return TranslateComparison((BinaryExpression)expression, "lt");
     case ExpressionType.LessThanOrEqual:
  return TranslateComparison((BinaryExpression)expression, "lte");
            case ExpressionType.AndAlso:
 return TranslateBinaryLogical((BinaryExpression)expression, parameterName);
        case ExpressionType.OrElse:
 return TranslateBinaryLogical((BinaryExpression)expression, parameterName);
       case ExpressionType.Not:
   return TranslateNot((UnaryExpression)expression, parameterName);
       case ExpressionType.Call:
    return TranslateMethodCall((MethodCallExpression)expression);
           case ExpressionType.MemberAccess:
      // Handle direct boolean member access (e.g., p => p.IsActive)
      if (expression is MemberExpression memberExpr && memberExpr.Type == typeof(bool))
           {
   var propertyName = memberExpr.Member.Name;
   return $".has('{ToCamelCase(propertyName)}', eq(true))";
         }
 return string.Empty;
 default:
          return string.Empty;
     }
        }

     private string TranslateEquality(BinaryExpression binary, string op)
        {
            var propertyName = GetPropertyName(binary.Left);
            var value = GetValue(binary.Right);
        
   if (propertyName != null)
    {
   // Handle null comparisons specially
     if (value == null)
   {
     // p.Name == null should check if property does not exist
     // p.Name != null should check if property does exist
      if (op == "eq")
         {
   // For equality with null, check if property does not exist
   return $".hasNot('{ToCamelCase(propertyName)}')";
       }
    else if (op == "neq")
        {
    // For inequality with null, filter to elements that have this property
    return $".has('{ToCamelCase(propertyName)}')";
       }
      }
        else
   {
 // Normal value comparison
 return $".has('{ToCamelCase(propertyName)}', {op}({FormatValue(value)}))";
 }
    }

   return string.Empty;
  }

    private string TranslateComparison(BinaryExpression binary, string op)
        {
            var propertyName = GetPropertyName(binary.Left);
     var value = GetValue(binary.Right);
            
            if (propertyName != null && value != null)
   {
            return $".has('{ToCamelCase(propertyName)}', {op}({FormatValue(value)}))";
   }

            return string.Empty;
        }

        private string TranslateBinaryLogical(BinaryExpression binary, string parameterName)
        {
     var left = TranslatePredicate(binary.Left, parameterName);
   var right = TranslatePredicate(binary.Right, parameterName);
    
    // For AndAlso, just concatenate the predicates
  if (binary.NodeType == ExpressionType.AndAlso)
     {
 return left + right;
      }
       // For OrElse, collect all OR conditions and flatten them
      else if (binary.NodeType == ExpressionType.OrElse)
 {
      // Collect all OR conditions into a flat list
       var conditions = new List<string>();
 CollectOrConditions(binary, conditions);
    
     if (conditions.Count > 0)
       {
           // Create a single flat or() statement with all conditions
    return $".where(or({string.Join(", ", conditions)}))";
         }
       // Fallback if extraction fails
          return left + right;
  }

         return string.Empty;
   }

        /// <summary>
        /// Recursively collects all OR conditions from a binary expression tree into a flat list
        /// </summary>
        private void CollectOrConditions(Expression expression, List<string> conditions)
    {
if (expression is BinaryExpression binaryExpr && binaryExpr.NodeType == ExpressionType.OrElse)
      {
    // Recursively process left side
         CollectOrConditions(binaryExpr.Left, conditions);
       // Recursively process right side
                CollectOrConditions(binaryExpr.Right, conditions);
    }
       else
      {
   // This is a leaf condition (not an OR expression)
                var condition = ExtractConditionForOr(expression);
          if (!string.IsNullOrEmpty(condition))
           {
        conditions.Add(condition);
    }
            }
        }

        private string ExtractConditionForOr(Expression expression)
  {
  // For non-OR expressions, translate the predicate normally
     var predicate = TranslatePredicate(expression, null);

  if (string.IsNullOrEmpty(predicate))
     return string.Empty;
 
 // Remove leading dot and any .where() wrapper
  if (predicate.StartsWith(".where(or(") && predicate.EndsWith("))"))
    {
     // This shouldn't happen with the new CollectOrConditions, but handle it anyway
      predicate = predicate.Substring(".where(or(".Length, predicate.Length - ".where(or(".Length - 2);
    }
   else if (predicate.StartsWith("."))
{
   predicate = predicate.Substring(1);
  }
     
 return predicate;
        }

   private string TranslateNot(UnaryExpression unary, string parameterName)
        {
 // Handle simple negation of boolean properties
    if (unary.Operand is MemberExpression memberExpr)
            {
        var propertyName = memberExpr.Member.Name;
          return $".has('{ToCamelCase(propertyName)}', eq(false))";
     }

     var inner = TranslatePredicate(unary.Operand, parameterName);
      // Would need .not() step wrapping - simplified for now
      return inner;
     }

     private string TranslateMethodCall(MethodCallExpression call)
     {
      if (call.Method.DeclaringType == typeof(string))
  {
    switch (call.Method.Name)
      {
   case "Contains":
  {
             var propertyName = GetPropertyName(call.Object);
    var value = GetValue(call.Arguments[0]);
        return $".has('{ToCamelCase(propertyName)}', containing({FormatValue(value)}))";
 }
         case "StartsWith":
     {
   var propertyName = GetPropertyName(call.Object);
  var value = GetValue(call.Arguments[0]);
       return $".has('{ToCamelCase(propertyName)}', startingWith({FormatValue(value)}))";
          }
      case "EndsWith":
    {
  var propertyName = GetPropertyName(call.Object);
     var value = GetValue(call.Arguments[0]);
        return $".has('{ToCamelCase(propertyName)}', endingWith({FormatValue(value)}))";
      }
 }
          }

      return string.Empty;
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

 private object GetValue(Expression expression)
    {
         if (expression is ConstantExpression constantExpr)
 {
return constantExpr.Value;
 }
  else if (expression is MemberExpression memberExpr)
   {
       // Evaluate the member expression
   var objectMember = Expression.Convert(memberExpr, typeof(object));
       var getterLambda = Expression.Lambda<Func<object>>(objectMember);
 var getter = getterLambda.Compile();
    return getter();
   }
 else if (expression is UnaryExpression unaryExpr)
 {
         return GetValue(unaryExpr.Operand);
      }
            else if (expression is BinaryExpression binaryExpr)
     {
     // Handle binary expressions like baseAge + offset
   try
   {
       // Compile and evaluate the binary expression
  var lambda = Expression.Lambda<Func<object>>(
          Expression.Convert(binaryExpr, typeof(object)));
  var compiled = lambda.Compile();
return compiled();
   }
   catch
    {
            // If compilation fails, return null
     return null;
      }
            }

      return null;
    }

 private string GetEdgeLabelFromExpression(Expression expression)
      {
     // Extract property name from p => p.Companies
    if (expression is MemberExpression memberExpr)
      {
   // Convert property name to camelCase for edge label
         return ToCamelCase(memberExpr.Member.Name);
   }

   return null;
        }

        private string FormatValue(object value)
   {
            if (value == null)
      return "null";
 
       if (value is string str)
          return $"'{str.Replace("'", "\\'")}'";
  
      if (value is bool b)
                return b.ToString().ToLowerInvariant();
  
 if (value is decimal || value is double || value is float)
     return Convert.ToDouble(value).ToString(System.Globalization.CultureInfo.InvariantCulture);
 
  return value.ToString();
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

  protected override Expression VisitConstant(ConstantExpression node)
    {
  // Check if this is a GraphQueryable
         if (node.Value != null && node.Value.GetType().IsGenericType &&
           node.Value.GetType().GetGenericTypeDefinition() == typeof(GraphQueryable<>))
      {
   // Extract element type
     _elementType = node.Type.GetGenericArguments()[0];
    }

          return node;
        }
    }
}
