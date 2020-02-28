using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace Stardust.Paradox.Data.Annotations
{
    public interface IEdgeNavigation<T> where T : IVertex
    {
    }


    public interface IEdgeCollection
    {
        Task LoadAsync();

        Task SaveChangesAsync();
    }

    public interface IEdgeCollection<TTout> : IEdgeCollection, ICollection<IEdge<TTout>>, ICollection<TTout>,
        IEdgeNavigation<TTout> where TTout : IVertex
    {
        Task<IEnumerable<TTout>> ToVerticesAsync();

        Task<IEnumerable<TTout>> ToVerticesAsync([Optional] Expression<Func<TTout, object>> filterSelector,
            [Optional] object value,
            [Optional] Expression<Func<TTout, object>> orderSelector, [Optional] bool isDesc,
            [Optional] int pageNumber, [Optional] int pageSize);

        Task LoadAsync(Expression<Func<TTout, object>> filterSelector = null, object value = null,
            Expression<Func<TTout, object>> orderSelector = null, bool isDesc = false,
            int pageNumber = 0, int pageSize = 0);

        Task<IEnumerable<IEdge<TTout>>> ToEdgesAsync();
        void Add(TTout vertex, IDictionary<string, object> edgeProperties);
        void AddDual(TTout vertex);
    }

    public interface IEdgeCollection<TIn, TOut> : IEdgeCollection, ICollection<IEdge<TIn, TOut>>
        where TIn : IVertex where TOut : IVertex
    {
        Task<IEnumerable<TIn>> InVerticesAsync();

        Task<IEnumerable<TOut>> OutVerticesAsync();

        Task<IEnumerable<IEdge<TIn, TOut>>> AsEnumerableAsync();
    }
}