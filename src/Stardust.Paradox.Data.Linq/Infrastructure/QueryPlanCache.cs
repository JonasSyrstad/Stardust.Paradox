using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace Stardust.Paradox.Data.Linq.Infrastructure
{
    /// <summary>
    /// Represents a cached query translation plan
    /// </summary>
    internal class CachedQueryPlan
    {
   public string GremlinQuery { get; set; }
        public Type ElementType { get; set; }
        public bool IsCountQuery { get; set; }
  public bool IsLongCountQuery { get; set; }
        public bool IsAnyQuery { get; set; }
        public bool IsFirstQuery { get; set; }
        public bool IsSingleQuery { get; set; }
        public bool UseFirstOrDefault { get; set; }
        public bool UseSingleOrDefault { get; set; }
     public bool IsDistinctQuery { get; set; }
      public bool IsAllQuery { get; set; }
        public bool IsSumQuery { get; set; }
  public bool IsAverageQuery { get; set; }
        public bool IsMinQuery { get; set; }
        public bool IsMaxQuery { get; set; }
        public bool IsGroupByQuery { get; set; }
 public bool RequiresClientSideProjection { get; set; }
        public LambdaExpression ClientSideProjection { get; set; }
        public List<string> ParameterNames { get; set; }
    }

    /// <summary>
    /// Computes hash codes for expression trees for caching purposes
    /// </summary>
    internal static class ExpressionHasher
    {
    public static int GetHashCode(Expression expression)
        {
   if (expression == null)
      return 0;

            var visitor = new HashVisitor();
   visitor.Visit(expression);
 return visitor.HashCode;
 }

    private class HashVisitor : ExpressionVisitor
        {
  private int _hash = 17;

   public int HashCode => _hash;

            protected override Expression VisitConstant(ConstantExpression node)
            {
  // Skip GraphQueryable constants as they vary per instance
  if (node.Value != null && node.Value.GetType().Name.Contains("GraphQueryable"))
{
            _hash = (_hash * 31) + node.Type.GetHashCode();
     }
           else if (node.Value != null)
      {
         _hash = (_hash * 31) + node.Value.GetHashCode();
   }
     _hash = (_hash * 31) + node.Type.GetHashCode();
      return base.VisitConstant(node);
   }

            protected override Expression VisitMethodCall(MethodCallExpression node)
 {
       _hash = (_hash * 31) + node.Method.GetHashCode();
    if (node.Arguments != null)
       {
       _hash = (_hash * 31) + node.Arguments.Count;
                }
      return base.VisitMethodCall(node);
            }

protected override Expression VisitMember(MemberExpression node)
    {
       _hash = (_hash * 31) + node.Member.GetHashCode();
     return base.VisitMember(node);
          }

       protected override Expression VisitBinary(BinaryExpression node)
        {
  _hash = (_hash * 31) + node.NodeType.GetHashCode();
           return base.VisitBinary(node);
      }

 protected override Expression VisitUnary(UnaryExpression node)
            {
        _hash = (_hash * 31) + node.NodeType.GetHashCode();
      return base.VisitUnary(node);
     }

        protected override Expression VisitLambda<T>(Expression<T> node)
     {
  if (node.Parameters != null)
       {
      _hash = (_hash * 31) + node.Parameters.Count;
    }
        return base.VisitLambda(node);
        }
   }
    }
}
