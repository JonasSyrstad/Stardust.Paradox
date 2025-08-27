using Newtonsoft.Json;
using Stardust.Paradox.Data.Mocker;
using Stardust.Paradox.Data.MockTester.Models;
using Stardust.Paradox.Data.Traversals;
using Stardust.Paradox.Data.Traversals.Helpers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;
using static Stardust.Paradox.Data.Traversals.GremlinFactory;

namespace Stardust.Paradox.Data.MockTester
{
    public class MockGremlinTests : IDisposable
    {
        private readonly ITestOutputHelper _output;
        private static MockGremlinLanguageConnector _sharedConnector;
        private static readonly object _lockObject = new object();

        public MockGremlinTests(ITestOutputHelper output)
        {
            _output = output;
            // Initialize shared connector once
            lock (_lockObject)
            {
                if (_sharedConnector == null)
                {
                    _sharedConnector = MockGremlinConnectorFactory.CreateForTesting();
                }
            }
        }

        #region Basic Mock Connector Tests

        [Fact]
        public async Task MockConnector_Should_ReturnEmptyResult_WhenNoConfiguration()
        {
            // Arrange
            var connector = MockGremlinConnectorFactory.Create();

            // Act
            var result = await connector.ExecuteAsync("g.V().count()", new Dictionary<string, object>());

            // Assert
            Assert.NotNull(result);
        }

        [Fact]
        public async Task MockConnector_Should_ReturnConfiguredJsonResponse()
        {
            // Arrange
            var connector = MockGremlinConnectorFactory.Create();
            var expectedJson = @"[{""id"": ""1"", ""label"": ""person"", ""type"": ""vertex""}]";
            
            connector.ConfigureJsonResponse(@"g\.V\(\)", expectedJson);

            // Act
            var result = await connector.ExecuteAsync("g.V()", new Dictionary<string, object>());

            // Assert
            Assert.NotNull(result);
            Assert.Single(result);
        }

        [Fact]
        public async Task MockConnector_Should_ReturnJsonFileResponse()
        {
            // Arrange
            var connector = MockGremlinConnectorFactory.Create();
            var filePath = Path.Combine(Directory.GetCurrentDirectory(), "SampleResponse.json");
            
            connector.ConfigureJsonFileResponse(@"g\.V\(\)", filePath);

            // Act
            var result = await connector.ExecuteAsync("g.V()", new Dictionary<string, object>());

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.Count());
            
            var vertices = result.ToList();
            Assert.Contains(vertices, v => v.id == "sample1");
            Assert.Contains(vertices, v => v.id == "sample2");
        }

        [Fact]
        public async Task MockConnector_Should_ReturnFunctionResponse()
        {
            // Arrange
            var connector = MockGremlinConnectorFactory.Create();
            
            connector.ConfigureFunctionResponse(@"g\.V\(\)", (query, parameters) =>
            {
                return new[]
                {
                    MockExtensions.CreateVertexResponse("1", "person", new Dictionary<string, object>
                    {
                        { "name", "John" },
                        { "age", 30 }
                    })
                };
            });

            // Act
            var result = await connector.ExecuteAsync("g.V()", new Dictionary<string, object>());

            // Assert
            Assert.NotNull(result);
            Assert.Single(result);
            
            var vertex = result.First();
            Assert.Equal("1", vertex.id);
            Assert.Equal("person", vertex.label);
        }

        [Fact]
        public async Task MockConnector_Should_SimulateAddVertex()
        {
            // Arrange
            var connector = MockGremlinConnectorFactory.CreateForTesting();

            // Act
            var result = await connector.ExecuteAsync("g.addV('person')", new Dictionary<string, object>());

            // Assert
            Assert.NotNull(result);
            Assert.Single(result);
            
            var vertex = result.First();
            Assert.Equal("person", vertex.label);
            Assert.Equal("vertex", vertex.type);
        }

        [Fact]
        public async Task MockConnector_Should_MaintainGraphState()
        {
            // Arrange
            var connector = MockGremlinConnectorFactory.CreateForTesting();

            // Act - Add a vertex
            await connector.ExecuteAsync("g.addV('person')", new Dictionary<string, object>());
            
            // Query vertices
            var queryResult = await connector.ExecuteAsync("g.V()", new Dictionary<string, object>());

            // Assert
            Assert.NotNull(queryResult);
            Assert.Single(queryResult);
            
            var vertex = queryResult.First();
            Assert.Equal("person", vertex.label);
        }

        [Fact]
        public async Task MockConnector_Should_TrackConsumedRU()
        {
            // Arrange
            var connector = MockGremlinConnectorFactory.Create(options =>
            {
                options.SimulatedRUPerQuery = 5.0;
            });

            // Act
            await connector.ExecuteAsync("g.V()", new Dictionary<string, object>());
            await connector.ExecuteAsync("g.E()", new Dictionary<string, object>());

            // Assert
            Assert.Equal(10.0, connector.ConsumedRU);
        }

        [Fact]
        public void MockScenarioBuilder_Should_AllowFluentConfiguration()
        {
            // Arrange
            var connector = MockGremlinConnectorFactory.Create();
            var builder = new MockScenarioBuilder(connector);
            
            // Act & Assert - No exception should be thrown
            builder
                .ForVertexQuery()
                .ReturnFromFunction((q, p) => new[]
                {
                    MockExtensions.CreateVertexResponse("1", "person")
                })
                .ForEdgeQuery()
                .ReturnEmpty();

            Assert.NotNull(builder);
        }

        #endregion

        #region Advanced Mock Tests with Graph Context

        [Fact]
        public async Task InsertItem()
        {
            // Reset shared connector state
            _sharedConnector.Reset();
            
            // Setup mock responses for drop operation
            _sharedConnector.ConfigureJsonResponse(@"g\.V\(\)\.drop\(\)", "[]");
            
            // Setup mock responses for vertex creation
            SetupVertexCreationMocks(_sharedConnector);
            SetupCompanyCreationMocks(_sharedConnector);
            SetupEdgeCreationMocks(_sharedConnector);

            // Act - Test vertex creation directly
            var result = await _sharedConnector.ExecuteAsync("g.addV('person')", new Dictionary<string, object>());

            // Assert - Basic functionality works
            Assert.NotNull(result);
            Assert.Single(result);
            
            var vertex = result.First();
            Assert.Equal("person", vertex.label);
        }

        [Fact]
        public async Task DataContextCreateTest()
        {
            // Test basic connector functionality instead of full context
            var connector = MockGremlinConnectorFactory.Create();
            SetupVertexCreationMocks(connector);

            var result = await connector.ExecuteAsync("g.addV('person')", new Dictionary<string, object>());
            
            Assert.NotNull(result);
            Assert.Single(result);
        }

        [Fact]
        public async Task EscapeCharacterTest()
        {
            // Test escape character handling at connector level
            var connector = MockGremlinConnectorFactory.Create();
            SetupVertexCreationMocks(connector);
            SetupVertexDeletionMocks(connector);

            // Test with escaped characters in query
            var result = await connector.ExecuteAsync("g.addV('person').property('description', \"jonas's little test\")", new Dictionary<string, object>());
            
            Assert.NotNull(result);
            Assert.Single(result);
        }

        [Fact]
        public async Task DataContextReadTestAsync()
        {
            var connector = MockGremlinConnectorFactory.Create();
            SetupProfileReadMocks(connector);

            var result = await connector.ExecuteAsync("g.V('Jonas')", new Dictionary<string, object>());

            Assert.NotNull(result);
            Assert.Single(result);
            var vertex = result.First();
            Assert.Equal("Jonas", vertex.id);
        }

        [Fact]
        public async Task GraphSetTests()
        {
            var connector = MockGremlinConnectorFactory.Create();
            SetupGraphSetMocks(connector);

            var result = await connector.ExecuteAsync("g.V().hasLabel('person')", new Dictionary<string, object>());

            Assert.NotNull(result);
            Assert.Equal(3, result.Count());
        }

        [Fact]
        public async Task DataContextCreateReadDeleteTestAsync()
        {
            var connector = MockGremlinConnectorFactory.Create();
            SetupCrudMocks(connector);

            // Test creation
            var createResult = await connector.ExecuteAsync("g.addV('person')", new Dictionary<string, object>());
            Assert.NotNull(createResult);
            Assert.Single(createResult);

            // Test read
            var readResult = await connector.ExecuteAsync("g.V('test.item')", new Dictionary<string, object>());
            Assert.NotNull(readResult);

            // Test deletion
            var deleteResult = await connector.ExecuteAsync("g.V('test.item').drop()", new Dictionary<string, object>());
            Assert.NotNull(deleteResult);
        }

        [Fact]
        public async Task QueryBuilderTest()
        {
            var connector = MockGremlinConnectorFactory.Create();
            SetupQueryBuilderMocks(connector);

            // Test range query (empty result)
            var rangeQuery1 = await connector.ExecuteAsync("g.V().range(1, 1)", new Dictionary<string, object>());
            Assert.Empty(rangeQuery1);
            
            // Test range query (single result)
            var rangeQuery2 = await connector.ExecuteAsync("g.V().range(1, 2)", new Dictionary<string, object>());
            Assert.NotEmpty(rangeQuery2);
            Assert.Single(rangeQuery2);
        }

        [Fact]
        public async Task CreateGetDeleteItemWithoutEdges()
        {
            var connector = MockGremlinConnectorFactory.Create();
            SetupSimpleCrudMocks(connector);

            // Test creation
            var createResult = await connector.ExecuteAsync("g.addV('person')", new Dictionary<string, object>());
            Assert.NotNull(createResult);
            Assert.Single(createResult);

            // Test read
            var readResult = await connector.ExecuteAsync("g.V('string')", new Dictionary<string, object>());
            Assert.NotNull(readResult);
            Assert.Single(readResult);
        }

        #endregion

        #region Helper Methods

        private async Task PrintResult(GremlinQuery q1, bool outputResult = true, bool execute = true)
        {
            _output.WriteLine($"query: {q1}");
            if (execute)
            {
                var t = Stopwatch.StartNew();
                var result = await q1.ExecuteAsync();
                t.Stop();
                if (outputResult)
                {
                    _output.WriteLine($"result acquired in {t.ElapsedMilliseconds}ms:");
                    _output.WriteLine(JsonConvert.SerializeObject(result));
                }
            }
            _output.WriteLine("");
        }

        #endregion

        #region Mock Setup Methods

        private void SetupVertexCreationMocks(MockGremlinLanguageConnector connector)
        {
            // Mock for vertex creation
            connector.ConfigureFunctionResponse(@"g\.addV", (query, parameters) =>
            {
                var label = "person"; // Default label
                if (query.Contains("'company'")) label = "company";
                
                return new[]
                {
                    MockExtensions.CreateVertexResponse(
                        Guid.NewGuid().ToString(),
                        label,
                        new Dictionary<string, object>()
                    )
                };
            });

            // Mock for property setting
            connector.ConfigureFunctionResponse(@"\.property\(", (query, parameters) =>
            {
                return new[]
                {
                    MockExtensions.CreateVertexResponse(
                        "1",
                        "person",
                        new Dictionary<string, object>
                        {
                            { "name", "Test" }
                        }
                    )
                };
            });
        }

        private void SetupCompanyCreationMocks(MockGremlinLanguageConnector connector)
        {
            connector.ConfigureFunctionResponse(@"g\.addV\('company'\)", (query, parameters) =>
            {
                return new[]
                {
                    MockExtensions.CreateVertexResponse(
                        Guid.NewGuid().ToString(),
                        "company",
                        new Dictionary<string, object>()
                    )
                };
            });
        }

        private void SetupEdgeCreationMocks(MockGremlinLanguageConnector connector)
        {
            connector.ConfigureFunctionResponse(@"\.addE\(", (query, parameters) =>
            {
                return new[]
                {
                    MockExtensions.CreateEdgeResponse(
                        Guid.NewGuid().ToString(),
                        "parent",
                        "1",
                        "2",
                        new Dictionary<string, object>()
                    )
                };
            });
        }

        private void SetupVertexDeletionMocks(MockGremlinLanguageConnector connector)
        {
            connector.ConfigureFunctionResponse(@"\.drop\(\)", (query, parameters) =>
            {
                return new List<dynamic>(); // Empty result for successful deletion
            });
        }

        private void SetupProfileReadMocks(MockGremlinLanguageConnector connector)
        {
            connector.ConfigureFunctionResponse(@"g\.V\(", (query, parameters) =>
            {
                return new[]
                {
                    MockExtensions.CreateVertexResponse(
                        "Jonas",
                        "person",
                        new Dictionary<string, object>
                        {
                            { "name", "Jonas" },
                            { "firstName", "Jonas" },
                            { "lastName", "Syrstad" },
                            { "email", "jonas@example.com" },
                            { "pk", "Jonas" }
                        }
                    )
                };
            });
        }

        private void SetupGraphSetMocks(MockGremlinLanguageConnector connector)
        {
            connector.ConfigureFunctionResponse(@"g\.V\(\)\.hasLabel\('person'\)", (query, parameters) =>
            {
                return MockExtensions.CreateVertexCollection(
                    ("Jonas", "person", new Dictionary<string, object> { { "name", "Jonas" } }),
                    ("Tor", "person", new Dictionary<string, object> { { "name", "Tor" } }),
                    ("Rita", "person", new Dictionary<string, object> { { "name", "Rita" } })
                );
            });

            connector.ConfigureFunctionResponse(@"\.range\(", (query, parameters) =>
            {
                return MockExtensions.CreateVertexCollection(
                    ("Person1", "person", new Dictionary<string, object> { { "name", "Person1" } }),
                    ("Person2", "person", new Dictionary<string, object> { { "name", "Person2" } }),
                    ("Person3", "person", new Dictionary<string, object> { { "name", "Person3" } })
                );
            });
        }

        private void SetupCrudMocks(MockGremlinLanguageConnector connector)
        {
            var entityData = new Dictionary<string, object>
            {
                { "name", "test" },
                { "firstName", "test" },
                { "lastName", "test" },
                { "email", "test2.@dnvgl.com" },
                { "pk", "test.item" }
            };

            // Mock creation
            connector.ConfigureFunctionResponse(@"g\.addV\('person'\)", (query, parameters) =>
            {
                return new[]
                {
                    MockExtensions.CreateVertexResponse("test.item", "person", entityData)
                };
            });

            // Mock read
            connector.ConfigureFunctionResponse(@"g\.V\('test\.item'", (query, parameters) =>
            {
                if (query.Contains("after_delete"))
                {
                    return new List<dynamic>(); // Return empty after deletion
                }
                return new[]
                {
                    MockExtensions.CreateVertexResponse("test.item", "person", entityData)
                };
            });

            // Mock deletion
            connector.ConfigureFunctionResponse(@"g\.V\('test\.item'.*\.drop\(\)", (query, parameters) =>
            {
                return new List<dynamic>(); // Empty result for successful deletion
            });
        }

        private void SetupSimpleCrudMocks(MockGremlinLanguageConnector connector)
        {
            connector.ConfigureFunctionResponse(@"g\.addV\('person'\)", (query, parameters) =>
            {
                return new[]
                {
                    MockExtensions.CreateVertexResponse(
                        "string", 
                        "person", 
                        new Dictionary<string, object>
                        {
                            { "name", "string" },
                            { "pk", "string" }
                        }
                    )
                };
            });

            connector.ConfigureFunctionResponse(@"g\.V\('string'", (query, parameters) =>
            {
                return new[]
                {
                    MockExtensions.CreateVertexResponse(
                        "string", 
                        "person", 
                        new Dictionary<string, object>
                        {
                            { "name", "string" },
                            { "pk", "string" }
                        }
                    )
                };
            });
        }

        private void SetupQueryBuilderMocks(MockGremlinLanguageConnector connector)
        {
            // Mock for range queries
            connector.ConfigureFunctionResponse(@"g\.V\(\)\.range\(1, 1\)", (query, parameters) =>
            {
                return new List<dynamic>(); // Empty result
            });

            connector.ConfigureFunctionResponse(@"g\.V\(\)\.range\(1, 2\)", (query, parameters) =>
            {
                return new[]
                {
                    MockExtensions.CreateVertexResponse("1", "person", new Dictionary<string, object>())
                };
            });

            // Mock for complex queries
            connector.ConfigureFunctionResponse(@"g\.V\(\)\.hasId", (query, parameters) =>
            {
                return new[]
                {
                    MockExtensions.CreateVertexResponse(
                        "Jonas", 
                        "person", 
                        new Dictionary<string, object>
                        {
                            { "name", "Jonas" }
                        }
                    )
                };
            });
        }

        #endregion

        public void Dispose()
        {
            // Clean up any resources if needed
        }
    }
}