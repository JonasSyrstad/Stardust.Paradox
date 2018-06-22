using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using Microsoft.Azure.Graphs;
using Stardust.Core;
using Stardust.Nucleus;
using Stardust.Paradox.Data;
using Stardust.Paradox.Data.Traversals;
using Stardust.Particles;
using Xunit;

namespace Stardust.Paradox.CosmosDbTest
{

    public class GremlinTests
    {
        static GremlinTests()
        {
            Stardust.Nucleus.Resolver.LoadModuleConfiguration<TestBp>();
        }

        public GremlinTests()
        {
            Resolver =Stardust.Nucleus.Resolver.CreateScopedResolver();
        }

        public IDependencyResolver Resolver { get; }

        [Fact]
        public async Task InsertItem()
        {
           
            
            var c =  Resolver.GetService<IGremlinLanguageConnector>() as Class1;
            await c.Upsert(new ServiceDefinition {id = "Root", name = "root"});
            await c.Upsert(new ServiceDefinition { id = "Jonas", name = "jonas" });
            var jonas = await Resolver.GetService<IGremlinLanguageConnector>().V().Has("name", "jonas").ExecuteAsync();
        }
    }

    public class ServiceDefinition
    {
        public string id { get; set; }
        public string name { get; set; }
    }

    public class TestBp : IBlueprint
    {
        public void Bind(IConfigurator configuration)
        {
            configuration.Bind<IGremlinLanguageConnector>().To<Class1>().SetTransientScope();
            //configuration.Bind<Vertices>().ToSelf();
        }

        public Type LoggingType => typeof(LoggingDefaultImplementation);
    }

    public class Class1 : IGremlinLanguageConnector
    {
        private DocumentClient _client;

        public Class1()
        {
            _client = new DocumentClient(
                new Uri("https://localhost:8081"),
                "C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==");
        }

        public IEnumerable<T> Execute<T>(string query)
        {
            return Task.Run(async () => await ExecuteAsync<T>(query)).Result;

        }

        public async Task<IEnumerable<T>> ExecuteAsync<T>(string query)
        {
            var graph = await DocumentCollection<T>();
            var gremlinQ = _client.CreateGremlinQuery<T>(graph, query);
            var d = await gremlinQ.ExecuteNextAsync<T>();
            return d.AsEnumerable();
        }

        private async Task<DocumentCollection> DocumentCollection<T>()
        {
            Database database = await _client.CreateDatabaseIfNotExistsAsync(new Database { Id = "graphTest" });
            DocumentCollection graph = await _client.CreateDocumentCollectionIfNotExistsAsync(
                UriFactory.CreateDatabaseUri("graphTest"),
                new DocumentCollection { Id = "services" },
                new RequestOptions { OfferThroughput = 1000 });
            return graph;
        }

        public async Task Upsert<T>(T graphItem, IEnumerable<EdgeReference> edges=null)
        {
            var collection = await DocumentCollection<T>();
            var i = _client.CreateGremlinQuery<T>(collection,
                $"g.addV('{graphItem.GetType().Name}').property(id,{((dynamic)graphItem).Id})");
            while (i.HasMoreResults)
            {
                await i.ExecuteNextAsync();
            }
            var upsertStatement =await _client.UpsertDocumentAsync(collection.DocumentsLink, graphItem, disableAutomaticIdGeneration: true);

        }
    }

    public class EdgeReference
    {
        public string EdgeName { get; set; }

        public string EdgeId { get; set; }
    }
}
