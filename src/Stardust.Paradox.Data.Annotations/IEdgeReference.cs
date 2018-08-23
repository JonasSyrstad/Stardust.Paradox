using System.Threading.Tasks;
using Stardust.Paradox.Data.Annotations.DataTypes;

namespace Stardust.Paradox.Data.Annotations
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