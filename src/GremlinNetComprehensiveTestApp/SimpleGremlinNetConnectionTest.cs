using System;
using System.Threading.Tasks;
using Stardust.Paradox.Data.InMemory;
using System.Collections.Generic;
using System.Linq;

namespace GremlinNetComprehensiveTestApp
{
    class SimpleGremlinNetConnectionTest
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("?? Simple Gremlin.Net Connection Test");
            Console.WriteLine("=" + new string('=', 50));

            GremlinServer? server = null;

            try
            {
                // Start minimal server with maximum debug output
                Console.WriteLine("?? Starting GremlinServer with debug logging...");
                
                server = await GremlinDatabase.StartAsync(options =>
                {
                    options.Host = "localhost";
                    options.Port = 8182;        
                    options.HttpPort = 8183;    
                    options.EnableTcp = false;
                    options.EnableWebSocket = true;
                    options.EnableHttp = false;
                    options.EnableLogging = true;        // Enable all logging
                    options.EnableDebugLogging = true;   // Enable debug logging
                    options.MaxConnections = 5;
                    options.DatabaseOptions.EnableDebugLogging = true;
                    options.DatabaseOptions.EnableQueryLogging = true;
                });

                Console.WriteLine("? Server started");

                // Add minimal test data
                Console.WriteLine("\n?? Adding simple test data...");
                var directConnector = server.Connector;
                await directConnector.ExecuteAsync("g.addV('test').property('id', '1')", new Dictionary<string, object>());
                
                var stats = directConnector.GetStatistics();
                Console.WriteLine($"   ? Added data: {stats.VertexCount} vertices");

                // Test direct connector first to ensure data is working
                Console.WriteLine("\n?? Testing direct connector...");
                var directResult = await directConnector.ExecuteAsync("g.V().count()", new Dictionary<string, object>());
                Console.WriteLine($"   ? Direct g.V().count() = {directResult?.FirstOrDefault()}");

                // Now test with Gremlin.Net - but give it time to show all server-side logs
                Console.WriteLine("\n?? Testing Gremlin.Net client...");
                Console.WriteLine("   Creating client...");
                
                using var client = Stardust.Paradox.Data.InMemory.Demo.GremlinClientFactory.CreateGremlinNetClientAsync("localhost", 8183);
                
                Console.WriteLine("   ? Client created");
                Console.WriteLine("   ?? Sending g.V().count() query...");
                Console.WriteLine("   ? Please wait for server-side processing logs...");
                
                // Add delay to capture server logs
                await Task.Delay(1000);
                
                try
                {
                    var result = await client.ExecuteAsync("g.V().count()");
                    Console.WriteLine($"   ? SUCCESS: g.V().count() = {result?.FirstOrDefault()}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"   ? FAILED: {ex.Message}");
                    Console.WriteLine($"   ?? Check server logs above for detailed error information");
                }

                // Give time for final logs
                await Task.Delay(2000);

            }
            catch (Exception ex)
            {
                Console.WriteLine($"?? Fatal error: {ex.Message}");
            }
            finally
            {
                // Cleanup
                Console.WriteLine("\n?? Cleaning up...");
                
                if (server != null)
                {
                    await GremlinDatabase.StopAsync(server);
                }
                Console.WriteLine("? Cleanup completed");
            }

            Console.WriteLine("\n?? Test completed. Press any key to exit...");
            Console.ReadKey();
        }
    }
}