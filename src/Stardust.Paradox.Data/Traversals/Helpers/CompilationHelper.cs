using System;
using System.Collections.Generic;
using System.Text;

namespace Stardust.Paradox.Data.Traversals.Helpers
{
    public static class CompilationHelper
    {
        public static string CompileQuery(this GremlinQuery query)
        {
            return query.CompileQuery();
        }
    }
}
