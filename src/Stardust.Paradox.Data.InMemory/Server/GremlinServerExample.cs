#if NET8_0_OR_GREATER || NETCOREAPP3_1_OR_GREATER
using System;
using Stardust.Paradox.Data.InMemory.Management;
using System.Collections.Generic;
using System.Threading.Tasks;
using Stardust.Paradox.Data.InMemory;

namespace Stardust.Paradox.Data.InMemory.Server
{
    /// <summary>
    /// Example console application demonstrating how to start and use the Gremlin server
    /// </summary>
    public class InMemoryGremlinServerExample
    {
        public static async Task RunExampleAsync()
        {
            Console.WriteLine("=== Stardust Paradox InMemory Gremlin Server Example ===\n");

            try
            {
                // Example 1: Quick start for development
                Console.WriteLine("1. Starting multi-protocol development server...");
                var devServer = await GremlinDatabase.StartWebSocketDevServerAsync();
                
                Console.WriteLine($"   ? TCP server started on {devServer.Options.Host}:{devServer.Options.Port}");
                if (devServer.WebSocketSupported)
                {
                    Console.WriteLine($"   ? WebSocket server started on {devServer.Options.Host}:{devServer.Options.GetHttpPort()}");
                    Console.WriteLine($"   ? HTTP server started on {devServer.Options.Host}:{devServer.Options.GetHttpPort()}");
                }
                Console.WriteLine($"   ? Active connections: {devServer.ActiveConnectionCount}");

                // Example 2: Add some test data
                Console.WriteLine("\n2. Adding test data...");
                var connector = devServer.Connector;
                
                await connector.ExecuteAsync("g.addV('person').property('id', '1').property('name', 'Alice').property('age', 30)", null);
                await connector.ExecuteAsync("g.addV('person').property('id', '2').property('name', 'Bob').property('age', 25)", null);
                await connector.ExecuteAsync("g.addV('company').property('id', '3').property('name', 'TechCorp')", null);
                
                await connector.ExecuteAsync("g.V('1').addE('works_for').to(g.V('3'))", null);
                await connector.ExecuteAsync("g.V('2').addE('works_for').to(g.V('3'))", null);
                await connector.ExecuteAsync("g.V('1').addE('friends').to(g.V('2'))", null);

                Console.WriteLine("   ? Added 3 vertices and 3 edges");

                // Example 3: Execute various queries
                Console.WriteLine("\n3. Executing sample queries...");
                
                var personCount = await connector.ExecuteAsync("g.V().hasLabel('person').count()", null);
                Console.WriteLine($"   ? Person count: {string.Join(", ", personCount)}");

                var names = await connector.ExecuteAsync("g.V().hasLabel('person').values('name')", null);
                Console.WriteLine($"   ? Person names: {string.Join(", ", names)}");

                var colleagues = await connector.ExecuteAsync("g.V().hasLabel('person').out('works_for').in('works_for').dedup().values('name')", null);
                Console.WriteLine($"   ? Colleagues: {string.Join(", ", colleagues)}");

                var avgAge = await connector.ExecuteAsync("g.V().hasLabel('person').values('age').mean()", null);
                Console.WriteLine($"   ? Average age: {string.Join(", ", avgAge)}");

                // Example 4: Show server statistics
                Console.WriteLine("\n4. Server statistics:");
                var stats = devServer.GetStatistics();
                Console.WriteLine($"   ? Vertex count: {stats["vertexCount"]}");
                Console.WriteLine($"   ? Edge count: {stats["edgeCount"]}");
                Console.WriteLine($"   ? Total RU consumed: {stats["totalRU"]:F2}");
                Console.WriteLine($"   ? Active connections: {stats["activeConnections"]}");

                // Example 5: Test with authentication
                Console.WriteLine("\n5. Starting authenticated server...");
                var authServer = await GremlinDatabase.StartAsync(options =>
                {
                    options.Host = "localhost";
                    options.Port = 8183;
                    options.EnableLogging = true;
                    options.Authentication = new GremlinAuthenticationOptions
                    {
                        EnableBasicAuth = true,
                        Username = "admin",
                        Password = "password"
                    };
                });
                
                Console.WriteLine($"   ? Authenticated server started on port {authServer.Options.Port}");
                Console.WriteLine("   ? Username: admin, Password: password");

                // Example 6: Global statistics
                Console.WriteLine("\n6. Global statistics:");
                var globalStats = GremlinDatabase.GetGlobalStatistics();
                Console.WriteLine($"   ? Total servers running: {globalStats["totalServers"]}");
                Console.WriteLine($"   ? Total active connections: {globalStats["totalActiveConnections"]}");
                Console.WriteLine($"   ? Total vertices across all servers: {globalStats["totalVertices"]}");

                Console.WriteLine("\n=== Example completed successfully! ===");
                Console.WriteLine("\nServers are now running. You can connect using:");
                Console.WriteLine("- TCP: localhost:8182 (text or JSON queries)");
                if (devServer.WebSocketSupported)
                {
                    Console.WriteLine("- WebSocket: ws://localhost:8183 (with sub-protocol 'gremlin-ws')");
                    Console.WriteLine("- HTTP: http://localhost:8183 (POST for queries, GET for status)");
                }
                Console.WriteLine("- Authenticated TCP: localhost:8183 (admin/password)");
                Console.WriteLine("\nPress any key to stop servers and exit...");
                Console.ReadKey();

                // Cleanup
                await GremlinDatabase.StopAllAsync();
                Console.WriteLine("\n? All servers stopped gracefully.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n? Error: {ex.Message}");
                await GremlinDatabase.StopAllAsync();
                throw;
            }
        }
    }
}

#if EXAMPLE_MAIN
namespace Stardust.Paradox.Data.InMemory.Example
{
    class Program
    {
        static async Task Main(string[] args)
        {
            await GremlinServerExample.RunExampleAsync();
        }
    }
}
#endif

#endif