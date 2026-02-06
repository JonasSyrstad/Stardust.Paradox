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

        public static GremlinQuery Range(this GremlinQuery queryBase, int lowEnd, int highEnd)
        {
            return new ComposedGremlinQuery(queryBase,
                $".range({queryBase.ComposeParameter(lowEnd)},{queryBase.ComposeParameter(highEnd)})");
        }

        public static GremlinQuery SkipTake(this GremlinQuery queryBase, int skip, int take)
        {
            return queryBase.Range(skip, skip + take);
        }

        public static GremlinQuery SkipTake(this GremlinQuery queryBase, GraphScope scope, int skip, int take)
        {
            return queryBase.Range(scope, skip, skip + take);
        }

        public static GremlinQuery Range(this GremlinQuery queryBase, GraphScope scope, int lowEnd, int highEnd)
        {
            return new ComposedGremlinQuery(queryBase,
                $".range({scope.ToString().ToLower()},{queryBase.ComposeParameter(lowEnd)},{queryBase.ComposeParameter(highEnd)})");
        }

        public static GremlinQuery Limit(this GremlinQuery queryBase, int items)
        {
            return new ComposedGremlinQuery(queryBase, $".limit({queryBase.ComposeParameter(items)})");
        }

        public static GremlinQuery Limit(this GremlinQuery queryBase, GraphScope scope, int items)
        {
            return new ComposedGremlinQuery(queryBase, $".limit({scope.ToString().ToLower()},{items})");
        }
    }
}