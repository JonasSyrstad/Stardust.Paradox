using System;
using System.Runtime.CompilerServices;

namespace Stardust.Paradox.Data.Traversals
{
    public static class UpdateStepExtensions
    {
        public static GremlinQuery AddV(this GremlinQuery queryBase)
        {
            return new ComposedGremlinQuery(queryBase, $"addV()");
        }

        public static GremlinQuery AddV(this GremlinQuery queryBase,string label)
        {
            return new ComposedGremlinQuery(queryBase, $"addV('{label}')");
        }   
            
        public static GremlinQuery Property(this GremlinQuery queryBase,string name,string value)
        {
            return queryBase.Params("property", name, value);
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