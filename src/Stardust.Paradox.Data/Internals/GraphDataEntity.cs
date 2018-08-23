using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Stardust.Paradox.Data.Annotations;
using Stardust.Paradox.Data.CodeGeneration;
using Stardust.Particles;

namespace Stardust.Paradox.Data.Internals
{
    public abstract class GraphDataEntity : IVertex
    {
        public event PropertyChangedHandler PropertyChanged;
        public event PropertyChangingHandler PropertyChanging;
        private string gremlinUpdateStatement = "";
        private string vertexSelector = "";
        protected internal string _entityKey;

        internal GraphContextBase _context;

        private readonly ConcurrentDictionary<string, IInlineCollection> _inlineCollections = new ConcurrentDictionary<string, IInlineCollection>();
        private readonly ConcurrentDictionary<string, IEdgeCollection> _edges = new ConcurrentDictionary<string, IEdgeCollection>();
        internal static ConcurrentDictionary<string, List<string>> _eagerLodedProperties = new ConcurrentDictionary<string, List<string>>();
        internal bool _eagerLoding;
        private bool _isLoading = true;

        protected IEdgeCollection<TOut> GetEdgeCollection<TOut>(string edgeLabel, string reverseLabel, string gremlinQuery) where TOut : IVertex
        {
            if (reverseLabel == "") reverseLabel = null;
            if (edgeLabel == "") edgeLabel = null;
            if (_edges.TryGetValue(edgeLabel ?? "r" + reverseLabel, out var edgeCollection)) return edgeCollection as IEdgeCollection<TOut>;
            edgeCollection = gremlinQuery.ContainsCharacters() ? new EdgeCollection<TOut>(gremlinQuery, _context, this) :
                new EdgeCollection<TOut>(edgeLabel, _context, this, reverseLabel);
            _edges.TryAdd(edgeLabel ?? "r" + reverseLabel, edgeCollection);
            return (IEdgeCollection<TOut>)edgeCollection;
        }

        protected IInlineCollection<T> GetInlineCollection<T>(string name)
        {
            IInlineCollection i;
            if (_inlineCollections.TryGetValue(name, out i)) return (IInlineCollection<T>)i;
            i = new InlineCollection<T>(this, name);
            _inlineCollections.TryAdd(name, i);
            return (IInlineCollection<T>)i;
        }

        protected IEdgeReference<TOut> GetEdgeReference<TOut>(string edgeLabel, string reverseLabel, string gremlinQuery)
            where TOut : IVertex
        {
            if (reverseLabel == "") reverseLabel = null;
            if (edgeLabel == "") edgeLabel = null;
            if (_edges.TryGetValue(edgeLabel ?? "r" + reverseLabel, out var edgeCollection)) return edgeCollection as IEdgeReference<TOut>;
            edgeCollection = new EdgeReference<TOut>(edgeLabel, _context, this, reverseLabel, gremlinQuery);
            _edges.TryAdd(edgeLabel ?? "r" + reverseLabel, edgeCollection);
            return (IEdgeReference<TOut>)edgeCollection;
        }

        protected internal virtual void OnPropertyChanged(object value, string propertyName = null)
        {
            if (_isLoading) return;
            if (IsDeleted)
                throw new EntityDeletedException(
                    $"Entitiy {GetType().GetInterfaces().First().Name}.{_entityKey} is marked as deleted.");
            if (vertexSelector.IsNullOrWhiteSpace()) return;

            if (value != null)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedHandlerArgs(value, propertyName));
                gremlinUpdateStatement += $".property('{propertyName.ToCamelCase()}',{GetValue(value)})";
                IsDirty = true;
            }
        }

        protected internal bool OnPropertyChanging(object newValue, object oldValue, string propertyName = null)
        {
            if (_isLoading) return true;
            if ((IsNew && newValue != null) || newValue?.ToString() != oldValue?.ToString())
            {
                PropertyChanging?.Invoke(this, new PropertyChangingHandlerArgs(newValue, oldValue, propertyName));
                return true;
            }
            return false;
        }

        protected internal void Reset(bool isNew)
        {
            _isLoading = false;
            IsNew = isNew;
            IsDeleted = false;
            gremlinUpdateStatement = "";
            _edges.Clear();
            vertexSelector = isNew ? $"g.addV('{Label}').property('id',{GetValue(_entityKey)})" : $"g.V('{_entityKey}')";
            IsDirty = false;
            IsNew = isNew;
            IsDeleted = false;
        }

        [JsonIgnore]
        public bool IsDirty { get; internal set; }

        internal string GetUpdateStatement()
        {
            return vertexSelector + gremlinUpdateStatement;
        }

        private string GetValue(object value)
        {
            if (value == null) return null;
            switch (value)
            {
                case string _:
                    return $"'{value}'";
                case DateTime time:
                    return $"{time.Ticks}";
                case int no:
                    return no.ToString(CultureInfo.InvariantCulture);
                case decimal dec:
                    return dec.ToString(CultureInfo.InvariantCulture);
                case long lng:
                    return lng.ToString(CultureInfo.InvariantCulture);
                case float flt:
                    return flt.ToString(CultureInfo.InvariantCulture);
                case double dbl:
                    return dbl.ToString(CultureInfo.InvariantCulture);
                case bool yn:
                    return yn.ToString(CultureInfo.InvariantCulture).ToLower();
                case IInlineCollection i:
                    return $"'{i.ToTransferData()}'";
            }
            throw new ArgumentException("Unknown type", nameof(value));
        }

        internal void SetContext(GraphContextBase graphContextBase)
        {
            _context = graphContextBase;
        }

        internal void Delete()
        {
            gremlinUpdateStatement = ".drop()";
            IsDeleted = true;
            IsDirty = true;
        }

        internal bool IsDeleted { get; set; }
        internal bool IsNew { get; set; }

        [JsonProperty("label", DefaultValueHandling = DefaultValueHandling.Include)]
        public abstract string Label { get; }

        public IEnumerable<IEdgeCollection> GetEdges()
        {
            return from e in _edges select e.Value;
        }

        public override string ToString()
        {
            return JsonConvert.SerializeObject(this);
        }

        public async Task Eager(bool doEagerLoad)
        {
            try
            {
                if (!doEagerLoad) return;
                var fullName = GetType().FullName;
                if (fullName != null && _eagerLodedProperties.TryGetValue(fullName, out var propsToLoad))
                {
                    var eagerTasks = new List<Task>();
                    if (propsToLoad == null) return;
                    foreach (var prop in propsToLoad)
                    {
                        var loader = GraphContextBase.TransferData(this, prop);
                        if (loader != null) eagerTasks.Add(loader.LoadAsync());
                        else
                        {
                            Logging.DebugMessage($"unable to load property {prop}");
                        }
                    }
                    await Task.WhenAll(eagerTasks);
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Unable to load {GetType()}:{_entityKey}", ex);
            }


        }
    }
}