using System;

namespace Stardust.Paradox.Data.Annotations
{
    [AttributeUsage(AttributeTargets.Property)]
    public class GremlinQueryAttribute : Attribute
    {
        public string Query { get; }

        public GremlinQueryAttribute(string query)
        {
            Query = query;
        }
    }
}