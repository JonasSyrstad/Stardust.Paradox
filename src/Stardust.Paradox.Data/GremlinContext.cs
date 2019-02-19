using System;
using Stardust.Paradox.Data.Traversals;

namespace Stardust.Paradox.Data
{
    public class GremlinContext
    {
        internal readonly IGremlinLanguageConnector _connector;

        internal GremlinContext(IGremlinLanguageConnector connector)
        {
            _connector = connector;
        }

        public static bool ParallelSaveExecution { get; set; }

        public GremlinQuery V()=>new GremlinQuery(_connector,"g.V()");

        public GremlinQuery V(string id)
	    {
		    var q = new GremlinQuery(_connector, "");
		    q._query = $"g.V({q.ComposeParameter(id)})";
		    return q;
	    }

	    public GremlinQuery V(string id,string partitionKey)
	    {
			var q = new GremlinQuery(_connector, "");
		    q._query = $"g.V([{q.ComposeParameter(id)},{q.ComposeParameter(partitionKey)}])";
		    return q;
		}

	    public GremlinQuery V(Tuple<string,string> idAndPartitionKey)
	    {
		    var q = new GremlinQuery(_connector, "");
		    q._query = $"g.V([{q.ComposeParameter(idAndPartitionKey.Item1)},{q.ComposeParameter(idAndPartitionKey.Item2)}])";
		    return q;
	    }

		public GremlinQuery E() => new GremlinQuery(_connector, "g.E()");

        public GremlinQuery E(string id)
	    {
			var q = new GremlinQuery(_connector, "");
		    q._query = $"g.E{q.ComposeParameter(id)})";
		    return q;
		}
    }
}