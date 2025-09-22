using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Stardust.Paradox.Data.InMemory.Tests
{
    /// <summary>
    /// Comprehensive test suite for all GremlinServer connection protocols and GraphSON versions
    /// Tests TCP, WebSocket, HTTP, and Direct protocols with GraphSON v1, v2, v3
    /// </summary>
    public class GremlinServerProtocolTests : IDisposable
    {
        private GremlinServer? _server;
        private readonly List<IDisposable> _disposables = new();

        [Theory]
        [InlineData(true, true, true)] // All protocols enabled
        [InlineData(true, false, false)] // TCP only
        [InlineData(false, true, false)] // WebSocket only  
        [InlineData(false, false, true)] // HTTP only
        public async Task GremlinServer_Should_Start_With_Different_Protocol_Combinations(bool enableTcp, bool enableWebSocket, bool enableHttp)
        {
            // Arrange
            var options = new GremlinServerOptions
            {
                Host = "localhost",
                Port = GetRandomPort(),
                HttpPort = GetRandomPort(),
                EnableTcp = enableTcp,
                EnableWebSocket = enableWebSocket,
                EnableHttp = enableHttp,
                EnableLogging = false,
                EnableDebugLogging = false
            };

            // Act
            _server = new GremlinServer(options);
            await _server.StartAsync();

            // Assert
            _server.IsRunning.Should().BeTrue();
            _server.Options.EnableTcp.Should().Be(enableTcp);
            _server.Options.EnableWebSocket.Should().Be(enableWebSocket);
            _server.Options.EnableHttp.Should().Be(enableHttp);

            var stats = _server.GetStatistics();
            stats["isRunning"].Should().Be(true);
            stats["GraphSONVersions"].Should().BeEquivalentTo(new[] { "v1", "v2", "v3" });
            stats["SupportedSubProtocols"].Should().BeEquivalentTo(new[] { "gremlin-ws", "graphson-v1", "graphson-v2", "graphson-v3" });
        }

        [Fact]
        public async Task GremlinServer_Should_Handle_Direct_Protocol_Queries()
        {
            // Arrange
            await StartServerAsync(enableTcp: false, enableWebSocket: false, enableHttp: false);

            // Act & Assert - Test basic queries
            var result1 = await _server.Connector.ExecuteAsync("g.inject(42)", new Dictionary<string, object>());
            result1.Should().NotBeNull();
            result1.First().Should().Be(42);

            var result2 = await _server.Connector.ExecuteAsync("g.inject(1, 2, 3)", new Dictionary<string, object>());
            result2.Should().NotBeNull();
            result2.Should().HaveCount(3);
            result2.Should().BeEquivalentTo(new[] { 1, 2, 3 });

            // Test with bindings
            var result3 = await _server.Connector.ExecuteAsync("g.inject(x)", new Dictionary<string, object> { ["x"] = 100 });
            result3.Should().NotBeNull();
            result3.First().Should().Be(100);
        }

        [Fact]
        public async Task GremlinServer_Should_Handle_TCP_Protocol()
        {
            // Arrange
            await StartServerAsync(enableTcp: true, enableWebSocket: false, enableHttp: false);
            
            // Act & Assert
            using var tcpClient = new TcpClient();
            await tcpClient.ConnectAsync("localhost", _server.Options.Port);
            tcpClient.Connected.Should().BeTrue();

            var stream = tcpClient.GetStream();

            // Test simple query
            var query1 = "g.inject(42)";
            var queryBytes1 = Encoding.UTF8.GetBytes(query1);
            await stream.WriteAsync(queryBytes1, 0, queryBytes1.Length);

            var buffer = new byte[4096];
            var bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
            var response1 = Encoding.UTF8.GetString(buffer, 0, bytesRead);
            
            response1.Should().NotBeEmpty();
            var responseObj1 = JsonConvert.DeserializeObject<JObject>(response1.TrimEnd('\n'));
            responseObj1["success"].Value<bool>().Should().BeTrue();
            responseObj1["data"].Should().NotBeNull();

            // Test JSON query format
            var jsonQuery = JsonConvert.SerializeObject(new
            {
                requestId = Guid.NewGuid().ToString(),
                op = "eval",
                args = new
                {
                    gremlin = "g.inject(1, 2, 3)",
                    bindings = new { }
                }
            });

            var queryBytes2 = Encoding.UTF8.GetBytes(jsonQuery);
            await stream.WriteAsync(queryBytes2, 0, queryBytes2.Length);

            bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
            var response2 = Encoding.UTF8.GetString(buffer, 0, bytesRead);
            
            response2.Should().NotBeEmpty();
            var responseObj2 = JsonConvert.DeserializeObject<JObject>(response2.TrimEnd('\n'));
            responseObj2["status"]["code"].Value<int>().Should().Be(200);
        }

        [Theory]
        [InlineData(GraphSONVersion.V1)]
        [InlineData(GraphSONVersion.V2)]
        [InlineData(GraphSONVersion.V3)]
        public async Task GremlinServer_Should_Handle_WebSocket_Protocol_With_All_GraphSON_Versions(GraphSONVersion version)
        {
            // Arrange
            await StartServerAsync(enableTcp: false, enableWebSocket: true, enableHttp: false);

            using var client = new ClientWebSocket();
            var protocol = version switch
            {
                GraphSONVersion.V1 => "graphson-v1",
                GraphSONVersion.V2 => "graphson-v2", 
                GraphSONVersion.V3 => "gremlin-ws",
                _ => "gremlin-ws"
            };

            client.Options.AddSubProtocol(protocol);
            _disposables.Add(client);

            var uri = new Uri($"ws://localhost:{_server.Options.GetHttpPort()}/gremlin");
            
            // Act
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await client.ConnectAsync(uri, cts.Token);

            // Assert connection
            client.State.Should().Be(WebSocketState.Open);

            // Test GraphSON serialization
            var serializer = new TinkerPopGraphSONSerializer(version);
            var message = new TinkerPopMessage
            {
                RequestId = Guid.NewGuid(),
                Op = TinkerPopOperations.Eval,
                Processor = "",
                Args = new Dictionary<string, object>
                {
                    ["gremlin"] = "g.inject(42)",
                    ["bindings"] = new Dictionary<string, object>(),
                    ["language"] = "gremlin-groovy"
                }
            };

            var messageJson = serializer.SerializeMessage(message);
            var messageBytes = Encoding.UTF8.GetBytes(messageJson);

            await client.SendAsync(new ArraySegment<byte>(messageBytes), WebSocketMessageType.Text, true, cts.Token);

            // Receive response
            var buffer = new byte[4096];
            var result = await client.ReceiveAsync(new ArraySegment<byte>(buffer), cts.Token);
            
            result.MessageType.Should().Be(WebSocketMessageType.Text);
            var responseJson = Encoding.UTF8.GetString(buffer, 0, result.Count);
            
            responseJson.Should().NotBeEmpty();
            var response = JsonConvert.DeserializeObject<JObject>(responseJson);
            response["status"]["code"].Value<int>().Should().Be(200);
            response["result"]["data"].Should().NotBeNull();

            await client.CloseAsync(WebSocketCloseStatus.NormalClosure, "Test complete", cts.Token);
        }

        [Fact]
        public async Task GremlinServer_Should_Handle_Binary_WebSocket_Messages()
        {
            // Arrange
            await StartServerAsync(enableTcp: false, enableWebSocket: true, enableHttp: false);

            using var client = new ClientWebSocket();
            client.Options.AddSubProtocol("gremlin-ws");
            _disposables.Add(client);

            var uri = new Uri($"ws://localhost:{_server.Options.GetHttpPort()}/gremlin");
            
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await client.ConnectAsync(uri, cts.Token);

            // Test TinkerPop binary format
            var messageJson = JsonConvert.SerializeObject(new
            {
                requestId = Guid.NewGuid().ToString(),
                op = "eval",
                processor = "",
                args = new
                {
                    gremlin = "g.inject(42)",
                    bindings = new { },
                    language = "gremlin-groovy"
                }
            });

            // Test different binary formats
            var binaryFormats = new[]
            {
                // Standard UTF-8 JSON
                Encoding.UTF8.GetBytes(messageJson),
                // TinkerPop binary format with MIME type
                Encoding.UTF8.GetBytes($"!application/vnd.gremlin-v3.0+json{messageJson}")
            };

            foreach (var binaryMessage in binaryFormats)
            {
                // Act
                await client.SendAsync(new ArraySegment<byte>(binaryMessage), WebSocketMessageType.Binary, true, cts.Token);

                // Assert
                var buffer = new byte[4096];
                var result = await client.ReceiveAsync(new ArraySegment<byte>(buffer), cts.Token);
                
                // Should receive binary response for binary clients
                result.MessageType.Should().BeOneOf(WebSocketMessageType.Text, WebSocketMessageType.Binary);
                
                var responseJson = Encoding.UTF8.GetString(buffer, 0, result.Count);
                responseJson.Should().NotBeEmpty();
                
                var response = JsonConvert.DeserializeObject<JObject>(responseJson);
                response["status"]["code"].Value<int>().Should().Be(200);
            }

            await client.CloseAsync(WebSocketCloseStatus.NormalClosure, "Test complete", cts.Token);
        }

        [Fact]
        public async Task GremlinServer_Should_Handle_HTTP_Protocol()
        {
            // Arrange
            await StartServerAsync(enableTcp: false, enableWebSocket: false, enableHttp: true);

            using var httpClient = new HttpClient();
            _disposables.Add(httpClient);

            var baseUrl = $"http://localhost:{_server.Options.GetHttpPort()}";

            // Test GET request (health check)
            var getResponse = await httpClient.GetAsync(baseUrl);
            getResponse.IsSuccessStatusCode.Should().BeTrue();
            
            var getContent = await getResponse.Content.ReadAsStringAsync();
            var getStats = JsonConvert.DeserializeObject<JObject>(getContent);
            getStats["isRunning"].Value<bool>().Should().BeTrue();

            // Test POST request (query execution)
            var postData = JsonConvert.SerializeObject(new
            {
                requestId = Guid.NewGuid().ToString(),
                op = "eval",
                args = new
                {
                    gremlin = "g.inject(1, 2, 3)",
                    bindings = new { }
                }
            });

            var postContent = new StringContent(postData, Encoding.UTF8, "application/json");
            var postResponse = await httpClient.PostAsync(baseUrl, postContent);
            
            postResponse.IsSuccessStatusCode.Should().BeTrue();
            var postResponseContent = await postResponse.Content.ReadAsStringAsync();
            
            var result = JsonConvert.DeserializeObject<JObject>(postResponseContent);
            result["status"]["code"].Value<int>().Should().Be(200);
            result["result"]["data"].Should().NotBeNull();

            // Test CORS headers
            postResponse.Headers.Should().ContainKey("Access-Control-Allow-Origin");
            postResponse.Headers.GetValues("Access-Control-Allow-Origin").First().Should().Be("*");
        }

        [Theory]
        [InlineData(GraphSONVersion.V1)]
        [InlineData(GraphSONVersion.V2)]
        [InlineData(GraphSONVersion.V3)]
        public void GraphSONSerializer_Should_Handle_All_Versions_Correctly(GraphSONVersion version)
        {
            // Arrange
            var serializer = new TinkerPopGraphSONSerializer(version);
            var testData = new object[]
            {
                42,
                "test string",
                true,
                DateTime.UtcNow,
                Guid.NewGuid()
            };

            foreach (var data in testData)
            {
                // Test message serialization/deserialization
                var message = new TinkerPopMessage
                {
                    RequestId = Guid.NewGuid(),
                    Op = TinkerPopOperations.Eval,
                    Args = new Dictionary<string, object>
                    {
                        ["gremlin"] = "g.inject(x)",
                        ["bindings"] = new Dictionary<string, object> { ["x"] = data }
                    }
                };

                // Act
                var serialized = serializer.SerializeMessage(message);
                var deserialized = serializer.DeserializeMessage(serialized);

                // Assert
                serialized.Should().NotBeEmpty();
                deserialized.Should().NotBeNull();
                deserialized.RequestId.Should().Be(message.RequestId);
                deserialized.Op.Should().Be(message.Op);

                // Test response serialization
                var response = serializer.CreateSuccessResponse(message.RequestId, new object[] { data }.Cast<dynamic>(), null);
                var responseJson = serializer.SerializeResponse(response);

                responseJson.Should().NotBeEmpty();
                responseJson.Should().Contain(message.RequestId.ToString());

                // Verify version-specific formatting
                var responseObj = JsonConvert.DeserializeObject<JObject>(responseJson);
                responseObj["status"]["code"].Value<int>().Should().Be(200);

                if (version != GraphSONVersion.V1)
                {
                    // V2 and V3 should have type information
                    responseObj["requestId"]["@type"]?.Value<string>().Should().Be("g:UUID");
                }
            }

            // Test MIME type
            var mimeType = serializer.GetMimeType();
            mimeType.Should().NotBeEmpty();
            mimeType.Should().Contain("gremlin");
        }

        [Fact]
        public async Task GremlinServer_Should_Handle_Multiple_Concurrent_Connections()
        {
            // Arrange
            await StartServerAsync(enableTcp: true, enableWebSocket: true, enableHttp: true);

            var tasks = new List<Task>();
            const int connectionCount = 5;

            // Test concurrent TCP connections
            for (int i = 0; i < connectionCount; i++)
            {
                var connectionId = i;
                tasks.Add(Task.Run(async () =>
                {
                    using var tcpClient = new TcpClient();
                    await tcpClient.ConnectAsync("localhost", _server.Options.Port);
                    
                    var stream = tcpClient.GetStream();
                    var query = $"g.inject({connectionId})";
                    var queryBytes = Encoding.UTF8.GetBytes(query);
                    await stream.WriteAsync(queryBytes, 0, queryBytes.Length);

                    var buffer = new byte[4096];
                    var bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                    var response = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    
                    response.Should().NotBeEmpty();
                }));
            }

            // Test concurrent WebSocket connections
            for (int i = 0; i < connectionCount; i++)
            {
                var connectionId = i;
                tasks.Add(Task.Run(async () =>
                {
                    using var client = new ClientWebSocket();
                    client.Options.AddSubProtocol("gremlin-ws");
                    
                    var uri = new Uri($"ws://localhost:{_server.Options.GetHttpPort()}/gremlin");
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                    
                    await client.ConnectAsync(uri, cts.Token);
                    
                    var message = JsonConvert.SerializeObject(new
                    {
                        requestId = Guid.NewGuid().ToString(),
                        op = "eval",
                        args = new
                        {
                            gremlin = $"g.inject({connectionId})",
                            bindings = new { }
                        }
                    });

                    var messageBytes = Encoding.UTF8.GetBytes(message);
                    await client.SendAsync(new ArraySegment<byte>(messageBytes), WebSocketMessageType.Text, true, cts.Token);

                    var buffer = new byte[4096];
                    var result = await client.ReceiveAsync(new ArraySegment<byte>(buffer), cts.Token);
                    
                    result.Count.Should().BeGreaterThan(0);
                    
                    await client.CloseAsync(WebSocketCloseStatus.NormalClosure, "Test complete", cts.Token);
                }));
            }

            // Act & Assert
            await Task.WhenAll(tasks);

            // Verify server statistics
            var stats = _server.GetStatistics();
            stats["isRunning"].Should().Be(true);
        }

        [Fact]
        public async Task GremlinServer_Should_Handle_Authentication_When_Enabled()
        {
            // Arrange
            var options = new GremlinServerOptions
            {
                Host = "localhost",
                Port = GetRandomPort(),
                HttpPort = GetRandomPort(),
                EnableTcp = false,
                EnableWebSocket = true,
                EnableHttp = false,
                EnableLogging = false,
                Authentication = new GremlinAuthenticationOptions
                {
                    EnableBasicAuth = true,
                    Username = "testuser",
                    Password = "testpass"
                }
            };

            _server = new GremlinServer(options);
            await _server.StartAsync();

            using var client = new ClientWebSocket();
            client.Options.AddSubProtocol("gremlin-ws");
            _disposables.Add(client);

            var uri = new Uri($"ws://localhost:{_server.Options.GetHttpPort()}/gremlin");
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            
            await client.ConnectAsync(uri, cts.Token);

            // Test unauthenticated request
            var message = new TinkerPopMessage
            {
                RequestId = Guid.NewGuid(),
                Op = TinkerPopOperations.Eval,
                Args = new Dictionary<string, object>
                {
                    ["gremlin"] = "g.inject(42)"
                }
            };

            var serializer = new TinkerPopGraphSONSerializer();
            var messageJson = serializer.SerializeMessage(message);
            var messageBytes = Encoding.UTF8.GetBytes(messageJson);

            await client.SendAsync(new ArraySegment<byte>(messageBytes), WebSocketMessageType.Text, true, cts.Token);

            var buffer = new byte[4096];
            var result = await client.ReceiveAsync(new ArraySegment<byte>(buffer), cts.Token);
            
            var responseJson = Encoding.UTF8.GetString(buffer, 0, result.Count);
            var response = JsonConvert.DeserializeObject<JObject>(responseJson);
            
            // Should receive authentication challenge
            response["status"]["code"].Value<int>().Should().Be(407); // Authenticate

            await client.CloseAsync(WebSocketCloseStatus.NormalClosure, "Test complete", cts.Token);
        }

        [Fact]
        public async Task GremlinServer_Should_Handle_Error_Scenarios_Gracefully()
        {
            // Arrange
            await StartServerAsync(enableTcp: true, enableWebSocket: true, enableHttp: true);

            // Test invalid Gremlin query via TCP
            using var tcpClient = new TcpClient();
            await tcpClient.ConnectAsync("localhost", _server.Options.Port);
            
            var stream = tcpClient.GetStream();
            var invalidQuery = "invalid.gremlin.query()";
            var queryBytes = Encoding.UTF8.GetBytes(invalidQuery);
            await stream.WriteAsync(queryBytes, 0, queryBytes.Length);

            var buffer = new byte[4096];
            var bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
            var response = Encoding.UTF8.GetString(buffer, 0, bytesRead);
            
            response.Should().NotBeEmpty();
            // Should contain error information but not crash the server
            _server.IsRunning.Should().BeTrue();

            // Test malformed JSON via WebSocket
            using var client = new ClientWebSocket();
            client.Options.AddSubProtocol("gremlin-ws");
            var uri = new Uri($"ws://localhost:{_server.Options.GetHttpPort()}/gremlin");
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            
            await client.ConnectAsync(uri, cts.Token);

            var malformedJson = "{ invalid json }";
            var malformedBytes = Encoding.UTF8.GetBytes(malformedJson);
            await client.SendAsync(new ArraySegment<byte>(malformedBytes), WebSocketMessageType.Text, true, cts.Token);

            var wsBuffer = new byte[4096];
            var wsResult = await client.ReceiveAsync(new ArraySegment<byte>(wsBuffer), cts.Token);
            
            wsResult.Count.Should().BeGreaterThan(0);
            var wsResponse = Encoding.UTF8.GetString(wsBuffer, 0, wsResult.Count);
            var wsResponseObj = JsonConvert.DeserializeObject<JObject>(wsResponse);
            
            // Should receive error response
            wsResponseObj["status"]["code"].Value<int>().Should().Be(498); // MalformedRequest

            await client.CloseAsync(WebSocketCloseStatus.NormalClosure, "Test complete", cts.Token);
        }

        [Fact]
        public async Task GremlinServer_Should_Support_Complex_Graph_Operations()
        {
            // Arrange
            await StartServerAsync(enableTcp: false, enableWebSocket: false, enableHttp: false);

            // Act & Assert - Test graph creation and traversals
            await _server.Connector.ExecuteAsync("g.addV('person').property('name', 'Alice').property('age', 30)", new Dictionary<string, object>());
            await _server.Connector.ExecuteAsync("g.addV('person').property('name', 'Bob').property('age', 25)", new Dictionary<string, object>());
            await _server.Connector.ExecuteAsync("g.V().has('name', 'Alice').addE('knows').to(g.V().has('name', 'Bob'))", new Dictionary<string, object>());

            var vertexCount = await _server.Connector.ExecuteAsync("g.V().count()", new Dictionary<string, object>());
            vertexCount.First().Should().Be(2L);

            var edgeCount = await _server.Connector.ExecuteAsync("g.E().count()", new Dictionary<string, object>());
            edgeCount.First().Should().Be(1L);

            var aliceFriends = await _server.Connector.ExecuteAsync("g.V().has('name', 'Alice').out('knows').values('name')", new Dictionary<string, object>());
            aliceFriends.First().Should().Be("Bob");

            // Test with parameters
            var parameterizedResult = await _server.Connector.ExecuteAsync(
                "g.V().has('name', username).values('age')", 
                new Dictionary<string, object> { ["username"] = "Alice" });
            parameterizedResult.First().Should().Be(30);
        }

        private async Task StartServerAsync(bool enableTcp = true, bool enableWebSocket = true, bool enableHttp = true)
        {
            var options = new GremlinServerOptions
            {
                Host = "localhost",
                Port = GetRandomPort(),
                HttpPort = GetRandomPort(),
                EnableTcp = enableTcp,
                EnableWebSocket = enableWebSocket,
                EnableHttp = enableHttp,
                EnableLogging = false,
                EnableDebugLogging = false,
                MaxConnections = 50
            };

            _server = new GremlinServer(options);
            await _server.StartAsync();
            
            // Give the server a moment to fully initialize
            await Task.Delay(500);
        }

        private static int GetRandomPort()
        {
            // Get a random port between 8000-9000 to avoid conflicts
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

            if (_server != null)
            {
                try
                {
                    _server.StopAsync().Wait(5000);
                    _server.Dispose();
                }
                catch
                {
                    // Ignore disposal errors
                }
                _server = null;
            }
        }
    }
}