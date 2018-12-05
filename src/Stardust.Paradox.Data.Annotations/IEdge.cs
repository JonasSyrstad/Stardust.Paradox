using System.Collections.Generic;
using System.Threading.Tasks;

namespace Stardust.Paradox.Data.Annotations
{
    public interface IEdge:IGraphEntity
    {
    }

    public interface IEdge<T> : IEdge where T : IVertex
    {
        T Vertex { get; set; }

        string EdgeType { get; }

        IDictionary<string ,object> Properties { get; }
    }

    public interface IEdge<TIn, TOut>:IEdgeEntity where TIn : IVertex where TOut : IVertex
    {
        Task<TIn> InVAsync();

        string InVertexId { get; }

        string OutVertextId { get; }

        Task<TOut> OutVAsync();
    }
}