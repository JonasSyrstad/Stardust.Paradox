using System.Collections.Generic;
using Gremlin.Net.Structure.IO.GraphSON;
using Newtonsoft.Json.Linq;

namespace Stardust.Paradox.Data.Providers.Gremlin
{
    public class InternalGraphSONReader1 : GraphSONReader
    {
        

        //public InternalGraphSONReader1()
        //{
        //    //_internal=new GraphSON2Reader();
        //}

        //public InternalGraphSONReader1(IReadOnlyDictionary<string, IGraphSONDeserializer> deserializerByGraphSONType) : base(deserializerByGraphSONType)
        //{
        //    //_internal = new GraphSON2Reader(deserializerByGraphSONType);
        //}

        //public override dynamic ToObject(IEnumerable<JToken> graphSonData)
        //{
        //    return graphSonData;
        //}

        public override dynamic ToObject(JToken jToken)
        {
            return jToken;
        }
    }
}