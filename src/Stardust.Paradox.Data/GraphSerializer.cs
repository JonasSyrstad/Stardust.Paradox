using Newtonsoft.Json;
using Stardust.Paradox.Data.Internals;

namespace Stardust.Paradox.Data
{
    public static class GraphSerializer
    {
        public static JsonSerializerSettings RegisterGraphSerializer(this JsonSerializerSettings jsonSettings)
        {
            jsonSettings.Converters.Add(new GraphJsonConverter());
            return jsonSettings;
        }
    }
}