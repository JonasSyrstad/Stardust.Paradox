namespace Stardust.Paradox.Data.Traversals
{
    public class UpdatableGremlinQuery : GremlinQuery
    {
        internal UpdatableGremlinQuery(IGremlinLanguageConnector connector, string query) : base(connector, query)
        {
        }

        public override bool IsUpdatableGremlinQuery => true;
    }
}