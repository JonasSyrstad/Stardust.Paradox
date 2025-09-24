using System;
using System.Threading.Tasks;
using System.Linq;
using Gremlin.Net.Driver;
using Stardust.Paradox.Data.InMemory;
using Stardust.Paradox.Data.InMemory.Management;
using Stardust.Paradox.Data.InMemory.Server;

namespace GremlinNetComprehensiveTestApp
{
    public static class SimpleGremlinNetConnectionTest
    {
        public static async Task<TestResult> RunTestAsync()
        {
            Console.WriteLine("?? Running Simplified Gremlin.Net Connection Test");
            Console.WriteLine("=" + new string('=', 50));

            InMemoryGremlinServer? server = null;
            GremlinClient? client = null;

            try
            {
                // Start the in-memory server
                Console.WriteLine("?? Starting InMemory Gremlin server...");
                server = await GremlinDatabase.StartAsync(options =>
                {
                    options.Host = "localhost";
                    options.Port = 8183;
                    options.EnableWebSocket = true;
                    options.EnableLogging = false;
                });

                Console.WriteLine($"? Server started on port {server.Options.GetHttpPort()}");

                // Test both direct connector and Gremlin.Net client
                var directConnector = server.Connector;
                if (directConnector == null)
                {
                    return new TestResult
                    {
                        Success = false,
                        Message = "Server connector is null",
                        Details = "InMemory server did not provide a valid connector"
                    };
                }

                // Test 1: Direct connector
                Console.WriteLine("\n?? Testing direct connector...");
                var directResult = await directConnector.ExecuteAsync("g.inject(1, 2, 3)", new System.Collections.Generic.Dictionary<string, object>());
                var directCount = directResult?.ToList().Count ?? 0;
                Console.WriteLine($"   Direct result count: {directCount}");

                if (directCount != 3)
                {
                    return new TestResult
                    {
                        Success = false,
                        Message = "Direct connector test failed",
                        Details = $"Expected 3 results, got {directCount}"
                    };
                }

                // Test 2: Gremlin.Net client (if WebSocket is supported)
                if (server.WebSocketSupported)
                {
                    Console.WriteLine("\n?? Testing Gremlin.Net client...");
                    
                    try
                    {
                        var gremlinServer = new GremlinServer("localhost", server.Options.GetHttpPort(), false);
                        client = new GremlinClient(gremlinServer);

                        var gremlinResult = await client.SubmitAsync<dynamic>("g.inject(42)");
                        var gremlinValue = gremlinResult.ToList().FirstOrDefault();
                        Console.WriteLine($"   Gremlin.Net result: {gremlinValue}");

                        if (gremlinValue?.ToString() != "42")
                        {
                            return new TestResult
                            {
                                Success = false,
                                Message = "Gremlin.Net client test failed",
                                Details = $"Expected '42', got '{gremlinValue}'"
                            };
                        }
                    }
                    catch (Exception ex)
                    {
                        return new TestResult
                        {
                            Success = false,
                            Message = "Gremlin.Net client failed",
                            Details = ex.Message
                        };
                    }
                }
                else
                {
                    Console.WriteLine("\n?? Skipping Gremlin.Net test (WebSocket not supported on this platform)");
                }

                return new TestResult
                {
                    Success = true,
                    Message = "All tests passed",
                    Details = $"Direct connector: {directCount} results, Gremlin.Net: {(server.WebSocketSupported ? "Working" : "Skipped")}"
                };
            }
            catch (Exception ex)
            {
                return new TestResult
                {
                    Success = false,
                    Message = "Test failed with exception",
                    Details = ex.Message
                };
            }
            finally
            {
                try
                {
                    client?.Dispose();
                    if (server != null)
                    {
                        await GremlinDatabase.StopAsync(server);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"?? Cleanup warning: {ex.Message}");
                }
            }
        }
    }
}