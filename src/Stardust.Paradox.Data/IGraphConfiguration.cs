using Stardust.Paradox.Data.Annotations;
using Stardust.Paradox.Data.Traversals;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using Stardust.Paradox.Data.CodeGeneration;

namespace Stardust.Paradox.Data
{

    public interface IGraphConfiguration<T> where T : IGraphEntity
    {
        IOutEdgeConfiguration<T> AddOutEdge(Expression<Func<T, object>> outPropertyLambda);

        IOutEdgeConfiguration<T> AddOutEdge(Expression<Func<T, object>> outPropertyLambda, string label);

        [Obsolete("Use AddInEdge", false)]
        IInEdgeConfiguration<T> AddEdge(Expression<Func<T, object>> inPropertyLambda);
        [Obsolete("Use AddInEdge", false)]
        IInEdgeConfiguration<T> AddEdge(Expression<Func<T, object>> inPropertyLambda, bool eagerLoading);
        [Obsolete("Use AddInEdge", false)]
        IInEdgeConfiguration<T> AddEdge(Expression<Func<T, object>> inPropertyLambda, string label);
        [Obsolete("Use AddInEdge", false)]
        IInEdgeConfiguration<T> AddEdge(Expression<Func<T, object>> inPropertyLambda, string label, bool eagerLoading);

        IInEdgeConfiguration<T> AddInEdge(Expression<Func<T, object>> inPropertyLambda);
        InReverse<TReverse, T> In<TReverse>(Expression<Func<T, IEdgeNavigation<TReverse>>> inPropertyLambda) where TReverse : IVertex;

        InReverse<TReverse, T> In<TReverse>(Expression<Func<T, IEdgeNavigation<TReverse>>> inPropertyLambda, string label) where TReverse : IVertex;

        OutReverse<TReverse, T> Out<TReverse>(Expression<Func<T, IEdgeNavigation<TReverse>>> inPropertyLambda) where TReverse : IVertex;

        OutReverse<TReverse, T> Out<TReverse>(Expression<Func<T, IEdgeNavigation<TReverse>>> inPropertyLambda, string label) where TReverse : IVertex;
        IInEdgeConfiguration<T> AddInEdge(Expression<Func<T, object>> inPropertyLambda, bool eagerLoading);
        
        IInEdgeConfiguration<T> AddInEdge(Expression<Func<T, object>> inPropertyLambda, string label);
        IInEdgeConfiguration<T> AddInEdge(Expression<Func<T, object>> inPropertyLambda, string label, bool eagerLoading);
        IGraphConfiguration<T> AddInline(Expression<Func<T, object>> inPropertyLambda, SerializationType serialization);

        IGraphConfiguration<T> AddQuery(Expression<Func<T, object>> inPropertyLambda, string gremlinQuery);

        IGraphConfiguration<T> AddQuery(Expression<Func<T, object>> inPropertyLambda, Func<GremlinContext, GremlinQuery> g);
        /// <summary>
        /// Start configuring a new collection. This method builds the entity, no further configuration is possible
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="label"></param>
        /// <returns></returns>
        IGraphConfiguration<Tn> ConfigureCollection<Tn>(string label) where Tn : IGraphEntity;
        /// <summary>
        /// Start configuring a new collection. This method builds the entity, no further configuration is possible
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        IGraphConfiguration<Tn> ConfigureCollection<Tn>() where Tn : IGraphEntity;


    }


    public class InReverse<T,TParent> where T: IVertex where TParent : IGraphEntity
    {
        private IGraphConfiguration<TParent> _c;
        private readonly string _label;

        internal InReverse(IGraphConfiguration<TParent> c,string label)
        {
            _c = c;
            _label = label;
        }

        public IGraphConfiguration<TParent> Out(Expression<Func<T, object>> func)
        {
            
            var prop = func.Body as MemberExpression;
            if (!CodeGenerator._FluentConfig.TryGetValue(typeof(T), out Dictionary<MemberInfo, FluentConfig> t))
            {
                t = new Dictionary<MemberInfo, FluentConfig>();
                CodeGenerator._FluentConfig.Add(typeof(T), t);
            }

            if (t.TryGetValue(prop.Member, out FluentConfig def)) throw new ArgumentOutOfRangeException(func.Name, "binding is already added");
            t.Add(prop.Member, new FluentConfig
            {
                ReverseEdgeLabel = _label

            });
            return _c;
        }
    }
    public class OutReverse<T, TParent> where T : IVertex where TParent : IGraphEntity
    {
        private IGraphConfiguration<TParent> _c;
        private readonly string _label;

        internal OutReverse(IGraphConfiguration<TParent> c, string label)
        {
            _c = c;
            _label = label;
        }

        public IGraphConfiguration<TParent> In(Expression<Func<T, object>> func)
        {

            var prop = func.Body as MemberExpression;
            if (!CodeGenerator._FluentConfig.TryGetValue(typeof(T), out Dictionary<MemberInfo, FluentConfig> t))
            {
                t = new Dictionary<MemberInfo, FluentConfig>();
                CodeGenerator._FluentConfig.Add(typeof(T), t);
            }

            if (t.TryGetValue(prop.Member, out FluentConfig def)) throw new ArgumentOutOfRangeException(func.Name, "binding is already added");
            t.Add(prop.Member, new FluentConfig
            {
                EdgeLabel = _label

            });
            return _c;
        }
    }

    public interface IInEdgeConfiguration<T> where T : IGraphEntity
    {
        /// <summary>
        ///  Binds the corresponding accessor property
        /// </summary>
        /// <typeparam name="TReverse"></typeparam>
        /// <param name="inPropertyLambda"></param>
        /// <returns></returns>
        IGraphConfiguration<T> Out<TReverse>(Expression<Func<TReverse, object>> inPropertyLambda);

        /// <summary>
        /// Binds the corresponding accessor property
        /// </summary>
        /// <typeparam name="TReverse"></typeparam>
        /// <param name="inPropertyLambda"></param>
        /// <param name="eagerLoading">Use with care, may cause entire graph to be loaded</param>
        /// <returns></returns>
        IGraphConfiguration<T> Out<TReverse>(Expression<Func<TReverse, object>> inPropertyLambda, bool eagerLoading);
        /// <summary>
        /// Start configuring a new collection. This method builds the entity, no further configuration is possible
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="label"></param>
        /// <returns></returns>

        /// <summary>
        ///  Binds the corresponding accessor property
        /// </summary>
        /// <typeparam name="TReverse"></typeparam>
        /// <param name="inPropertyLambda"></param>
        /// <returns></returns>
        [Obsolete("Use Out",false)]
        IGraphConfiguration<T> Reverse<TReverse>(Expression<Func<TReverse, object>> inPropertyLambda);

        /// <summary>
        /// Binds the corresponding accessor property
        /// </summary>
        /// <typeparam name="TReverse"></typeparam>
        /// <param name="inPropertyLambda"></param>
        /// <param name="eagerLoading">Use with care, may cause entire graph to be loaded</param>
        /// <returns></returns>
        [Obsolete("Use Out",false)]
        IGraphConfiguration<T> Reverse<TReverse>(Expression<Func<TReverse, object>> inPropertyLambda, bool eagerLoading);
        /// <summary>
        /// Start configuring a new collection. This method builds the entity, no further configuration is possible
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="label"></param>
        /// <returns></returns>
        IGraphConfiguration<Tn> ConfigureCollection<Tn>(string label) where Tn : IGraphEntity;

        /// <summary>
        /// Start configuring a new collection. This method builds the entity, no further configuration is possible
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        IGraphConfiguration<Tn> ConfigureCollection<Tn>() where Tn : IGraphEntity;
    }

    public interface IOutEdgeConfiguration<T> where T : IGraphEntity
    {
        /// <summary>
        ///  Binds the corresponding accessor property
        /// </summary>
        /// <typeparam name="TReverse"></typeparam>
        /// <param name="inPropertyLambda"></param>
        /// <returns></returns>
        IGraphConfiguration<T> In<TReverse>(Expression<Func<TReverse, object>> inPropertyLambda);

        /// <summary>
        /// Start configuring a new collection. This method builds the entity, no further configuration is possible
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="label"></param>
        /// <returns></returns>
        IGraphConfiguration<Tn> ConfigureCollection<Tn>(string label) where Tn : IGraphEntity;

        /// <summary>
        /// Start configuring a new collection. This method builds the entity, no further configuration is possible
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        IGraphConfiguration<Tn> ConfigureCollection<Tn>() where Tn : IGraphEntity;
    }

    public interface IEdgeConfiguration<T>: IOutEdgeConfiguration<T>,IInEdgeConfiguration<T> where T : IGraphEntity
    {
       
    }

    public interface IGraphConfiguration
    {

        //IGraphConfiguration AddCollection<T>() where T : IVertex;
        //IGraphConfiguration AddCollection<T>(string label) where T : IVertex;

        IGraphConfiguration<T> ConfigureCollection<T>(string label) where T : IGraphEntity;

        IGraphConfiguration<T> ConfigureCollection<T>() where T : IGraphEntity;

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        IGraphConfiguration SafeAsync();

        void BuildModel();
    }
}