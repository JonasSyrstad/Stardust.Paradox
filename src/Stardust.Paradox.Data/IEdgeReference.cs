using System;
using System.Data.SqlTypes;
using System.Threading.Tasks;

namespace Stardust.Paradox.Data
{
    public interface IEdgeReference<T>:INullable<T>, IEdgeReference  where T : IVertex
    {
        Task LoadAsync();

        Task<T> ToVertexAsync();

        IEdge<T> Edge { get; set; }

        Task SetVertexAsync(T vertex);

        T Vertex { get; set; }


    }

    public interface IEdgeReference
    {
    }
}