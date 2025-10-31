using System;
using System.Linq.Expressions;
using Stardust.Paradox.Data.Annotations;

namespace Stardust.Paradox.Data.Linq
{
    /// <summary>
    /// Implementation of edge traversal
    /// </summary>
    /// <typeparam name="TEdge">The edge type</typeparam>
    internal class EdgeTraversal<TEdge> : IEdgeTraversal<TEdge> where TEdge : IEdgeEntity
    {
        private readonly IVertex _vertex;
        private readonly LambdaExpression _edgeSelector;
        private readonly EdgeDirection _direction;
   
        public EdgeTraversal(IVertex vertex, LambdaExpression edgeSelector, EdgeDirection direction)
        {
            _vertex = vertex;
            _edgeSelector = edgeSelector;
            _direction = direction;
        }
        
        public TConcreteEdge Cast<TConcreteEdge>() where TConcreteEdge : TEdge
        {
            // This method is intended to be used within a LINQ select expression
            // The actual implementation will be handled by the query translator
            throw new InvalidOperationException(
                "This method should only be used within a LINQ query expression and will be translated to Gremlin.");
        }
    }
}