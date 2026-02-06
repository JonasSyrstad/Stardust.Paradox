using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Stardust.Paradox.Data.Annotations;
using Stardust.Particles;

namespace Stardust.Paradox.Data.Internals
{
    internal class EdgeReference<T> : IEdgeReference<T>, IEdgeCollection where T : IVertex
    {
        private readonly EdgeCollection<T> _innerCollection;

        internal EdgeReference(string edgeLabel, IGraphContext context, GraphDataEntity parent, string reverseLabel,
            string gremlinQuery)
        {
            _innerCollection = gremlinQuery.ContainsCharacters()
                ? new EdgeCollection<T>(gremlinQuery, context, parent)
                : new EdgeCollection<T>(edgeLabel, context, parent, reverseLabel);
        }

        public async Task SaveChangesAsync()
        {
            await _innerCollection.SaveChangesAsync();
        }

        public async Task LoadAsync()
        {
            await _innerCollection.LoadAsync();
        }

        public async Task<T> ToVertexAsync()
        {
            return (await _innerCollection.ToVerticesAsync()).SingleOrDefault();
        }

        public IEdge<T> Edge
        {
            get
            {
                _innerCollection.LoadAsync().Wait();
                return _innerCollection.SingleOrDefault<IEdge<T>>();
            }
            set
            {
                var prev = ToVertexAsync().Result;
                if (prev != null)
                    _innerCollection.Remove(_innerCollection.SingleOrDefault<IEdge<T>>());
                _innerCollection.Add(value);
            }
        }

        public async Task SetVertexAsync(T vertex)
        {
            var prev = await ToVertexAsync();
            if (prev != null)
                _innerCollection.Remove(_innerCollection.SingleOrDefault<IEdge<T>>());
            if (vertex != null)
                _innerCollection.Add(vertex);
        }

        public async Task<bool> HasValueAsync()
        {
            await LoadAsync();
            return Edge != null;
        }

        [JsonIgnore]
        public T Vertex
        {
            get => _innerCollection.ToVerticesAsync().Result.SingleOrDefault();
            set => SetVertexAsync(value).Wait();
        }

        public bool HasValue => Edge != null;
    }
}