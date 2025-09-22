using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

#if NETCOREAPP3_1_OR_GREATER || NET6_0_OR_GREATER

namespace Stardust.Paradox.Data.InMemory
{
    /// <summary>
    /// WebSocket client for connecting to the Gremlin server
    /// Available only on .NET Core 3.1+ and .NET 6+
    /// </summary>
    public class GremlinWebSocketClient : IDisposable
    {
        private readonly ClientWebSocket _webSocket;
        private readonly string _uri;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private bool _disposed;

        public GremlinWebSocketClient(string host = "localhost", int port = 8183)
        {
            _uri = $"ws://{host}:{port}";
            _webSocket = new ClientWebSocket();
            _webSocket.Options.AddSubProtocol("gremlin-ws");
            _cancellationTokenSource = new CancellationTokenSource();
        }

        /// <summary>
        /// Connect to the Gremlin WebSocket server
        /// </summary>
        public async Task ConnectAsync()
        {
            await _webSocket.ConnectAsync(new Uri(_uri), _cancellationTokenSource.Token);
        }

        /// <summary>
        /// Check if connected to server
        /// </summary>
        public bool IsConnected => _webSocket?.State == WebSocketState.Open;

        /// <summary>
        /// Send a Gremlin query and receive response
        /// </summary>
        /// <param name="gremlinQuery">Gremlin query string</param>
        /// <param name="bindings">Query parameters</param>
        /// <returns>Parsed response object</returns>
        public async Task<dynamic> ExecuteQueryAsync(string gremlinQuery, object bindings = null)
        {
            if (!IsConnected)
                throw new InvalidOperationException("Not connected to server");

            var request = new
            {
                requestId = Guid.NewGuid().ToString(),
                op = "eval",
                args = new
                {
                    gremlin = gremlinQuery,
                    bindings = bindings ?? new { }
                }
            };

            var requestJson = JsonConvert.SerializeObject(request);
            var requestBytes = Encoding.UTF8.GetBytes(requestJson);

            // Send request
            await _webSocket.SendAsync(
                new ArraySegment<byte>(requestBytes),
                WebSocketMessageType.Text,
                true,
                _cancellationTokenSource.Token);

            // Receive response
            var buffer = new byte[4096];
            var result = await _webSocket.ReceiveAsync(
                new ArraySegment<byte>(buffer),
                _cancellationTokenSource.Token);

            if (result.MessageType == WebSocketMessageType.Text)
            {
                var responseJson = Encoding.UTF8.GetString(buffer, 0, result.Count);
                return JsonConvert.DeserializeObject(responseJson);
            }

            throw new InvalidOperationException("Unexpected response type");
        }

        /// <summary>
        /// Send a simple text query
        /// </summary>
        /// <param name="query">Gremlin query string</param>
        /// <returns>Server response as dynamic object</returns>
        public async Task<dynamic> ExecuteSimpleQueryAsync(string query)
        {
            if (!IsConnected)
                throw new InvalidOperationException("Not connected to server");

            var queryBytes = Encoding.UTF8.GetBytes(query);

            // Send query
            await _webSocket.SendAsync(
                new ArraySegment<byte>(queryBytes),
                WebSocketMessageType.Text,
                true,
                _cancellationTokenSource.Token);

            // Receive response
            var buffer = new byte[4096];
            var result = await _webSocket.ReceiveAsync(
                new ArraySegment<byte>(buffer),
                _cancellationTokenSource.Token);

            if (result.MessageType == WebSocketMessageType.Text)
            {
                var responseJson = Encoding.UTF8.GetString(buffer, 0, result.Count);
                return JsonConvert.DeserializeObject(responseJson);
            }

            throw new InvalidOperationException("Unexpected response type");
        }

        /// <summary>
        /// Close the WebSocket connection
        /// </summary>
        public async Task CloseAsync()
        {
            if (_webSocket.State == WebSocketState.Open)
            {
                await _webSocket.CloseAsync(
                    WebSocketCloseStatus.NormalClosure,
                    "Client closing",
                    _cancellationTokenSource.Token);
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;

                try
                {
                    CloseAsync().Wait(5000);
                }
                catch
                {
                    // Ignore errors during disposal
                }

                _cancellationTokenSource?.Cancel();
                _webSocket?.Dispose();
                _cancellationTokenSource?.Dispose();
            }
        }
    }

    /// <summary>
    /// Example usage of the WebSocket client
    /// </summary>
    public static class GremlinWebSocketClientExample
    {
        public static async Task RunWebSocketClientExampleAsync()
        {
            Console.WriteLine("=== Gremlin WebSocket Client Example ===\n");

            // Start a WebSocket-enabled server for testing
            Console.WriteLine("Starting WebSocket server...");
            var server = await GremlinDatabase.StartWebSocketDevServerAsync();

            try
            {
                // Create and connect WebSocket client
                using (var client = new GremlinWebSocketClient("localhost", 8183))
                {
                    Console.WriteLine("Connecting to WebSocket server...");
                    await client.ConnectAsync();
                    Console.WriteLine("? Connected to WebSocket server");

                    // Example 1: Simple text query
                    Console.WriteLine("\n1. Simple WebSocket query:");
                    var response1 = await client.ExecuteSimpleQueryAsync("g.addV('person').property('name', 'Alice')");
                    Console.WriteLine($"   Response: {JsonConvert.SerializeObject(response1, Formatting.Indented)}");

                    // Example 2: JSON query with parameters
                    Console.WriteLine("\n2. WebSocket JSON query with parameters:");
                    var response2 = await client.ExecuteQueryAsync(
                        "g.addV('person').property('name', name).property('age', age)",
                        new { name = "Bob", age = 30 });
                    Console.WriteLine($"   Response: {JsonConvert.SerializeObject(response2, Formatting.Indented)}");

                    // Example 3: Query for data
                    Console.WriteLine("\n3. Query for data via WebSocket:");
                    var response3 = await client.ExecuteSimpleQueryAsync("g.V().hasLabel('person').values('name')");
                    Console.WriteLine($"   Names: {JsonConvert.SerializeObject(response3, Formatting.Indented)}");

                    // Example 4: Complex traversal
                    Console.WriteLine("\n4. Complex traversal via WebSocket:");
                    await client.ExecuteSimpleQueryAsync("g.addE('knows').from(g.V().has('name', 'Alice')).to(g.V().has('name', 'Bob'))");
                    var response4 = await client.ExecuteSimpleQueryAsync("g.V().has('name', 'Alice').out('knows').values('name')");
                    Console.WriteLine($"   Alice knows: {JsonConvert.SerializeObject(response4, Formatting.Indented)}");

                    // Example 5: Statistics query
                    Console.WriteLine("\n5. Statistics via WebSocket:");
                    var response5 = await client.ExecuteQueryAsync("g.V().count()");
                    Console.WriteLine($"   Vertex count: {JsonConvert.SerializeObject(response5, Formatting.Indented)}");

                    Console.WriteLine("\n? WebSocket client example completed successfully!");

                    await client.CloseAsync();
                    Console.WriteLine("? WebSocket connection closed");
                }
            }
            finally
            {
                Console.WriteLine("\nStopping WebSocket server...");
                await GremlinDatabase.StopAsync(server);
                Console.WriteLine("? Server stopped");
            }
        }
    }
}

#endif