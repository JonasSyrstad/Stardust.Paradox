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
		    if (_connector != null && _connector.CanParameterizeQueries)
		    {
			    return new GremlinQuery(_connector, "g.V({0})",id);
		    }
			return new GremlinQuery(_connector, $"g.V('{id}')");
	    }

	    public GremlinQuery E() => new GremlinQuery(_connector, "g.E()");

        public GremlinQuery E(string id)
	    {
		    if (_connector.CanParameterizeQueries)
		    {
			    return new GremlinQuery(_connector, "g.E({0})", id);
		    }
			return new GremlinQuery(_connector, $"g.E('{id}')");
	    }
    }
}