using Newtonsoft.Json;
using Stardust.Paradox.Data.Annotations;
using Stardust.Paradox.Data.CodeGeneration;
using Stardust.Particles;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Stardust.Paradox.Data.Annotations.DataTypes;

namespace Stardust.Paradox.Data.Internals
{
	public abstract class GraphDataEntity : IGraphEntityInternal, IVertex
	{
		public event PropertyChangedHandler PropertyChanged;
		public event PropertyChangingHandler PropertyChanging;
		private string vertexSelector = "";
		protected internal string _entityKey;

		private Dictionary<string, Update> UpdateChain = new Dictionary<string, Update>();

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
			if (_inlineCollections.TryGetValue(name, out IInlineCollection i)) return (IInlineCollection<T>)i;
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

		public virtual void OnPropertyChanged(object value, string propertyName = null)
		{
			if (_isLoading) return;
			if (IsDeleted)
				throw new EntityDeletedException(
					$"Entitiy {GetType().GetInterfaces().First().Name}.{_entityKey} is marked as deleted.");
			if (vertexSelector.IsNullOrWhiteSpace()) return;

			if (value != null)
			{
				PropertyChanged?.Invoke(this, new PropertyChangedHandlerArgs(value, propertyName));
				//gremlinUpdateStatement += $".property('{propertyName.ToCamelCase()}',{GetValue(value)})";
				if (UpdateChain.TryGetValue(propertyName.ToCamelCase(), out var update))
					update.Value = GetValue(value);
				else UpdateChain.Add(propertyName.ToCamelCase(), new Update { PropertyName = propertyName.ToCamelCase(), Value = GetValue(value) });

				IsDirty = true;
			}
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
				default:
					return value;
			}
		}

		public bool OnPropertyChanging(object newValue, object oldValue, string propertyName = null)
		{
			if (_isLoading) return true;
			if ((IsNew && newValue != null) || newValue?.ToString() != oldValue?.ToString())
			{
				PropertyChanging?.Invoke(this, new PropertyChangingHandlerArgs(newValue, oldValue, propertyName));
				return true;
			}
			return false;
		}
		private Dictionary<string, object> selectorParameters = new Dictionary<string, object>();
		private bool _parametrized;

		public void Reset(bool isNew)
		{
			_isLoading = false;
			IsNew = isNew;
			IsDeleted = false;
			UpdateChain.Clear();
			_edges.Clear();
			if (_parametrized)
			{
				selectorParameters.Clear();
				selectorParameters.Add("___ekey", _entityKey);
				if (isNew)
					selectorParameters.Add("__label", Label);
				vertexSelector = isNew ? $"g.addV(__label).property('id',___ekey)" : $"g.V(___ekey)";
			}
			else
			{
				
					vertexSelector = isNew ? $"g.addV('{Label}').property('id',{Update.GetValue(_entityKey)})" : $"g.V('{_entityKey}')";
				
			}
			//vertexSelector = isNew ? $"g.addV('{Label}').property('id',{Update.GetValue(_entityKey)})" : $"g.V('{_entityKey}')";
			IsDirty = false;
			IsNew = isNew;
			IsDeleted = false;
		}

		[JsonIgnore]
		public bool IsDirty { get; internal set; }

		public string EntityKey
		{
			get => _entityKey;
			set => _entityKey = value;
		}

		public bool EagerLoading
		{
			get => _eagerLoding;
			set => _eagerLoding = value;
		}


		public string GetUpdateStatement(bool parameterized)
		{
			if (parameterized)
			{
				return vertexSelector + string.Join("", UpdateChain.Select(u => u.Value.ParameterizedUpdateStatement));
			}
			return vertexSelector + string.Join("", UpdateChain.Select(u => u.Value.UpdateStatement));
		}



		internal static string EscapeString(string value)
		{
			return value.EscapeGremlinString();
		}

		void IGraphEntityInternal.SetContext(GraphContextBase graphContextBase, bool connectorCanParameterizeQueries)
		{
			_parametrized = connectorCanParameterizeQueries;
			_context = graphContextBase;
		}

		public void Delete()
		{
			//gremlinUpdateStatement = ".drop()";
			UpdateChain.Add("____drop____", new Update { Parameterless = ".drop()" });
			IsDeleted = true;
			IsDirty = true;
		}

		public bool IsDeleted { get; internal set; }
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
					await Task.WhenAll(eagerTasks).ConfigureAwait(false);
				}
			}
			catch (Exception ex)
			{
				throw new Exception($"Unable to load {GetType()}:{_entityKey}", ex);
			}


		}

		public void DoLoad(dynamic o)
		{
			if (Label != o.label.ToString()) throw new InvalidCastException($"Unable to cast graph item with label {o.label} to {Label}");
		}

		[JsonIgnore]
		public string _EntityType => "vertex";

		public Dictionary<string, object> GetParameterizedValues()
		{
			var p= UpdateChain.Where(v => v.Value.HasParameters)
				.ToDictionary(k => $"__{k.Value.PropertyName}", v => GetValue(v));
			foreach (var selectorParameter in selectorParameters)
			{
				p.Add(selectorParameter.Key, selectorParameter.Value);
			}
			return p;
		}

		private static object GetValue(KeyValuePair<string, Update> v)
		{
			if (v.Value.Value == null) return null;
			if (v.Value.Value is DateTime dt)
				return dt.Ticks;
			return v.Value.Value;
		}
	}
}