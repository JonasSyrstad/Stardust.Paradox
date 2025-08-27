using Stardust.Paradox.Data.MockTester.Models;
using Stardust.Paradox.Data.Mocker;
using Stardust.Paradox.Data.Traversals;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;
using static Stardust.Paradox.Data.Traversals.GremlinFactory;

namespace Stardust.Paradox.Data.MockTester
{
    public class MockAdvancedQueryTests : IDisposable
    {
        private readonly ITestOutputHelper _output;

        public MockAdvancedQueryTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public async Task ExecuteAdvancedQueryTest()
        {
            // Arrange
            var connector = MockGremlinConnectorFactory.CreateForTesting();
            SetupSiblingQueryMocks(connector);

            // Act - Test sibling query directly
            var result = await connector.ExecuteAsync("g.V().has('name', 'Sanne')", new Dictionary<string, object>());

            // Assert
            Assert.Equal(3, result.Count());
            _output.WriteLine($"Found {result.Count()} siblings");
        }

        [Fact]
        public async Task GetTreeTest()
        {
            // Arrange
            var connector = MockGremlinConnectorFactory.CreateForTesting();
            SetupTreeQueryMocks(connector);

            // Act - Test tree traversal query
            var result = await connector.ExecuteAsync("g.V('Tor').repeat(out('parent'))", new Dictionary<string, object>());

            // Assert
            Assert.NotNull(result);
            Assert.NotEmpty(result);
            _output.WriteLine("Tree structure retrieved successfully");
        }

        [Fact]
        public async Task DataContextToVerticesFilteredAsync()
        {
            // Arrange
            var connector = MockGremlinConnectorFactory.Create(options => options.LogQueries = true);
            
            // Use broader pattern
            connector.ConfigureFunctionResponse(@"has\('name', 'Marena'\)", (query, parameters) =>
            {
                return new[]
                {
                    MockExtensions.CreateVertexResponse("Marena", "person", new Dictionary<string, object>
                    {
                        { "name", "Marena" },
                        { "pk", "Marena" }
                    })
                };
            });

            // Act - Test filtered query
            var result = await connector.ExecuteAsync("g.V('Jonas').out('parent').has('name', 'Marena')", new Dictionary<string, object>());

            // Assert
            Assert.Single(result);
            // Access the properties dictionary to get the name
            var properties = (Dictionary<string, object>)result.First().properties;
            Assert.Equal("Marena", properties["name"]);
        }

        [Fact]
        public async Task ComplexQueryWithMultipleSteps()
        {
            // Arrange
            var connector = MockGremlinConnectorFactory.CreateForTesting();
            SetupComplexQueryMocks(connector);

            // Act - Test complex multi-step query
            var result = await connector.ExecuteAsync("g.V().has('name', 'Jonas')", new Dictionary<string, object>());

            // Assert
            Assert.NotNull(result);
            Assert.NotEmpty(result);
            _output.WriteLine($"Complex query returned {result.Count()} results");
        }

        [Fact]
        public async Task QueryWithProjection()
        {
            // Arrange
            var connector = MockGremlinConnectorFactory.CreateForTesting();
            SetupProjectionQueryMocks(connector);

            // Act - Test projection query
            var result = await connector.ExecuteAsync("g.V().hasLabel('person').values('name')", new Dictionary<string, object>());

            // Assert
            Assert.NotNull(result);
            Assert.NotEmpty(result);
            _output.WriteLine($"Projection query returned {result.Count()} names");
        }

        [Fact]
        public async Task ConditionalTraversal()
        {
            // Arrange
            var connector = MockGremlinConnectorFactory.CreateForTesting();
            SetupConditionalTraversalMocks(connector);

            // Act - Test conditional traversal
            var result = await connector.ExecuteAsync("g.V().choose(hasLabel('person'), values('name'), constant('not-person'))", new Dictionary<string, object>());

            // Assert
            Assert.NotNull(result);
            Assert.NotEmpty(result);
            _output.WriteLine($"Conditional traversal returned {result.Count()} results");
        }

        [Fact]
        public async Task AggregationQuery()
        {
            // Arrange
            var connector = MockGremlinConnectorFactory.CreateForTesting();
            SetupAggregationQueryMocks(connector);

            // Act - Test aggregation queries
            var countResult = await connector.ExecuteAsync("g.V().hasLabel('person').count()", new Dictionary<string, object>());
            var groupResult = await connector.ExecuteAsync("g.V().hasLabel('person').group()", new Dictionary<string, object>());

            // Assert
            Assert.NotNull(countResult);
            Assert.NotNull(groupResult);
            _output.WriteLine($"Found {countResult.FirstOrDefault()} persons");
        }

        [Fact]
        public async Task PathQuery()
        {
            // Arrange
            var connector = MockGremlinConnectorFactory.CreateForTesting();
            SetupPathQueryMocks(connector);

            // Act - Test path query
            var result = await connector.ExecuteAsync("g.V('Jonas').out('parent').in('parent').path().by('name')", new Dictionary<string, object>());

            // Assert
            Assert.NotNull(result);
            Assert.NotEmpty(result);
            _output.WriteLine($"Path query returned {result.Count()} paths");
        }

        #region Helper Methods

        private void SetupSiblingQueryMocks(MockGremlinLanguageConnector connector)
        {
            connector.ConfigureFunctionResponse(@"g\.V\(\)\.has\('name', 'Sanne'\)", (query, parameters) =>
            {
                return MockExtensions.CreateVertexCollection(
                    ("Herman", "person", new Dictionary<string, object> { { "name", "Herman" } }),
                    ("Marena", "person", new Dictionary<string, object> { { "name", "Marena" } }),
                    ("Mathilde", "person", new Dictionary<string, object> { { "name", "Mathilde" } })
                );
            });
        }

        private void SetupTreeQueryMocks(MockGremlinLanguageConnector connector)
        {
            connector.ConfigureFunctionResponse(@"g\.V\('Tor'\)", (query, parameters) =>
            {
                return new[]
                {
                    MockExtensions.CreateVertexResponse("Tor", "person", new Dictionary<string, object>
                    {
                        { "name", "Tor" },
                        { "pk", "Tor" }
                    })
                };
            });

            // Mock tree traversal
            connector.ConfigureFunctionResponse(@"\.repeat\(", (query, parameters) =>
            {
                return MockExtensions.CreateVertexCollection(
                    ("Jonas", "person", new Dictionary<string, object> { { "name", "Jonas" } }),
                    ("Sanne", "person", new Dictionary<string, object> { { "name", "Sanne" } }),
                    ("Herman", "person", new Dictionary<string, object> { { "name", "Herman" } })
                );
            });
        }

        private void SetupFilteredQueryMocks(MockGremlinLanguageConnector connector)
        {
            connector.ConfigureFunctionResponse(@"g\.V\('Jonas'", (query, parameters) =>
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

            // Fix pattern to match the full filtered query
            connector.ConfigureFunctionResponse(@"g\.V\('Jonas'\)\.out\('parent'\)\.has\('name', 'Marena'\)", (query, parameters) =>
            {
                return new[]
                {
                    MockExtensions.CreateVertexResponse("Marena", "person", new Dictionary<string, object>
                    {
                        { "name", "Marena" },
                        { "pk", "Marena" }
                    })
                };
            });
        }

        private void SetupComplexQueryMocks(MockGremlinLanguageConnector connector)
        {
            connector.ConfigureFunctionResponse(@"g\.V\(\)\.has\('name', 'Jonas'\)", (query, parameters) =>
            {
                return MockExtensions.CreateVertexCollection(
                    ("Sanne", "person", new Dictionary<string, object> { { "name", "Sanne" } }),
                    ("Herman", "person", new Dictionary<string, object> { { "name", "Herman" } }),
                    ("Marena", "person", new Dictionary<string, object> { { "name", "Marena" } })
                );
            });
        }

        private void SetupProjectionQueryMocks(MockGremlinLanguageConnector connector)
        {
            connector.ConfigureFunctionResponse(@"g\.V\(\)\.hasLabel\('person'\)\.values\('name'\)", (query, parameters) =>
            {
                return new[] { "Jonas", "Tor", "Rita", "Kine", "Sanne", "Herman", "Marena" };
            });
        }

        private void SetupConditionalTraversalMocks(MockGremlinLanguageConnector connector)
        {
            connector.ConfigureFunctionResponse(@"g\.V\(\)\.choose\(", (query, parameters) =>
            {
                return new[] { "Jonas", "Tor", "Rita", "not-person", "Sanne" };
            });
        }

        private void SetupAggregationQueryMocks(MockGremlinLanguageConnector connector)
        {
            connector.ConfigureFunctionResponse(@"g\.V\(\)\.hasLabel\('person'\)\.count\(\)", (query, parameters) =>
            {
                return new dynamic[] { 7 };
            });

            connector.ConfigureFunctionResponse(@"g\.V\(\)\.hasLabel\('person'\)\.group\(\)", (query, parameters) =>
            {
                return new dynamic[]
                {
                    new Dictionary<string, object>
                    {
                        { "Technical Solution Architect", new[] { "Jonas" } },
                        { "Farmer", new[] { "Tor", "Rita" } },
                        { "null", new[] { "Kine", "Sanne", "Herman", "Marena" } }
                    }
                };
            });
        }

        private void SetupPathQueryMocks(MockGremlinLanguageConnector connector)
        {
            connector.ConfigureFunctionResponse(@"g\.V\('Jonas'\).*\.path\(\)", (query, parameters) =>
            {
                return new[]
                {
                    new[] { "Jonas", "Tor", "Jonas" },
                    new[] { "Jonas", "Rita", "Jonas" },
                    new[] { "Jonas", "Tor", "Sanne" },
                    new[] { "Jonas", "Rita", "Herman" }
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