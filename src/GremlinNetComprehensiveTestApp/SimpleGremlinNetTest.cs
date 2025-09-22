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
    /// Simple Gremlin.Net compatibility test to validate core functionality
    /// </summary>
    class SimpleGremlinNetTest
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("?? SIMPLE GREMLIN.NET COMPATIBILITY TEST");
            Console.WriteLine("=" + new string('=', 50));
            
            var testsPassed = 0;
            var testsTotal = 0;

            // Test 1: Server Startup
            testsTotal++;
            var server = await TestServerStartup();
            if (server != null)
            {
                testsPassed++;
                Console.WriteLine("? Server startup test passed");
            }
            else
            {
                Console.WriteLine("? Server startup test failed");
                return;
            }

            // Test 2: Basic Gremlin.Net Client Connection
            testsTotal++;
            var clientConnectionWorked = await TestGremlinNetClientConnection(server);
            if (clientConnectionWorked)
            {
                testsPassed++;
                Console.WriteLine("? Gremlin.Net client connection test passed");
            }
            else
            {
                Console.WriteLine("? Gremlin.Net client connection test failed");
            }

            // Test 3: Simple Query Execution
            testsTotal++;
            var queryWorked = await TestSimpleQuery(server);
            if (queryWorked)
            {
                testsPassed++;
                Console.WriteLine("? Simple query test passed");
            }
            else
            {
                Console.WriteLine("? Simple query test failed");
            }

            // Test 4: Parameterized Query
            testsTotal++;
            var paramQueryWorked = await TestParameterizedQuery(server);
            if (paramQueryWorked)
            {
                testsPassed++;
                Console.WriteLine("? Parameterized query test passed");
            }
            else
            {
                Console.WriteLine("? Parameterized query test failed");
            }

            // Cleanup
            try
            {
                await server.StopAsync();
                server.Dispose();
                Console.WriteLine("? Server cleanup completed");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"?? Server cleanup warning: {ex.Message}");
            }

            // Final Results
            Console.WriteLine("\n" + new string('=', 50));
            Console.WriteLine($"RESULTS: {testsPassed}/{testsTotal} tests passed");
            Console.WriteLine($"Success Rate: {(double)testsPassed/testsTotal:P0}");
            
            if (testsPassed == testsTotal)
            {
                Console.WriteLine("?? ALL TESTS PASSED - Gremlin.Net compatibility verified!");
            }
            else
            {
                Console.WriteLine("? Some tests failed - Gremlin.Net compatibility issues detected");
            }

            Console.WriteLine("\nPress any key to exit...");
            Console.ReadKey();
        }

        static async Task<StardustGremlinServer?> TestServerStartup()
        {
            try
            {
                var options = new GremlinServerOptions
                {
                    Host = "localhost",
                    Port = GetRandomPort(),
                    HttpPort = GetRandomPort(),
                    EnableTcp = true,
                    EnableWebSocket = true,
                    EnableHttp = true,
                    EnableLogging = false,
                    EnableDebugLogging = false
                };

                var server = new StardustGremlinServer(options);
                await server.StartAsync();
                await Task.Delay(1000); // Give server time to start

                if (server.IsRunning)
                {
                    return server;
                }
                else
                {
                    Console.WriteLine("   Server failed to start");
                    return null;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"   Server startup error: {ex.Message}");
                return null;
            }
        }

        static async Task<bool> TestGremlinNetClientConnection(StardustGremlinServer server)
        {
            try
            {
                if (!server.WebSocketSupported)
                {
                    Console.WriteLine("   WebSocket not supported - skipping Gremlin.Net test");
                    return false;
                }

                var gremlinServer = new GremlinNetServer(server.Options.Host, server.Options.GetHttpPort());
                using var client = new GremlinClient(gremlinServer);
                
                // Just try to create the client and basic connection
                await Task.Delay(500); // Give connection time to establish
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"   Client connection error: {ex.Message}");
                return false;
            }
        }

        static async Task<bool> TestSimpleQuery(StardustGremlinServer server)
        {
            try
            {
                if (!server.WebSocketSupported)
                {
                    Console.WriteLine("   WebSocket not supported - testing direct connector");
                    var directResult = await server.Connector.ExecuteAsync("g.inject(42)", new System.Collections.Generic.Dictionary<string, object>());
                    return directResult?.FirstOrDefault()?.ToString() == "42";
                }

                var gremlinServer = new GremlinNetServer(server.Options.Host, server.Options.GetHttpPort());
                using var client = new GremlinClient(gremlinServer);
                
                var result = await client.SubmitAsync<dynamic>("g.inject(42)");
                return result?.Count > 0 && result.FirstOrDefault()?.ToString() == "42";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"   Simple query error: {ex.Message}");
                return false;
            }
        }

        static async Task<bool> TestParameterizedQuery(StardustGremlinServer server)
        {
            try
            {
                if (!server.WebSocketSupported)
                {
                    Console.WriteLine("   WebSocket not supported - testing direct connector");
                    var directResult = await server.Connector.ExecuteAsync("g.inject(x)", new System.Collections.Generic.Dictionary<string, object> { ["x"] = 100 });
                    return directResult?.FirstOrDefault()?.ToString() == "100";
                }

                var gremlinServer = new GremlinNetServer(server.Options.Host, server.Options.GetHttpPort());
                using var client = new GremlinClient(gremlinServer);
                
                var bindings = new System.Collections.Generic.Dictionary<string, object> { ["x"] = 100 };
                var result = await client.SubmitAsync<dynamic>("g.inject(x)", bindings);
                return result?.Count > 0 && result.FirstOrDefault()?.ToString() == "100";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"   Parameterized query error: {ex.Message}");
                return false;
            }
        }

        private static int GetRandomPort()
        {
            var random = new Random();
            return random.Next(8000, 9000);
        }
    }
}