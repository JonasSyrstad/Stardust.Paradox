using System;
using System.Globalization;

namespace Stardust.Paradox.Data.Traversals
{
    public static class MapAndAggregationExtensions
    {
        public static GremlinQuery Fold(this GremlinQuery queryBase)
        {
            return new ComposedGremlinQuery(queryBase, $"fold()");
        }

        public static GremlinQuery Unfold(this GremlinQuery queryBase)
        {
            return new ComposedGremlinQuery(queryBase, $"unfold()");
        }

        public static GremlinQuery Tail(this GremlinQuery queryBase)
        {
            return new ComposedGremlinQuery(queryBase, $"tail()");
        }

        public static GremlinQuery Tail(this GremlinQuery queryBase, int last)
        {
            return new ComposedGremlinQuery(queryBase, $"tail({last})");
        }

        public static GremlinQuery Tail(this GremlinQuery queryBase, GraphScope scope)
        {
            return new ComposedGremlinQuery(queryBase, $"tail({scope.ToString().ToLower()})");
        }

        public static GremlinQuery Tail(this GremlinQuery queryBase, GraphScope scope, int last)
        {
            return new ComposedGremlinQuery(queryBase, $"tail({scope.ToString().ToLower()},{last})");
        }

        

        public static GremlinQuery Sum(this GremlinQuery queryBase)
        {
            return new ComposedGremlinQuery(queryBase, $"sum()");
        }

        public static GremlinQuery Max(this GremlinQuery queryBase)
        {
            return new ComposedGremlinQuery(queryBase, $"max()");
        }

        public static GremlinQuery Min(this GremlinQuery queryBase)
        {
            return new ComposedGremlinQuery(queryBase, $"min()");
        }

        public static GremlinQuery Mean(this GremlinQuery queryBase)
        {
            return new ComposedGremlinQuery(queryBase, $"mean()");
        }

        public static GremlinQuery Union(this GremlinQuery queryBase,  params Func<GremlinQuery, string>[] expression)
        {
            return new LambdaComposedGremlinQuery(queryBase, $"union({{0}})", expression);
        }

        public static GremlinQuery Coin(this GremlinQuery queryBase,double bias)
        {
            return new ComposedGremlinQuery(queryBase, $"coin({bias.ToString("N",CultureInfo.InvariantCulture)})");
        }

        public static GremlinQuery PeerPressure(this GremlinQuery queryBase)
        {
            return new ComposedGremlinQuery(queryBase, $"peerPressure()");
        }

        public static GremlinQuery Label(this GremlinQuery queryBase)
        {   
            return new ComposedGremlinQuery(queryBase, $"label()");
        }
    }
}