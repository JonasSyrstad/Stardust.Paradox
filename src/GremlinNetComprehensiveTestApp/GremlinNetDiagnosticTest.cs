using System;
using System.Linq;
using System.Threading.Tasks;
using Stardust.Paradox.Data.InMemory;
using Gremlin.Net.Driver;

// Aliases to resolve naming conflicts
using StardustGremlinServer = Stardust.Paradox.Data.InMemory.GremlinServer;
using GremlinNetServer = Gremlin.Net.Driver.GremlinServer;

namespace GremlinNetComprehensiveTestApp
{
    /// <summary>
    /// Diagnostic test to analyze WebSocket handshake issues
    /// </summary>
    class GremlinNetDiagnosticTest
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("?? GREMLIN.NET WEBSOCKET DIAGNOSTIC TEST");
            Console.WriteLine("=" + new string('=', 50));
            
            var port = GetRandomPort();
            
            // Start server with detailed logging
            var options = new GremlinServerOptions
            {
                Host = "localhost",
                Port = port,
                HttpPort = port + 1,
                EnableTcp = true,
                EnableWebSocket = true,
                EnableHttp = true,
                EnableLogging = true,
                EnableDebugLogging = true
            };

            var server = new StardustGremlinServer(options);
            
            try
            {
                Console.WriteLine($"Starting server on TCP:{port}, HTTP/WebSocket:{port + 1}...");
                await server.StartAsync();
                
                if (!server.IsRunning)
                {
                    Console.WriteLine("? Server failed to start");
                    return;
                }
                
                Console.WriteLine("? Server started successfully");
                Console.WriteLine($"WebSocket supported: {server.WebSocketSupported}");
                
                // Show server configuration
                var stats = server.GetStatistics();
                Console.WriteLine("\n?? Server Configuration:");
                if (stats.ContainsKey("serverOptions"))
                {
                    var serverOptions = stats["serverOptions"];
                    Console.WriteLine($"  {serverOptions}");
                }
                
                // Wait a bit for server to be fully ready
                await Task.Delay(2000);
                
                Console.WriteLine("\n?? Testing Gremlin.Net WebSocket Connection...");
                
                // Test different Gremlin.Net connection approaches
                await TestGremlinNetConnection($"localhost", port + 1);
                await TestGremlinNetConnection($"127.0.0.1", port + 1);
                
                // Test direct WebSocket connection to understand the issue
                await TestDirectWebSocketConnection("localhost", port + 1);
                
                Console.WriteLine("\nPress any key to continue...");
                Console.ReadKey();
                
            }
            catch (Exception ex)
            {
                Console.WriteLine($"? Test failed: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Inner: {ex.InnerException.Message}");
                }
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }
            finally
            {
                try
                {
                    await server.StopAsync();
                    server.Dispose();
                    Console.WriteLine("? Server stopped");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"?? Error stopping server: {ex.Message}");
                }
            }
        }

        static async Task TestGremlinNetConnection(string host, int port)
        {
            Console.WriteLine($"\n?? Testing Gremlin.Net connection to {host}:{port}");
            
            try
            {
                var gremlinServer = new GremlinNetServer(host, port);
                using var client = new GremlinClient(gremlinServer);
                
                Console.WriteLine("  Client created successfully");
                
                // Try a simple query with timeout
                using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(10));
                
                var task = client.SubmitAsync<dynamic>("g.inject(42)");
                var result = await task.ConfigureAwait(false);
                
                if (result?.Count > 0)
                {
                    Console.WriteLine($"  ? Query successful: {result.FirstOrDefault()}");
                }
                else
                {
                    Console.WriteLine("  ? Query returned no results");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ? Connection failed: {ex.GetType().Name}: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"     Inner: {ex.InnerException.GetType().Name}: {ex.InnerException.Message}");
                }
            }
        }

        static async Task TestDirectWebSocketConnection(string host, int port)
        {
            Console.WriteLine($"\n?? Testing direct WebSocket connection to {host}:{port}");
            
            try
            {
                using var webSocket = new System.Net.WebSockets.ClientWebSocket();
                
                // Test different WebSocket URLs that Gremlin.Net might use
                var testUrls = new[]
                {
                    $"ws://{host}:{port}/gremlin",
                    $"ws://{host}:{port}/",
                    $"ws://{host}:{port}/ws",
                    $"ws://{host}:{port}/websocket"
                };
                
                foreach (var url in testUrls)
                {
                    try
                    {
                        Console.WriteLine($"  Testing URL: {url}");
                        
                        using var freshWebSocket = new System.Net.WebSockets.ClientWebSocket();
                        freshWebSocket.Options.AddSubProtocol("gremlin-ws");
                        
                        using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(5));
                        await freshWebSocket.ConnectAsync(new Uri(url), cts.Token);
                        
                        Console.WriteLine($"    ? Connected successfully to {url}");
                        Console.WriteLine($"    State: {freshWebSocket.State}");
                        Console.WriteLine($"    Sub-protocol: {freshWebSocket.SubProtocol ?? "none"}");
                        
                        await freshWebSocket.CloseAsync(
                            System.Net.WebSockets.WebSocketCloseStatus.NormalClosure, 
                            "Test complete", 
                            cts.Token);
                        
                        break; // Success, no need to test other URLs
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"    ? Failed: {ex.GetType().Name}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ? Direct WebSocket test failed: {ex.GetType().Name}: {ex.Message}");
            }
        }

        private static int GetRandomPort()
        {
            var random = new Random();
            return random.Next(8000, 9000);
        }
    }
}