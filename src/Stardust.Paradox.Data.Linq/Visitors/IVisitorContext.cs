using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;

namespace Stardust.Paradox.Data.Linq.Visitors
{
    /// <summary>
    /// Context interface that provides state and helper methods to expression visitors
    /// </summary>
    public interface IVisitorContext
    {
      /// <summary>
      /// The label for the graph element type being queried
        /// </summary>
        string Label { get; }

        /// <summary>
     /// The Gremlin query string builder
        /// </summary>
     StringBuilder GremlinQuery { get; }

 /// <summary>
        /// The element type of the query result
        /// </summary>
    Type ElementType { get; set; }

        /// <summary>
        /// Parameters used in the query
    /// </summary>
        Dictionary<string, object> Parameters { get; }

        /// <summary>
        /// Indicates if the query is a count query
        /// </summary>
     bool IsCountQuery { get; set; }

        /// <summary>
        /// Indicates if the query is a long count query
 /// </summary>
     bool IsLongCountQuery { get; set; }

        /// <summary>
        /// Indicates if the query is an any query
  /// </summary>
        bool IsAnyQuery { get; set; }

     /// <summary>
        /// Indicates if the query is a first query
        /// </summary>
bool IsFirstQuery { get; set; }

        /// <summary>
        /// Indicates if the query uses FirstOrDefault
  /// </summary>
    bool UseFirstOrDefault { get; set; }

        /// <summary>
  /// Indicates if the query is a single query
      /// </summary>
        bool IsSingleQuery { get; set; }

    /// <summary>
        /// Indicates if the query uses SingleOrDefault
        /// </summary>
      bool UseSingleOrDefault { get; set; }

   /// <summary>
        /// Indicates if the query is a distinct query
  /// </summary>
        bool IsDistinctQuery { get; set; }

        /// <summary>
        /// Indicates if the query is an all query
        /// </summary>
        bool IsAllQuery { get; set; }

        /// <summary>
        /// Indicates if the query is a sum query
        /// </summary>
        bool IsSumQuery { get; set; }

   /// <summary>
     /// Indicates if the query is an average query
        /// </summary>
        bool IsAverageQuery { get; set; }

     /// <summary>
        /// Indicates if the query is a min query
        /// </summary>
        bool IsMinQuery { get; set; }

 /// <summary>
     /// Indicates if the query is a max query
        /// </summary>
        bool IsMaxQuery { get; set; }

        /// <summary>
        /// Indicates if the query is a group by query
        /// </summary>
        bool IsGroupByQuery { get; set; }

        /// <summary>
    /// Indicates if client-side projection is required
   /// </summary>
        bool RequiresClientSideProjection { get; set; }

        /// <summary>
     /// The lambda expression for client-side projection
        /// </summary>
 LambdaExpression ClientSideProjection { get; set; }

        /// <summary>
        /// Converts a property name to camel case
   /// </summary>
        string ToCamelCase(string name);

      /// <summary>
    /// Translates a predicate expression to a Gremlin query fragment
        /// </summary>
     string TranslatePredicate(Expression expression, string parameterName);

        /// <summary>
        /// Gets the property name from an expression
        /// </summary>
     string GetPropertyName(Expression expression);

        /// <summary>
        /// Gets the value from an expression
     /// </summary>
 object GetValue(Expression expression);

        /// <summary>
        /// Formats a value with parameterization
        /// </summary>
        string FormatValueWithParameterization(object value);

        /// <summary>
  /// Strips quote expressions from an expression
        /// </summary>
    Expression StripQuotes(Expression expression);

        /// <summary>
 /// Visits a child expression using the translator's Visit method
        /// </summary>
        Expression VisitExpression(Expression expression);

        /// <summary>
        /// Gets the In-edge label for a navigation property (from fluent config or attributes)
        /// </summary>
        string GetInEdgeLabel(Type entityType, MemberInfo member);

        /// <summary>
        /// Gets the Out-edge label for a navigation property (from fluent config or attributes)
        /// </summary>
        string GetOutEdgeLabel(Type entityType, MemberInfo member);

        /// <summary>
      /// Gets any edge label (In or Out) for a navigation property
        /// </summary>
  string GetAnyEdgeLabel(Type entityType, MemberInfo member);
    }
}
