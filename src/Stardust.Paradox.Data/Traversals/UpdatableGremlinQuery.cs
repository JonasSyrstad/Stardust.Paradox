namespace Stardust.Paradox.Data.Traversals
{
    public class UpdatableGremlinQuery : GremlinQuery
    {
        public UpdatableGremlinQuery(IGremlinLanguageConnector connector, string query) : base(connector, query)
        {
        }

        public override bool IsUpdatableGremlinQuery => true;
    }
}