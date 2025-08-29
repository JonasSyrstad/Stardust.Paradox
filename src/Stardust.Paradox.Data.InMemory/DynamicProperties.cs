using System.Collections.Generic;
using System.Dynamic;

namespace Stardust.Paradox.Data.InMemory
{
    /// <summary>
    /// Dynamic wrapper for properties dictionary to support dynamic property access
    /// </summary>
    public class DynamicProperties : DynamicObject
    {
        private readonly Dictionary<string, object> _properties;

        public DynamicProperties(Dictionary<string, object> properties)
        {
            _properties = properties ?? new Dictionary<string, object>();
        }

        public override bool TryGetMember(GetMemberBinder binder, out object result)
        {
            return _properties.TryGetValue(binder.Name, out result);
        }

        public override bool TrySetMember(SetMemberBinder binder, object value)
        {
            _properties[binder.Name] = value;
            return true;
        }

        public override IEnumerable<string> GetDynamicMemberNames()
        {
            return _properties.Keys;
        }

        public Dictionary<string, object> GetProperties()
        {
            return _properties;
        }

        public override string ToString()
        {
            return $"DynamicProperties({_properties.Count} items)";
        }
    }
}
