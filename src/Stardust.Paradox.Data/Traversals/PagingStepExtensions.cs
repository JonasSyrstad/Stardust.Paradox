namespace Stardust.Paradox.Data.Traversals
{
    public static class PagingStepExtensions
    {
        public static GremlinQuery Skip(this GremlinQuery queryBase, int itemsToSkip)
        {
            return queryBase.Range(itemsToSkip, -1);
        }

        public static GremlinQuery Skip(this GremlinQuery queryBase, GraphScope scope, int itemsToSkip)
        {
            return queryBase.Range(scope, itemsToSkip, -1);
        }

        public static GremlinQuery Range(this GremlinQuery queryBase, int itemsToSkip, int itemsToInclude)
        {
            return new ComposedGremlinQuery(queryBase, $".range({itemsToSkip},{itemsToInclude})");
        }
        public static GremlinQuery Range(this GremlinQuery queryBase, GraphScope scope, int itemsToSkip, int itemsToInclude)
        {
            return new ComposedGremlinQuery(queryBase, $".range({scope.ToString().ToLower()},{itemsToSkip},{itemsToInclude})");
        }

        public static GremlinQuery Limit(this GremlinQuery queryBase, int items)
        {
            return new ComposedGremlinQuery(queryBase, $".limit({items})");
        }
        public static GremlinQuery Limit(this GremlinQuery queryBase, GraphScope scope, int items)
        {
            return new ComposedGremlinQuery(queryBase, $".limit({scope.ToString().ToLower()},{items})");
        }
    }
}