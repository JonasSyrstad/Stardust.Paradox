using System.Collections.Specialized;
using Stardust.Particles;

namespace Stardust.Paradox.CosmosDbTest
{
    public class NullManager : IConfigurationReader
    {
        public NameValueCollection AppSettings => new NameValueCollection();
    }
}