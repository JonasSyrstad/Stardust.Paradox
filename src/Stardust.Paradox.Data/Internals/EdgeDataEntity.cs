using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Stardust.Paradox.Data.Annotations;
using Stardust.Paradox.Data.Annotations.DataTypes;
using Stardust.Paradox.Data.CodeGeneration;

namespace Stardust.Paradox.Data.Internals
{
    internal interface IDynamicEntity
    {
        IDictionary<string, object> _dynamicProperties { get; }
    }


    public abstract class EdgeDataEntity<TIn, TOut> : IGraphEntityInternal, IEdgeEntityInternal, IEdge<TIn, TOut>,
        IDynamicGraphEntity, IDynamicEntity where TIn : IVertex where TOut : IVertex
    {
        private readonly ConcurrentDictionary<string, IInlineCollection> _inlineCollections =
            new ConcurrentDictionary<string, IInlineCollection>();

        internal GraphContextBase _context;
        private string _edgeSelector;
        protected internal string _entityKey;
        private TIn _inV;
        private bool _isLoading;
        private TOut _outV;
        private bool _parametrized;

        private readonly Dictionary<string, object> selectorParameters = new Dictionary<string, object>();

        //private string gremlinUpdateStatement = "";
        private readonly Dictionary<string, Update> UpdateChain = new Dictionary<string, Update>();

        public bool IsNew { get; private set; }
        public IDictionary<string, object> _dynamicProperties { get; } = new Dictionary<string, object>();

        public object GetProperty(string propertyName)
        {
            if (_dynamicProperties.TryGetValue(propertyName, out var v)) return v;
            return null;
        }

        public void SetProperty(string propertyName, object value)
        {
            if (GetProperty(propertyName) == value)
                return;
            if (_isLoading) return;
            if (IsDeleted)
                throw new EntityDeletedException(
                    $"Entitiy {GetType().GetInterfaces().First().Name}.{_entityKey} is marked as deleted.");

            if (value != null)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedHandlerArgs(value, propertyName));
                //gremlinUpdateStatement += $".property('{propertyName.ToCamelCase()}',{GetValue(value)})";
                if (UpdateChain.TryGetValue(propertyName.ToCamelCase(), out var update))
                    update.Value = GetValue(value);
                else
                    UpdateChain.Add(propertyName.ToCamelCase(),
                        new Update {PropertyName = propertyName.ToCamelCase(), Value = GetValue(value)});
                if (_dynamicProperties.ContainsKey(propertyName))
                    _dynamicProperties[propertyName] = value;
                else _dynamicProperties.Add(propertyName, value);
                IsDirty = true;
            }
            else
            {
                UpdateChain.Add(propertyName.ToCamelCase(), new Update
                {
                    Parameterless = $".property('{propertyName.ToCamelCase()}',null)"
                });
                if (_dynamicProperties.ContainsKey(propertyName))
                    _dynamicProperties.Remove(propertyName);
                IsDirty = true;
            }
        }

        public string[] DynamicPropertyNames => _dynamicProperties.Keys.ToArray();

        public async Task<TIn> InVAsync()
        {
            return _inV != null ? _inV : _inV = await _context.GetOrCreate<TIn>(InVertexId);
        }

        public string InVertexId { get; internal set; }

        public string OutVertextId { get; internal set; }

        public async Task<TOut> OutVAsync()
        {
            return _outV != null ? _outV : _outV = await _context.GetOrCreate<TOut>(OutVertextId);
        }

        void IEdgeEntityInternal.SetInVertex(IVertex vertex)
        {
            var old = _inV as GraphDataEntity;
            var newV = vertex as GraphDataEntity;
            if (old?._entityKey == newV?._entityKey) return;
            _inV = (TIn) vertex;
            InVertexId = newV?.EntityKey;
        }

        void IEdgeEntityInternal.SetOutVertex([NotNull] IVertex vertex)
        {
            var old = _inV as GraphDataEntity;
            var newV = vertex as GraphDataEntity;
            if (old?._entityKey == newV?._entityKey) return;
            if (newV == null) throw new GraphValidationException("New out Vertex is null");
            _outV = (TOut) vertex;
            OutVertextId = newV.EntityKey;
            IsDirty = true;
        }

        public string EntityKey
        {
            get => _entityKey;
            set => _entityKey = value;
        }

        public bool EagerLoading
        {
            get => false;
            set { }
        }

        public string GetUpdateStatement(bool parameterized)
        {
            if (_edgeSelector == null) MakeUpdateStatement(parameterized);
            if (parameterized)
                return _edgeSelector + string.Join("", UpdateChain.Select(u => u.Value.ParameterizedUpdateStatement));
            return _edgeSelector + string.Join("", UpdateChain.Select(u => u.Value.UpdateStatement));
        }

        public void SetContext(GraphContextBase graphContextBase, bool connectorCanParameterizeQueries)
        {
            _parametrized = connectorCanParameterizeQueries;
            _context = graphContextBase;
        }

        Task IGraphEntityInternal.Eager(bool doEagerLoad)
        {
            return Task.CompletedTask;
        }

        public void DoLoad(dynamic o)
        {
            _isLoading = true;
            if (Label != o.label.ToString())
                throw new InvalidCastException($"Unable to cast graph item with label {o.label} to {Label}");
            InVertexId = o.inV.ToString();
            OutVertextId = o.outV.ToString();
        }

        public abstract string Label { get; }
        public event PropertyChangedHandler PropertyChanged;
        public event PropertyChangingHandler PropertyChanging;

        public virtual void OnPropertyChanged(object value, string propertyName = null)
        {
            if (_isLoading) return;
            if (IsDeleted)
                throw new EntityDeletedException(
                    $"Entitiy {GetType().GetInterfaces().First().Name}.{_entityKey} is marked as deleted.");
            //if (value != null)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedHandlerArgs(value, propertyName));
                if (UpdateChain.TryGetValue(propertyName.ToCamelCase(), out var update))
                    update.Value = GetValue(value);
                else
                    UpdateChain.Add(propertyName.ToCamelCase(),
                        new Update {PropertyName = propertyName.ToCamelCase(), Value = GetValue(value)});

                IsDirty = true;
            }
        }

        public bool IsDeleted { get; protected internal set; }

        public bool OnPropertyChanging(object newValue, object oldValue, string propertyName = null)
        {
            RegisterNotifiable(newValue, oldValue, propertyName);
            if (_isLoading) return true;
            if (IsNew && newValue != null || newValue?.ToString() != oldValue?.ToString())
            {
                PropertyChanging?.Invoke(this, new PropertyChangingHandlerArgs(newValue, oldValue, propertyName));
                return true;
            }

            return false;
        }

        public void Reset(bool isNew)
        {
            _isLoading = false;
            IsNew = isNew;
            IsDeleted = false;
            UpdateChain.Clear();

            if (_parametrized)
            {
                selectorParameters.Clear();

                if (!isNew)
                {
                    selectorParameters.Add("___ekey", _entityKey);
                    _edgeSelector = "g.E(___ekey)";
                }
            }
            else
            {
                if (!isNew) _edgeSelector = $"g.E('{_entityKey.EscapeGremlinString()}')";
            }

            IsDirty = isNew;
            IsNew = isNew;
            IsDeleted = false;
        }

        public void Delete()
        {
            UpdateChain.Add("____drop____", new Update {Parameterless = ".drop()"});
            IsDeleted = true;
            IsDirty = true;
        }

        [JsonIgnore] public bool IsDirty { get; internal set; }

        [JsonIgnore] public string _EntityType => "edge";

        public Dictionary<string, object> GetParameterizedValues()
        {
            var p = UpdateChain.Where(v => v.Value.HasParameters)
                .ToDictionary(k => $"__{k.Value.PropertyName}", v => v.Value.Value);
            foreach (var selectorParameter in selectorParameters) p.Add(selectorParameter.Key, selectorParameter.Value);
            return p;
        }

        public void RegisterNotifiable(string propName, IComplexProperty notifiable)
        {
            notifiable.PropertyChanged += (o, args) => Notifiable_PropertyChanged(o, args, propName);
#pragma warning disable 618
            notifiable.StartNotifications();
#pragma warning restore 618
        }

        private string MakeUpdateStatement(bool parameterized)
        {
            if (InVertexId != null && OutVertextId != null)
            {
                if (parameterized)
                {
                    selectorParameters.Clear();
                    selectorParameters.Add("inVId", InVertexId);
                    selectorParameters.Add("outVID", OutVertextId);
                    selectorParameters.Add("___label", Label);
                    selectorParameters.Add("___ekey", _entityKey);
                    _edgeSelector = "";
                    _edgeSelector +=
                        "g.V(inVId).as('a').V(outVID).as('b').addE(___label).from('b').to('a').property('id',___ekey)";
                    IsDirty = true;
                }
                else
                {
                    _edgeSelector = "";
                    _edgeSelector +=
                        $"g.V('{InVertexId.EscapeGremlinString()}').as('a').V('{OutVertextId.EscapeGremlinString()}').as('b').addE('{Label}').from('b').to('a').property('id','{_entityKey}')";
                    IsDirty = true;
                }
            }

            return _edgeSelector;
        }

        internal object GetValue(object value)
        {
            if (value == null) return null;
            switch (value)
            {
                case IInlineCollection i:
                    return i.ToTransferData();
                case EpochDateTime e:
                    return e.Epoch;
                case IComplexProperty p:
                    return JsonConvert.SerializeObject(value);
                case Enum e:
                    return e.ToString();
                default:
                    return value;
            }
        }

        protected IInlineCollection<T> GetInlineCollection<T>(string name)
        {
            if (_inlineCollections.TryGetValue(name, out var i)) return (IInlineCollection<T>) i;
            i = new InlineCollection<T>(this, name);
            _inlineCollections.TryAdd(name, i);
            return (IInlineCollection<T>) i;
        }

        private void RegisterNotifiable(object newValue, object oldValue, string propertyName)
        {
            if (newValue != oldValue)
            {
                if (newValue is IComplexProperty newNotifiable)
                {
                    RegisterNotifiable(propertyName, newNotifiable);
#pragma warning disable 618
                    newNotifiable.StartNotifications();
#pragma warning restore 618
                }

                if (oldValue is IComplexProperty oldNotifiable)
                {
#pragma warning disable 618
                    oldNotifiable.EndNotifications();
#pragma warning restore 618
                }
            }
        }

        private void Notifiable_PropertyChanged(object sender, PropertyChangedEventArgs e, string propName)
        {
            OnPropertyChanged(sender, propName);
        }
    }
}