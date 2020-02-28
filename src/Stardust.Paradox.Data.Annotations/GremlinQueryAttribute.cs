using System;

namespace Stardust.Paradox.Data.Annotations
{
    [AttributeUsage(AttributeTargets.Property)]
    public class GremlinQueryAttribute : Attribute
    {
        public GremlinQueryAttribute(string query)
        {
            Query = query;
        }

        public string Query { get; }
    }
}