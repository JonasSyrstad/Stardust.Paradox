using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Stardust.Paradox.Data.Internals
{
    class GraphJsonConverter : JsonConverter
    {
        internal static void AddPair(Type contract, Type implementation)
        {
            _interfaceClassPairs.Add(contract, implementation);
        }

        private static Dictionary<Type, Type> _interfaceClassPairs = new Dictionary<Type, Type>();

        public override bool CanConvert(Type objectType)
        {
            return _interfaceClassPairs.TryGetValue(objectType, out var _);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, Newtonsoft.Json.JsonSerializer serializer)
        {
            if (_interfaceClassPairs.TryGetValue(objectType, out var t)) return serializer.Deserialize(reader, t);

            throw new NotSupportedException(string.Format("Type {0} unexpected.", objectType));
        }

        internal static Type GetImplementationType(Type entityType)
        {
            return _interfaceClassPairs.TryGetValue(entityType, out var t) ? t : null;
        }
        public override void WriteJson(JsonWriter writer, object value, Newtonsoft.Json.JsonSerializer serializer)
        {
            serializer.Serialize(writer, value);
        }
    }
}