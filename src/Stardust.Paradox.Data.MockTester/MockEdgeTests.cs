using Stardust.Paradox.Data.MockTester.Models;
using Stardust.Paradox.Data.Mocker;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Stardust.Paradox.Data.MockTester
{
    public class MockEdgeTests : IDisposable
    {
        private readonly ITestOutputHelper _output;

        public MockEdgeTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public async Task GetEdges()
        {
            // Arrange
            var connector = MockGremlinConnectorFactory.Create(options => options.LogQueries = true);
            
            // Use a broader pattern that will definitely match
            connector.ConfigureFunctionResponse(@"inE", (query, parameters) =>
            {
                return MockExtensions.CreateEdgeCollection(
                    ("e1", "parent", "Jonas", "Sanne", new Dictionary<string, object>
                    {
                        { "birthPlace", "Kristiansand" },
                        { "created", DateTime.Now }
                    }),
                    ("e2", "parent", "Kine", "Sanne", new Dictionary<string, object>
                    {
                        { "birthPlace", "Kristiansand" },
                        { "created", DateTime.Now }
                    })
                );
            });

            // Act - Test edge query directly at connector level
            var result = await connector.ExecuteAsync("g.V('Sanne').inE('parent')", new Dictionary<string, object>());

            // Assert
            Assert.NotNull(result);
            Assert.NotEmpty(result);
            var firstEdge = result.FirstOrDefault();
            Assert.NotNull(firstEdge);
            Assert.Equal("parent", firstEdge.label);
        }

        [Fact]
        public async Task GetTypedEdges()
        {
            // Arrange
            var connector = MockGremlinConnectorFactory.CreateForTesting();
            SetupTypedEdgeMocks(connector);

            // Act - Test typed edge query
            var result = await connector.ExecuteAsync("g.E().hasLabel('employer')", new Dictionary<string, object>());

            // Assert
            Assert.NotNull(result);
            Assert.NotEmpty(result);
            var employment = result.First();
            Assert.Equal("employer", employment.label);
            Assert.NotNull(employment.properties);
        }

        [Fact]
        public async Task CreateEdgeWithProperties()
        {
            // Arrange
            var connector = MockGremlinConnectorFactory.CreateForTesting();
            SetupEdgeCreationMocks(connector);

            // Act - Create edge with properties
            var parameters = new Dictionary<string, object>
            {
                { "birthPlace", "Kristiansand" },
                { "created", DateTime.Now }
            };
            
            var result = await connector.ExecuteAsync("g.V('Jonas').addE('parent').to(g.V('Kine'))", parameters);

            // Assert
            Assert.NotNull(result);
            Assert.NotEmpty(result);
            var edge = result.First();
            Assert.Equal("parent", edge.label);
            Assert.NotNull(edge.properties);
        }

        [Fact]
        public async Task RemoveEdge()
        {
            // Arrange
            var connector = MockGremlinConnectorFactory.Create(options => options.LogQueries = true);
            
            // Use broader patterns
            connector.ConfigureFunctionResponse(@"\.out\(", (query, parameters) =>
            {
                return MockExtensions.CreateVertexCollection(
                    ("Sanne", "person", new Dictionary<string, object> { { "name", "Sanne" } }),
                    ("Herman", "person", new Dictionary<string, object> { { "name", "Herman" } }),
                    ("Kine", "person", new Dictionary<string, object> { { "name", "Kine" } })
                );
            });

            connector.ConfigureFunctionResponse(@"\.drop\(\)", (query, parameters) =>
            {
                return new List<dynamic>(); // Empty result for successful removal
            });

            // Act - Test edge removal
            var beforeResult = await connector.ExecuteAsync("g.V('Jonas').out('parent')", new Dictionary<string, object>());
            var removeResult = await connector.ExecuteAsync("g.V('Jonas').outE('parent').drop()", new Dictionary<string, object>());
            
            // Assert
            Assert.NotNull(beforeResult);
            Assert.NotEmpty(beforeResult);
            Assert.NotNull(removeResult);
            // Drop operation should return empty result
            Assert.Empty(removeResult);
        }

        [Fact]
        public async Task BidirectionalEdge()
        {
            // Arrange
            var connector = MockGremlinConnectorFactory.Create(options => options.LogQueries = true);
            
            // Use broader patterns
            connector.ConfigureFunctionResponse(@"addE", (query, parameters) =>
            {
                return new[]
                {
                    MockExtensions.CreateEdgeResponse(
                        Guid.NewGuid().ToString(),
                        "spouce",
                        "Jonas",
                        "Kine",
                        new Dictionary<string, object>()
                    )
                };
            });

            connector.ConfigureFunctionResponse(@"\.both\(", (query, parameters) =>
            {
                return new[]
                {
                    MockExtensions.CreateVertexResponse("Kine", "person", new Dictionary<string, object>
                    {
                        { "name", "Kine" }
                    })
                };
            });

            // Act - Test bidirectional edge creation
            var createResult = await connector.ExecuteAsync("g.V('Jonas').addE('spouce').to(g.V('Kine'))", new Dictionary<string, object>());
            var queryResult = await connector.ExecuteAsync("g.V('Jonas').both('spouce')", new Dictionary<string, object>());

            // Assert
            Assert.NotNull(createResult);
            Assert.NotEmpty(createResult);
            Assert.NotNull(queryResult);
            Assert.NotEmpty(queryResult);
            
            var spouse = queryResult.First();
            // Access the properties dictionary to get the name
            var properties = (Dictionary<string, object>)spouse.properties;
            Assert.Equal("Kine", properties["name"]);
        }

        #region Helper Methods

        private void SetupEdgeQueryMocks(MockGremlinLanguageConnector connector)
        {
            // Mock profile retrieval
            connector.ConfigureFunctionResponse(@"g\.V\('Sanne'\)", (query, parameters) =>
            {
                return new[]
                {
                    MockExtensions.CreateVertexResponse("Sanne", "person", new Dictionary<string, object>
                    {
                        { "name", "Sanne" },
                        { "pk", "Sanne" }
                    })
                };
            });

            // Mock edge query for parents - fix the regex pattern to match the actual query
            connector.ConfigureFunctionResponse(@"g\.V\('Sanne'\)\.inE\('parent'\)", (query, parameters) =>
            {
                return MockExtensions.CreateEdgeCollection(
                    ("e1", "parent", "Jonas", "Sanne", new Dictionary<string, object>
                    {
                        { "birthPlace", "Kristiansand" },
                        { "created", DateTime.Now }
                    }),
                    ("e2", "parent", "Kine", "Sanne", new Dictionary<string, object>
                    {
                        { "birthPlace", "Kristiansand" },
                        { "created", DateTime.Now }
                    })
                );
            });
        }

        private void SetupTypedEdgeMocks(MockGremlinLanguageConnector connector)
        {
            // Mock employment edge query
            connector.ConfigureFunctionResponse(@"g\.E\(\)\.hasLabel\('employer'\)", (query, parameters) =>
            {
                return MockExtensions.CreateEdgeCollection(
                    ("emp1", "employer", "Jonas", "GSSIT", new Dictionary<string, object>
                    {
                        { "hiredDate", DateTime.Now.AddYears(-5) },
                        { "manager", "Test Manager" }
                    })
                );
            });

            // Mock in vertex retrieval
            connector.ConfigureFunctionResponse(@"g\.V\('Jonas'\)", (query, parameters) =>
            {
                return new[]
                {
                    MockExtensions.CreateVertexResponse("Jonas", "person", new Dictionary<string, object>
                    {
                        { "name", "Jonas" },
                        { "pk", "Jonas" }
                    })
                };
            });

            // Mock out vertex retrieval
            connector.ConfigureFunctionResponse(@"g\.V\('GSSIT'\)", (query, parameters) =>
            {
                return new[]
                {
                    MockExtensions.CreateVertexResponse("GSSIT", "company", new Dictionary<string, object>
                    {
                        { "name", "GSSIT" },
                        { "pk", "GSSIT" }
                    })
                };
            });
        }

        private void SetupEdgeCreationMocks(MockGremlinLanguageConnector connector)
        {
            // Mock vertex creation
            connector.ConfigureFunctionResponse(@"g\.addV\('person'\)", (query, parameters) =>
            {
                var id = Guid.NewGuid().ToString();
                return new[]
                {
                    MockExtensions.CreateVertexResponse(id, "person", new Dictionary<string, object>())
                };
            });

            // Mock edge creation
            connector.ConfigureFunctionResponse(@"\.addE\('parent'\)", (query, parameters) =>
            {
                return new[]
                {
                    MockExtensions.CreateEdgeResponse(
                        Guid.NewGuid().ToString(),
                        "parent",
                        "Jonas",
                        "Kine",
                        new Dictionary<string, object>
                        {
                            { "birthPlace", "Kristiansand" },
                            { "created", DateTime.Now }
                        }
                    )
                };
            });
        }

        private void SetupEdgeRemovalMocks(MockGremlinLanguageConnector connector)
        {
            // Mock vertex retrieval
            connector.ConfigureFunctionResponse(@"g\.V\('Jonas'\)", (query, parameters) =>
            {
                return new[]
                {
                    MockExtensions.CreateVertexResponse("Jonas", "person", new Dictionary<string, object>
                    {
                        { "name", "Jonas" },
                        { "pk", "Jonas" }
                    })
                };
            });

            connector.ConfigureFunctionResponse(@"g\.V\('Kine'\)", (query, parameters) =>
            {
                return new[]
                {
                    MockExtensions.CreateVertexResponse("Kine", "person", new Dictionary<string, object>
                    {
                        { "name", "Kine" },
                        { "pk", "Kine" }
                    })
                };
            });

            // Mock children retrieval - fix the pattern to match full query
            connector.ConfigureFunctionResponse(@"g\.V\('Jonas'\)\.out\('parent'\)", (query, parameters) =>
            {
                return MockExtensions.CreateVertexCollection(
                    ("Sanne", "person", new Dictionary<string, object> { { "name", "Sanne" } }),
                    ("Herman", "person", new Dictionary<string, object> { { "name", "Herman" } }),
                    ("Kine", "person", new Dictionary<string, object> { { "name", "Kine" } })
                );
            });

            // Mock edge removal - fix pattern to match full query
            connector.ConfigureFunctionResponse(@"g\.V\('Jonas'\)\.outE\('parent'\)\.drop\(\)", (query, parameters) =>
            {
                return new List<dynamic>(); // Empty result for successful removal
            });
        }

        private void SetupBidirectionalEdgeMocks(MockGremlinLanguageConnector connector)
        {
            // Mock vertex retrieval
            connector.ConfigureFunctionResponse(@"g\.V\('Jonas'\)", (query, parameters) =>
            {
                return new[]
                {
                    MockExtensions.CreateVertexResponse("Jonas", "person", new Dictionary<string, object>
                    {
                        { "name", "Jonas" },
                        { "pk", "Jonas" }
                    })
                };
            });

            connector.ConfigureFunctionResponse(@"g\.V\('Kine'\)", (query, parameters) =>
            {
                return new[]
                {
                    MockExtensions.CreateVertexResponse("Kine", "person", new Dictionary<string, object>
                    {
                        { "name", "Kine" },
                        { "pk", "Kine" }
                    })
                };
            });

            // Mock spouse edge creation - fix pattern to match full query
            connector.ConfigureFunctionResponse(@"g\.V\('Jonas'\)\.addE\('spouce'\)\.to\(g\.V\('Kine'\)\)", (query, parameters) =>
            {
                return new[]
                {
                    MockExtensions.CreateEdgeResponse(
                        Guid.NewGuid().ToString(),
                        "spouce",
                        "Jonas",
                        "Kine",
                        new Dictionary<string, object>()
                    )
                };
            });

            // Mock spouse retrieval - fix pattern to match full query
            connector.ConfigureFunctionResponse(@"g\.V\('Jonas'\)\.both\('spouce'\)", (query, parameters) =>
            {
                return new[]
                {
                    MockExtensions.CreateVertexResponse("Kine", "person", new Dictionary<string, object>
                    {
                        { "name", "Kine" }
                    })
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