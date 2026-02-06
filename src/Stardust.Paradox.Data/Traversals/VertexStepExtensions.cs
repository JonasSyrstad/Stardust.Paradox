namespace Stardust.Paradox.Data.Traversals
{
    public static class VertexStepExtensions
    {
        public static GremlinQuery PageRank(this GremlinQuery queryBase)
        {
            return new ComposedGremlinQuery(queryBase, "pageRank()");
        }

        public static GremlinQuery E(this GremlinQuery queryBase, string edgeLabel)
        {
            return new ComposedGremlinQuery(queryBase, $"E({queryBase.ComposeParameter(edgeLabel)})");
        }

        public static GremlinQuery E(this GremlinQuery queryBase)
        {
            return new ComposedGremlinQuery(queryBase, "E()");
        }

        public static GremlinQuery Out(this GremlinQuery queryBase, params string[] names)
        {
            return queryBase.Params("out", names);
        }

        public static GremlinQuery In(this GremlinQuery queryBase, params string[] names)
        {
            return queryBase.Params("in", names);
        }

        public static GremlinQuery Both(this GremlinQuery queryBase, params string[] names)
        {
            return queryBase.Params("both", names);
        }

        public static GremlinQuery OutE(this GremlinQuery queryBase, params string[] names)
        {
            return queryBase.Params("outE", names);
        }

        public static GremlinQuery InE(this GremlinQuery queryBase, params string[] names)
        {
            return queryBase.Params("inE", names);
        }

        public static GremlinQuery BothE(this GremlinQuery queryBase, params string[] names)
        {
            return queryBase.Params("bothE", names);
        }

        public static GremlinQuery InV(this GremlinQuery queryBase)
        {
            return new ComposedGremlinQuery(queryBase, "inV()");
        }

        public static GremlinQuery OutV(this GremlinQuery queryBase)
        {
            return new ComposedGremlinQuery(queryBase, "outV()");
        }

        public static GremlinQuery BothV(this GremlinQuery queryBase)
        {
            return new ComposedGremlinQuery(queryBase, "bothV()");
        }

        public static GremlinQuery OtherV(this GremlinQuery queryBase)
        {
            return new ComposedGremlinQuery(queryBase, "otherV()");
        }

        public static GremlinQuery SubGraph(this GremlinQuery queryBase, string graphLabel)
        {
            return new ComposedGremlinQuery(queryBase, $"subgraph({queryBase.ComposeParameter(graphLabel)})");
        }
    }
}