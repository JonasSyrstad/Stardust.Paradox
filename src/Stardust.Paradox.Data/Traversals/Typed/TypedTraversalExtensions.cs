using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using Stardust.Paradox.Data.Annotations;
using Stardust.Paradox.Data.Annotations.DataTypes;
using Stardust.Paradox.Data.CodeGeneration;
using Stardust.Paradox.Data.Internals;

namespace Stardust.Paradox.Data.Traversals.Typed
{
    public static partial class TypedTraversalExtensions
    {
        public static async Task<IEnumerable<T>> GetTypedAsync<T>(this IGraphSet<T> graphSet, Func<GremlinQuery<T>, GremlinQuery<T>> query) where T : IVertex
        {
            var g = graphSet as GraphSet<T>;
            //var context= new GremlinContext<T>(g._context);
            //var baseQuery = context.G();
            //var q = query.Invoke(baseQuery);
            return await graphSet.GetAsync(gremlinContext =>
            {
                var context = new GremlinContext<T>(g._context);
                var baseQuery = context.G();
                var q = query.Invoke(baseQuery);
                return q;
            });


        }

        public static async Task<IEnumerable<T>> GetTypedAsync<T>(this IEdgeGraphSet<T> graphSet, Func<GremlinQuery<T>, GremlinQuery<T>> query) where T : IEdgeEntity
        {
            var g = graphSet as EdgeGraphSet<T>;
            //var context= new GremlinContext<T>(g._context);
            //var baseQuery = context.G();
            //var q = query.Invoke(baseQuery);
            return await graphSet.GetAsync(gremlinContext =>
            {
                var context = new GremlinContext<T>(g._context);
                var baseQuery = context.G();
                var q = query.Invoke(baseQuery);
                return q;
            });


        }

        public static GremlinQuery<T> V<T>(this GremlinQuery<T> baseQuery)
        {
            CodeGenerator.typeLables.TryGetValue(typeof(T), out var label);
            return new GremlinQuery<T>(new GremlinQuery<T>(baseQuery, "g.V()").HasLabel(label));
        }

        public static GremlinQuery<T> V<T>(this GremlinQuery<T> baseQuery, string id)
        {
            CodeGenerator.typeLables.TryGetValue(typeof(T), out var label);
            return new GremlinQuery<T>(new GremlinQuery<T>(baseQuery, $"g.V({baseQuery.ComposeParameter(id)})").HasLabel(label));
        }

        public static GremlinQuery<T2> V<T1, T2>(this GremlinQuery<T1> baseQuery, string id)
        {
            CodeGenerator.typeLables.TryGetValue(typeof(T2), out var label);
            return new GremlinQuery<T2>(new GremlinQuery<T2>(baseQuery, $"g.V({baseQuery.ComposeParameter(id)})").HasLabel(label));
        }

        public static GremlinQuery<T2> V<T1, T2>(this GremlinQuery<T1> baseQuery)
        {
            CodeGenerator.typeLables.TryGetValue(typeof(T2), out var label);
            return new GremlinQuery<T2>(new GremlinQuery<T2>(baseQuery, $"g.V()").HasLabel(label));
        }

        public static GremlinQuery<T> E<T>(this GremlinQuery<T> baseQuery)
        {
            CodeGenerator.typeLables.TryGetValue(typeof(T), out var label);
            return new GremlinQuery<T>(new GremlinQuery<T>(baseQuery, "g.E()").HasLabel(label));
        }

        public static GremlinQuery<T> E<T>(this GremlinQuery<T> baseQuery, string id)
        {
            CodeGenerator.typeLables.TryGetValue(typeof(T), out var label);
            return new GremlinQuery<T>(new GremlinQuery<T>(baseQuery, $"g.E({baseQuery.ComposeParameter(id)})").HasLabel(label));
        }



        public static GremlinQuery<T> Out<T>(this GremlinQuery g) where T : IVertex
        {
            return new GremlinQuery<T>(g.Out());
        }

        public static GremlinQuery<T1> Out<T1, T2>(this GremlinQuery<T2> g, Expression<Func<T2, IEdgeNavigation<T1>>> navExpression) where T1 : IVertex where T2 : IVertex
        {

            var eLabel = GetEdgeLabel(navExpression);
            CodeGenerator.typeLables.TryGetValue(typeof(T1), out var label);
            return new GremlinQuery<T1>(g.Out(eLabel).HasLabel(label));
        }

        public static GremlinQuery<T> In<T>(this GremlinQuery g) where T : IVertex
        {
            CodeGenerator.typeLables.TryGetValue(typeof(T), out var label);
            return new GremlinQuery<T>(g.In());
        }

        public static GremlinQuery<T1> In<T1, T2>(this GremlinQuery<T2> g, Expression<Func<T2, IEdgeNavigation<T1>>> navExpression) where T1 : IVertex where T2 : IVertex
        {
            var eLabel = GetEdgeLabel(navExpression);

            CodeGenerator.typeLables.TryGetValue(typeof(T1), out var label);
            return new GremlinQuery<T1>(g.In(eLabel).HasLabel(label));
        }

        private static string GetEdgeLabel<T1, T2>(Expression<Func<T2, IEdgeNavigation<T1>>> navExpression) where T1 : IVertex where T2 : IVertex
        {
            var prop = navExpression.Body as MemberExpression;
            FluentConfig edgeLabel = null;
            if (CodeGenerator._FluentConfig.TryGetValue(typeof(T2), out var t))
                t.TryGetValue(prop.Member, out edgeLabel);
            if (edgeLabel == null)
                throw new InvalidDataContractException(
                    $"property {prop.Type.Name}.{prop.Member.Name} is not a navigation property");
            var eLabel = edgeLabel.EdgeLabel ?? edgeLabel.ReverseEdgeLabel;
            return eLabel;
        }

        public static GremlinQuery<T> Dedup<T>(this GremlinQuery<T> queryBase)
        {
            return new GremlinQuery<T>(((GremlinQuery)queryBase).Dedup());
        }

        public static GremlinQuery<T> As<T>(this GremlinQuery<T> queryBase, string name)
        {
            return new GremlinQuery<T>(((GremlinQuery)queryBase).As(name));
        }
       
        public static GremlinEdgeQuery InE<T1, T2>(this GremlinQuery<T2> g, Expression<Func<T2, IEdgeNavigation<T1>>> navExpression) where T1 : IVertex where T2 : IVertex
        {
            var eLabel = GetEdgeLabel(navExpression);

            return new GremlinEdgeQuery(g.InE(eLabel));
        }

        public static GremlinEdgeQuery OutE<T1, T2>(this GremlinQuery<T2> g, Expression<Func<T2, IEdgeNavigation<T1>>> navExpression) where T1 : IVertex where T2 : IVertex
        {
            var eLabel = GetEdgeLabel(navExpression);

            return new GremlinEdgeQuery(g.OutE(eLabel));
        }

        public static GremlinQuery<T> AsTypedEdge<T>(this GremlinEdgeQuery baseQuery) where T : IEdgeEntity
        {
            return new GremlinQuery<T>(baseQuery);
        }

        public static GremlinQuery<T> OutV<T>(GremlinQuery baseQuery) where T : IVertex
        {
            return new GremlinQuery<T>(baseQuery.OutV());
        }

        public static GremlinQuery<T> InV<T>(GremlinQuery baseQuery) where T : IVertex
        {
            return new GremlinQuery<T>(baseQuery.InV());
        }

        public static GremlinQuery<T> OtherV<T>(GremlinQuery baseQuery) where T : IVertex
        {
            return new GremlinQuery<T>(baseQuery.OtherV());
        }

        internal static GremlinQuery<T> P<T>(this PredicateGremlinQuery<T> queryBase)
        {
            return new GremlinQuery<T>(new ComposedGremlinQuery(queryBase, $"P."));
        }
    }
    public class GremlinEdgeQuery:GremlinQuery
    {
        internal GremlinEdgeQuery(IGremlinLanguageConnector connector, string query) : base(connector, query)
        {
        }

        internal GremlinEdgeQuery(GremlinQuery baseQuery, string query) : base(baseQuery, query)
        {
        }

        internal GremlinEdgeQuery(IGremlinLanguageConnector connector) : base(connector)
        {
        }

        internal GremlinEdgeQuery(IGremlinLanguageConnector connector, string query, string id) : base(connector, query, id)
        {
        }
        internal GremlinEdgeQuery(GremlinQuery baseQuery) : base(baseQuery, null)
        {
        }
    }
}
