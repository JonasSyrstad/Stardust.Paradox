using Stardust.Paradox.Data.Annotations;

namespace Stardust.Paradox.Data.Linq
{
    /// <summary>
    /// Represents an intermediate edge traversal result that can be cast to a specific edge type
    /// </summary>
    /// <typeparam name="TEdge">The edge entity interface type</typeparam>
    public interface IEdgeTraversal<TEdge> where TEdge : IEdgeEntity
    {
        /// <summary>
        /// Cast the edge traversal result to a specific edge implementation type
        /// </summary>
        /// <typeparam name="TConcreteEdge">The concrete edge type to cast to</typeparam>
        /// <returns>A queryable of the concrete edge type</returns>
        TConcreteEdge Cast<TConcreteEdge>() where TConcreteEdge : TEdge;
    }
}