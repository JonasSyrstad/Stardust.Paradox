#if NET8_0_OR_GREATER || NETCOREAPP3_1_OR_GREATER
using System;
using Stardust.Paradox.Data.InMemory.Management;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Stardust.Paradox.Data.InMemory.Server
{
    /// <summary>
    /// Simple TCP client for connecting to the Gremlin server
    /// Demonstrates how to send queries and receive responses
    /// </summary>
    public class GremlinTcpClient : IDisposable
    {
        private readonly TcpClient _tcpClient;
        private readonly NetworkStream _stream;
        private readonly string _host;
        private readonly int _port;
        private bool _disposed;

        public GremlinTcpClient(string host = "localhost", int port = 8182)
        {
            _host = host;
            _port = port;
            _tcpClient = new TcpClient();
        }

        /// <summary>
        /// Connect to the Gremlin server
        /// </summary>
        public async Task ConnectAsync()
        {
            await _tcpClient.ConnectAsync(_host, _port);
        }

        /// <summary>
        /// Check if connected to server
        /// </summary>
        public bool IsConnected => _tcpClient?.Connected == true;

        /// <summary>
        /// Send a simple text query and receive response
        /// </summary>
        /// <param name="query">Gremlin query string</param>
        /// <returns>Server response as JSON string</returns>
        public async Task<string> ExecuteQueryAsync(string query)
        {
            if (!IsConnected)
                throw new InvalidOperationException("Not connected to server");

            var stream = _tcpClient.GetStream();
            
            // Send query
            var queryBytes = Encoding.UTF8.GetBytes(query);
            await stream.WriteAsync(queryBytes, 0, queryBytes.Length);
            await stream.FlushAsync();

            // Read response
            var buffer = new byte[4096];
            var bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
            
            if (bytesRead == 0)
                throw new InvalidOperationException("Server closed connection");

            return Encoding.UTF8.GetString(buffer, 0, bytesRead).TrimEnd('\n');
        }

        /// <summary>
        /// Send a JSON-formatted Gremlin request
        /// </summary>
        /// <param name="gremlinQuery">Gremlin query string</param>
        /// <param name="bindings">Query parameters</param>
        /// <returns>Parsed response object</returns>
        public async Task<dynamic> ExecuteJsonQueryAsync(string gremlinQuery, object bindings = null)
        {
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
            var response = await ExecuteQueryAsync(requestJson);
            
            return JsonConvert.DeserializeObject(response);
        }

        /// <summary>
        /// Disconnect from server
        /// </summary>
        public void Disconnect()
        {
            _tcpClient?.Close();
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                Disconnect();
                _tcpClient?.Dispose();
            }
        }
    }

    /// <summary>
    /// Example usage of the Gremlin TCP client
    /// </summary>
    public static class GremlinClientExample
    {
        public static async Task RunClientExampleAsync()
        {
            Console.WriteLine("=== Gremlin TCP Client Example ===\n");

            // First, start a server for testing
            Console.WriteLine("Starting test server...");
            var server = await GremlinDatabase.StartAsync(options =>
            {
                options.Host = "localhost";
                options.Port = 8184; // Use different port for client example
                options.EnableLogging = true;
            });

            try
            {
                // Create and connect client
                using (var client = new GremlinTcpClient("localhost", 8184))
                {
                    Console.WriteLine("Connecting to server...");
                    await client.ConnectAsync();
                    Console.WriteLine("? Connected to Gremlin server");

                    // Example 1: Simple text query
                    Console.WriteLine("\n1. Simple text query:");
                    var response1 = await client.ExecuteQueryAsync("g.addV('person').property('name', 'Alice')");
                    Console.WriteLine($"   Response: {response1}");

                    // Example 2: JSON query with parameters
                    Console.WriteLine("\n2. JSON query with parameters:");
                    var response2 = await client.ExecuteJsonQueryAsync(
                        "g.addV('person').property('name', name).property('age', age)",
                        new { name = "Bob", age = 30 });
                    Console.WriteLine($"   Response: {JsonConvert.SerializeObject(response2, Formatting.Indented)}");

                    // Example 3: Query for data
                    Console.WriteLine("\n3. Query for data:");
                    var response3 = await client.ExecuteQueryAsync("g.V().hasLabel('person').values('name')");
                    Console.WriteLine($"   Names: {response3}");

                    // Example 4: Complex traversal
                    Console.WriteLine("\n4. Complex traversal:");
                    await client.ExecuteQueryAsync("g.addE('knows').from(g.V().has('name', 'Alice')).to(g.V().has('name', 'Bob'))");
                    var response4 = await client.ExecuteQueryAsync("g.V().has('name', 'Alice').out('knows').values('name')");
                    Console.WriteLine($"   Alice knows: {response4}");

                    // Example 5: Statistics query
                    Console.WriteLine("\n5. Statistics:");
                    var response5 = await client.ExecuteJsonQueryAsync("g.V().count()");
                    Console.WriteLine($"   Vertex count: {JsonConvert.SerializeObject(response5, Formatting.Indented)}");

                    Console.WriteLine("\n? Client example completed successfully!");
                }
            }
            finally
            {
                Console.WriteLine("\nStopping test server...");
                await GremlinDatabase.StopAsync(server);
                Console.WriteLine("? Server stopped");
            }
        }
    }
}

#endif