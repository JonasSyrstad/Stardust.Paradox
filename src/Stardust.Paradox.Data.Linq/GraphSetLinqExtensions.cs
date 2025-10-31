using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;
using Stardust.Paradox.Data.Annotations;

namespace Stardust.Paradox.Data.Linq
{
    /// <summary>
    /// Extension methods to enable LINQ queries on IGraphSet and IEdgeGraphSet
    /// </summary>
    public static class GraphSetLinqExtensions
    {
  // Provider cache to reuse providers for same entity types
     private static readonly ConcurrentDictionary<(IGraphContext, string), GremlinQueryProvider> _providerCache =
  new ConcurrentDictionary<(IGraphContext, string), GremlinQueryProvider>(
      concurrencyLevel: Environment.ProcessorCount,
   capacity: 32);

  /// <summary>
     /// Creates a LINQ-queryable interface for a vertex graph set
        /// </summary>
        /// <typeparam name="T">The vertex type</typeparam>
        /// <param name="graphSet">The graph set</param>
        /// <returns>An IQueryable for LINQ operations</returns>
        public static IQueryable<T> AsQueryable<T>(this IGraphSet<T> graphSet)
         where T : IVertex
    {
  var label = GetLabel(typeof(T));
    
     // Use cached provider for same context and label
   var key = (graphSet.Context, label);
var provider = _providerCache.GetOrAdd(key, _ => 
  new GremlinQueryProvider(graphSet.Context, label));
     
 return new GraphQueryable<T>(provider);
        }

   /// <summary>
        /// Creates a LINQ-queryable interface for an edge graph set
/// </summary>
        /// <typeparam name="T">The edge type</typeparam>
 /// <param name="graphSet">The edge graph set</param>
    /// <returns>An IQueryable for LINQ operations</returns>
    public static IQueryable<T> AsQueryable<T>(this IEdgeGraphSet<T> graphSet)
     where T : IEdgeEntity
  {
     var label = GetLabel(typeof(T));
  
        // Use cached provider for same context and label
  var key = (graphSet.Context, label);
        var provider = _providerCache.GetOrAdd(key, _ => 
new GremlinQueryProvider(graphSet.Context, label));
      
   return new GraphQueryable<T>(provider);
        }

        /// <summary>
        /// Traverses outgoing edges from vertices to their edge entities
        /// </summary>
        /// <typeparam name="TVertex">The source vertex type</typeparam>
        /// <typeparam name="TEdge">The edge entity type</typeparam>
        /// <param name="source">The source queryable</param>
        /// <returns>An IQueryable of edge entities</returns>
        public static IQueryable<TEdge> OutE<TVertex, TEdge>(this IQueryable<TVertex> source)
     where TVertex : IVertex
            where TEdge : IEdgeEntity
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            
            var edgeLabel = GetLabel(typeof(TEdge));
            var expression = Expression.Call(
      null,
    ((MethodInfo)MethodBase.GetCurrentMethod()).MakeGenericMethod(typeof(TVertex), typeof(TEdge)),
      source.Expression);

            return source.Provider.CreateQuery<TEdge>(expression);
        }

        /// <summary>
        /// Traverses outgoing edges from vertices using a lambda to identify the edge property
        /// </summary>
        /// <typeparam name="TVertex">The source vertex type</typeparam>
        /// <typeparam name="TEdge">The edge entity type</typeparam>
        /// <param name="source">The source queryable</param>
      /// <param name="edgeSelector">Lambda expression selecting the edge collection property</param>
     /// <returns>An IQueryable of edge entities</returns>
 public static IQueryable<TEdge> OutE<TVertex, TEdge>(
          this IQueryable<TVertex> source,
       Expression<Func<TVertex, IEdgeCollection<IVertex>>> edgeSelector)
            where TVertex : IVertex
    where TEdge : IEdgeEntity
        {
         if (source == null) throw new ArgumentNullException(nameof(source));
            if (edgeSelector == null) throw new ArgumentNullException(nameof(edgeSelector));

  // Extract the edge label from the property
            var edgeLabel = GetEdgeLabelFromProperty(edgeSelector);
            
 var expression = Expression.Call(
       null,
     ((MethodInfo)MethodBase.GetCurrentMethod()).MakeGenericMethod(typeof(TVertex), typeof(TEdge)),
                source.Expression,
   Expression.Quote(edgeSelector));

         return source.Provider.CreateQuery<TEdge>(expression);
        }

        /// <summary>
        /// Traverses incoming edges to vertices from their edge entities
     /// </summary>
  /// <typeparam name="TVertex">The source vertex type</typeparam>
    /// <typeparam name="TEdge">The edge entity type</typeparam>
        /// <param name="source">The source queryable</param>
        /// <returns>An IQueryable of edge entities</returns>
  public static IQueryable<TEdge> InE<TVertex, TEdge>(this IQueryable<TVertex> source)
            where TVertex : IVertex
 where TEdge : IEdgeEntity
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
     
   var edgeLabel = GetLabel(typeof(TEdge));
   var expression = Expression.Call(
          null,
     ((MethodInfo)MethodBase.GetCurrentMethod()).MakeGenericMethod(typeof(TVertex), typeof(TEdge)),
    source.Expression);

    return source.Provider.CreateQuery<TEdge>(expression);
        }

        /// <summary>
        /// Traverses incoming edges using a lambda to identify the edge property
        /// </summary>
        /// <typeparam name="TVertex">The source vertex type</typeparam>
        /// <typeparam name="TEdge">The edge entity type</typeparam>
        /// <param name="source">The source queryable</param>
   /// <param name="edgeSelector">Lambda expression selecting the edge collection property</param>
    /// <returns>An IQueryable of edge entities</returns>
      public static IQueryable<TEdge> InE<TVertex, TEdge>(
 this IQueryable<TVertex> source,
        Expression<Func<TVertex, IEdgeCollection<IVertex>>> edgeSelector)
            where TVertex : IVertex
            where TEdge : IEdgeEntity
        {
    if (source == null) throw new ArgumentNullException(nameof(source));
       if (edgeSelector == null) throw new ArgumentNullException(nameof(edgeSelector));

   var edgeLabel = GetEdgeLabelFromProperty(edgeSelector);
          
    var expression = Expression.Call(
        null,
        ((MethodInfo)MethodBase.GetCurrentMethod()).MakeGenericMethod(typeof(TVertex), typeof(TEdge)),
   source.Expression,
        Expression.Quote(edgeSelector));

            return source.Provider.CreateQuery<TEdge>(expression);
        }

      /// <summary>
        /// Traverses both incoming and outgoing edges
        /// </summary>
     /// <typeparam name="TVertex">The source vertex type</typeparam>
 /// <typeparam name="TEdge">The edge entity type</typeparam>
 /// <param name="source">The source queryable</param>
        /// <returns>An IQueryable of edge entities</returns>
        public static IQueryable<TEdge> BothE<TVertex, TEdge>(this IQueryable<TVertex> source)
   where TVertex : IVertex
            where TEdge : IEdgeEntity
        {
        if (source == null) throw new ArgumentNullException(nameof(source));
        
     var edgeLabel = GetLabel(typeof(TEdge));
    var expression = Expression.Call(
     null,
 ((MethodInfo)MethodBase.GetCurrentMethod()).MakeGenericMethod(typeof(TVertex), typeof(TEdge)),
                source.Expression);

         return source.Provider.CreateQuery<TEdge>(expression);
        }

        /// <summary>
        /// Traverses both incoming and outgoing edges using a lambda
        /// </summary>
        /// <typeparam name="TVertex">The source vertex type</typeparam>
/// <typeparam name="TEdge">The edge entity type</typeparam>
        /// <param name="source">The source queryable</param>
        /// <param name="edgeSelector">Lambda expression selecting the edge collection property</param>
      /// <returns>An IQueryable of edge entities</returns>
        public static IQueryable<TEdge> BothE<TVertex, TEdge>(
     this IQueryable<TVertex> source,
      Expression<Func<TVertex, IEdgeCollection<IVertex>>> edgeSelector)
      where TVertex : IVertex
          where TEdge : IEdgeEntity
        {
         if (source == null) throw new ArgumentNullException(nameof(source));
     if (edgeSelector == null) throw new ArgumentNullException(nameof(edgeSelector));

            var edgeLabel = GetEdgeLabelFromProperty(edgeSelector);
            
            var expression = Expression.Call(
              null,
                ((MethodInfo)MethodBase.GetCurrentMethod()).MakeGenericMethod(typeof(TVertex), typeof(TEdge)),
    source.Expression,
       Expression.Quote(edgeSelector));

            return source.Provider.CreateQuery<TEdge>(expression);
        }

        /// <summary>
 /// Traverses outgoing edges from vertices to their edge entities (shorthand)
        /// </summary>
        /// <typeparam name="TEdge">The edge entity type</typeparam>
        /// <param name="source">The source queryable</param>
      /// <returns>An IQueryable of edge entities</returns>
 public static IQueryable<TEdge> OutE<TEdge>(this IQueryable source)
            where TEdge : IEdgeEntity
 {
 if (source == null) throw new ArgumentNullException(nameof(source));
         
 // Get the element type from the source
  var sourceElementType = source.ElementType;
         if (!typeof(IVertex).IsAssignableFrom(sourceElementType))
          {
   throw new ArgumentException("Source must be a queryable of vertex types", nameof(source));
   }
      
var edgeLabel = GetLabel(typeof(TEdge));
  var method = typeof(GraphSetLinqExtensions).GetMethods(BindingFlags.Public | BindingFlags.Static)
   .First(m => m.Name == nameof(OutE) && m.GetGenericArguments().Length == 2);
    var genericMethod = method.MakeGenericMethod(sourceElementType, typeof(TEdge));
      
   var expression = Expression.Call(
    null,
 genericMethod,
       source.Expression);

    return source.Provider.CreateQuery<TEdge>(expression);
}

   /// <summary>
   /// Traverses incoming edges to vertices from their edge entities (shorthand)
        /// </summary>
        /// <typeparam name="TEdge">The edge entity type</typeparam>
        /// <param name="source">The source queryable</param>
        /// <returns>An IQueryable of edge entities</returns>
        public static IQueryable<TEdge> InE<TEdge>(this IQueryable source)
      where TEdge : IEdgeEntity
   {
 if (source == null) throw new ArgumentNullException(nameof(source));
         
    // Get the element type from the source
    var sourceElementType = source.ElementType;
  if (!typeof(IVertex).IsAssignableFrom(sourceElementType))
       {
         throw new ArgumentException("Source must be a queryable of vertex types", nameof(source));
       }
        
   var edgeLabel = GetLabel(typeof(TEdge));
       var method = typeof(GraphSetLinqExtensions).GetMethods(BindingFlags.Public | BindingFlags.Static)
       .First(m => m.Name == nameof(InE) && m.GetGenericArguments().Length == 2);
      var genericMethod = method.MakeGenericMethod(sourceElementType, typeof(TEdge));
     
   var expression = Expression.Call(
    null,
 genericMethod,
  source.Expression);

    return source.Provider.CreateQuery<TEdge>(expression);
        }

 /// <summary>
   /// Asynchronously converts the query to a list
      /// </summary>
        public static Task<List<T>> ToListAsync<T>(this IQueryable<T> source)
{
      if (source == null) throw new ArgumentNullException(nameof(source));

         // Execute the query synchronously and wrap in a Task
   return Task.FromResult(source.ToList());
        }

     /// <summary>
      /// Asynchronously gets the first element from the query
/// </summary>
  public static Task<T> FirstAsync<T>(this IQueryable<T> source)
 {
      if (source == null) throw new ArgumentNullException(nameof(source));

  return Task.FromResult(source.First());
        }

    /// <summary>
 /// Asynchronously gets the first element or default
        /// </summary>
        public static Task<T> FirstOrDefaultAsync<T>(this IQueryable<T> source)
    {
   if (source == null) throw new ArgumentNullException(nameof(source));

          return Task.FromResult(source.FirstOrDefault());
        }

      /// <summary>
        /// Asynchronously counts the elements in the query
   /// </summary>
        public static Task<int> CountAsync<T>(this IQueryable<T> source)
  {
       if (source == null) throw new ArgumentNullException(nameof(source));

    return Task.FromResult(source.Count());
        }

    /// <summary>
        /// Asynchronously checks if any elements exist
     /// </summary>
   public static Task<bool> AnyAsync<T>(this IQueryable<T> source)
 {
        if (source == null) throw new ArgumentNullException(nameof(source));

   return Task.FromResult(source.Any());
   }

        private static string GetEdgeLabelFromProperty<TVertex>(Expression<Func<TVertex, IEdgeCollection<IVertex>>> edgeSelector)
    {
          // Extract property info from lambda
    var memberExpr = edgeSelector.Body as MemberExpression;
        if (memberExpr == null)
            {
           throw new ArgumentException("Edge selector must be a property access expression", nameof(edgeSelector));
        }

            var propertyInfo = memberExpr.Member as PropertyInfo;
            if (propertyInfo == null)
{
    throw new ArgumentException("Edge selector must reference a property", nameof(edgeSelector));
            }

            // Try to get OutLabelAttribute or EdgeLabelAttribute
       var outLabelAttr = propertyInfo.GetCustomAttribute<OutLabelAttribute>();
            if (outLabelAttr != null)
      {
        return outLabelAttr.ReverseLabel;
      }

       var edgeLabelAttr = propertyInfo.GetCustomAttribute<EdgeLabelAttribute>();
     if (edgeLabelAttr != null)
      {
                return edgeLabelAttr.Label;
   }

         // Fallback to property name in camelCase
 return ToCamelCase(propertyInfo.Name);
        }

        private static string GetLabel(Type entityType)
        {
         // Try to get the label mapping from GraphContextBase using reflection
            var graphContextBaseType = typeof(IGraphContext).Assembly.GetType("Stardust.Paradox.Data.GraphContextBase");
            if (graphContextBaseType != null)
            {
   var mappingField = graphContextBaseType.GetField("_dataSetLabelMapping",
              BindingFlags.Static | BindingFlags.NonPublic);

      if (mappingField != null)
        {
          var mapping = mappingField.GetValue(null) as IDictionary;
             if (mapping != null && mapping.Contains(entityType))
       {
   return mapping[entityType] as string;
      }
    }
      }

    // Fallback to attribute or convention
       var labelAttr = entityType.GetCustomAttribute<VertexLabelAttribute>();
    if (labelAttr != null)
            {
             return labelAttr.Label;
            }

   // Check for edge label attributes
    var edgeLabelAttr = entityType.GetCustomAttribute<EdgeLabelAttribute>();
            if (edgeLabelAttr != null)
   {
     return edgeLabelAttr.Label;
        }

            var inLabelAttr = entityType.GetCustomAttribute<InLabelAttribute>();
            if (inLabelAttr != null)
{
                return inLabelAttr.Label;
  }

     // Use convention: remove 'I' prefix and convert to camelCase
            var name = entityType.Name;
            if (name.StartsWith("I") && name.Length > 1)
  {
              name = name.Substring(1);
            }
            return ToCamelCase(name);
   }

        private static string ToCamelCase(string name)
        {
          if (string.IsNullOrEmpty(name) || name.Length == 0)
           return name;
            return char.ToLowerInvariant(name[0]) + name.Substring(1);
        }
    }
}
