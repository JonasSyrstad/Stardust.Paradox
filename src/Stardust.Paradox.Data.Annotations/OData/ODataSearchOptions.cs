using System;
using System.Collections.Generic;
using System.Text;

namespace Stardust.Paradox.Data.Annotations.OData
{
    public class ODataSearchOptions
    {
        public string Search { get; set; }
        
        public string[] SearchableProperties { get; set; }
        
        public string Filter { get; set; }
        
        public int? Top { get; set; }
        
        public int? Take { get; set; }
        
        public int? Tail { get; set; }
        
        public OrderingOptions OrderBy { get; set; }
    }

    public class OrderingOptions
    {
        public string PropertyName { get; set; }
        
        public OrderingTypes Ordering { get; set; }
    }

    public enum OrderingTypes
    {
        Ascending,
        Descending
    }
}
