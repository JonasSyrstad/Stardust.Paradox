using System.Collections.Generic;
using System.Threading.Tasks;
using Stardust.Paradox.Data.Annotations;

namespace Stardust.Paradox.Data
{
    public interface IEdgeGraphSet<T> : IGraphSetBase<T>
        where T : IGraphEntity
    {
        T Create(IVertex inVertex, IVertex outVertex);

        Task<T> GetAsync(string inId, string outId, bool partitioned = false);

		Task<T> GetAsync(string inId, string outId,string partitionKey=null);

        Task<T> GetPartitionedAsync((string, string) idAndPartitionKey);

		Task<T> GetPartitionedAsync((string, string) idAndPartitionKey);

		Task<IEnumerable<T>> GetByInIdAsync(string inId);
		Task<IEnumerable<T>> GetByOutIdAsync(string outId);
        bool UseFallBack { get; set; }
    }
}