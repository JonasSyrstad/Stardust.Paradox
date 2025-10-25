using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
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
            var provider = new GremlinQueryProvider(graphSet.Context, label);
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
            var provider = new GremlinQueryProvider(graphSet.Context, label);
            return new GraphQueryable<T>(provider);
        }

        /// <summary>
        /// Asynchronously converts the query to a list
        /// </summary>
        public static Task<List<T>> ToListAsync<T>(this IQueryable<T> source)
            where T : IGraphEntity
        {
            if (source == null) throw new ArgumentNullException(nameof(source));

            // Execute the query synchronously and wrap in a Task
            return Task.FromResult(source.ToList());
        }

        /// <summary>
        /// Asynchronously gets the first element from the query
        /// </summary>
        public static Task<T> FirstAsync<T>(this IQueryable<T> source)
            where T : IGraphEntity
        {
            if (source == null) throw new ArgumentNullException(nameof(source));

            return Task.FromResult(source.First());
        }

        /// <summary>
        /// Asynchronously gets the first element or default
        /// </summary>
        public static Task<T> FirstOrDefaultAsync<T>(this IQueryable<T> source)
            where T : IGraphEntity
        {
            if (source == null) throw new ArgumentNullException(nameof(source));

            return Task.FromResult(source.FirstOrDefault());
        }

        /// <summary>
        /// Asynchronously counts the elements in the query
        /// </summary>
        public static Task<int> CountAsync<T>(this IQueryable<T> source)
            where T : IGraphEntity
        {
            if (source == null) throw new ArgumentNullException(nameof(source));

            return Task.FromResult(source.Count());
        }

        /// <summary>
        /// Asynchronously checks if any elements exist
        /// </summary>
        public static Task<bool> AnyAsync<T>(this IQueryable<T> source)
  where T : IGraphEntity
        {
            if (source == null) throw new ArgumentNullException(nameof(source));

            return Task.FromResult(source.Any());
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
