using System;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace Stardust.Paradox.Data.Traversals
{
    public static class UpdateStepExtensions
    {
        public static GremlinQuery AddV(this GremlinQuery queryBase)
        {
            return new ComposedGremlinQuery(queryBase, $"addV()");
        }

        public static GremlinQuery AddV(this GremlinQuery queryBase, string label)
        {
            return new ComposedGremlinQuery(queryBase, $"addV('{label}')");
        }

        public static GremlinQuery Property(this GremlinQuery queryBase, string name, string value)
        {
            return queryBase.Params("property", name, value);
        }

        public static GremlinQuery Property(this GremlinQuery queryBase, string name, object value)
        {
            if (value is int i)
            {
                return Params(queryBase, "property", name, i);
            }
            if (value is long l)
            {
                return Params(queryBase, "property", name, l);
            }
            if (value is bool b)
            {
                return Params(queryBase, "property", name, b);
            }
            if (value is decimal d)
            {
                return Params(queryBase, "property", name, d);
            }
            if (value is double dd)
            {
                return Params(queryBase, "property", name, dd);
            }
            if (value is DateTime dt)
            {
                return Params(queryBase, "property", name, dt);
            }

            if (value is string s)
                return queryBase.Params("property", name, s);
            return queryBase.Params(name, value.ToString());

        }

        private static GremlinQuery Params(this GremlinQuery queryBase, string property, string name, long p2)
        {
            return new ComposedGremlinQuery(queryBase, $"{property}('{name}',{p2.ToString(CultureInfo.InvariantCulture)})");
        }

        private static GremlinQuery Params(this GremlinQuery queryBase, string property, string name, decimal p2)
        {
            return new ComposedGremlinQuery(queryBase, $"{property}('{name}',{p2.ToString(CultureInfo.InvariantCulture)})");
        }

        private static GremlinQuery Params(this GremlinQuery queryBase, string property, string name, bool p2)
        {
            return new ComposedGremlinQuery(queryBase, $"{property}('{name}',{p2.ToString(CultureInfo.InvariantCulture)})");
        }

        private static GremlinQuery Params(this GremlinQuery queryBase, string property, string name, DateTime p2)
        {
            return new ComposedGremlinQuery(queryBase, $"{property}('{name}',{p2.ToString(CultureInfo.InvariantCulture)})");
        }

        private static GremlinQuery Params(this GremlinQuery queryBase, string property, string name, double p2)
        {
            return new ComposedGremlinQuery(queryBase, $"{property}('{name}',{p2.ToString(CultureInfo.InvariantCulture)})");
        }

        private static GremlinQuery Params(this GremlinQuery queryBase, string property, string name, float p2)
        {
            return new ComposedGremlinQuery(queryBase, $"{property}('{name}',{p2.ToString(CultureInfo.InvariantCulture)})");
        }

        public static GremlinQuery Property(this GremlinQuery queryBase, string name, Func<PredicateGremlinQuery, GremlinQuery> expression)
        {
            return new LambdaComposedGremlinQuery(queryBase, $"property('{name}',{{0}})", query => expression.Invoke(new PredicateGremlinQuery(query._connector)).CompileQuery());
        }
        public static GremlinQuery Properties(this GremlinQuery queryBase, string name)
        {
            return queryBase.Params("properties", name);
        }

        public static GremlinQuery Properties(this GremlinQuery queryBase)
        {
            return new ComposedGremlinQuery(queryBase, $"properties()");
        }

        public static GremlinQuery AddE(this GremlinQuery queryBase, string label)
        {
            return new ComposedGremlinQuery(queryBase, $"addE('{label}')");
        }

        public static GremlinQuery Inject(this GremlinQuery queryBase, params string[] values)
        {
            return queryBase.Params("inject", values);
        }
        public static GremlinQuery Inject(this GremlinQuery queryBase, params long[] values)
        {
            return queryBase.Params("inject", values);
        }

        public static GremlinQuery Inject(this GremlinQuery queryBase, params decimal[] values)
        {
            return queryBase.Params("inject", values);
        }
    }
}