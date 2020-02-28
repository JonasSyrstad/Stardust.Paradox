using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Stardust.Paradox.Data.Annotations;
using Stardust.Paradox.Data.CodeGeneration;
using Stardust.Paradox.Data.Traversals;

namespace Stardust.Paradox.Data.Internals
{
    internal class EdgeGraphSet<T> : IEdgeGraphSet<T> where T : IEdgeEntity
    {
        internal readonly IGraphContext _context;
        private readonly bool _useVerticesIdsAsEdgeId;
        private string label;

        internal EdgeGraphSet(IGraphContext context, bool useVerticesIdsAsEdgeId)
        {
            _context = context;
            _useVerticesIdsAsEdgeId = useVerticesIdsAsEdgeId;
        }

        public async Task<T> GetAsync(string id)
        {
            return await _context.EAsync<T>(id).ConfigureAwait(false);
        }

        public Task<T> GetPartitionedAsync((string, string) idAndPartitionKey)
        {
            return GetPartitionedAsync(idAndPartitionKey.Item1, idAndPartitionKey.Item2);
        }

        public async Task<T> GetPartitionedAsync(string id, string partitionKey)
        {
            return await _context.EAsync<T>(id, partitionKey).ConfigureAwait(false);
        }

        public async Task<IEnumerable<T>> FilterAsync(Expression<Func<T, object>> byProperty, string hasValue)
        {
            var propertyName = QueryFuncExt.GetInfo(byProperty).ToCamelCase();
            return await _context.EAsync<T>(g =>
                    g.E().HasLabel(GraphContextBase._dataSetLabelMapping[typeof(T)]).Has(propertyName, hasValue))
                .ConfigureAwait(false);
        }

        public async Task<IEnumerable<T>> FilterAsync(Expression<Func<T, object>> byProperty, string hasValue, int page,
            int pageSize = 20)
        {
            var propertyName = QueryFuncExt.GetInfo(byProperty).ToCamelCase();
            return await _context.EAsync<T>(g =>
                g.E().HasLabel(GraphContextBase._dataSetLabelMapping[typeof(T)]).Has(propertyName, hasValue)
                    .SkipTake(page * pageSize, pageSize)).ConfigureAwait(false);
        }

        public async Task<IEnumerable<T>> AllAsync()
        {
            return await _context.EAsync<T>(g => g.E().HasLabel(GraphContextBase._dataSetLabelMapping[typeof(T)]))
                .ConfigureAwait(false);
        }

        public async Task<IEnumerable<T>> AllAsync(int page, int pageSize = 20)
        {
            return await _context.EAsync<T>(g =>
                    g.E().HasLabel(GraphContextBase._dataSetLabelMapping[typeof(T)])
                        .SkipTake(page * pageSize, pageSize))
                .ConfigureAwait(false);
        }


        public void Attach(T item)
        {
            _context.Attach(item);
        }

        public async Task DeleteAsync(string id)
        {
            var item = await GetAsync(id).ConfigureAwait(false);
            _context.Delete(item);
        }

        public async Task<IEnumerable<T>> GetAsync(Func<GremlinContext, GremlinQuery> query, int page,
            int pageSize = 20)
        {
            return await _context
                .EAsync<T>(context => query.Extend(g => g.SkipTake(page * pageSize, pageSize)).Invoke(context))
                .ConfigureAwait(false);
        }

        public async Task<IEnumerable<T>> GetAsync(Func<GremlinContext, GremlinQuery> query)
        {
            return await _context.EAsync<T>(query).ConfigureAwait(false);
        }

        public async Task<IEnumerable<T>> GetAsync(int page, int pageSize = 20)
        {
            if (pageSize <= 0) pageSize = 20;
            return await _context.EAsync<T>(g =>
                    g.V().HasLabel(GraphContextBase._dataSetLabelMapping[typeof(T)])
                        .SkipTake(page * pageSize, pageSize))
                .ConfigureAwait(false);
        }

        public T Create(IVertex inVertex, IVertex outVertex)
        {
            var inV = inVertex as IGraphEntityInternal;
            var outV = outVertex as IGraphEntityInternal;
            if (label == null)
                label = CodeGenerator.EdgeLables[typeof(T)];
            var entity = _context.CreateEntity<T>(_useVerticesIdsAsEdgeId
                ? $"{label}{inV.EntityKey}{outV.EntityKey}"
                : Guid.NewGuid().ToString());
            var edge = entity as IEdgeEntityInternal;
            if (edge == null) throw new InvalidOperationException("Unable to construct edge");
            edge.SetInVertex(inVertex);
            edge.SetOutVertex(outVertex);
            edge.Reset(true);
            return entity;
        }

        public async Task<T> GetAsync(string inId, string outId, bool partitioned = false)
        {
            if (_useVerticesIdsAsEdgeId)
            {
                if (label == null)
                    label = CodeGenerator.EdgeLables[typeof(T)];
                if (partitioned)
                    return await GetPartitionedAsync($"{label}{inId}{outId}".ToTuple());
                return await GetAsync($"{label}{inId}{outId}");
            }

            var e = await _context.EAsync<T>(g =>
                    g.V(inId.EscapeGremlinString()).InE()
                        .Where(p => p.__().OtherV().HasId(outId.EscapeGremlinString())))
                .ConfigureAwait(false);
            return e.SingleOrDefault();
        }

        public async Task<IEnumerable<T>> GetByInIdAsync(string inId)
        {
            var e = await _context
                .EAsync<T>(g => g.V(inId.EscapeGremlinString()).InE(GraphContextBase._dataSetLabelMapping[typeof(T)]))
                .ConfigureAwait(false);
            return e;
        }

        public async Task<IEnumerable<T>> GetByOutIdAsync(string outId)
        {
            var e = await _context
                .EAsync<T>(g => g.V(outId.EscapeGremlinString()).OutE(GraphContextBase._dataSetLabelMapping[typeof(T)]))
                .ConfigureAwait(false);
            return e;
        }
    }
}