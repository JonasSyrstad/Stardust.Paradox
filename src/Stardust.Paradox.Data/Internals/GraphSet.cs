using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Stardust.Paradox.Data.CodeGeneration;
using Stardust.Paradox.Data.Traversals;

namespace Stardust.Paradox.Data.Internals
{
    internal class GraphSet<T> : IGraphSet<T> where T : IVertex
    {
        private readonly IGraphContext _context;

        internal GraphSet(IGraphContext context)
        {
            _context = context;
        }

        public async Task<T> GetAsync(string id)
        {
            return await _context.VAsync<T>(id);
        }

        public async Task<IEnumerable<T>> FilterAsync(Expression<Func<T, object>> byProperty, string hasValue)
        {
            var propertyName = QueryFuncExt.GetInfo(byProperty).ToCamelCase();
            return await _context.VAsync<T>(g =>g.V().HasLabel(GraphContextBase._dataSetLabelMapping[typeof(T)]).Has(propertyName, hasValue));
        }

        public async Task<IEnumerable<T>> FilterAsync(Expression<Func<T, object>> byProperty, string hasValue, int page, int pageSize = 20)
        {
            var propertyName = QueryFuncExt.GetInfo(byProperty).ToCamelCase();
            return await _context.VAsync<T>(g => g.V().HasLabel(GraphContextBase._dataSetLabelMapping[typeof(T)]).Has(propertyName, hasValue).Range(page * pageSize, (page * pageSize) + pageSize));
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

        public void Attatch(T item)
        {
            _context.Attatch<T>(item);
        }

        public async Task DeleteAsync(string id)
        {
            var item = await GetAsync(id);
            _context.Delete(item);
        }

        public async Task<IEnumerable<T>> GetAsync(Func<GremlinContext, GremlinQuery> query, int page, int pageSize = 20)
        {
            return await _context.VAsync<T>(context => query.Extend(g => g.Range(page * pageSize, (page * pageSize) + pageSize)).Invoke(context));
        }

        public async Task<IEnumerable<T>> GetAsync(Func<GremlinContext, GremlinQuery> query)
        {
            return await _context.VAsync<T>(query);
        }

        public async Task<IEnumerable<T>> GetAsync(int page, int pageSize = 20)
        {
            if (pageSize <= 0) pageSize = 20;
            return await _context.VAsync<T>(g => g.V().HasLabel(GraphContextBase._dataSetLabelMapping[typeof(T)]).Range(page * pageSize, (page * pageSize) +pageSize));
        }
    }
}