using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using Microsoft.Azure.Graphs;
using Newtonsoft.Json;
using Stardust.Paradox.Data.Internals;
using Stardust.Particles;

namespace Stardust.Paradox.Data.Providers.CosmosDb
{
    public class CosmosDbLanguageConnector : LanguageConnectorBase, IGremlinLanguageConnector
    {
        private static readonly ConcurrentDictionary<string, DocumentClient> _clients =
            new ConcurrentDictionary<string, DocumentClient>();

        private static ResourceResponse<DocumentCollection> _graph;
        private readonly string _collectionName;
        private readonly string _databaseName;
        private readonly int _throughput;
        private readonly DocumentClient _client;

        public CosmosDbLanguageConnector(string cosmosDbAccountName, string authKeyOrResourceToken, string databaseName,
            string collectionName = null, int throughput = 1000, ILogging logger = null) : base(logger)
        {
            _databaseName = databaseName;
            _throughput = throughput;
            _collectionName = collectionName ?? databaseName;
            if (_client != null) return;
            if (_clients.TryGetValue(cosmosDbAccountName.ToLower().Trim(), out _client)) return;
            _client = new DocumentClient(new Uri($"https://{cosmosDbAccountName}.documents.azure.com:443/"),
                authKeyOrResourceToken);
            _clients.TryAdd(cosmosDbAccountName.ToLower().Trim(), _client);
        }

        public CosmosDbLanguageConnector(string cosmosDbAccountName, string authKeyOrResourceToken, string databaseName,
            JsonSerializerSettings serializationSettings, ConnectionPolicy connectionPolicy,
            ConsistencyLevel consistencyLevel = ConsistencyLevel.Session, string collectionName = null,
            int throughput = 1000, ILogging logger = null) : base(logger)
        {
            _databaseName = databaseName;
            _collectionName = collectionName ?? databaseName;
            _throughput = throughput;

            if (_client != null) return;
            if (_clients.TryGetValue(cosmosDbAccountName.ToLower().Trim(), out _client)) return;
            _client = new DocumentClient(new Uri($"https://{cosmosDbAccountName}.documents.azure.com:443/"),
                authKeyOrResourceToken, serializationSettings, connectionPolicy, consistencyLevel);
            _clients.TryAdd(cosmosDbAccountName.ToLower().Trim(), _client);
        }

        public async Task<IEnumerable<dynamic>> ExecuteAsync(string query,
            Dictionary<string, object> parametrizedValues)
        {
            try
            {
                var graph = await DocumentCollection().ConfigureAwait(false);
                var gremlinQ = _client.CreateGremlinQuery(graph, query);
                var d = await gremlinQ.ExecuteNextAsync().ConfigureAwait(false);
                Log($"gremlin: {query} (ru cost: {d.RequestCharge})");
                ConsumedRU += d.RequestCharge;
                return d.AsEnumerable();
            }

            catch (Exception ex) when (Log(query, ex))
            {
                throw;
            }
        }


        public bool CanParameterizeQueries => false;
        public double ConsumedRU { get; private set; }

        private async Task<DocumentCollection> DocumentCollection()
        {
            if (_graph != null) return _graph;
            Database database = await _client.CreateDatabaseIfNotExistsAsync(new Database {Id = _databaseName})
                .ConfigureAwait(false);
            _graph = await _client.CreateDocumentCollectionIfNotExistsAsync(
                UriFactory.CreateDatabaseUri(_databaseName),
                new DocumentCollection {Id = _collectionName},
                new RequestOptions {OfferThroughput = _throughput}).ConfigureAwait(false);
            return _graph;
        }
    }
}