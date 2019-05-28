using System;
using System.Globalization;
using System.Linq;
using Stardust.Paradox.Data.Annotations.DataTypes;

namespace Stardust.Paradox.Data.Traversals
{
    public static class PredicateExtensions
    {
        public static GremlinQuery Is(this GremlinQuery queryBase, int value)
        {
            return new ComposedGremlinQuery(queryBase, $".is({queryBase.ComposeParameter(value)})");
        }

        public static GremlinQuery Is(this GremlinQuery queryBase, string value)
        {
            return new ComposedGremlinQuery(queryBase, $".is({queryBase.ComposeParameter(value)})");
        }

        public static GremlinQuery Is(this GremlinQuery queryBase, Func<PredicateGremlinQuery, GremlinQuery> expression)
        {
            return new LambdaComposedGremlinQuery(queryBase, "is({0})", query => expression.Invoke(new PredicateGremlinQuery(query)).CompileQuery());
        }

        public static GremlinQuery Not(this GremlinQuery queryBase, Func<PredicateGremlinQuery, GremlinQuery> expression)
        {
            return new LambdaComposedGremlinQuery(queryBase, "not({0})", query => expression.Invoke(new PredicateGremlinQuery(query)).CompileQuery());
        }
        public static GremlinQuery And(this GremlinQuery queryBase, Func<PredicateGremlinQuery, GremlinQuery> expression)
        {
            return new LambdaComposedGremlinQuery(queryBase, "and({0})", query => expression.Invoke(new PredicateGremlinQuery(query)).CompileQuery());
        }

	    public static GremlinQuery And(this GremlinQuery queryBase)
	    {
		    return new ComposedGremlinQuery(queryBase, "and()");
	    }

		public static GremlinQuery Or(this GremlinQuery queryBase, Func<PredicateGremlinQuery, GremlinQuery> expression)
        {
            return new LambdaComposedGremlinQuery(queryBase, "or({0})", query => expression.Invoke(new PredicateGremlinQuery(query)).CompileQuery());
        }

	    public static GremlinQuery Or(this GremlinQuery queryBase)
	    {
		    return new ComposedGremlinQuery(queryBase, "or()");
	    }

		public static GremlinQuery Optional(this GremlinQuery queryBase, Func<PredicateGremlinQuery, GremlinQuery> expression)
        {
            return new LambdaComposedGremlinQuery(queryBase, "optional({0})", query => expression.Invoke(new PredicateGremlinQuery(query)).CompileQuery());
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
            return new ComposedGremlinQuery(queryBase, $"eq({queryBase.ComposeParameter(value)})");
        }


	    public static GremlinQuery Eq(this PredicateGremlinQuery queryBase, EpochDateTime value)
	    {
		    return new ComposedGremlinQuery(queryBase, $"eq({queryBase.ComposeParameter(value)})");
	    }

		public static GremlinQuery Eq(this PredicateGremlinQuery queryBase, decimal value)
        {
            return new ComposedGremlinQuery(queryBase, $"eq({queryBase.ComposeParameter(value)})");
        }

        public static GremlinQuery Eq(this PredicateGremlinQuery queryBase, string value)
        {
            return new ComposedGremlinQuery(queryBase, $"eq({queryBase.ComposeParameter(value)})");
        }

        public static GremlinQuery Eq(this PredicateGremlinQuery queryBase, bool value)
        {
            return new ComposedGremlinQuery(queryBase, $"eq({queryBase.ComposeParameter(value)})");
        }

        public static GremlinQuery Neq(this PredicateGremlinQuery queryBase, long value)
        {
            return new ComposedGremlinQuery(queryBase, $"neq({queryBase.ComposeParameter(value)})");
        }

	    public static GremlinQuery Neq(this PredicateGremlinQuery queryBase, EpochDateTime value)
	    {
		    return new ComposedGremlinQuery(queryBase, $"neq({queryBase.ComposeParameter(value)})");
	    }

		public static GremlinQuery Neq(this PredicateGremlinQuery queryBase, decimal value)
        {
            return new ComposedGremlinQuery(queryBase, $"neq({queryBase.ComposeParameter(value)})");
        }

        public static GremlinQuery Neq(this PredicateGremlinQuery queryBase, string value)
        {
            return new ComposedGremlinQuery(queryBase, $"neq({queryBase.ComposeParameter(value)})");
        }

        public static GremlinQuery Neq(this PredicateGremlinQuery queryBase, bool value)
        {
            return new ComposedGremlinQuery(queryBase, $"neq({queryBase.ComposeParameter(value)})");
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
            return new ComposedGremlinQuery(queryBase, $"has({queryBase.ComposeParameter(key)})");
        }

        public static GremlinQuery HasNot(this GremlinQuery queryBase, string key)
        {
            return new ComposedGremlinQuery(queryBase, $"hasNot({queryBase.ComposeParameter(key)})");
        }

        public static GremlinQuery Has(this GremlinQuery queryBase, string key, Func<PredicateGremlinQuery, GremlinQuery> expression)
        {
            return new ComposedGremlinQuery(queryBase, $"has({queryBase.ComposeParameter(key)},{expression.Invoke(new PredicateGremlinQuery(queryBase))})");
        }

        public static GremlinQuery Has(this GremlinQuery queryBase, string label, string key, Func<PredicateGremlinQuery, GremlinQuery> expression)
        {
            return new ComposedGremlinQuery(queryBase, $"has({queryBase.ComposeParameter(label)},{queryBase.ComposeParameter(key)},{expression.Invoke(new PredicateGremlinQuery(queryBase))})");
        }

        public static GremlinQuery Has(this GremlinQuery queryBase, string key, string value)
        {
            return new ComposedGremlinQuery(queryBase, $"has({queryBase.ComposeParameter(key)},{queryBase.ComposeParameter(value)})");
        }

        public static GremlinQuery Has(this GremlinQuery queryBase, string key, bool value)
        {
            return new ComposedGremlinQuery(queryBase, $"has({queryBase.ComposeParameter(key)},{queryBase.ComposeParameter(value)})");
        }

	    public static GremlinQuery Has(this GremlinQuery queryBase, string key, EpochDateTime value)
	    {
		    return new ComposedGremlinQuery(queryBase, $"has({queryBase.ComposeParameter(key)},{queryBase.ComposeParameter(value)})");
	    }

		public static GremlinQuery Has(this GremlinQuery queryBase, string label, string key, string value)
        {
            return new ComposedGremlinQuery(queryBase, $"has({queryBase.ComposeParameter(label)},{queryBase.ComposeParameter(key)},{queryBase.ComposeParameter(value)})");
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

	    public static GremlinQuery Within(this PredicateGremlinQuery queryBase, params EpochDateTime[] values)
	    {
		    return queryBase.Params("within", values);
	    }

		public static GremlinQuery Without(this PredicateGremlinQuery queryBase, params string[] values)
        {
            return queryBase.Params("without", values);
        }

	    public static GremlinQuery Without(this PredicateGremlinQuery queryBase, params EpochDateTime[] values)
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
            return new ComposedGremlinQuery(queryBase, $"timeLimit({queryBase.ComposeParameter(timeInMs)})");
        }

        public static GremlinQuery Lt(this PredicateGremlinQuery queryBase, int value)
        {
            return new ComposedGremlinQuery(queryBase, $"lt({queryBase.ComposeParameter(value)})");
        }

	    public static GremlinQuery Lt(this PredicateGremlinQuery queryBase, EpochDateTime value)
	    {
		    return new ComposedGremlinQuery(queryBase, $"lt({queryBase.ComposeParameter(value)})");
	    }

		public static GremlinQuery Lt(this PredicateGremlinQuery queryBase, decimal value)
        {
            return new ComposedGremlinQuery(queryBase, $"lt({queryBase.ComposeParameter(value)})");
        }

        public static GremlinQuery Lte(this PredicateGremlinQuery queryBase, int value)
        {
            return new ComposedGremlinQuery(queryBase, $"lte({value})");
        }

	    public static GremlinQuery Lte(this PredicateGremlinQuery queryBase, EpochDateTime value)
	    {
		    return new ComposedGremlinQuery(queryBase, $"lte({value})");
	    }

		public static GremlinQuery Lte(this PredicateGremlinQuery queryBase, decimal value)
        {
            return new ComposedGremlinQuery(queryBase, $"lte({queryBase.ComposeParameter(value)})");
        }

        public static GremlinQuery Gt(this PredicateGremlinQuery queryBase, string value)
        {
            return new ComposedGremlinQuery(queryBase, $"gt({queryBase.ComposeParameter(value)})");
        }

	    public static GremlinQuery Gt(this PredicateGremlinQuery queryBase, EpochDateTime value)
	    {
		    return new ComposedGremlinQuery(queryBase, $"gt({queryBase.ComposeParameter(value)})");
	    }

		public static GremlinQuery Gt(this PredicateGremlinQuery queryBase, int value)
        {
            return new ComposedGremlinQuery(queryBase, $"gt({queryBase.ComposeParameter(value)})");
        }

        public static GremlinQuery Gt(this PredicateGremlinQuery queryBase, decimal value)
        {
            return new ComposedGremlinQuery(queryBase, $"gt({queryBase.ComposeParameter(value)})");
        }

        public static GremlinQuery Gte(this PredicateGremlinQuery queryBase, int value)
        {
            return new ComposedGremlinQuery(queryBase, $"gte({queryBase.ComposeParameter(value)})");
        }

	    public static GremlinQuery Gte(this PredicateGremlinQuery queryBase, EpochDateTime value)
	    {
		    return new ComposedGremlinQuery(queryBase, $"gte({queryBase.ComposeParameter(value)})");
	    }

		public static GremlinQuery Gte(this PredicateGremlinQuery queryBase, decimal value)
        {
            return new ComposedGremlinQuery(queryBase, $"gte({queryBase.ComposeParameter(value)})");
        }


        public static GremlinQuery Inside(this PredicateGremlinQuery queryBase, int start, int end)
        {
            return new ComposedGremlinQuery(queryBase, $"inside({queryBase.ComposeParameter(start)},{queryBase.ComposeParameter(end)})");
        }

        public static GremlinQuery Inside(this PredicateGremlinQuery queryBase, decimal start, decimal end)
        {
            return new ComposedGremlinQuery(queryBase, $"inside({queryBase.ComposeParameter(start)},{queryBase.ComposeParameter(end)})");
        }

	    public static GremlinQuery Inside(this PredicateGremlinQuery queryBase, EpochDateTime start, EpochDateTime end)
	    {
		    return new ComposedGremlinQuery(queryBase, $"inside({queryBase.ComposeParameter(start)},{queryBase.ComposeParameter(end)})");
	    }

		public static GremlinQuery Outside(this PredicateGremlinQuery queryBase, int start, int end)
        {
            return new ComposedGremlinQuery(queryBase, $"outside({queryBase.ComposeParameter(start)},{queryBase.ComposeParameter(end)})");
        }

	    public static GremlinQuery Outside(this PredicateGremlinQuery queryBase, EpochDateTime start, EpochDateTime end)
	    {
		    return new ComposedGremlinQuery(queryBase, $"outside({queryBase.ComposeParameter(start)},{queryBase.ComposeParameter(end)})");
	    }

		public static GremlinQuery Outside(this PredicateGremlinQuery queryBase, decimal start, decimal end)
        {
            return new ComposedGremlinQuery(queryBase, $"outside({queryBase.ComposeParameter(start)},{queryBase.ComposeParameter(end)})");
        }

        public static GremlinQuery Between(this PredicateGremlinQuery queryBase, int start, int end)
        {
            return new ComposedGremlinQuery(queryBase, $"between({queryBase.ComposeParameter(start)},{queryBase.ComposeParameter(end)})");
        }

	    public static GremlinQuery Between(this PredicateGremlinQuery queryBase, EpochDateTime start, EpochDateTime end)
	    {
		    return new ComposedGremlinQuery(queryBase, $"between({queryBase.ComposeParameter(start)},{queryBase.ComposeParameter(end)})");
	    }

		public static GremlinQuery Between(this PredicateGremlinQuery queryBase, decimal start, decimal end)
        {
            return new ComposedGremlinQuery(queryBase, $"between({queryBase.ComposeParameter(start)},{queryBase.ComposeParameter(end)})");
        }

        public static GremlinQuery StartingWith(this PredicateGremlinQuery queryBase, string value)
        {
            return new ComposedGremlinQuery(queryBase, $"startingWith({queryBase.ComposeParameter(value)})");
        }

        public static GremlinQuery NotStartingWith(this PredicateGremlinQuery queryBase, string value)
        {
            return new ComposedGremlinQuery(queryBase, $"notStartingWith({queryBase.ComposeParameter(value)})");
        }

        public static GremlinQuery EndingWith(this PredicateGremlinQuery queryBase, string value)
        {
            return new ComposedGremlinQuery(queryBase, $"endingWith({queryBase.ComposeParameter(value)})");
        }

        public static GremlinQuery NotEndingWith(this PredicateGremlinQuery queryBase, string value)
        {
            return new ComposedGremlinQuery(queryBase, $"notEndingWith({queryBase.ComposeParameter(value)})");
        }

        public static GremlinQuery Containing(this PredicateGremlinQuery queryBase, string value)
        {
            return new ComposedGremlinQuery(queryBase, $"containing({queryBase.ComposeParameter(value)})");
        }

        public static GremlinQuery NotContaining(this PredicateGremlinQuery queryBase, string value)
        {
            return new ComposedGremlinQuery(queryBase, $"notContaining({queryBase.ComposeParameter(value)})");
        }
    }
}