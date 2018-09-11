using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Gremlin.Net.Driver;
using Gremlin.Net.Structure.IO.GraphSON;

namespace Stardust.Paradox.Data.Providers.Gremlin
{
    public class GremlinNetLanguageConnector : IGremlinLanguageConnector, IDisposable
    {
        public static bool IsAspNetCore { get; set; }
        private GremlinClient _client;

        /// <summary>
        /// Connecting to Cosmos DB
        /// </summary>
        /// <param name="gremlinHostname">DBAccountName.gremlin.cosmosdb.azure.com</param>
        /// <param name="databaseName"></param>
        /// <param name="graphName"></param>
        /// <param name="accessKey"></param>
        public GremlinNetLanguageConnector(string gremlinHostname, string databaseName, string graphName, string accessKey)
        {
            var server = new GremlinServer(gremlinHostname, 443, true, $"/dbs/{databaseName}/colls/{graphName}", accessKey);
            
            _client = new GremlinClient(server, new InternalGraphSONReader1(), new GraphSON2Writer(), GremlinClient.GraphSON2MimeType);
        }

        public GremlinNetLanguageConnector(string gremlinHostname, string username, string password, int port = 8182, bool enableSsl = true)
        {
            var server = new GremlinServer(gremlinHostname, port, enableSsl, username, password);

            _client = new GremlinClient(server, new InternalGraphSONReader1(), new GraphSON2Writer(), GremlinClient.GraphSON2MimeType);
        }

        public virtual async Task<IEnumerable<dynamic>> ExecuteAsync(string compileQuery)
        {
            var resp = await _client.SubmitAsync<dynamic>(compileQuery);
            return resp;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                try
                {
                    _client?.Dispose();
                }
                catch (Exception ex)
                { }

            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
