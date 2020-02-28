using System;
using System.Linq;
using Stardust.Paradox.Data.Annotations;
using Stardust.Particles.Collection.Arrays;

namespace Stardust.Paradox.Data.Traversals
{
    public static class ModulationStepExtensions
    {
        public static GremlinQuery By(this GremlinQuery queryBase, OrderingTypes ordering)
        {
            return new ComposedGremlinQuery(queryBase, $".by({ordering.ToString().ToLower()})");
        }

        public static GremlinQuery By(this GremlinQuery queryBase, string name, OrderingTypes ordering)
        {
            return new ComposedGremlinQuery(queryBase,
                $".by({queryBase.ComposeParameter(name)},{ordering.ToString().ToLower()})");
        }

        public static GremlinQuery By(this GremlinQuery queryBase)
        {
            return new ComposedGremlinQuery(queryBase, ".by()");
        }

        public static GremlinQuery By(this GremlinQuery queryBase, string name)
        {
            return new ComposedGremlinQuery(queryBase, $".by({queryBase.ComposeParameter(name)})");
        }

        public static GremlinQuery By(this GremlinQuery queryBase, Func<GremlinQuery, GremlinQuery> inner)
        {
            return new LambdaComposedGremlinQuery(queryBase, ".by('{0}')",
                query => inner.Invoke(new PredicateGremlinQuery(query)));
        }

        public static GremlinQuery Dedup(this GremlinQuery queryBase)
        {
            return new ComposedGremlinQuery(queryBase, ".dedup()");
        }

        public static GremlinQuery CyclicPath(this GremlinQuery queryBase)
        {
            return new ComposedGremlinQuery(queryBase, ".cyclicPath()");
        }

        public static GremlinQuery SimplePath(this GremlinQuery queryBase)
        {
            return new ComposedGremlinQuery(queryBase, ".simplePath()");
        }

        public static GremlinQuery Sample(this GremlinQuery queryBase, int startVertex)
        {
            return new ComposedGremlinQuery(queryBase, $".sample({queryBase.ComposeParameter(startVertex)})");
        }

        public static GremlinQuery Where(this GremlinQuery queryBase, string name,
            Func<PredicateGremlinQuery, GremlinQuery> expression)
        {
            return new LambdaComposedGremlinQuery(queryBase, $".where('{name.EscapeGremlinString()}',{{0}})",
                query => expression.Invoke(new PredicateGremlinQuery(query)).CompileQuery());
        }

        public static GremlinQuery Where(this GremlinQuery queryBase,
            Func<PredicateGremlinQuery, GremlinQuery> expression)
        {
            return new LambdaComposedGremlinQuery(queryBase, ".where({0})",
                query => expression.Invoke(new PredicateGremlinQuery(query)).CompileQuery());
        }

        public static GremlinQuery Match(this GremlinQuery queryBase,
            params Func<PredicateGremlinQuery, GremlinQuery>[] expression)
        {
            return new LambdaComposedGremlinQuery(queryBase, ".match({0})",
                query => string.Join(",",
                    expression.Select(i => i.Invoke(new PredicateGremlinQuery(query)).CompileQuery())));
        }

        public static GremlinQuery Select(this GremlinQuery queryBase, params string[] items)
        {
            return new ComposedGremlinQuery(queryBase,
                $".select({string.Join(",", items.Select(s => $"{queryBase.ComposeParameter(s)}"))})");
        }

        public static GremlinQuery Select(this GremlinQuery queryBase, SelectTypes selectType, params string[] items)
        {
            if (items.ContainsElements())
                return new ComposedGremlinQuery(queryBase,
                    $".select({selectType.ToString().ToLower()},{string.Join(",", items.Select(s => $"{queryBase.ComposeParameter(s)}"))})");
            return new ComposedGremlinQuery(queryBase, $".select({selectType.ToString().ToLower()})");
        }

        public static GremlinQuery Count(this GremlinQuery queryBase)
        {
            return new ComposedGremlinQuery(queryBase, ".count()");
        }

        public static GremlinQuery GroupCount(this GremlinQuery queryBase)
        {
            return new ComposedGremlinQuery(queryBase, ".groupCount()");
        }

        public static GremlinQuery Group(this GremlinQuery queryBase)
        {
            return new ComposedGremlinQuery(queryBase, ".group()");
        }

        public static GremlinQuery Order(this GremlinQuery queryBase)
        {
            return new ComposedGremlinQuery(queryBase, ".order()");
        }

        public static GremlinQuery Path(this GremlinQuery queryBase)
        {
            return new ComposedGremlinQuery(queryBase, ".path()");
        }

        public static GremlinQuery Project(this GremlinQuery queryBase, params string[] values)
        {
            return new ComposedGremlinQuery(queryBase,
                $".project({string.Join(",", values.Select(s => $"{queryBase.ComposeParameter(s)}"))})");
        }

        public static GremlinQuery Tree(this GremlinQuery queryBase)
        {
            return new ComposedGremlinQuery(queryBase, ".tree()");
        }

        public static GremlinQuery Aggregate(this GremlinQuery queryBase, string value)
        {
            return new ComposedGremlinQuery(queryBase, $".aggregate({queryBase.ComposeParameter(value)})");
        }

        public static GremlinQuery Store(this GremlinQuery queryBase, string value)
        {
            return new ComposedGremlinQuery(queryBase, $".store({queryBase.ComposeParameter(value)})");
        }

        public static GremlinQuery From(this GremlinQuery queryBase)
        {
            return new ComposedGremlinQuery(queryBase, ".from()");
        }

        public static GremlinQuery To(this GremlinQuery queryBase)
        {
            return new ComposedGremlinQuery(queryBase, ".to()");
        }

        public static GremlinQuery From(this GremlinQuery queryBase, string key)
        {
            return new ComposedGremlinQuery(queryBase, $".from('{key}')");
        }

        public static GremlinQuery To(this GremlinQuery queryBase, string key)
        {
            return new ComposedGremlinQuery(queryBase, $".to('{key}')");
        }
    }

    public enum SelectTypes
    {
        First,
        Last,
        All
    }
}