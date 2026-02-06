using System.Linq;
using Stardust.Paradox.Data.Annotations.DataTypes;

namespace Stardust.Paradox.Data.Traversals
{
    public static class BarrierStepExtensions
    {
        public static GremlinQuery Barrier(this GremlinQuery queryBase)
        {
            return new ComposedGremlinQuery(queryBase, ".barrier()");
        }

        /// <summary>
        ///     Not supported by all gremlin implementations
        /// </summary>
        /// <param name="queryBase"></param>
        /// <returns></returns>
        public static GremlinQuery Explain(this GremlinQuery queryBase)
        {
            return new ComposedGremlinQuery(queryBase, ".explain()");
        }

        public static GremlinQuery Cap(this GremlinQuery queryBase, params string[] values)
        {
            return queryBase.Params("cap", values);
        }

        internal static GremlinQuery Params(this GremlinQuery queryBase, string name, params string[] values)
        {
            return new ComposedGremlinQuery(queryBase,
                $".{name}({string.Join(",", values.Select(s => $"{queryBase.ComposeParameter(s)}"))})");
        }

        internal static GremlinQuery Params(this GremlinQuery queryBase, string name, params EpochDateTime[] values)
        {
            return new ComposedGremlinQuery(queryBase,
                $".{name}({string.Join(",", values.Select(s => $"{queryBase.ComposeParameter(s)}"))})");
        }

        internal static GremlinQuery Params(this GremlinQuery queryBase, string name, params int[] values)
        {
            return new ComposedGremlinQuery(queryBase,
                $".{name}({string.Join(",", values.Select(s => queryBase.ComposeParameter(s)))})");
        }

        internal static GremlinQuery Params(this GremlinQuery queryBase, string name, params long[] values)
        {
            return new ComposedGremlinQuery(queryBase,
                $".{name}({string.Join(",", values.Select(s => queryBase.ComposeParameter(s)))})");
        }

        internal static GremlinQuery Params(this GremlinQuery queryBase, string name, params decimal[] values)
        {
            return new ComposedGremlinQuery(queryBase,
                $".{name}({string.Join(",", values.Select(s => queryBase.ComposeParameter(s)))})");
        }

        internal static GremlinQuery Params(this GremlinQuery queryBase, string name, params bool[] values)
        {
            return new ComposedGremlinQuery(queryBase,
                $".{name}({string.Join(",", values.Select(s => queryBase.ComposeParameter(s)))})");
        }
    }
}