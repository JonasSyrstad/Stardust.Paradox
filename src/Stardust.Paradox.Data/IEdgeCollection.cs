using System.Collections.Generic;
using System.Threading.Tasks;
using Stardust.Paradox.Data.Internals;

namespace Stardust.Paradox.Data
{
    public interface IEdgeCollection
    {
        Task LoadAsync();
        Task SaveChangesAsync();
    }
    public interface IEdgeCollection<TTout> : IEdgeCollection, ICollection<IEdge<TTout>>, ICollection<TTout> where TTout : IVertex
    {
        Task<IEnumerable<TTout>> ToVerticesAsync();

        Task<IEnumerable<IEdge<TTout>>> ToEdgesAsync();
        void Add(TTout vertex, IDictionary<string, object> edgeProperties);
        void AddDual(TTout vertex);
    }
}