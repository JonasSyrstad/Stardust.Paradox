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
        /// Traverse outgoing edges and return the edges (simple overload returning IEdge)
   /// </summary>
        public static IQueryable<IEdge<TTarget>> OutE<TSource, TTarget>(
      this IQueryable<TSource> source,
         Expression<Func<TSource, IEdgeCollection<TTarget>>> edgeSelector)
          where TSource : IVertex
     where TTarget : IVertex
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (edgeSelector == null) throw new ArgumentNullException(nameof(edgeSelector));

    var method = typeof(GraphTraversalExtensions)
     .GetMethods(BindingFlags.Public | BindingFlags.Static)
 .Where(m => m.Name == nameof(OutE) && m.GetGenericArguments().Length == 2)
            .First();
      var genericMethod = method.MakeGenericMethod(typeof(TSource), typeof(TTarget));

  return source.Provider.CreateQuery<IEdge<TTarget>>(
             Expression.Call(
            null,
   genericMethod,
         source.Expression,
          Expression.Quote(edgeSelector)));
        }

        /// <summary>
     /// Traverse incoming edges and return the edges (simple overload returning IEdge)
        /// </summary>
        public static IQueryable<IEdge<TSource>> InE<TSource, TTarget>(
   this IQueryable<TTarget> source,
      Expression<Func<TTarget, IEdgeCollection<TSource>>> edgeSelector)
  where TSource : IVertex
    where TTarget : IVertex
        {
     if (source == null) throw new ArgumentNullException(nameof(source));
 if (edgeSelector == null) throw new ArgumentNullException(nameof(edgeSelector));

       var method = typeof(GraphTraversalExtensions)
     .GetMethods(BindingFlags.Public | BindingFlags.Static)
  .Where(m => m.Name == nameof(InE) && m.GetGenericArguments().Length == 2)
            .First();
     var genericMethod = method.MakeGenericMethod(typeof(TSource), typeof(TTarget));

         return source.Provider.CreateQuery<IEdge<TSource>>(
              Expression.Call(
          null,
      genericMethod,
           source.Expression,
         Expression.Quote(edgeSelector)));
        }

    /// <summary>
    /// Traverse outgoing edges and return the edges (typed edge overload)
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

     var method = typeof(GraphTraversalExtensions)
         .GetMethods(BindingFlags.Public | BindingFlags.Static)
     .Where(m => m.Name == nameof(OutE) && m.GetGenericArguments().Length == 3)
           .First();
   var genericMethod = method.MakeGenericMethod(typeof(TSource), typeof(TTarget), typeof(TEdge));

  return source.Provider.CreateQuery<TEdge>(
   Expression.Call(
      null,
           genericMethod,
      source.Expression,
 Expression.Quote(edgeSelector)));
      }

  /// <summary>
      /// Traverse incoming edges and return the edges (typed edge overload)
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

       var method = typeof(GraphTraversalExtensions)
        .GetMethods(BindingFlags.Public | BindingFlags.Static)
        .Where(m => m.Name == nameof(InE) && m.GetGenericArguments().Length == 3)
                .First();
var genericMethod = method.MakeGenericMethod(typeof(TSource), typeof(TTarget), typeof(TEdge));

     return source.Provider.CreateQuery<TEdge>(
    Expression.Call(
null,
       genericMethod,
    source.Expression,
     Expression.Quote(edgeSelector)));
        }

        /// <summary>
     /// Traverse outgoing edges by edge type (no lambda, just edge type)
        /// </summary>
        public static IQueryable<TEdge> OutE<TEdge>(this IQueryable<IVertex> source)
        where TEdge : IEdgeEntity
     {
    if (source == null) throw new ArgumentNullException(nameof(source));

     var method = typeof(GraphTraversalExtensions)
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
       .Where(m => m.Name == nameof(OutE) && m.GetGenericArguments().Length == 1)
     .First();
            var genericMethod = method.MakeGenericMethod(typeof(TEdge));

     return source.Provider.CreateQuery<TEdge>(
       Expression.Call(
                 null,
      genericMethod,
          source.Expression));
        }

        /// <summary>
    /// Traverse incoming edges by edge type (no lambda, just edge type)
        /// </summary>
public static IQueryable<TEdge> InE<TEdge>(this IQueryable<IVertex> source)
            where TEdge : IEdgeEntity
        {
    if (source == null) throw new ArgumentNullException(nameof(source));

            var method = typeof(GraphTraversalExtensions)
        .GetMethods(BindingFlags.Public | BindingFlags.Static)
           .Where(m => m.Name == nameof(InE) && m.GetGenericArguments().Length == 1)
     .First();
      var genericMethod = method.MakeGenericMethod(typeof(TEdge));

        return source.Provider.CreateQuery<TEdge>(
          Expression.Call(
      null,
        genericMethod,
        source.Expression));
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
      /// Repeat a traversal pattern for a graph recursion
        /// </summary>
     public static IQueryable<T> Repeat<T>(
            this IQueryable<T> source,
            Expression<Func<IQueryable<T>, IQueryable<T>>> repeatTraversal)
            where T : IVertex
{
  if (source == null) throw new ArgumentNullException(nameof(source));
      if (repeatTraversal == null) throw new ArgumentNullException(nameof(repeatTraversal));

   var method = typeof(GraphTraversalExtensions).GetMethod(nameof(Repeat),
   BindingFlags.Public | BindingFlags.Static);
     var genericMethod = method.MakeGenericMethod(typeof(T));

        return source.Provider.CreateQuery<T>(
        Expression.Call(
  null,
          genericMethod,
             source.Expression,
           Expression.Quote(repeatTraversal)));
}

        /// <summary>
      /// Specify termination condition for repeat traversal
        /// </summary>
        public static IQueryable<T> Until<T>(
  this IQueryable<T> source,
  Expression<Func<T, bool>> predicate)
  where T : IVertex
    {
  if (source == null) throw new ArgumentNullException(nameof(source));
     if (predicate == null) throw new ArgumentNullException(nameof(predicate));

    var method = typeof(GraphTraversalExtensions).GetMethod(nameof(Until),
     BindingFlags.Public | BindingFlags.Static);
            var genericMethod = method.MakeGenericMethod(typeof(T));

      return source.Provider.CreateQuery<T>(
       Expression.Call(
    null,
       genericMethod,
    source.Expression,
   Expression.Quote(predicate)));
        }

 /// <summary>
   /// Emit intermediate results during repeat traversal
        /// </summary>
        public static IQueryable<T> Emit<T>(this IQueryable<T> source)
    where T : IVertex
      {
  if (source == null) throw new ArgumentNullException(nameof(source));

  var methods = typeof(GraphTraversalExtensions).GetMethods(BindingFlags.Public | BindingFlags.Static)
.Where(m => m.Name == nameof(Emit) && m.GetParameters().Length == 1)
     .ToArray();
          if (methods.Length != 1)
    throw new InvalidOperationException($"Expected 1 Emit method with 1 parameter, found {methods.Length}");
 
    var method = methods[0];
    var genericMethod = method.MakeGenericMethod(typeof(T));

  return source.Provider.CreateQuery<T>(
     Expression.Call(
   null,
      genericMethod,
        source.Expression));
      }

   /// <summary>
      /// Emit intermediate results that match a predicate during repeat traversal
  /// </summary>
        public static IQueryable<T> Emit<T>(
         this IQueryable<T> source,
Expression<Func<T, bool>> predicate)
   where T : IVertex
  {
    if (source == null) throw new ArgumentNullException(nameof(source));
            if (predicate == null) throw new ArgumentNullException(nameof(predicate));

    var methods = typeof(GraphTraversalExtensions).GetMethods(BindingFlags.Public | BindingFlags.Static)
.Where(m => m.Name == nameof(Emit) && m.GetParameters().Length == 2)
     .ToArray();
         if (methods.Length != 1)
 throw new InvalidOperationException($"Expected 1 Emit method with 2 parameters, found {methods.Length}");

    var method = methods[0];
  var genericMethod = method.MakeGenericMethod(typeof(T));

            return source.Provider.CreateQuery<T>(
       Expression.Call(
     null,
genericMethod,
source.Expression,
  Expression.Quote(predicate)));
 }

        /// <summary>
        /// Limit repeat traversal to a fixed number of iterations
/// </summary>
 public static IQueryable<T> Times<T>(
    this IQueryable<T> source,
            int iterations)
         where T : IVertex
    {
            if (source == null) throw new ArgumentNullException(nameof(source));
         if (iterations < 0) throw new ArgumentException("Iterations must be non-negative", nameof(iterations));

var method = typeof(GraphTraversalExtensions).GetMethod(nameof(Times),
       BindingFlags.Public | BindingFlags.Static);
  var genericMethod = method.MakeGenericMethod(typeof(T));

     return source.Provider.CreateQuery<T>(
 Expression.Call(
 null,
   genericMethod,
        source.Expression,
    Expression.Constant(iterations)));
        }

    /// <summary>
        /// Get current loop count in a repeat traversal
     /// </summary>
    public static IQueryable<int> Loops<T>(this IQueryable<T> source)
    where T : IVertex
{
         if (source == null) throw new ArgumentNullException(nameof(source));

   var method = typeof(GraphTraversalExtensions).GetMethod(nameof(Loops),
     BindingFlags.Public | BindingFlags.Static);
    var genericMethod = method.MakeGenericMethod(typeof(T));

          return source.Provider.CreateQuery<int>(
   Expression.Call(
    null,
        genericMethod,
      source.Expression));
        }

  /// <summary>
        /// Label the current step for later reference in select or where operations
        /// </summary>
      public static IQueryable<T> As<T>(
   this IQueryable<T> source,
    string label)
         where T : IVertex
      {
   if (source == null) throw new ArgumentNullException(nameof(source));
  if (string.IsNullOrWhiteSpace(label)) throw new ArgumentException("Label cannot be empty", nameof(label));

    var method = typeof(GraphTraversalExtensions).GetMethod(nameof(As),
       BindingFlags.Public | BindingFlags.Static);
  var genericMethod = method.MakeGenericMethod(typeof(T));

 return source.Provider.CreateQuery<T>(
                Expression.Call(
          null,
     genericMethod,
source.Expression,
         Expression.Constant(label)));
        }

    /// <summary>
        /// Retrieve the full path traversed
  /// </summary>
        public static IQueryable<IGraphPath> Path<T>(this IQueryable<T> source)
    where T : IVertex
  {
            if (source == null) throw new ArgumentNullException(nameof(source));

   var method = typeof(GraphTraversalExtensions).GetMethod(nameof(Path),
    BindingFlags.Public | BindingFlags.Static);
          var genericMethod = method.MakeGenericMethod(typeof(T));

    return source.Provider.CreateQuery<IGraphPath>(
       Expression.Call(
    null,
      genericMethod,
source.Expression));
    }

      /// <summary>
        /// Pattern matching with multiple traversal patterns
   /// </summary>
        public static IQueryable<T> Match<T>(
     this IQueryable<T> source,
 params Expression<Func<IQueryable<T>, IQueryable<object>>>[] patterns)
     where T : IVertex
   {
        if (source == null) throw new ArgumentNullException(nameof(source));
     if (patterns == null || patterns.Length == 0) 
    throw new ArgumentException("At least one pattern is required", nameof(patterns));

   var method = typeof(GraphTraversalExtensions).GetMethod(nameof(Match),
        BindingFlags.Public | BindingFlags.Static);
    var genericMethod = method.MakeGenericMethod(typeof(T));

  var patternArray = Expression.NewArrayInit(
  typeof(Expression<Func<IQueryable<T>, IQueryable<object>>>),
       patterns.Select(p => Expression.Quote(p)));

   return source.Provider.CreateQuery<T>(
Expression.Call(
    null,
    genericMethod,
      source.Expression,
   patternArray));
     }

 /// <summary>
        /// Select by label - retrieve previously labeled steps
    /// </summary>
   public static IQueryable<TResult> SelectByLabel<TSource, TResult>(
   this IQueryable<TSource> source,
    string label)
 where TSource : IVertex
where TResult : IVertex
        {
    if (source == null) throw new ArgumentNullException(nameof(source));
  if (string.IsNullOrWhiteSpace(label)) throw new ArgumentException("Label cannot be empty", nameof(label));

   var method = typeof(GraphTraversalExtensions).GetMethod(nameof(SelectByLabel),
    BindingFlags.Public | BindingFlags.Static);
 var genericMethod = method.MakeGenericMethod(typeof(TSource), typeof(TResult));

  return source.Provider.CreateQuery<TResult>(
    Expression.Call(
        null,
      genericMethod,
      source.Expression,
           Expression.Constant(label)));
      }

        /// <summary>
   /// Select multiple labels and project to an anonymous type or tuple
 /// </summary>
 public static IQueryable<TResult> SelectByLabels<TSource, TResult>(
        this IQueryable<TSource> source,
     params string[] labels)
       where TSource : IVertex
  {
       if (source == null) throw new ArgumentNullException(nameof(source));
 if (labels == null || labels.Length == 0) 
        throw new ArgumentException("At least one label is required", nameof(labels));

 var method = typeof(GraphTraversalExtensions).GetMethod(nameof(SelectByLabels),
     BindingFlags.Public | BindingFlags.Static);
       var genericMethod = method.MakeGenericMethod(typeof(TSource), typeof(TResult));

            return source.Provider.CreateQuery<TResult>(
          Expression.Call(
  null,
  genericMethod,
           source.Expression,
       Expression.Constant(labels)));
}

     /// <summary>
      /// Barrier synchronization step - ensures all traversers complete before proceeding
        /// </summary>
    public static IQueryable<T> Barrier<T>(this IQueryable<T> source)
   where T : IVertex
    {
   if (source == null) throw new ArgumentNullException(nameof(source));

    var method = typeof(GraphTraversalExtensions).GetMethod(nameof(Barrier),
   BindingFlags.Public | BindingFlags.Static);
         var genericMethod = method.MakeGenericMethod(typeof(T));

      return source.Provider.CreateQuery<T>(
         Expression.Call(
     null,
    genericMethod,
   source.Expression));
        }

        /// <summary>
        /// Local scope - execute traversal in local context
  /// </summary>
      public static IQueryable<TResult> Local<TSource, TResult>(
        this IQueryable<TSource> source,
    Expression<Func<IQueryable<TSource>, IQueryable<TResult>>> localTraversal)
        where TSource : IVertex
where TResult : IVertex
    {
       if (source == null) throw new ArgumentNullException(nameof(source));
     if (localTraversal == null) throw new ArgumentNullException(nameof(localTraversal));

          var method = typeof(GraphTraversalExtensions).GetMethod(nameof(Local),
 BindingFlags.Public | BindingFlags.Static);
 var genericMethod = method.MakeGenericMethod(typeof(TSource), typeof(TResult));

       return source.Provider.CreateQuery<TResult>(
    Expression.Call(
      null,
       genericMethod,
        source.Expression,
    Expression.Quote(localTraversal)));
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
