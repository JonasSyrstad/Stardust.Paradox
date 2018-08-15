using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Stardust.Paradox.Data.Traversals;
using Stardust.Particles;

namespace Stardust.Paradox.Data.Internals
{
    class EdgeCollection<TTout> : IEdgeCollection<TTout> where TTout : IVertex
    {
        private readonly string _edgeLabel;
        private readonly string _gremlinQuery;
        private readonly IGraphContext _context;
        private readonly GraphDataEntity _parent;
        private readonly string _reverseLabel;

        internal bool IsDirty
        {
            get => _isDirty;
            set
            {
                if (value) _parent.IsDirty = true;
                _isDirty = value;
            }
        }

        public async Task SaveChangesAsync()
        {
            if (!IsDirty) return;
            foreach (var edge in _addedCollection)
            {
                await edge.AddToVertexAsync(_edgeLabel ?? _reverseLabel);
            }
            _addedCollection.Clear();
            foreach (var edge in _deletedCollection)
            {
                await edge.DropEdgeAsync();
            }
            _deletedCollection.Clear();
            IsDirty = false;
        }

        internal EdgeCollection(string edgeLabel, IGraphContext context, GraphDataEntity parent, string reverseLabel)
        {
            _edgeLabel = edgeLabel;
            _context = context;
            _parent = parent;
            _reverseLabel = reverseLabel;
        }

        internal EdgeCollection(string gremlinQuery, IGraphContext context, GraphDataEntity parent)
        {
            _gremlinQuery = gremlinQuery;
            _context = context;
            _parent = parent;
        }

        private readonly ICollection<IEdge<TTout>> _collection = new List<IEdge<TTout>>();
        private readonly ICollection<Edge<TTout>> _addedCollection = new List<Edge<TTout>>();
        private readonly ICollection<Edge<TTout>> _deletedCollection = new List<Edge<TTout>>();
        protected bool _isLoaded;
        private bool _isDirty;
        private string _referenceType;

        public async Task<IEnumerable<TTout>> ToVerticesAsync()
        {
            await LoadAsync();
            return _collection.Select(e => e.Vertex);
        }

        IEnumerator<TTout> IEnumerable<TTout>.GetEnumerator()
        {
            foreach (var edge in _collection)
            {
                yield return edge.Vertex;
            }
        }

        public IEnumerator<IEdge<TTout>> GetEnumerator()
        {
            if (!_isLoaded&&_parent._eagerLoding)
            {
                LoadEdges().Wait();
            }
            return _collection.GetEnumerator();
        }

        public Task LoadAsync()
        {
            return LoadEdges();
        }

        private async Task LoadEdges()
        {
            IsDirty = false;
            _addedCollection.Clear();
            _deletedCollection.Clear();
            var v = await GetEdgeContent();
            _collection.Clear();
            foreach (var tout in v)
            {
                _collection.Add(new Edge<TTout>((tout as GraphDataEntity)._entityKey, tout, _parent, _context) { EdgeType = _referenceType });
            }
            _isLoaded = true;
        }


        private async Task<IEnumerable<TTout>> GetEdgeContent()
        {
            if (_gremlinQuery.ContainsCharacters()) return await _context.VAsync<TTout>(g => new GremlinQuery(g._connector, _gremlinQuery.Replace("{id}", _parent._entityKey)));
            if (_reverseLabel != null && _reverseLabel != _edgeLabel)
                return await _context.VAsync<TTout>(ReverseLabelQuery());
            if (_reverseLabel != null && _reverseLabel == _edgeLabel)
                return await _context.VAsync<TTout>(EdgeLabelQuery());
            return await _context.VAsync<TTout>(SimpleEdgeLabelQuery());
        }

        private Func<GremlinContext, GremlinQuery> SimpleEdgeLabelQuery()
        {
            _referenceType = "out";
            return g => g.V(_parent._entityKey).InE(_edgeLabel).OutV();
        }

        private Func<GremlinContext, GremlinQuery> EdgeLabelQuery()
        {
            _referenceType = "both";
            return g => g.V(_parent._entityKey).As("i").BothE(_edgeLabel).BothV().Where(p => p.P.Not(q => q.Eq("i")));
        }

        private Func<GremlinContext, GremlinQuery> ReverseLabelQuery()
        {
            _referenceType = "in";
            return g => g.V(_parent._entityKey).OutE(_reverseLabel).InV();
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public void Add(IEdge<TTout> item)
        {
            var edge = item as Edge<TTout>;
            if (_gremlinQuery.ContainsCharacters()) throw new InvalidOperationException("Unable to insert edge on query navigation property");
            _addedCollection.Add(item as Edge<TTout>);
            _collection.Add(item);
            IsDirty = true;
        }

        public void Add(TTout vertex)
        {
            Add(new Edge<TTout>("", vertex, _parent, _context) { AddReverse = _reverseLabel.ContainsCharacters() });
        }

        public void AddDual(TTout vertex)
        {
            Add(new Edge<TTout>("", vertex, _parent, _context) { AddReverse = true, ReverseLabel = _reverseLabel });
        }

        public void Clear()
        {
            _collection.Clear();
        }

        public bool Contains(TTout item)
        {
            var e = item as GraphDataEntity;
            return _collection.Any(Predicate(e));
        }

        public void CopyTo(TTout[] array, int arrayIndex)
        {
            throw new NotImplementedException();
        }

        public bool Remove(TTout item)
        {
            var e = item as GraphDataEntity;
            foreach (var edge in _collection.Where(Predicate(e)).Select(i => i).ToArray())
            {
                _collection.Remove(edge);
            }
            return true;
        }

        private static Func<IEdge<TTout>, bool> Predicate(GraphDataEntity e)
        {
            return i =>
            {
                var r = i.Vertex as GraphDataEntity;
                return r._entityKey == e._entityKey;
            };
        }

        public bool Contains(IEdge<TTout> item) => _collection.Contains(item);

        public void CopyTo(IEdge<TTout>[] array, int arrayIndex) => _collection.CopyTo(array, arrayIndex);

        public bool Remove(IEdge<TTout> item)
        {
            _deletedCollection.Add(item as Edge<TTout>);

            var result = _collection.Remove(item);
            if (result)
                IsDirty = true;
            return result;
        }

        public int Count => _collection.Count;
        public bool IsReadOnly => _gremlinQuery.ContainsCharacters();
    }
}