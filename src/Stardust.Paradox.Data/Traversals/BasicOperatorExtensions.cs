using System;
using System.Linq;

namespace Stardust.Paradox.Data.Traversals
{
    //public static class GraphStepExtensions
    //{
    //    public 

    //    public static GremlinQuery Variables(this GremlinQuery queryBase, string edgeLabel)
    //    {
    //        return new ComposedGremlinQuery(queryBase, $".E('{edgeLabel}')");
    //    }
    //}

    public static class BasicOperatorExtensions
    {
        public static GremlinQuery Drop(this GremlinQuery queryBase)
        {
            return new ComposedGremlinQuery(queryBase, $".drop()");
        }

        public static GremlinQuery As(this GremlinQuery queryBase, string name)
        {
            return new ComposedGremlinQuery(queryBase, $".as('{name}')");
        }

        public static GremlinQuery V(this GremlinQuery queryBase, string id)
        {
            return new ComposedGremlinQuery(queryBase, $".V('{id}')");
        }

        public static GremlinQuery V(this GremlinQuery queryBase)
        {
            return new ComposedGremlinQuery(queryBase, $".V()");
        }

        public static GremlinQuery Constant(this GremlinQuery queryBase, string value)
        {
            return new ComposedGremlinQuery(queryBase, $".constant('{value}')");
        }

        public static GremlinQuery Constant(this GremlinQuery queryBase, long value)
        {
            return new ComposedGremlinQuery(queryBase, $".constant({value})");
        }

        public static GremlinQuery Constant(this GremlinQuery queryBase, bool value)
        {
            return new ComposedGremlinQuery(queryBase, $".constant({value.ToString().ToLower()})");
        }

        public static GremlinQuery Values(this GremlinQuery queryBase, string name)
        {
            return new ComposedGremlinQuery(queryBase, $".values('{name}')");
        }

        public static GremlinQuery Value(this GremlinQuery queryBase)
        {
            return new ComposedGremlinQuery(queryBase, $".value()");
        }

        public static GremlinQuery Emit(this GremlinQuery queryBase)
        {
            return new ComposedGremlinQuery(queryBase, $".emit()");
        }

        public static GremlinQuery Sack(this GremlinQuery queryBase)
        {
            return new ComposedGremlinQuery(queryBase, $".sack()");
        }

        public static GremlinQuery Sack(this GremlinQuery queryBase, SackTypes type)
        {
            return new ComposedGremlinQuery(queryBase, $".sack({type.ToString().ToLower()})");
        }

        public static GremlinQuery ValueMap(this GremlinQuery queryBase)
        {
            return new ComposedGremlinQuery(queryBase, $".valueMap()");
        }

        public static GremlinQuery ValueMap(this GremlinQuery queryBase, bool inclueAll)
        {
            return new ComposedGremlinQuery(queryBase, $".valueMap({inclueAll.ToString().ToLower()})");
        }

        public static GremlinQuery ValueMap(this GremlinQuery queryBase, params string[] names)
        {
            return queryBase.Params("valueMap", names);
        }

        public static GremlinQuery ValueMap(this GremlinQuery queryBase, bool inclueAll, params string[] names)
        {
            return new ComposedGremlinQuery(queryBase, $".valueMap({inclueAll.ToString().ToLower()},{string.Join(",", names.Select(s => $"'{s}'"))})");
        }

        public static GremlinQuery PropertiesMap(this GremlinQuery queryBase, params string[] names)
        {
            return new ComposedGremlinQuery(queryBase, $".propertiesMap({string.Join(",", names.Select(s => $"'{s}'"))})");
        }

        public static GremlinQuery GroupBy(this GremlinQuery queryBase, string name)
        {
            return new ComposedGremlinQuery(queryBase, $".group().by('{name}')");
        }

        public static GremlinQuery Id(this GremlinQuery queryBase)
        {
            return new ComposedGremlinQuery(queryBase, $".id()");
        }

        public static GremlinQuery Local(this GremlinQuery queryBase, Func<GremlinQuery, GremlinQuery> expression)
        {
            return new LambdaComposedGremlinQuery(queryBase, $"local({{0}})", expression);
        }

        public static GremlinQuery Profile(this GremlinQuery queryBase)
        {
            return new ComposedGremlinQuery(queryBase, $"profile()");
        }
    }
}