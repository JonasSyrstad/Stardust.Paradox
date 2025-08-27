using Stardust.Paradox.Data.MockTester.Models;
using Stardust.Paradox.Data.Mocker;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Stardust.Paradox.Data.MockTester
{
    public class MockPerformanceTests : IDisposable
    {
        private readonly ITestOutputHelper _output;

        public MockPerformanceTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public async Task MockConnector_Performance_MassiveDataSet()
        {
            // Arrange
            var connector = MockGremlinConnectorFactory.CreateForTesting();
            SetupLargeDatasetMocks(connector);

            // Act
            var stopwatch = Stopwatch.StartNew();
            var result = await connector.ExecuteAsync("g.V().count()", new Dictionary<string, object>());
            stopwatch.Stop();

            // Assert
            Assert.NotNull(result);
            _output.WriteLine($"Large dataset query executed in {stopwatch.ElapsedMilliseconds}ms");
            Assert.True(stopwatch.ElapsedMilliseconds < 1000, "Query should execute in under 1 second");
        }

        [Fact]
        public async Task MockConnector_RU_Tracking_Accuracy()
        {
            // Arrange
            var connector = MockGremlinConnectorFactory.Create(options =>
            {
                options.SimulatedRUPerQuery = 2.5;
                options.LogQueries = true;
            });

            var initialRU = connector.ConsumedRU;

            // Act
            for (int i = 0; i < 10; i++)
            {
                await connector.ExecuteAsync($"g.V().has('id', '{i}')", new Dictionary<string, object>());
            }

            // Assert
            var expectedRU = initialRU + (10 * 2.5);
            Assert.Equal(expectedRU, connector.ConsumedRU);
            _output.WriteLine($"Total RU consumed: {connector.ConsumedRU}");
        }

        [Fact]
        public async Task MockConnector_ConcurrentQueries()
        {
            // Arrange
            var connector = MockGremlinConnectorFactory.CreateForTesting();
            SetupConcurrentQueryMocks(connector);

            // Act
            var tasks = new List<Task<IEnumerable<dynamic>>>();
            for (int i = 0; i < 50; i++)
            {
                var task = connector.ExecuteAsync($"g.V().has('id', '{i}')", new Dictionary<string, object>());
                tasks.Add(task);
            }

            var stopwatch = Stopwatch.StartNew();
            await Task.WhenAll(tasks);
            stopwatch.Stop();

            // Assert
            _output.WriteLine($"50 concurrent queries executed in {stopwatch.ElapsedMilliseconds}ms");
            Assert.True(stopwatch.ElapsedMilliseconds < 5000, "Concurrent queries should complete in under 5 seconds");
            
            foreach (var task in tasks)
            {
                Assert.NotNull(task.Result);
            }
        }

        [Fact]
        public async Task MockConnector_StateConsistency_LargeOperations()
        {
            // Arrange
            var connector = MockGremlinConnectorFactory.CreateForTesting();

            // Act - Perform many operations
            for (int i = 0; i < 100; i++)
            {
                await connector.ExecuteAsync($"g.addV('person').property('id', '{i}')", new Dictionary<string, object>());
            }

            // Query the state
            var vertices = await connector.ExecuteAsync("g.V()", new Dictionary<string, object>());

            // Assert
            Assert.Equal(100, vertices.Count());
            _output.WriteLine($"Successfully maintained state for {vertices.Count()} vertices");
        }

        [Fact]
        public async Task MockConnector_MemoryUsage_LongRunning()
        {
            // Arrange
            var connector = MockGremlinConnectorFactory.CreateForTesting();
            SetupMemoryTestMocks(connector);

            var initialMemory = GC.GetTotalMemory(true);

            // Act - Perform many operations to test memory management
            for (int i = 0; i < 1000; i++)
            {
                await connector.ExecuteAsync($"g.V().has('name', 'test{i}')", new Dictionary<string, object>());
                
                if (i % 100 == 0)
                {
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                }
            }

            var finalMemory = GC.GetTotalMemory(true);
            var memoryIncrease = finalMemory - initialMemory;

            // Assert
            _output.WriteLine($"Memory increase: {memoryIncrease / 1024 / 1024}MB");
            Assert.True(memoryIncrease < 50 * 1024 * 1024, "Memory increase should be less than 50MB");
        }

        [Fact]
        public async Task MockConnector_ComplexGraph_Performance()
        {
            // Arrange
            var connector = MockGremlinConnectorFactory.CreateForTesting();
            SetupComplexGraphMocks(connector);

            var stopwatch = Stopwatch.StartNew();

            // Act - Create a complex graph structure using connector directly
            for (int i = 0; i < 50; i++)
            {
                await connector.ExecuteAsync($"g.addV('person').property('name', 'Person {i}')", new Dictionary<string, object>());
            }

            for (int i = 0; i < 10; i++)
            {
                await connector.ExecuteAsync($"g.addV('company').property('name', 'Company {i}')", new Dictionary<string, object>());
            }

            // Create relationships
            for (int i = 0; i < 49; i++)
            {
                await connector.ExecuteAsync($"g.V().has('name', 'Person {i}').addE('parent').to(g.V().has('name', 'Person {i + 1}'))", new Dictionary<string, object>());
            }

            stopwatch.Stop();

            // Assert
            _output.WriteLine($"Complex graph created in {stopwatch.ElapsedMilliseconds}ms");
            Assert.True(stopwatch.ElapsedMilliseconds < 10000, "Complex graph creation should complete in under 10 seconds");
        }

        [Fact]
        public void MockConnector_ResetPerformance()
        {
            // Arrange
            var connector = MockGremlinConnectorFactory.CreateForTesting();
            
            // Add some data
            for (int i = 0; i < 100; i++)
            {
                connector.GetGraphStore().AddVertex($"g.addV('person').property('id', '{i}')", new Dictionary<string, object>());
            }

            // Act
            var stopwatch = Stopwatch.StartNew();
            connector.Reset();
            stopwatch.Stop();

            // Assert
            _output.WriteLine($"Reset completed in {stopwatch.ElapsedMilliseconds}ms");
            Assert.True(stopwatch.ElapsedMilliseconds < 100, "Reset should complete in under 100ms");
            Assert.Equal(0, connector.ConsumedRU);
            Assert.Empty(connector.GetGraphStore().Vertices);
        }

        [Fact]
        public async Task MockConnector_QueryPattern_Matching_Performance()
        {
            // Arrange
            var connector = MockGremlinConnectorFactory.Create();
            
            // Configure many patterns
            for (int i = 0; i < 100; i++)
            {
                connector.ConfigureFunctionResponse($@"pattern{i}", (q, p) => new[] 
                { 
                    MockExtensions.CreateVertexResponse($"v{i}", "person") 
                });
            }

            // Act
            var stopwatch = Stopwatch.StartNew();
            var result = await connector.ExecuteAsync("pattern50", new Dictionary<string, object>());
            stopwatch.Stop();

            // Assert
            _output.WriteLine($"Pattern matching completed in {stopwatch.ElapsedMilliseconds}ms");
            Assert.True(stopwatch.ElapsedMilliseconds < 100, "Pattern matching should be fast");
            Assert.Single(result);
        }

        #region Helper Methods

        private void SetupLargeDatasetMocks(MockGremlinLanguageConnector connector)
        {
            connector.ConfigureFunctionResponse(@"g\.V\(\)\.count\(\)", (query, parameters) =>
            {
                return new dynamic[] { 1000000 }; // Simulate 1 million vertices
            });
        }

        private void SetupConcurrentQueryMocks(MockGremlinLanguageConnector connector)
        {
            connector.ConfigureFunctionResponse(@"g\.V\(\)\.has\('id'", (query, parameters) =>
            {
                var id = ExtractIdFromQuery(query);
                return new[]
                {
                    MockExtensions.CreateVertexResponse(id, "person", new Dictionary<string, object>
                    {
                        { "id", id },
                        { "name", $"Person {id}" }
                    })
                };
            });
        }

        private void SetupMemoryTestMocks(MockGremlinLanguageConnector connector)
        {
            connector.ConfigureFunctionResponse(@"g\.V\(\)\.has\('name'", (query, parameters) =>
            {
                // Return minimal data to test memory efficiency
                return new[]
                {
                    MockExtensions.CreateVertexResponse("1", "person", new Dictionary<string, object>
                    {
                        { "name", "test" }
                    })
                };
            });
        }

        private void SetupComplexGraphMocks(MockGremlinLanguageConnector connector)
        {
            // Mock vertex creation for persons
            connector.ConfigureFunctionResponse(@"g\.addV\('person'\)", (query, parameters) =>
            {
                return new[]
                {
                    MockExtensions.CreateVertexResponse(
                        Guid.NewGuid().ToString(),
                        "person",
                        new Dictionary<string, object>()
                    )
                };
            });

            // Mock vertex creation for companies
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

            // Mock edge creation
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

        private string ExtractIdFromQuery(string query)
        {
            // Simple extraction for test purposes
            var start = query.IndexOf("'") + 1;
            var end = query.LastIndexOf("'");
            if (start > 0 && end > start)
            {
                return query.Substring(start, end - start);
            }
            return "default";
        }

        #endregion

        public void Dispose()
        {
            // Force garbage collection to clean up test resources
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }
    }
}