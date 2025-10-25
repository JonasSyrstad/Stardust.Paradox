using System;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Stardust.Paradox.Data.Annotations;

namespace Stardust.Paradox.Data.Linq
{
    /// <summary>
    /// Extension methods for graph traversal operations on IQueryable
    /// </summary>
    public static class GraphTraversalExtensions
    {
 /// <summary>
        /// Traverse outgoing edges to target vertices
  /// </summary>
        public static IQueryable<TTarget> Out<TSource, TTarget>(
    this IQueryable<TSource> source,
 Expression<Func<TSource, IEdgeCollection<TTarget>>> edgeSelector)
            where TSource : IVertex
    where TTarget : IVertex
   {
          if (source == null) throw new ArgumentNullException(nameof(source));
        if (edgeSelector == null) throw new ArgumentNullException(nameof(edgeSelector));

            var method = typeof(GraphTraversalExtensions).GetMethod(nameof(Out), 
     BindingFlags.Public | BindingFlags.Static);
       var genericMethod = method.MakeGenericMethod(typeof(TSource), typeof(TTarget));

   return source.Provider.CreateQuery<TTarget>(
  Expression.Call(
              null,
        genericMethod,
  source.Expression,
    Expression.Quote(edgeSelector)));
        }

 /// <summary>
        /// Traverse incoming edges from source vertices
        /// </summary>
        public static IQueryable<TSource> In<TSource, TTarget>(
  this IQueryable<TTarget> source,
 Expression<Func<TTarget, IEdgeCollection<TSource>>> edgeSelector)
            where TSource : IVertex
            where TTarget : IVertex
        {
     if (source == null) throw new ArgumentNullException(nameof(source));
    if (edgeSelector == null) throw new ArgumentNullException(nameof(edgeSelector));

var method = typeof(GraphTraversalExtensions).GetMethod(nameof(In), 
        BindingFlags.Public | BindingFlags.Static);
 var genericMethod = method.MakeGenericMethod(typeof(TSource), typeof(TTarget));

    return source.Provider.CreateQuery<TSource>(
   Expression.Call(
  null,
 genericMethod,
      source.Expression,
     Expression.Quote(edgeSelector)));
        }

        /// <summary>
    /// Traverse outgoing edges and return the edges
        /// </summary>
   public static IQueryable<TEdge> OutE<TSource, TTarget, TEdge>(
            this IQueryable<TSource> source,
  Expression<Func<TSource, IEdgeCollection<TTarget>>> edgeSelector)
 where TSource : IVertex
where TTarget : IVertex
         where TEdge : IEdgeEntity
        {
      if (source == null) throw new ArgumentNullException(nameof(source));
      if (edgeSelector == null) throw new ArgumentNullException(nameof(edgeSelector));

     var method = typeof(GraphTraversalExtensions).GetMethod(nameof(OutE), 
     BindingFlags.Public | BindingFlags.Static);
   var genericMethod = method.MakeGenericMethod(typeof(TSource), typeof(TTarget), typeof(TEdge));

  return source.Provider.CreateQuery<TEdge>(
   Expression.Call(
      null,
           genericMethod,
      source.Expression,
 Expression.Quote(edgeSelector)));
        }

  /// <summary>
      /// Traverse incoming edges and return the edges
        /// </summary>
        public static IQueryable<TEdge> InE<TSource, TTarget, TEdge>(
      this IQueryable<TSource> source,
 Expression<Func<TSource, IEdgeCollection<TTarget>>> edgeSelector)
  where TSource : IVertex
    where TTarget : IVertex
  where TEdge : IEdgeEntity
     {
  if (source == null) throw new ArgumentNullException(nameof(source));
    if (edgeSelector == null) throw new ArgumentNullException(nameof(edgeSelector));

            var method = typeof(GraphTraversalExtensions).GetMethod(nameof(InE), 
       BindingFlags.Public | BindingFlags.Static);
var genericMethod = method.MakeGenericMethod(typeof(TSource), typeof(TTarget), typeof(TEdge));

        return source.Provider.CreateQuery<TEdge>(
    Expression.Call(
        null,
       genericMethod,
    source.Expression,
           Expression.Quote(edgeSelector)));
        }

        /// <summary>
    /// Traverse edges in both directions
        /// </summary>
 public static IQueryable<TTarget> Both<TSource, TTarget>(
 this IQueryable<TSource> source,
Expression<Func<TSource, IEdgeCollection<TTarget>>> edgeSelector)
     where TSource : IVertex
  where TTarget : IVertex
      {
         if (source == null) throw new ArgumentNullException(nameof(source));
            if (edgeSelector == null) throw new ArgumentNullException(nameof(edgeSelector));

      var method = typeof(GraphTraversalExtensions).GetMethod(nameof(Both), 
          BindingFlags.Public | BindingFlags.Static);
   var genericMethod = method.MakeGenericMethod(typeof(TSource), typeof(TTarget));

      return source.Provider.CreateQuery<TTarget>(
   Expression.Call(
          null,
        genericMethod,
   source.Expression,
      Expression.Quote(edgeSelector)));
        }

     /// <summary>
        /// Traverse edges in both directions and return the edges
        /// </summary>
     public static IQueryable<TEdge> BothE<TSource, TTarget, TEdge>(
       this IQueryable<TSource> source,
 Expression<Func<TSource, IEdgeCollection<TTarget>>> edgeSelector)
      where TSource : IVertex
where TTarget : IVertex
        where TEdge : IEdgeEntity
        {
if (source == null) throw new ArgumentNullException(nameof(source));
         if (edgeSelector == null) throw new ArgumentNullException(nameof(edgeSelector));

 var method = typeof(GraphTraversalExtensions).GetMethod(nameof(BothE), 
 BindingFlags.Public | BindingFlags.Static);
   var genericMethod = method.MakeGenericMethod(typeof(TSource), typeof(TTarget), typeof(TEdge));

            return source.Provider.CreateQuery<TEdge>(
         Expression.Call(
                null,
       genericMethod,
         source.Expression,
  Expression.Quote(edgeSelector)));
   }

   /// <summary>
 /// From an edge, traverse to the outgoing vertex
        /// </summary>
        public static IQueryable<TVertex> OutV<TEdge, TVertex>(
      this IQueryable<TEdge> source)
   where TEdge : IEdgeEntity
          where TVertex : IVertex
        {
     if (source == null) throw new ArgumentNullException(nameof(source));

 var method = typeof(GraphTraversalExtensions).GetMethod(nameof(OutV), 
         BindingFlags.Public | BindingFlags.Static);
   var genericMethod = method.MakeGenericMethod(typeof(TEdge), typeof(TVertex));

     return source.Provider.CreateQuery<TVertex>(
        Expression.Call(
        null,
 genericMethod,
        source.Expression));
  }

        /// <summary>
        /// From an edge, traverse to the incoming vertex
        /// </summary>
        public static IQueryable<TVertex> InV<TEdge, TVertex>(
   this IQueryable<TEdge> source)
    where TEdge : IEdgeEntity
where TVertex : IVertex
 {
      if (source == null) throw new ArgumentNullException(nameof(source));

       var method = typeof(GraphTraversalExtensions).GetMethod(nameof(InV), 
         BindingFlags.Public | BindingFlags.Static);
       var genericMethod = method.MakeGenericMethod(typeof(TEdge), typeof(TVertex));

      return source.Provider.CreateQuery<TVertex>(
Expression.Call(
      null,
        genericMethod,
        source.Expression));
     }

  /// <summary>
/// From an edge, traverse to both vertices
 /// </summary>
    public static IQueryable<TVertex> BothV<TEdge, TVertex>(
 this IQueryable<TEdge> source)
     where TEdge : IEdgeEntity
 where TVertex : IVertex
        {
        if (source == null) throw new ArgumentNullException(nameof(source));

  var method = typeof(GraphTraversalExtensions).GetMethod(nameof(BothV), 
      BindingFlags.Public | BindingFlags.Static);
 var genericMethod = method.MakeGenericMethod(typeof(TEdge), typeof(TVertex));

  return source.Provider.CreateQuery<TVertex>(
    Expression.Call(
        null,
 genericMethod,
          source.Expression));
    }

        /// <summary>
        /// From an edge, traverse to the other vertex (opposite of the source)
        /// </summary>
        public static IQueryable<TVertex> OtherV<TEdge, TVertex>(
    this IQueryable<TEdge> source)
            where TEdge : IEdgeEntity
  where TVertex : IVertex
        {
       if (source == null) throw new ArgumentNullException(nameof(source));

        var method = typeof(GraphTraversalExtensions).GetMethod(nameof(OtherV), 
    BindingFlags.Public | BindingFlags.Static);
   var genericMethod = method.MakeGenericMethod(typeof(TEdge), typeof(TVertex));

    return source.Provider.CreateQuery<TVertex>(
    Expression.Call(
   null,
  genericMethod,
source.Expression));
  }
    }
}
