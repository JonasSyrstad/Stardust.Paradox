namespace Stardust.Paradox.Data.Linq
{
    /// <summary>
    /// Predicate builder for graph traversals
    /// </summary>
    public static class P
    {
        public static GremlinPredicate Gt(object value) => new GremlinPredicate("P.gt", value);
        public static GremlinPredicate Lt(object value) => new GremlinPredicate("P.lt", value);
        public static GremlinPredicate Gte(object value) => new GremlinPredicate("P.gte", value);
        public static GremlinPredicate Lte(object value) => new GremlinPredicate("P.lte", value);
        public static GremlinPredicate Eq(object value) => new GremlinPredicate("P.eq", value);
        public static GremlinPredicate Neq(object value) => new GremlinPredicate("P.neq", value);
        public static GremlinPredicate Within(params object[] values) => new GremlinPredicate("P.within", values);
        public static GremlinPredicate Without(params object[] values) => new GremlinPredicate("P.without", values);
        public static GremlinPredicate Between(object start, object end) => new GremlinPredicate("P.between", start, end);
        public static GremlinPredicate Inside(object start, object end) => new GremlinPredicate("P.inside", start, end);
        public static GremlinPredicate Outside(object start, object end) => new GremlinPredicate("P.outside", start, end);
    }
}