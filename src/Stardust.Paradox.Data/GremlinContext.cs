using Stardust.Paradox.Data.CodeGeneration;
using Stardust.Paradox.Data.Traversals;
using Stardust.Paradox.Data.Traversals.Typed;

namespace Stardust.Paradox.Data
{
    public class GremlinContext<T> : GremlinContext
    {
        internal GremlinContext(IGraphContext connector) : base(connector)
        {
        }

        public GremlinQuery<T> G()
        {
            return new GremlinQuery<T>(_connector, "g");
        }

        //public IEnumerable<T> ExecuteAsync<T>(GremlinQuery<T> invoke) where T : IVertex
        //{
        //    return base.V(invoke.CompileQuery());
        //}
    }

    public class GremlinContext
    {
        internal readonly IGremlinLanguageConnector _connector;

        internal GremlinContext(IGremlinLanguageConnector connector)
        {
            _connector = connector;
        }

        protected GremlinContext(IGraphContext connector)
        {
            var c = connector as GraphContextBase;
            _connector = c._connector;
        }

        public static bool ParallelSaveExecution { get; set; }

        public GremlinQuery V()
        {
            return new GremlinQuery(_connector, "g.V()");
        }

        public GremlinQuery<T> V<T>()
        {
            CodeGenerator.TypeLabels.TryGetValue(typeof(T), out var label);
            return new GremlinQuery<T>(new GremlinQuery(_connector, "g.V()").HasLabel(label));
        }

        public GremlinQuery V(string id)
        {
            var q = new GremlinQuery(_connector, "");
            q._query = $"g.V({q.ComposeParameter(id)})";
            return q;
        }

        public GremlinQuery V(string id, string partitionKey)
        {
            var q = new GremlinQuery(_connector, "");
            q._query = $"g.V([{q.ComposeParameter(partitionKey)},{q.ComposeParameter(id)}])";
            return q;
        }

        public GremlinQuery V((string, string) idAndPartitionKey)
        {
            var q = new GremlinQuery(_connector, "");
            q._query =
                $"g.V([{q.ComposeParameter(idAndPartitionKey.Item2)},{q.ComposeParameter(idAndPartitionKey.Item1)}])";
            return q;
        }

        public GremlinQuery E()
        {
            return new GremlinQuery(_connector, "g.E()");
        }

        public GremlinQuery E(string id)
        {
            var q = new GremlinQuery(_connector, "");
            q._query = $"g.E{q.ComposeParameter(id)})";
            return q;
        }
    }
}