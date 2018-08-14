using System;
using System.Linq.Expressions;

namespace Stardust.Paradox.Data
{

    public interface IGraphConfiguration<T> : IGraphConfiguration where T : IVertex
    {
        IEdgeConfiguration<T> AddEdge<TEdgeProperty>(Expression<Func<T, TEdgeProperty>> inPropertyLambda, string label);
        IGraphConfiguration<T> AddInline<TEdgeProperty>(Expression<Func<T, TEdgeProperty>> inPropertyLambda,string query, SerializationType serialization);
    }

    public interface IEdgeConfiguration<T> where T:IVertex
    {
        IGraphConfiguration<T> Reverse<TReverse>(Expression<Func<TReverse, object>> inPropertyLambda);
    }

    public interface IGraphConfiguration
    {
        IGraphConfiguration<T> AddCollection<T>() where T : IVertex;
        IGraphConfiguration<T> AddCollection<T>(string label) where T : IVertex;
    }
}