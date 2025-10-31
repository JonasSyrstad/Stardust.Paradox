using System;
using System.Linq.Expressions;
using Stardust.Paradox.Data.Annotations;

namespace Stardust.Paradox.Data.Linq
{
    /// <summary>
    /// Extension methods for edge traversal on vertices
    /// </summary>
    public static class VertexEdgeTraversalExtensions
    {
        /// <summary>
        /// Traverse outgoing edges from a vertex using a lambda expression
        /// </summary>
        /// <typeparam name="TVertex">The vertex type</typeparam>
        /// <typeparam name="TTarget">The target vertex type</typeparam>
        /// <param name="vertex">The source vertex</param>
        /// <param name="edgeSelector">Lambda expression selecting the edge collection</param>
        /// <returns>An edge traversal that can be cast to a specific edge type</returns>
        public static IEdgeTraversal<IEdgeEntity> OutE<TVertex, TTarget>(
            this TVertex vertex,
            Expression<Func<TVertex, IEdgeCollection<TTarget>>> edgeSelector)
            where TVertex : IVertex
            where TTarget : IVertex
        {
            if (vertex == null) throw new ArgumentNullException(nameof(vertex));
            if (edgeSelector == null) throw new ArgumentNullException(nameof(edgeSelector));
  
            return new EdgeTraversal<IEdgeEntity>(vertex, edgeSelector, EdgeDirection.Out);
        }
   
        /// <summary>
        /// Traverse incoming edges to a vertex using a lambda expression
        /// </summary>
        /// <typeparam name="TVertex">The vertex type</typeparam>
        /// <typeparam name="TTarget">The target vertex type</typeparam>
        /// <param name="vertex">The source vertex</param>
        /// <param name="edgeSelector">Lambda expression selecting the edge collection</param>
        /// <returns>An edge traversal that can be cast to a specific edge type</returns>
        public static IEdgeTraversal<IEdgeEntity> InE<TVertex, TTarget>(
            this TVertex vertex,
            Expression<Func<TVertex, IEdgeCollection<TTarget>>> edgeSelector)
            where TVertex : IVertex
            where TTarget : IVertex
        {
            if (vertex == null) throw new ArgumentNullException(nameof(vertex));
            if (edgeSelector == null) throw new ArgumentNullException(nameof(edgeSelector));
   
            return new EdgeTraversal<IEdgeEntity>(vertex, edgeSelector, EdgeDirection.In);
        }
    }
}