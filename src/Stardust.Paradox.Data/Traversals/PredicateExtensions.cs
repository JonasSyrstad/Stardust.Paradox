using System;
using System.Globalization;
using System.Linq;

namespace Stardust.Paradox.Data.Traversals
{
    public static class PredicateExtensions
    {
        public static GremlinQuery Is(this GremlinQuery queryBase, int value)
        {
            return new ComposedGremlinQuery(queryBase, $".is({value})");
        }

        public static GremlinQuery Is(this GremlinQuery queryBase, string value)
        {
            return new ComposedGremlinQuery(queryBase, $".is('{value}')");
        }

        public static GremlinQuery Is(this GremlinQuery queryBase, Func<PredicateGremlinQuery, GremlinQuery> expression)
        {
            return new LambdaComposedGremlinQuery(queryBase, "is({0})", query => expression.Invoke(new PredicateGremlinQuery(query._connector)).CompileQuery());
        }

        public static GremlinQuery Not(this GremlinQuery queryBase, Func<PredicateGremlinQuery, GremlinQuery> expression)
        {
            return new LambdaComposedGremlinQuery(queryBase, "not({0})", query => expression.Invoke(new PredicateGremlinQuery(query._connector)).CompileQuery());
        }
        public static GremlinQuery And(this GremlinQuery queryBase, Func<PredicateGremlinQuery, GremlinQuery> expression)
        {
            return new LambdaComposedGremlinQuery(queryBase, "and({0})", query => expression.Invoke(new PredicateGremlinQuery(query._connector)).CompileQuery());
        }

        public static GremlinQuery Or(this GremlinQuery queryBase, Func<PredicateGremlinQuery, GremlinQuery> expression)
        {
            return new LambdaComposedGremlinQuery(queryBase, "or({0})", query => expression.Invoke(new PredicateGremlinQuery(query._connector)).CompileQuery());
        }

        public static GremlinQuery Optional(this GremlinQuery queryBase, Func<PredicateGremlinQuery, GremlinQuery> expression)
        {
            return new LambdaComposedGremlinQuery(queryBase, "optional({0})", query => expression.Invoke(new PredicateGremlinQuery(query._connector)).CompileQuery());
        }

        public static GremlinQuery __(this PredicateGremlinQuery queryBase)
        {
            return new ComposedGremlinQuery(queryBase, $"__.");
        }
        public static GremlinQuery P(this PredicateGremlinQuery queryBase)
        {
            return new ComposedGremlinQuery(queryBase, $"P.");
        }

        public static GremlinQuery Eq(this PredicateGremlinQuery queryBase, int value)
        {
            return new ComposedGremlinQuery(queryBase, $"eq({value})");
        }

        public static GremlinQuery Eq(this PredicateGremlinQuery queryBase, decimal value)
        {
            return new ComposedGremlinQuery(queryBase, $"eq({value.ToString(CultureInfo.InvariantCulture)})");
        }

        public static GremlinQuery Eq(this PredicateGremlinQuery queryBase, string value)
        {
            return new ComposedGremlinQuery(queryBase, $"eq('{value}')");
        }

        public static GremlinQuery Eq(this PredicateGremlinQuery queryBase, bool value)
        {
            return new ComposedGremlinQuery(queryBase, $"eq({value.ToString().ToLower()})");
        }

        public static GremlinQuery Neq(this PredicateGremlinQuery queryBase, long value)
        {
            return new ComposedGremlinQuery(queryBase, $"neq({value})");
        }

        public static GremlinQuery Neq(this PredicateGremlinQuery queryBase, decimal value)
        {
            return new ComposedGremlinQuery(queryBase, $"neq({value.ToString(CultureInfo.InvariantCulture)})");
        }

        public static GremlinQuery Neq(this PredicateGremlinQuery queryBase, string value)
        {
            return new ComposedGremlinQuery(queryBase, $"neq('{value}')");
        }

        public static GremlinQuery Neq(this PredicateGremlinQuery queryBase, bool value)
        {
            return new ComposedGremlinQuery(queryBase, $"neq({value.ToString().ToLower()})");
        }
        public static GremlinQuery Identity(this PredicateGremlinQuery queryBase)
        {
            return new ComposedGremlinQuery(queryBase, $"identity()");
        }

        public static GremlinQuery HasLabel(this GremlinQuery queryBase,params string[] labels)
        {
            return queryBase.Params("hasLabel", labels);
        }

        public static GremlinQuery HasKey(this GremlinQuery queryBase, params string[] keys)
        {
            return queryBase.Params("hasKey", keys);
        }

        public static GremlinQuery HasId(this GremlinQuery queryBase, params string[] ids)
        {
            return queryBase.Params("hasId", ids);
        }
        public static GremlinQuery HasValue(this GremlinQuery queryBase, params string[] values)
        {
            return queryBase.Params("hasValue", values);
        }

        public static GremlinQuery Has(this GremlinQuery queryBase, string key)
        {
            return new ComposedGremlinQuery(queryBase, $"has('{key}')");
        }

        public static GremlinQuery HasNot(this GremlinQuery queryBase, string key)
        {
            return new ComposedGremlinQuery(queryBase, $"hasNot('{key}')");
        }

        public static GremlinQuery Has(this GremlinQuery queryBase, string key, Func<PredicateGremlinQuery, GremlinQuery> expression)
        {
            return new ComposedGremlinQuery(queryBase, $"has('{key}','{expression.Invoke(new PredicateGremlinQuery(queryBase._connector))}')");
        }

        public static GremlinQuery Has(this GremlinQuery queryBase, string label, string key, Func<PredicateGremlinQuery, GremlinQuery> expression)
        {
            return new ComposedGremlinQuery(queryBase, $"has('{label}','{key}','{expression.Invoke(new PredicateGremlinQuery(queryBase._connector))}')");
        }

        public static GremlinQuery Has(this GremlinQuery queryBase, string key, string value)
        {
            return new ComposedGremlinQuery(queryBase, $"has('{key}','{value}')");
        }

        public static GremlinQuery Has(this GremlinQuery queryBase, string key, bool value)
        {
            return new ComposedGremlinQuery(queryBase, $"has('{key}',{value.ToString().ToLower()})");
        }
        public static GremlinQuery Has(this GremlinQuery queryBase, string label, string key, string value)
        {
            return new ComposedGremlinQuery(queryBase, $"has('{label}','{key}','{value}')");
        }

        public static GremlinQuery Within(this PredicateGremlinQuery queryBase, params string[] values)
        {
            return queryBase.Params("within", values);
        }
        public static GremlinQuery Within(this PredicateGremlinQuery queryBase, params int[] values)
        {
            return queryBase.Params("within", values);
        }
        public static GremlinQuery Within(this PredicateGremlinQuery queryBase,params decimal[] values)
        {
            return queryBase.Params("within", values);
        }

        public static GremlinQuery Without(this PredicateGremlinQuery queryBase, params string[] values)
        {
            return queryBase.Params("without", values);
        }
        public static GremlinQuery Without(this PredicateGremlinQuery queryBase, params int[] values)
        {
            return queryBase.Params("without", values);
        }
        public static GremlinQuery Without(this PredicateGremlinQuery queryBase, params decimal[] values)
        {
            return queryBase.Params("without", values);
        }

        public static GremlinQuery TimeLimit(this PredicateGremlinQuery queryBase, int timeInMs)
        {
            return new ComposedGremlinQuery(queryBase, $"timeLimit({timeInMs})");
        }

        public static GremlinQuery Lt(this PredicateGremlinQuery queryBase, int value)
        {
            return new ComposedGremlinQuery(queryBase, $"lt({value})");
        }

        public static GremlinQuery Lt(this PredicateGremlinQuery queryBase, decimal value)
        {
            return new ComposedGremlinQuery(queryBase, $"lt({value.ToString(CultureInfo.InvariantCulture)})");
        }

        public static GremlinQuery Lte(this PredicateGremlinQuery queryBase, int value)
        {
            return new ComposedGremlinQuery(queryBase, $"lte({value})");
        }

        public static GremlinQuery Lte(this PredicateGremlinQuery queryBase, decimal value)
        {
            return new ComposedGremlinQuery(queryBase, $"lte({value.ToString(CultureInfo.InvariantCulture)})");
        }

        public static GremlinQuery Gt(this PredicateGremlinQuery queryBase, string value)
        {
            return new ComposedGremlinQuery(queryBase, $"gt('{value}')");
        }

        public static GremlinQuery Gt(this PredicateGremlinQuery queryBase, int value)
        {
            return new ComposedGremlinQuery(queryBase, $"gt({value})");
        }

        public static GremlinQuery Gt(this PredicateGremlinQuery queryBase, decimal value)
        {
            return new ComposedGremlinQuery(queryBase, $"gt({value.ToString(CultureInfo.InvariantCulture)})");
        }

        public static GremlinQuery Gte(this PredicateGremlinQuery queryBase, int value)
        {
            return new ComposedGremlinQuery(queryBase, $"gte({value})");
        }

        public static GremlinQuery Gte(this PredicateGremlinQuery queryBase, decimal value)
        {
            return new ComposedGremlinQuery(queryBase, $"gte({value.ToString(CultureInfo.InvariantCulture)})");
        }

        public static GremlinQuery Inside(this PredicateGremlinQuery queryBase, int start, int end)
        {
            return new ComposedGremlinQuery(queryBase, $"inside({start},{end})");
        }

        public static GremlinQuery Inside(this PredicateGremlinQuery queryBase, decimal start, decimal end)
        {
            return new ComposedGremlinQuery(queryBase, $"inside({start.ToString(CultureInfo.InvariantCulture)},{end.ToString(CultureInfo.InvariantCulture)})");
        }

        public static GremlinQuery Outside(this PredicateGremlinQuery queryBase, int start, int end)
        {
            return new ComposedGremlinQuery(queryBase, $"outside({start},{end})");
        }

        public static GremlinQuery Outside(this PredicateGremlinQuery queryBase, decimal start, decimal end)
        {
            return new ComposedGremlinQuery(queryBase, $"outside({start.ToString(CultureInfo.InvariantCulture)},{end.ToString(CultureInfo.InvariantCulture)})");
        }

        public static GremlinQuery Between(this PredicateGremlinQuery queryBase, int start, int end)
        {
            return new ComposedGremlinQuery(queryBase, $"between({start},{end})");
        }

        public static GremlinQuery Between(this PredicateGremlinQuery queryBase, decimal start, decimal end)
        {
            return new ComposedGremlinQuery(queryBase, $"between({start.ToString(CultureInfo.InvariantCulture)},{end.ToString(CultureInfo.InvariantCulture)})");
        }
    }
}