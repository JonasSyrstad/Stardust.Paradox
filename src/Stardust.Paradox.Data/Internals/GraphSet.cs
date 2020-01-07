using Stardust.Paradox.Data.Annotations;
using Stardust.Paradox.Data.CodeGeneration;
using Stardust.Paradox.Data.Traversals;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace Stardust.Paradox.Data.Internals
{
    internal class GraphSet<T> : IGraphSet<T> where T : IVertex
    {
        internal readonly IGraphContext _context;

        internal GraphSet(IGraphContext context)
        {
            _context = context;
        }

        public async Task<T> GetAsync(string id)
        {
            return await _context.VAsync<T>(id).ConfigureAwait(false);
        }

        public async Task<IEnumerable<T>> FilterAsync(Expression<Func<T, object>> byProperty, string hasValue)
        {
            var propertyName = QueryFuncExt.GetInfo(byProperty).ToCamelCase();
            return await _context.VAsync<T>(g => g.V().HasLabel(GraphContextBase._dataSetLabelMapping[typeof(T)]).Has(propertyName, hasValue)).ConfigureAwait(false);
        }

        public async Task<IEnumerable<T>> FilterAsync(Expression<Func<T, object>> byProperty, string hasValue, int page, int pageSize = 20)
        {
            var propertyName = QueryFuncExt.GetInfo(byProperty).ToCamelCase();
            return await _context.VAsync<T>(g => g.V().HasLabel(GraphContextBase._dataSetLabelMapping[typeof(T)]).Has(propertyName, hasValue).SkipTake(page * pageSize, pageSize)).ConfigureAwait(false);
        }

        public async Task<IEnumerable<T>> AllAsync()
        {
            return await _context.VAsync<T>(g => g.V().HasLabel(GraphContextBase._dataSetLabelMapping[typeof(T)])).ConfigureAwait(false);
        }

        public async Task<IEnumerable<T>> AllAsync(int page, int pageSize = 20)
        {
            return await _context.VAsync<T>(g => g.V().HasLabel(GraphContextBase._dataSetLabelMapping[typeof(T)]).SkipTake(page * pageSize, pageSize)).ConfigureAwait(false);
        }

	    public async Task<T> GetAsync(string id, string partitionKey)
	    {
			return await _context.VAsync<T>(id,partitionKey).ConfigureAwait(false);
		}

	    public Task<T> GetAsync((string, string) idAndPartitionKey)
	    {
			return GetAsync(idAndPartitionKey.Item1, idAndPartitionKey.Item2);
		}

	    public async Task DeleteAsync(string id, string partitionKey)
	    {
			var item = await GetAsync(id,partitionKey).ConfigureAwait(false);
		    _context.Delete(item);
		}

	    public Task DeleteAsync((string, string) idAndPartitionKey)
	    {
		    return DeleteAsync(idAndPartitionKey.Item1, idAndPartitionKey.Item2);
	    }

	    public T Create()
        {
            return Create(Guid.NewGuid().ToString());
        }

        public T Create(string id)
        {
            return _context.CreateEntity<T>(id);
        }

        public T Create(string id, Func<T, T> initializer)
        {
            return initializer.Invoke(Create(id));
        }

        public T Create(Func<T, T> initializer)
        {
            return Create(Guid.NewGuid().ToString(), initializer);
        }

        public void Attach(T item)
        {
            _context.Attach<T>(item);
        }

        public async Task DeleteAsync(string id)
        {
            var item = await GetAsync(id).ConfigureAwait(false);
            _context.Delete(item);
        }

        public async Task<IEnumerable<T>> GetAsync(Func<GremlinContext, GremlinQuery> query, int page, int pageSize = 20)
        {
            return await _context.VAsync<T>(context => query.Extend(g => g.SkipTake(page * pageSize, pageSize)).Invoke(context)).ConfigureAwait(false);
        }

        public async Task<IEnumerable<T>> GetAsync(Func<GremlinContext, GremlinQuery> query)
        {
            return await _context.VAsync<T>(query).ConfigureAwait(false);
        }

        public async Task<IEnumerable<T>> GetAsync(int page, int pageSize = 20)
        {
            if (pageSize <= 0) pageSize = 20;
            return await _context.VAsync<T>(g => g.V().HasLabel(GraphContextBase._dataSetLabelMapping[typeof(T)]).SkipTake(page * pageSize, pageSize)).ConfigureAwait(false);
        }
    }
}