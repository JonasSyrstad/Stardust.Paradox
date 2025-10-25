using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Stardust.Paradox.Data.InMemory;
using Xunit;
using Xunit.Abstractions;

namespace Stardust.Paradox.Data.InMemory.Tests
{
    /// <summary>
    /// Tests for query logging and debug export functionality
    /// </summary>
    public class QueryLogExportTests
    {
        private readonly ITestOutputHelper _output;

        public QueryLogExportTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public async Task ExecutedQueries_AreLoggedCorrectly()
        {
            // Arrange
            var connector = new InMemoryGremlinLanguageConnector();
            
            // Add some test data
            connector.AddVertex("person", new System.Collections.Generic.Dictionary<string, object>
            {
                ["name"] = "Alice",
                ["age"] = 30
            }, "1");
            
            connector.AddVertex("person", new System.Collections.Generic.Dictionary<string, object>
            {
                ["name"] = "Bob",
                ["age"] = 25
            }, "2");

            // Act - Execute some queries
            await connector.ExecuteAsync("g.V()", null);
            await connector.ExecuteAsync("g.V().hasLabel('person')", null);
            await connector.ExecuteAsync("g.V().has('name', 'Alice')", null);

            // Assert
            var queryLog = connector.GetQueryLog().ToList();
            Assert.Equal(3, queryLog.Count);
            
            _output.WriteLine($"Query log contains {queryLog.Count} entries");
            foreach (var entry in queryLog)
            {
                _output.WriteLine($"Query: {entry.GetType().GetProperty("Query")?.GetValue(entry)}");
            }
        }

        [Fact]
        public async Task FailedQueries_AreLoggedWithErrors()
        {
            // Arrange
            var connector = new InMemoryGremlinLanguageConnector();

            // Act - Execute a valid query and an invalid one
            await connector.ExecuteAsync("g.V()", null);
            
            try
            {
                await connector.ExecuteAsync("g.InvalidMethod()", null);
            }
            catch
            {
                // Expected to fail
            }

            // Assert
            var queryLog = connector.GetQueryLog().ToList();
            Assert.Equal(2, queryLog.Count);
            
            var failedQuery = queryLog.FirstOrDefault(q => 
                !(bool)q.GetType().GetProperty("Success")?.GetValue(q));
            Assert.NotNull(failedQuery);
            
            var errorMessage = failedQuery.GetType().GetProperty("ErrorMessage")?.GetValue(failedQuery);
            Assert.NotNull(errorMessage);
            
            _output.WriteLine($"Failed query error: {errorMessage}");
        }

        [Fact]
        public async Task QueryLogStatistics_AreCalculatedCorrectly()
        {
            // Arrange
            var connector = new InMemoryGremlinLanguageConnector();
            
            connector.AddVertex("person", new System.Collections.Generic.Dictionary<string, object>
            {
                ["name"] = "Alice"
            }, "1");

            // Act - Execute multiple queries
            await connector.ExecuteAsync("g.V()", null);
            await connector.ExecuteAsync("g.V().hasLabel('person')", null);
            await connector.ExecuteAsync("g.V().count()", null);

            var stats = connector.GetQueryLogStatistics();

            // Assert
            Assert.True((int)stats["totalQueries"] >= 3);
            Assert.True((int)stats["successfulQueries"] >= 3);
            Assert.True((double)stats["averageExecutionTimeMs"] >= 0);
            
            _output.WriteLine($"Total queries: {stats["totalQueries"]}");
            _output.WriteLine($"Successful queries: {stats["successfulQueries"]}");
            _output.WriteLine($"Average execution time: {stats["averageExecutionTimeMs"]}ms");
        }

        [Fact]
        public async Task DebugDataExport_ContainsQueryLogAndDatabaseStructures()
        {
            // Arrange
            var connector = new InMemoryGremlinLanguageConnector();
            
            // Add test data
            var v1 = connector.AddVertex("person", new System.Collections.Generic.Dictionary<string, object>
            {
                ["name"] = "Alice",
                ["age"] = 30
            }, "1");
            
            var v2 = connector.AddVertex("person", new System.Collections.Generic.Dictionary<string, object>
            {
                ["name"] = "Bob",
                ["age"] = 25
            }, "2");
            
            connector.AddEdge("knows", "1", "2", new System.Collections.Generic.Dictionary<string, object>
            {
                ["since"] = 2020
            });

            // Execute some queries
            await connector.ExecuteAsync("g.V()", null);
            await connector.ExecuteAsync("g.V().out('knows')", null);

            // Act
            var debugJson = connector.ExportDebugDataAsJson();

            // Assert
            Assert.NotNull(debugJson);
            Assert.Contains("queryLog", debugJson);
            Assert.Contains("databaseStructures", debugJson);
            Assert.Contains("queryLogStats", debugJson);
            Assert.Contains("performanceMetrics", debugJson);
            
            _output.WriteLine("Debug JSON export (first 500 chars):");
            _output.WriteLine(debugJson.Substring(0, Math.Min(500, debugJson.Length)));
        }

        [Fact]
        public async Task DebugDataExportToFile_CreatesValidJsonFile()
        {
            // Arrange
            var connector = new InMemoryGremlinLanguageConnector();
            var tempFile = Path.Combine(Path.GetTempPath(), $"debug_export_{Guid.NewGuid()}.json");
            
            // Add test data
            connector.AddVertex("person", new System.Collections.Generic.Dictionary<string, object>
            {
                ["name"] = "Alice"
            }, "1");

            // Execute a query
            await connector.ExecuteAsync("g.V().hasLabel('person')", null);

            try
            {
                // Act
                connector.ExportDebugDataToFile(tempFile);

                // Assert
                Assert.True(File.Exists(tempFile));
                var content = File.ReadAllText(tempFile);
                Assert.NotEmpty(content);
                Assert.Contains("queryLog", content);
                
                _output.WriteLine($"Debug export file created: {tempFile}");
                _output.WriteLine($"File size: {new FileInfo(tempFile).Length} bytes");
            }
            finally
            {
                // Cleanup
                if (File.Exists(tempFile))
                {
                    File.Delete(tempFile);
                }
            }
        }

        [Fact]
        public async Task ClearQueryLog_RemovesAllEntries()
        {
            // Arrange
            var connector = new InMemoryGremlinLanguageConnector();
            
            // Execute some queries
            await connector.ExecuteAsync("g.V()", null);
            await connector.ExecuteAsync("g.E()", null);

            var logCountBefore = connector.GetQueryLog().Count();
            Assert.True(logCountBefore > 0);

            // Act
            connector.ClearQueryLog();

            // Assert
            var logCountAfter = connector.GetQueryLog().Count();
            Assert.Equal(0, logCountAfter);
            
            _output.WriteLine($"Query log cleared. Before: {logCountBefore}, After: {logCountAfter}");
        }

        [Fact]
        public async Task ParserUsageIsTracked_InQueryLog()
        {
            // Arrange
            var connector = new InMemoryGremlinLanguageConnector();
            
            connector.AddVertex("person", new System.Collections.Generic.Dictionary<string, object>
            {
                ["name"] = "Alice"
            }, "1");

            // Act - Execute queries that should use different parsers
            await connector.ExecuteAsync("g.V()", null);
            await connector.ExecuteAsync("g.V().hasLabel('person')", null);

            // Assert
            var queryLog = connector.GetQueryLog().ToList();
            Assert.True(queryLog.Count > 0);
            
            foreach (var entry in queryLog)
            {
                var parserUsed = entry.GetType().GetProperty("ParserUsed")?.GetValue(entry);
                Assert.NotNull(parserUsed);
                _output.WriteLine($"Query parser: {parserUsed}");
            }

            var stats = connector.GetQueryLogStatistics();
            Assert.True(stats.ContainsKey("parserUsage"));
            
            _output.WriteLine($"Parser usage: {System.Text.Json.JsonSerializer.Serialize(stats["parserUsage"])}");
        }

        [Fact]
        public async Task DatabaseInternalStructures_ExportContainsIndices()
        {
            // Arrange
            var connector = new InMemoryGremlinLanguageConnector();
            
            // Add vertices with various properties
            connector.AddVertex("person", new System.Collections.Generic.Dictionary<string, object>
            {
                ["name"] = "Alice",
                ["age"] = 30,
                ["city"] = "New York"
            }, "1");
            
            connector.AddVertex("person", new System.Collections.Generic.Dictionary<string, object>
            {
                ["name"] = "Bob",
                ["age"] = 25,
                ["city"] = "London"
            }, "2");
            
            connector.AddVertex("company", new System.Collections.Generic.Dictionary<string, object>
            {
                ["name"] = "Acme Corp"
            }, "3");
            
            connector.AddEdge("works_at", "1", "3", null);
            connector.AddEdge("knows", "1", "2", null);

            // Execute a query
            await connector.ExecuteAsync("g.V()", null);

            
            // Act
            var debugJson = connector.ExportDebugDataAsJson();
            var structure = connector.Database.ExportInternalStructures();
            //Assert.Equal(2, ((Dictionary<string,HashSet<string>>)structure["outEdgeIndex"]).Count);
            var dbStructuresJson = connector.Database.ExportInternalStructuresAsJson();

            // Assert
            Assert.Contains("vertexLabelIndex", dbStructuresJson);
            Assert.Contains("edgeLabelIndex", dbStructuresJson);
            Assert.Contains("vertexPropertyIndex", dbStructuresJson);
            Assert.Contains("outEdgeIndex", dbStructuresJson);
            Assert.Contains("inEdgeIndex", dbStructuresJson);
            
            _output.WriteLine("Database structures export (first 1000 chars):");
            _output.WriteLine(dbStructuresJson.Substring(0, Math.Min(1000, dbStructuresJson.Length)));
        }
    }
}
