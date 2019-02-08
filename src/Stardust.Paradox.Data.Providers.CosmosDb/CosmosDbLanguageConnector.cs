using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using Microsoft.Azure.Graphs;
using Microsoft.Azure.Graphs.Elements;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Stardust.Particles;

namespace Stardust.Paradox.Data.Providers.CosmosDb
{
    public class CosmosDbLanguageConnector : IGremlinLanguageConnector
    {
        private readonly string _databaseName;
        private readonly int _throughput;
        private readonly string _collectionName;

        public CosmosDbLanguageConnector(string cosmosDbAccountName, string authKeyOrResourceToken, string databaseName, string collectionName = null, int throughput = 1000)
        {
            _databaseName = databaseName;
            _throughput = throughput;
            _collectionName = collectionName ?? databaseName;
            if (_client == null)
                _client = new DocumentClient(
                    new Uri($"https://{cosmosDbAccountName}.documents.azure.com:443/"),
                    authKeyOrResourceToken);
        }

        public CosmosDbLanguageConnector(string cosmosDbAccountName, string authKeyOrResourceToken, string databaseName, JsonSerializerSettings serializationSettings, ConnectionPolicy connectionPolicy, ConsistencyLevel consistencyLevel = ConsistencyLevel.Session, string collectionName = null, int throughput = 1000)
        {
            _databaseName = databaseName;
            _collectionName = collectionName ?? databaseName;
            _throughput = throughput;
            if (_client == null)
                _client = new DocumentClient(
                    new Uri($"https://{cosmosDbAccountName}.documents.azure.com:443/"), authKeyOrResourceToken, serializationSettings, connectionPolicy, consistencyLevel);
        }

        private static DocumentClient _client;
        private static ResourceResponse<DocumentCollection> _graph;
        public IEnumerable<T> Execute<T>(string query)
        {
            return Task.Run(async () => await ExecuteAsync<T>(query).ConfigureAwait(false)).GetAwaiter().GetResult();
        }

        public async Task<IEnumerable<T>> ExecuteAsync<T>(string query)
        {
            var graph = await DocumentCollection().ConfigureAwait(false);
            var gremlinQ = _client.CreateGremlinQuery<Vertex>(graph, query);
            var d = await gremlinQ.ExecuteNextAsync<Vertex>().ConfigureAwait(false);
            return d.Select(i => new JObject(i.GetVertexProperties().Select(s => new JProperty(s.Key, s.Value))))
                .Select(jObj => (T)jObj.ToObject(typeof(T))).ToList();
        }

        public async Task<IEnumerable<dynamic>> ExecuteAsync(string query,
	        Dictionary<string, object> parametrizedValues)
        {
			try
			{
				if (OutputAllQueries) Logging.DebugMessage($"gremlin: {query}");
				var graph = await DocumentCollection().ConfigureAwait(false);
				var gremlinQ = _client.CreateGremlinQuery(graph, query);
				var d = await gremlinQ.ExecuteNextAsync().ConfigureAwait(false);
				return d.AsEnumerable();
			}
			catch (Exception ex)
			{
				if (OutputDebugLog)
				{
					Logging.DebugMessage($"Failed query: {query}");
					Logging.Exception(ex);
					Console.WriteLine(query);
				}
				throw;
			}
        }

	    public bool OutputAllQueries { get; set; }

	    public bool OutputDebugLog { get; set; }

	    public bool CanParameterizeQueries => false;

	    private async Task<DocumentCollection> DocumentCollection()
        {
            if (_graph != null) return _graph;
            Database database = await _client.CreateDatabaseIfNotExistsAsync(new Database { Id = _databaseName }).ConfigureAwait(false);
            _graph = await _client.CreateDocumentCollectionIfNotExistsAsync(
                UriFactory.CreateDatabaseUri(_databaseName),
                new DocumentCollection { Id = _collectionName },
                new RequestOptions { OfferThroughput = _throughput }).ConfigureAwait(false);
            return _graph;
        }
    }
}
