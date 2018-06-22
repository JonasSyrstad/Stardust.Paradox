using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Stardust.Paradox.Data.Internals;
using Stardust.Paradox.Data.Tree;

namespace Stardust.Paradox.Data
{
    public static class GraphCollectionExtensions
    {
        public static Task<IEnumerable<T>> AsEnumerableAsync<T>(this ICollection<T> collection) where T : IVertex
        {
            return collection is IEdgeCollection<T> c ? c.ToVerticesAsync() : Task.FromResult(collection.Select(i => i));
        }

        public static void Add<T>(this ICollection<IEdge<T>> collection, T item) where T : IVertex
        {
            if (collection is IEdgeCollection<T> c)
                c.Add(item);
            else throw new Exception();
        }

        public static Task<IEnumerable<T>> ToVerticesAsync<T>(this ICollection<T> collection) where T : IVertex
        {
            return collection is IEdgeCollection<T> c ? c.ToVerticesAsync() : Task.FromResult(collection.Select(i => i));
        }
        public static Task<IEnumerable<T>> ToVerticesAsync<T>(this IReadOnlyCollection<T> collection) where T : IVertex
        {
            return collection is IEdgeCollection<T> c ? c.ToVerticesAsync() : Task.FromResult(collection.Select(i => i));
        }

        public static void AddRange<T>(this IEnumerable<T> inlineCollection, IEnumerable<T> range, bool throwException = false)
        {
            if (range == null) return;
            var c = inlineCollection as IInlineCollection<T>;
            var l = inlineCollection as ICollection<T>;
            if (c != null) c.AddRange(range);
            else if (l != null)
            {
                foreach (var item in range)
                {
                    l.Add(item);
                }
            }
            else if (throwException) throw new ArgumentException($"{inlineCollection?.GetType().FullName} does not support operation", nameof(inlineCollection));
        }

        public static async Task<IVertexTreeRoot<T>> GetTreeAsync<T>(this IVertex vertex, Expression<Func<T, object>> byProperty, bool incommingEdge = false) where T : IVertex
        {
            var v = vertex as GraphDataEntity;
            return await v._context.GetTreeAsync(v._entityKey, byProperty);
        }
    }
}