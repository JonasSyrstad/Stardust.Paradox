using Stardust.Paradox.Data.Annotations;
using Stardust.Paradox.Data.CodeGeneration;
using Stardust.Paradox.Data.Internals;
using Stardust.Paradox.Data.Traversals;
using Stardust.Particles;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;

namespace Stardust.Paradox.Data
{
    internal class GraphConfiguration<T> : GraphConfigurationBase, IGraphConfiguration<T>, IEdgeConfiguration<T> where T : IGraphEntity
    {
        private readonly string _label;
        private readonly string _collectionLabel;

        public GraphConfiguration(GraphContextBase context, string label, string collectionLabel) : base(context)
        {
            _label = label;
            _collectionLabel = collectionLabel;
        }

        public IEdgeConfiguration<T> AddEdge(Expression<Func<T, object>> inPropertyLambda)
        {
            return AddEdge(inPropertyLambda, false);
        }

        public IEdgeConfiguration<T> AddEdge(Expression<Func<T, object>> inPropertyLambda, bool eagerLoading)
        {
            var prop = inPropertyLambda.Body as MemberExpression;
            return AddEdge(inPropertyLambda, prop.Member.Name.ToCamelCase(), eagerLoading);
        }

        public IEdgeConfiguration<T> AddEdge(Expression<Func<T, object>> inPropertyLambda, string label)
        {
            return AddEdge(inPropertyLambda, label, false);
        }

        public IEdgeConfiguration<T> AddEdge(Expression<Func<T, object>> inPropertyLambda, string label, bool eagerLoading)
        {
            var prop = inPropertyLambda.Body as MemberExpression;
            if (!CodeGenerator._FluentConfig.TryGetValue(typeof(T), out Dictionary<MemberInfo, FluentConfig> t))
            {
                t = new Dictionary<MemberInfo, FluentConfig>();
                CodeGenerator._FluentConfig.Add(typeof(T), t);
            }

            if (t.TryGetValue(prop.Member, out FluentConfig def)) throw new ArgumentOutOfRangeException(inPropertyLambda.Name, "binding is already added");
            t.Add(prop.Member, new FluentConfig
            {
                EdgeLabel = label,
                EagerLoading = eagerLoading

            });
            return new GraphConfiguration<T>(_context, label, _collectionLabel);
        }

        public IGraphConfiguration<T> AddInline(Expression<Func<T, object>> inPropertyLambda,
             SerializationType serialization)
        {
            var prop = inPropertyLambda.Body as MemberExpression;
            if (!CodeGenerator._FluentConfig.TryGetValue(typeof(T), out var t))
            {
                t = new Dictionary<MemberInfo, FluentConfig>();
                CodeGenerator._FluentConfig.Add(typeof(T), t);
            }

            if (t.TryGetValue(prop.Member, out FluentConfig def)) throw new ArgumentOutOfRangeException(inPropertyLambda.Name, "binding is already added");
            t.Add(prop.Member, new FluentConfig
            {

                Inline = true,
                Serialization = serialization

            });
            return this;
        }

        public IGraphConfiguration<T> AddQuery(Expression<Func<T, object>> inPropertyLambda, string gremlinQuery)
        {
            var prop = inPropertyLambda.Body as MemberExpression;
            if (!CodeGenerator._FluentConfig.TryGetValue(typeof(T), out var t))
            {
                t = new Dictionary<MemberInfo, FluentConfig>();
                CodeGenerator._FluentConfig.Add(typeof(T), t);
            }

            if (t.TryGetValue(prop.Member, out FluentConfig def)) throw new ArgumentOutOfRangeException(inPropertyLambda.Name, "binding is already added");
            t.Add(prop.Member, new FluentConfig
            {
                Query = gremlinQuery
            });
            return this;
        }

        public IGraphConfiguration<T> AddQuery(Expression<Func<T, object>> inPropertyLambda, Func<GremlinContext, GremlinQuery> g)
        {
            var q = g.Invoke(new GremlinContext(null));
            return AddQuery(inPropertyLambda, q.CompileQuery());
        }

        public IGraphConfiguration<T> Reverse<TReverse>(Expression<Func<TReverse, object>> inPropertyLambda, bool eagerLoading)
        {
            if (_label.IsNullOrWhiteSpace()) throw new NullReferenceException("No edge label is defined");
            var prop = inPropertyLambda.Body as MemberExpression;
            if (!CodeGenerator._FluentConfig.TryGetValue(typeof(TReverse), out Dictionary<MemberInfo, FluentConfig> t))
            {
                t = new Dictionary<MemberInfo, FluentConfig>();
                CodeGenerator._FluentConfig.Add(typeof(TReverse), t);
            }

            if (t.TryGetValue(prop.Member, out FluentConfig def)) throw new ArgumentOutOfRangeException(inPropertyLambda.Name, "binding is already added");
            t.Add(prop.Member, new FluentConfig
            {
                ReverseEdgeLabel = _label,
                EagerLoading = eagerLoading

            });
            return new GraphConfiguration<T>(_context, null, _collectionLabel);
        }

        public override IGraphConfiguration<T1> ConfigureCollection<T1>(string label)
        {
            AddEntity(label, typeof(T1));
            return new GraphConfiguration<T1>(_context, null, label);
        }



        public override IGraphConfiguration<T1> ConfigureCollection<T1>()
        {
            var type = typeof(T1);
            var label = type.GetCustomAttribute<VertexLabelAttribute>()?.Label ??
                        type.Name.Remove(0, 1).ToCamelCase();
            return ConfigureCollection<T1>(label);
        }

        public IGraphConfiguration<T> Reverse<TReverse>(Expression<Func<TReverse, object>> inPropertyLambda)
        {
            return Reverse(inPropertyLambda, false);
        }
    }

    internal abstract class GraphConfigurationBase : IGraphConfiguration
    {
        protected GraphContextBase _context;
        private static Dictionary<string, Type> _pendingEntityDefinitions = new Dictionary<string, Type>();

        protected GraphConfigurationBase(GraphContextBase context)
        {
            _context = context;
        }

        protected void GenerateObservable(Type type, string label)
        {
            Type implementation;
            implementation = typeof(IVertex).IsAssignableFrom(type) ? 
                CodeGenerator.MakeDataEntity(type, label) : 
                CodeGenerator.MakeEdgeDataEntity(type, label);
            DependencyResolverAdapter.AddEntity(type, implementation);
            GraphJsonConverter.AddPair(type, implementation);
        }

        protected void AddEntity(string label, Type type)
        {
            if (_pendingEntityDefinitions.ContainsKey(label)) return;
            _pendingEntityDefinitions.Add(label, type);
        }
        public void BuildModel()
        {
            foreach (var pendingEntityDefinition in _pendingEntityDefinitions)
            {
                if (!GraphContextBase._dataSetLabelMapping.ContainsKey(pendingEntityDefinition.Value))
                {
                    GenerateObservable(pendingEntityDefinition.Value, pendingEntityDefinition.Key);
                    GraphContextBase._dataSetLabelMapping.Add(pendingEntityDefinition.Value, pendingEntityDefinition.Key);
                }
            }
            _pendingEntityDefinitions.Clear();
        }

        public abstract IGraphConfiguration<T> ConfigureCollection<T>(string label) where T : IGraphEntity;
        public abstract IGraphConfiguration<T> ConfigureCollection<T>() where T : IGraphEntity;

        public IGraphConfiguration SafeAsync()
        {
            GraphConfiguration.UseSafeAsync = true;
            return this as IGraphConfiguration;
        }
    }

    internal class GraphConfiguration : GraphConfigurationBase
    {
        public GraphConfiguration(GraphContextBase context) : base(context)
        {
        }

        public IGraphConfiguration AddCollection<T>() where T : IVertex
        {
            var type = typeof(T);
            var label = type.GetCustomAttribute<VertexLabelAttribute>()?.Label ??
                        type.Name.Remove(0, 1).ToCamelCase();
            return AddCollection<T>(label);
        }

        public IGraphConfiguration AddCollection<T>(string label) where T : IVertex
        {
            var type = typeof(T);
            base.AddEntity(label, type);
            //if (!GraphContextBase._dataSetLabelMapping.ContainsKey(type))
            //{
            //    GenerateObservable(type, label);
            //    GraphContextBase._dataSetLabelMapping.Add(type, label);
            //}
            return this;
        }

        public override IGraphConfiguration<T> ConfigureCollection<T>(string label)
        {
            var type = typeof(T);
            AddEntity(label, type);
            return new GraphConfiguration<T>(_context, null, label);
        }

        public override IGraphConfiguration<T> ConfigureCollection<T>()
        {
            var type = typeof(T);
            var label = type.GetCustomAttribute<VertexLabelAttribute>()?.Label ??
                        type.Name.Remove(0, 1).ToCamelCase();
            return ConfigureCollection<T>(label);
        }

        public static bool UseSafeAsync { get; set; }
    }
}