using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using Stardust.Paradox.Data.CodeGeneration;
using Stardust.Paradox.Data.Internals;
using Stardust.Particles;

namespace Stardust.Paradox.Data
{
    internal class GraphConfiguration<T> : GraphConfiguration, IGraphConfiguration<T>, IEdgeConfiguration<T> where T : IVertex
    {
        private readonly string _label;

        public GraphConfiguration(GraphContextBase context, string label) : base(context)
        {
            _label = label;
        }

        public GraphConfiguration(GraphContextBase context) : base(context)
        {
        }
        public IEdgeConfiguration<T> AddEdge<TEdgeProperty>(Expression<Func<T, TEdgeProperty>> inPropertyLambda, string label)
        {
            var prop = inPropertyLambda.Body as MemberExpression;
            Dictionary<MemberInfo, FluentConfig> t;
            if (!CodeGenerator._FluentConfig.TryGetValue(typeof(T), out t))
            {
                t = new Dictionary<MemberInfo, FluentConfig>();
                CodeGenerator._FluentConfig.Add(typeof(T), t);
            }

            FluentConfig def;
            if (t.TryGetValue(prop.Member, out def)) throw new ArgumentOutOfRangeException(inPropertyLambda.Name, "binding is already added");
            t.Add(prop.Member, new FluentConfig
            {
                EdgeLabel = label

            });
            return new GraphConfiguration<T>(_context, label);
        }

        public IGraphConfiguration<T> AddInline<TEdgeProperty>(Expression<Func<T, TEdgeProperty>> inPropertyLambda,
            string query, SerializationType serialization)
        {
            var prop = inPropertyLambda.Body as MemberExpression;
            if (!CodeGenerator._FluentConfig.TryGetValue(typeof(T), out var t))
            {
                t = new Dictionary<MemberInfo, FluentConfig>();
                CodeGenerator._FluentConfig.Add(typeof(T), t);
            }

            FluentConfig def;
            if (t.TryGetValue(prop.Member, out def)) throw new ArgumentOutOfRangeException(inPropertyLambda.Name, "binding is already added");
            t.Add(prop.Member, new FluentConfig
            {
                Query = query,
                Inline = true,
                Serialization = serialization

            });
            return this;
        }

        public IGraphConfiguration<T> Reverse<TReverse>(Expression<Func<TReverse, object>> inPropertyLambda)
        {
            if (_label.IsNullOrWhiteSpace()) throw new NullReferenceException("No edge label is defined");
            var prop = inPropertyLambda.Body as MemberExpression;
            Dictionary<MemberInfo, FluentConfig> t;
            if (!CodeGenerator._FluentConfig.TryGetValue(typeof(TReverse), out t))
            {
                t = new Dictionary<MemberInfo, FluentConfig>();
                CodeGenerator._FluentConfig.Add(typeof(TReverse), t);
            }

            FluentConfig def;
            if (t.TryGetValue(prop.Member, out def)) throw new ArgumentOutOfRangeException(inPropertyLambda.Name, "binding is already added");
            t.Add(prop.Member, new FluentConfig
            {
                EdgeLabel = _label

            });
            return new GraphConfiguration<T>(_context);
        }



    }
    internal class GraphConfiguration : IGraphConfiguration
    {
        protected readonly GraphContextBase _context;

        public GraphConfiguration(GraphContextBase context)
        {
            _context = context;
        }

        public IGraphConfiguration<T> AddCollection<T>() where T : IVertex
        {
            var type = typeof(T);
            var label = type.GetCustomAttribute<VertexLabelAttribute>()?.Label ??
                        type.Name.Remove(0, 1).ToCamelCase();
            if (!GraphContextBase._dataSetLabelMapping.ContainsKey(type))
            {
                GenerateObservable(type, label);

                GraphContextBase._dataSetLabelMapping.Add(type, label);
            }
            return new GraphConfiguration<T>(_context);
        }

        public IGraphConfiguration<T> AddCollection<T>(string label) where T : IVertex
        {
            var type = typeof(T);
            if (!GraphContextBase._dataSetLabelMapping.ContainsKey(type))
            {
                GenerateObservable(type, label);
                GraphContextBase._dataSetLabelMapping.Add(type, label);
            }
            return new GraphConfiguration<T>(_context);
        }

        private void GenerateObservable(Type type, string label)
        {
            var implementation = CodeGenerator.MakeDataEntity(type, label);
            DependencyResolverAdapter.AddEntity(type, implementation);
            GraphJsonConverter.AddPair(type, implementation);
        }
    }
}