using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;
using Newtonsoft.Json;

namespace Stardust.Paradox.Data.InMemory.Tests
{
    /// <summary>
    /// Performance and stress tests for GremlinServer protocols and GraphSON versions
    /// Validates server stability, throughput, and resource management under load
    /// </summary>
    public class GremlinServerPerformanceTests : IDisposable
    {
        private readonly List<GremlinServer> _servers = new();
        private readonly List<IDisposable> _disposables = new();

        [Fact]
        public async Task Performance_Direct_Protocol_Should_Handle_High_Throughput()
        {
            // Arrange
            var server = await StartServerAsync(new GremlinServerOptions
            {
                EnableTcp = false,
                EnableWebSocket = false,
                EnableHttp = false,
                EnableLogging = false,
                MaxConnections = 1000
            });

            const int queryCount = 1000;
            var stopwatch = Stopwatch.StartNew();

            // Act - Execute queries in parallel
            var tasks = Enumerable.Range(0, queryCount).Select(async i =>
            {
                var result = await server.Connector.ExecuteAsync($"g.inject({i})", new Dictionary<string, object>());
                return result.First();
            });

            var results = await Task.WhenAll(tasks);
            stopwatch.Stop();

            // Assert
            results.Should().HaveCount(queryCount);
            results.Should().BeEquivalentTo(Enumerable.Range(0, queryCount));

            var throughput = queryCount / stopwatch.Elapsed.TotalSeconds;
            throughput.Should().BeGreaterThan(100, "Should handle at least 100 queries/second for direct protocol");

            Console.WriteLine($"Direct Protocol: {queryCount} queries in {stopwatch.Elapsed.TotalMilliseconds:F2}ms ({throughput:F1} queries/sec)");
        }

        [Theory]
        [InlineData(GraphSONVersion.V1)]
        [InlineData(GraphSONVersion.V2)]
        [InlineData(GraphSONVersion.V3)]
        public async Task Performance_WebSocket_Protocol_Should_Handle_Sustained_Load(GraphSONVersion version)
        {
            // Arrange
            var server = await StartServerAsync(new GremlinServerOptions
            {
                Host = "localhost",
                HttpPort = GetRandomPort(),
                EnableTcp = false,
                EnableWebSocket = true,
                EnableHttp = false,
                EnableLogging = false,
                MaxConnections = 100
            });

            const int connectionsCount = 10;
            const int queriesPerConnection = 50;
            var protocol = version switch
            {
                GraphSONVersion.V1 => "graphson-v1",
                GraphSONVersion.V2 => "graphson-v2",
                GraphSONVersion.V3 => "gremlin-ws",
                _ => "gremlin-ws"
            };

            var stopwatch = Stopwatch.StartNew();

            // Act - Create multiple concurrent WebSocket connections
            var connectionTasks = Enumerable.Range(0, connectionsCount).Select(async connectionIndex =>
            {
                using var client = new ClientWebSocket();
                client.Options.AddSubProtocol(protocol);
                
                var uri = new Uri($"ws://localhost:{server.Options.GetHttpPort()}/gremlin");
                using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
                
                await client.ConnectAsync(uri, cts.Token);

                var serializer = new TinkerPopGraphSONSerializer(version);
                var queryResults = new List<object>();

                // Execute multiple queries on this connection
                for (int queryIndex = 0; queryIndex < queriesPerConnection; queryIndex++)
                {
                    var value = connectionIndex * queriesPerConnection + queryIndex;
                    var message = new TinkerPopMessage
                    {
                        RequestId = Guid.NewGuid(),
                        Op = TinkerPopOperations.Eval,
                        Args = new Dictionary<string, object>
                        {
                            ["gremlin"] = $"g.inject({value})",
                            ["bindings"] = new Dictionary<string, object>()
                        }
                    };

                    var messageJson = serializer.SerializeMessage(message);
                    var messageBytes = Encoding.UTF8.GetBytes(messageJson);

                    await client.SendAsync(new ArraySegment<byte>(messageBytes), WebSocketMessageType.Text, true, cts.Token);

                    var buffer = new byte[4096];
                    var result = await client.ReceiveAsync(new ArraySegment<byte>(buffer), cts.Token);
                    
                    var responseJson = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    var response = JsonConvert.DeserializeObject<dynamic>(responseJson);
                    
                    queryResults.Add(response.result.data[0].Value);
                }

                await client.CloseAsync(WebSocketCloseStatus.NormalClosure, "Test complete", cts.Token);
                return queryResults;
            });

            var allResults = await Task.WhenAll(connectionTasks);
            stopwatch.Stop();

            // Assert
            var totalQueries = connectionsCount * queriesPerConnection;
            var flatResults = allResults.SelectMany(r => r).ToList();
            
            flatResults.Should().HaveCount(totalQueries);
            
            var throughput = totalQueries / stopwatch.Elapsed.TotalSeconds;
            throughput.Should().BeGreaterThan(10, $"Should handle reasonable throughput for WebSocket {version}");

            Console.WriteLine($"WebSocket {version}: {totalQueries} queries across {connectionsCount} connections in {stopwatch.Elapsed.TotalMilliseconds:F2}ms ({throughput:F1} queries/sec)");
        }

        [Fact]
        public async Task Performance_Mixed_Protocol_Load_Should_Not_Degrade_Performance()
        {
            // Arrange
            var server = await StartServerAsync(new GremlinServerOptions
            {
                Host = "localhost",
                Port = GetRandomPort(),
                HttpPort = GetRandomPort(),
                EnableTcp = true,
                EnableWebSocket = true,
                EnableHttp = true,
                EnableLogging = false,
                MaxConnections = 200
            });

            const int operationsPerProtocol = 100;
            var stopwatch = Stopwatch.StartNew();

            // Act - Run all protocols simultaneously
            var directTask = Task.Run(async () =>
            {
                var results = new List<object>();
                for (int i = 0; i < operationsPerProtocol; i++)
                {
                    var result = await server.Connector.ExecuteAsync($"g.inject({i})", new Dictionary<string, object>());
                    results.Add(result.First());
                }
                return results;
            });

            var webSocketTask = Task.Run(async () =>
            {
                var results = new List<object>();
                using var client = new ClientWebSocket();
                client.Options.AddSubProtocol("gremlin-ws");
                
                var uri = new Uri($"ws://localhost:{server.Options.GetHttpPort()}/gremlin");
                using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
                
                await client.ConnectAsync(uri, cts.Token);
                var serializer = new TinkerPopGraphSONSerializer(GraphSONVersion.V3);

                for (int i = 0; i < operationsPerProtocol; i++)
                {
                    var message = new TinkerPopMessage
                    {
                        RequestId = Guid.NewGuid(),
                        Op = TinkerPopOperations.Eval,
                        Args = new Dictionary<string, object>
                        {
                            ["gremlin"] = $"g.inject({i + 1000})"
                        }
                    };

                    var messageBytes = Encoding.UTF8.GetBytes(serializer.SerializeMessage(message));
                    await client.SendAsync(new ArraySegment<byte>(messageBytes), WebSocketMessageType.Text, true, cts.Token);

                    var buffer = new byte[4096];
                    var result = await client.ReceiveAsync(new ArraySegment<byte>(buffer), cts.Token);
                    var responseJson = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    var response = JsonConvert.DeserializeObject<dynamic>(responseJson);
                    
                    results.Add(response.result.data[0].Value);
                }

                await client.CloseAsync(WebSocketCloseStatus.NormalClosure, "Test complete", cts.Token);
                return results;
            });

            var httpTask = Task.Run(async () =>
            {
                var results = new List<object>();
                using var httpClient = new HttpClient();

                for (int i = 0; i < operationsPerProtocol; i++)
                {
                    var postData = JsonConvert.SerializeObject(new
                    {
                        requestId = Guid.NewGuid().ToString(),
                        op = "eval",
                        args = new
                        {
                            gremlin = $"g.inject({i + 2000})"
                        }
                    });

                    var content = new StringContent(postData, Encoding.UTF8, "application/json");
                    var response = await httpClient.PostAsync($"http://localhost:{server.Options.GetHttpPort()}", content);
                    var responseContent = await response.Content.ReadAsStringAsync();
                    var responseObj = JsonConvert.DeserializeObject<dynamic>(responseContent);
                    
                    results.Add(responseObj.result.data[0].Value);
                }

                return results;
            });

            var allResults = await Task.WhenAll(directTask, webSocketTask, httpTask);
            stopwatch.Stop();

            // Assert
            var totalOperations = operationsPerProtocol * 3;
            var allFlatResults = allResults.SelectMany(r => r).ToList();
            
            allFlatResults.Should().HaveCount(totalOperations);
            
            var throughput = totalOperations / stopwatch.Elapsed.TotalSeconds;
            throughput.Should().BeGreaterThan(5, "Mixed protocol load should maintain reasonable throughput");

            Console.WriteLine($"Mixed Protocols: {totalOperations} operations across 3 protocols in {stopwatch.Elapsed.TotalMilliseconds:F2}ms ({throughput:F1} ops/sec)");

            // Verify server is still stable
            server.IsRunning.Should().BeTrue();
            var stats = server.GetStatistics();
            stats["isRunning"].Should().Be(true);
        }

        [Fact]
        public async Task Performance_Large_Graph_Operations_Should_Complete_Efficiently()
        {
            // Arrange
            var server = await StartServerAsync(new GremlinServerOptions
            {
                EnableTcp = false,
                EnableWebSocket = false,
                EnableHttp = false,
                EnableLogging = false
            });

            const int vertexCount = 1000;
            const int edgeCount = 500;

            var stopwatch = Stopwatch.StartNew();

            // Act - Create a moderately sized graph
            // Create vertices
            var vertexTasks = Enumerable.Range(0, vertexCount).Select(async i =>
            {
                return await server.Connector.ExecuteAsync(
                    $"g.addV('person').property('id', {i}).property('name', 'Person{i}')",
                    new Dictionary<string, object>());
            });

            await Task.WhenAll(vertexTasks);
            var vertexCreationTime = stopwatch.Elapsed;

            // Create edges
            var edgeTasks = Enumerable.Range(0, edgeCount).Select(async i =>
            {
                var fromId = i;
                var toId = (i + 1) % vertexCount;
                return await server.Connector.ExecuteAsync(
                    $"g.V().has('id', {fromId}).addE('knows').to(g.V().has('id', {toId}))",
                    new Dictionary<string, object>());
            });

            await Task.WhenAll(edgeTasks);
            var edgeCreationTime = stopwatch.Elapsed;

            // Perform complex traversals
            var traversalQueries = new[]
            {
                "g.V().count()",
                "g.E().count()",
                "g.V().out('knows').count()",
                "g.V().has('id', 0).out('knows').out('knows').count()",
                "g.V().groupCount().by(outE().count())"
            };

            var traversalTasks = traversalQueries.Select(async query =>
            {
                return await server.Connector.ExecuteAsync(query, new Dictionary<string, object>());
            });

            var traversalResults = await Task.WhenAll(traversalTasks);
            stopwatch.Stop();

            // Assert
            traversalResults[0].First().Should().Be((long)vertexCount); // Vertex count
            traversalResults[1].First().Should().Be((long)edgeCount); // Edge count

            var vertexCreationThroughput = vertexCount / vertexCreationTime.TotalSeconds;
            var edgeCreationThroughput = edgeCount / (edgeCreationTime.TotalSeconds - vertexCreationTime.TotalSeconds);

            vertexCreationThroughput.Should().BeGreaterThan(10, "Should create vertices efficiently");
            edgeCreationThroughput.Should().BeGreaterThan(5, "Should create edges efficiently");

            Console.WriteLine($"Graph Creation: {vertexCount} vertices ({vertexCreationThroughput:F1}/sec), {edgeCount} edges ({edgeCreationThroughput:F1}/sec)");
            Console.WriteLine($"Total time: {stopwatch.Elapsed.TotalMilliseconds:F2}ms");
        }

        [Fact]
        public async Task Performance_Memory_Usage_Should_Remain_Stable_Under_Load()
        {
            // Arrange
            var server = await StartServerAsync(new GremlinServerOptions
            {
                Host = "localhost",
                HttpPort = GetRandomPort(),
                EnableTcp = false,
                EnableWebSocket = true,
                EnableHttp = false,
                EnableLogging = false,
                MaxConnections = 50
            });

            var initialMemory = GC.GetTotalMemory(true);
            const int iterationCount = 100;
            const int queriesPerIteration = 10;

            // Act - Repeatedly create connections and execute queries
            for (int iteration = 0; iteration < iterationCount; iteration++)
            {
                var iterationTasks = Enumerable.Range(0, queriesPerIteration).Select(async queryIndex =>
                {
                    using var client = new ClientWebSocket();
                    client.Options.AddSubProtocol("gremlin-ws");
                    
                    var uri = new Uri($"ws://localhost:{server.Options.GetHttpPort()}/gremlin");
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                    
                    await client.ConnectAsync(uri, cts.Token);

                    var serializer = new TinkerPopGraphSONSerializer(GraphSONVersion.V3);
                    var message = new TinkerPopMessage
                    {
                        RequestId = Guid.NewGuid(),
                        Op = TinkerPopOperations.Eval,
                        Args = new Dictionary<string, object>
                        {
                            ["gremlin"] = $"g.inject({iteration * queriesPerIteration + queryIndex})"
                        }
                    };

                    var messageBytes = Encoding.UTF8.GetBytes(serializer.SerializeMessage(message));
                    await client.SendAsync(new ArraySegment<byte>(messageBytes), WebSocketMessageType.Text, true, cts.Token);

                    var buffer = new byte[4096];
                    await client.ReceiveAsync(new ArraySegment<byte>(buffer), cts.Token);

                    await client.CloseAsync(WebSocketCloseStatus.NormalClosure, "Test complete", cts.Token);
                });

                await Task.WhenAll(iterationTasks);

                // Force garbage collection periodically
                if (iteration % 20 == 0)
                {
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    GC.Collect();
                }
            }

            // Assert
            var finalMemory = GC.GetTotalMemory(true);
            var memoryIncrease = finalMemory - initialMemory;
            var memoryIncreasePercent = (double)memoryIncrease / initialMemory * 100;

            // Memory increase should be reasonable (less than 100% increase)
            memoryIncreasePercent.Should().BeLessThan(100, "Memory usage should not grow excessively");

            Console.WriteLine($"Memory: Initial={initialMemory / 1024 / 1024:F1}MB, Final={finalMemory / 1024 / 1024:F1}MB, Increase={memoryIncreasePercent:F1}%");

            // Server should still be responsive
            server.IsRunning.Should().BeTrue();
            var stats = server.GetStatistics();
            stats["isRunning"].Should().Be(true);
        }

        [Theory]
        [InlineData(1000)] // 1KB
        [InlineData(10000)] // 10KB
        [InlineData(100000)] // 100KB
        public async Task Performance_Large_Message_Handling_Should_Be_Efficient(int messageSize)
        {
            // Arrange
            var server = await StartServerAsync(new GremlinServerOptions
            {
                Host = "localhost",
                HttpPort = GetRandomPort(),
                EnableTcp = false,
                EnableWebSocket = true,
                EnableHttp = false,
                EnableLogging = false,
                WebSocketOptions = new GremlinWebSocketOptions
                {
                    MaxMessageSize = 1024 * 1024 // 1MB
                }
            });

            // Create a large string for testing
            var largeString = new string('A', messageSize);
            var stopwatch = Stopwatch.StartNew();

            using var client = new ClientWebSocket();
            client.Options.AddSubProtocol("gremlin-ws");
            
            var uri = new Uri($"ws://localhost:{server.Options.GetHttpPort()}/gremlin");
            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(1));
            
            await client.ConnectAsync(uri, cts.Token);

            var serializer = new TinkerPopGraphSONSerializer(GraphSONVersion.V3);
            var message = new TinkerPopMessage
            {
                RequestId = Guid.NewGuid(),
                Op = TinkerPopOperations.Eval,
                Args = new Dictionary<string, object>
                {
                    ["gremlin"] = "g.inject(largeData)",
                    ["bindings"] = new Dictionary<string, object>
                    {
                        ["largeData"] = largeString
                    }
                }
            };

            // Act
            var messageJson = serializer.SerializeMessage(message);
            var messageBytes = Encoding.UTF8.GetBytes(messageJson);

            await client.SendAsync(new ArraySegment<byte>(messageBytes), WebSocketMessageType.Text, true, cts.Token);

            var buffer = new byte[messageBytes.Length * 2]; // Ensure enough space for response
            var result = await client.ReceiveAsync(new ArraySegment<byte>(buffer), cts.Token);
            
            stopwatch.Stop();

            // Assert
            result.Count.Should().BeGreaterThan(0);
            
            var responseJson = Encoding.UTF8.GetString(buffer, 0, result.Count);
            var response = JsonConvert.DeserializeObject<dynamic>(responseJson);
            
            response.status.code.Value.Should().Be(200);
            response.result.data[0].Value.Should().Be(largeString);

            var processingTime = stopwatch.Elapsed.TotalMilliseconds;
            var throughputMBps = (messageBytes.Length / 1024.0 / 1024.0) / stopwatch.Elapsed.TotalSeconds;

            Console.WriteLine($"Large Message ({messageSize / 1024}KB): {processingTime:F2}ms, {throughputMBps:F2} MB/s");

            await client.CloseAsync(WebSocketCloseStatus.NormalClosure, "Test complete", cts.Token);
        }

        [Fact]
        public async Task Performance_Concurrent_GraphSON_Versions_Should_Not_Interfere()
        {
            // Arrange
            var server = await StartServerAsync(new GremlinServerOptions
            {
                Host = "localhost",
                HttpPort = GetRandomPort(),
                EnableTcp = false,
                EnableWebSocket = true,
                EnableHttp = false,
                EnableLogging = false,
                MaxConnections = 50
            });

            const int connectionsPerVersion = 10;
            const int queriesPerConnection = 20;
            var versions = new[] { GraphSONVersion.V1, GraphSONVersion.V2, GraphSONVersion.V3 };

            var stopwatch = Stopwatch.StartNew();

            // Act - Run all GraphSON versions concurrently
            var versionTasks = versions.Select(async version =>
            {
                var protocol = version switch
                {
                    GraphSONVersion.V1 => "graphson-v1",
                    GraphSONVersion.V2 => "graphson-v2",
                    GraphSONVersion.V3 => "gremlin-ws",
                    _ => "gremlin-ws"
                };

                var connectionTasks = Enumerable.Range(0, connectionsPerVersion).Select(async connectionIndex =>
                {
                    using var client = new ClientWebSocket();
                    client.Options.AddSubProtocol(protocol);
                    
                    var uri = new Uri($"ws://localhost:{server.Options.GetHttpPort()}/gremlin");
                    using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
                    
                    await client.ConnectAsync(uri, cts.Token);

                    var serializer = new TinkerPopGraphSONSerializer(version);
                    var results = new List<object>();

                    for (int queryIndex = 0; queryIndex < queriesPerConnection; queryIndex++)
                    {
                        var value = (int)version * 10000 + connectionIndex * 100 + queryIndex;
                        var message = new TinkerPopMessage
                        {
                            RequestId = Guid.NewGuid(),
                            Op = TinkerPopOperations.Eval,
                            Args = new Dictionary<string, object>
                            {
                                ["gremlin"] = $"g.inject({value})"
                            }
                        };

                        var messageBytes = Encoding.UTF8.GetBytes(serializer.SerializeMessage(message));
                        await client.SendAsync(new ArraySegment<byte>(messageBytes), WebSocketMessageType.Text, true, cts.Token);

                        var buffer = new byte[4096];
                        var result = await client.ReceiveAsync(new ArraySegment<byte>(buffer), cts.Token);
                        var responseJson = Encoding.UTF8.GetString(buffer, 0, result.Count);
                        var response = JsonConvert.DeserializeObject<dynamic>(responseJson);
                        
                        results.Add(response.result.data[0].Value);
                    }

                    await client.CloseAsync(WebSocketCloseStatus.NormalClosure, "Test complete", cts.Token);
                    return results;
                });

                var connectionResults = await Task.WhenAll(connectionTasks);
                return new { Version = version, Results = connectionResults.SelectMany(r => r).ToList() };
            });

            var versionResults = await Task.WhenAll(versionTasks);
            stopwatch.Stop();

            // Assert
            var totalOperations = versions.Length * connectionsPerVersion * queriesPerConnection;
            var allResults = versionResults.SelectMany(vr => vr.Results).ToList();
            
            allResults.Should().HaveCount(totalOperations);
            
            // Each version should have processed all its queries correctly
            foreach (var versionResult in versionResults)
            {
                var expectedCount = connectionsPerVersion * queriesPerConnection;
                versionResult.Results.Should().HaveCount(expectedCount);
            }

            var throughput = totalOperations / stopwatch.Elapsed.TotalSeconds;
            Console.WriteLine($"Concurrent GraphSON: {totalOperations} operations across {versions.Length} versions in {stopwatch.Elapsed.TotalMilliseconds:F2}ms ({throughput:F1} ops/sec)");
        }

        private async Task<GremlinServer> StartServerAsync(GremlinServerOptions options)
        {
            var server = new GremlinServer(options);
            await server.StartAsync();
            _servers.Add(server);
            
            // Give the server a moment to fully initialize
            await Task.Delay(1000);
            
            return server;
        }

        private static int GetRandomPort()
        {
            var random = new Random();
            return random.Next(8000, 9000);
        }

        public void Dispose()
        {
            foreach (var disposable in _disposables)
            {
                try
                {
                    disposable?.Dispose();
                }
                catch
                {
                    // Ignore disposal errors
                }
            }
            _disposables.Clear();

            foreach (var server in _servers)
            {
                try
                {
                    server.StopAsync().Wait(5000);
                    server.Dispose();
                }
                catch
                {
                    // Ignore disposal errors
                }
            }
            _servers.Clear();
        }
    }
}