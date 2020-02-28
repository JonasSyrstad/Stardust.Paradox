using System.Threading.Tasks;
using Stardust.Paradox.Data.Annotations.DataTypes;

namespace Stardust.Paradox.Data.Annotations
{
    public interface IEdgeReference<T> : INullable<T>, IEdgeReference, IEdgeNavigation<T> where T : IVertex
    {
        IEdge<T> Edge { get; set; }

        T Vertex { get; set; }
        Task LoadAsync();

        Task<T> ToVertexAsync();

        Task SetVertexAsync(T vertex);
    }

    public interface IEdgeReference
    {
    }
}