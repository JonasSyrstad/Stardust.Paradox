using System;
using System.Linq.Expressions;
using Stardust.Paradox.Data.Annotations;
using Stardust.Paradox.Data.Annotations.DataTypes;
using Stardust.Paradox.Data.CodeGeneration;

namespace Stardust.Paradox.Data.Traversals.Typed
{
    public class ComposedGremlinQuery<T> : GremlinQuery<T>
    {
        protected readonly GremlinQuery _queryBase;

        internal ComposedGremlinQuery(GremlinQuery queryBase, string query) : base(queryBase, query)
        {
            _queryBase = queryBase;
        }

        public override bool IsUpdatableGremlinQuery => _queryBase.IsUpdatableGremlinQuery;

        protected internal override string CompileQuery()
        {
            var query = _query.StartsWith(".") ? _query.Remove(0, 1) : _query;
            var baseQ = _queryBase.CompileQuery();
            if (!string.IsNullOrWhiteSpace(baseQ) && !baseQ.EndsWith(".")) query = $".{query}";
            return baseQ + query;
        }

        //internal override object ComposeParameter(object value)
        //{
        // return _queryBase.ComposeParameter(value);
        //}
    }

    public static partial class TypedTraversalExtensions
    {
        public static GremlinQuery<T> Has<T>(this GremlinQuery<T> g, Expression<Func<T, object>> func, string value)
            where T : IVertex
        {
            var prop = func.Body as MemberExpression;
            var f = g.Has(prop.Member.Name.ToCamelCase(), value);
            return new GremlinQuery<T>(f);
        }

        public static GremlinQuery<T> Has<T>(this GremlinQuery<T> g, Expression<Func<T, object>> func, bool value)
            where T : IVertex
        {
            var prop = func.Body as MemberExpression;
            var f = g.Has(prop.Member.Name.ToCamelCase(), value);
            return new GremlinQuery<T>(f);
        }

        public static GremlinQuery<T> Has<T>(this GremlinQuery<T> g, Expression<Func<T, object>> func,
            EpochDateTime value) where T : IVertex
        {
            var prop = func.Body as MemberExpression;
            var f = g.Has(prop.Member.Name.ToCamelCase(), value);
            return new GremlinQuery<T>(f);
        }

        public static GremlinQuery<T> Without<T>(this PredicateGremlinQuery<T> queryBase, string name)
        {
            return new GremlinQuery<T>(((PredicateGremlinQuery) queryBase).Without(name));
        }

        public static GremlinQuery<T> Where<T>(this GremlinQuery<T> queryBase, string name,
            Func<PredicateGremlinQuery<T>, GremlinQuery<T>> expression)
        {
            return new GremlinQuery<T>(new LambdaComposedGremlinQuery(queryBase,
                $".where('{name.EscapeGremlinString()}',{{0}})",
                query => expression.Invoke(new PredicateGremlinQuery<T>(new GremlinQuery<T>(query))).CompileQuery()));
        }

        public static GremlinQuery<T> Where<T>(this GremlinQuery<T> queryBase,
            Func<PredicateGremlinQuery<T>, GremlinQuery<T>> expression)
        {
            return new GremlinQuery<T>(new LambdaComposedGremlinQuery(queryBase, ".where({0})",
                query => expression.Invoke(new PredicateGremlinQuery<T>(new GremlinQuery<T>(query))).CompileQuery()));
        }
    }
}