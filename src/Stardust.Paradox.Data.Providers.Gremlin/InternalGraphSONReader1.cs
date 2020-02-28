using Gremlin.Net.Structure.IO.GraphSON;
using Newtonsoft.Json.Linq;

namespace Stardust.Paradox.Data.Providers.Gremlin
{
    public class InternalGraphSonReader1 : GraphSONReader
    {
       public override dynamic ToObject(JToken jToken)
        {
            return jToken;
        }
    }
}