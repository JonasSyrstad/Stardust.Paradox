using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Stardust.Paradox.Data.InMemory;
using Stardust.Paradox.Data.InMemory.Demo;

namespace GremlinNetComprehensiveTestApp
{
    class GremlinNetConnectionType5Test
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("?? Testing Connection Type 5 (Gremlin.Net) - g.V() Query");
            Console.WriteLine("=" + new string('=', 60));

            GremlinServer? server = null;
            IGremlinQueryClient? client = null;

            try
            {
                // Start server like the demo app does
                Console.WriteLine("?? Starting GremlinServer...");
                
                server = await GremlinDatabase.StartAsync(options =>
                {
                    options.Host = "localhost";
                    options.Port = 8182;        
                    options.HttpPort = 8183;    
                    options.EnableTcp = false;      // Only enable WebSocket for type 5
                    options.EnableWebSocket = true;
                    options.EnableHttp = false;
                    options.EnableLogging = false;
                    options.EnableDebugLogging = true; // Enable debug for troubleshooting
                    options.MaxConnections = 50;
                    options.DatabaseOptions.EnableDebugLogging = true;
                });

                Console.WriteLine("? Server started");
                Console.WriteLine($"   ?? WebSocket endpoint: ws://localhost:8183");

                // Add some test data first using direct connector
                Console.WriteLine("\n?? Adding test data...");
                var directConnector = server.Connector;
                await directConnector.ExecuteAsync("g.addV('person').property('name', 'Alice')", new Dictionary<string, object>());
                await directConnector.ExecuteAsync("g.addV('person').property('name', 'Bob')", new Dictionary<string, object>());
                await directConnector.ExecuteAsync("g.addV('product').property('name', 'Widget')", new Dictionary<string, object>());
                
                var stats = directConnector.GetStatistics();
                Console.WriteLine($"   ? Added data: {stats.VertexCount} vertices, {stats.EdgeCount} edges");

                // Create Gremlin.Net client (Connection Type 5)
                Console.WriteLine("\n?? Creating Gremlin.Net client (Connection Type 5)...");
                
                client = GremlinClientFactory.CreateGremlinNetClientAsync("localhost", 8183);
                Console.WriteLine("? Gremlin.Net client created");

                // Test basic g.V() query
                Console.WriteLine("\n?? Testing g.V() query...");
                Console.WriteLine("Query: g.V()");
                
                try
                {
                    var result = await client.ExecuteAsync("g.V()");
                    var resultList = result.ToList();
                    
                    Console.WriteLine($"? SUCCESS: g.V() returned {resultList.Count} results");
                    
                    for (int i = 0; i < Math.Min(resultList.Count, 5); i++)
                    {
                        Console.WriteLine($"   [{i + 1}] {FormatResult(resultList[i])}");
                    }
                    
                    if (resultList.Count > 5)
                    {
                        Console.WriteLine($"   ... and {resultList.Count - 5} more results");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"? FAILED: g.V() query failed");
                    Console.WriteLine($"   Error: {ex.Message}");
                    if (ex.InnerException != null)
                    {
                        Console.WriteLine($"   Inner: {ex.InnerException.Message}");
                    }
                    
                    // Try to diagnose the issue
                    Console.WriteLine("\n?? Diagnosing issue...");
                    await DiagnoseConnectionAsync(client);
                }

                // Test other basic queries if g.V() works
                if (client != null)
                {
                    await TestAdditionalQueries(client);
                }

            }
            catch (Exception ex)
            {
                Console.WriteLine($"?? Fatal error: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"   Inner exception: {ex.InnerException.Message}");
                }
                Console.WriteLine($"   Stack trace: {ex.StackTrace}");
            }
            finally
            {
                // Cleanup
                Console.WriteLine("\n?? Cleaning up...");
                
                try
                {
                    client?.Dispose();
                    if (server != null)
                    {
                        await GremlinDatabase.StopAsync(server);
                    }
                    Console.WriteLine("? Cleanup completed");
                }
                catch (Exception cleanupEx)
                {
                    Console.WriteLine($"?? Cleanup error: {cleanupEx.Message}");
                }
            }

            Console.WriteLine("\n?? Test completed. Press any key to exit...");
            Console.ReadKey();
        }

        private static async Task DiagnoseConnectionAsync(IGremlinQueryClient client)
        {
            try
            {
                Console.WriteLine("   ?? Testing simple inject query...");
                var result = await client.ExecuteAsync("g.inject(42)");
                var value = result?.FirstOrDefault();
                Console.WriteLine($"   ? inject(42) = {value}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"   ? inject(42) failed: {ex.Message}");
            }

            try
            {
                Console.WriteLine("   ?? Testing count query...");
                var result = await client.ExecuteAsync("g.V().count()");
                var count = result?.FirstOrDefault();
                Console.WriteLine($"   ? g.V().count() = {count}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"   ? g.V().count() failed: {ex.Message}");
            }

            try
            {
                Console.WriteLine("   ?? Testing identity query...");
                var result = await client.ExecuteAsync("g.inject(1).identity()");
                var identity = result?.FirstOrDefault();
                Console.WriteLine($"   ? g.inject(1).identity() = {identity}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"   ? g.inject(1).identity() failed: {ex.Message}");
            }
        }

        private static async Task TestAdditionalQueries(IGremlinQueryClient client)
        {
            Console.WriteLine("\n?? Testing additional queries...");

            var testQueries = new[]
            {
                ("g.V().count()", "Count all vertices"),
                ("g.V().hasLabel('person')", "Get person vertices"),
                ("g.V().values('name')", "Get all name values"),
                ("g.V().limit(1)", "Get first vertex"),
                ("g.E()", "Get all edges"),
                ("g.E().count()", "Count all edges")
            };

            foreach (var (query, description) in testQueries)
            {
                try
                {
                    Console.WriteLine($"\n   ?? {description}");
                    Console.WriteLine($"       Query: {query}");
                    
                    var result = await client.ExecuteAsync(query);
                    var resultList = result.ToList();
                    
                    Console.WriteLine($"   ? SUCCESS: {resultList.Count} results");
                    
                    if (resultList.Count > 0 && resultList.Count <= 3)
                    {
                        foreach (var item in resultList)
                        {
                            Console.WriteLine($"       ? {FormatResult(item)}");
                        }
                    }
                    else if (resultList.Count > 3)
                    {
                        Console.WriteLine($"       ? {FormatResult(resultList[0])} (and {resultList.Count - 1} more)");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"   ? FAILED: {ex.Message}");
                }
            }
        }

        private static string FormatResult(dynamic result)
        {
            if (result == null) return "null";
            
            try
            {
                if (result is string str) return $"\"{str}\"";
                if (result is int || result is long || result is double || result is float) return result.ToString();
                if (result is bool boolean) return boolean.ToString().ToLower();
                
                // Try to handle complex objects
                var json = Newtonsoft.Json.JsonConvert.SerializeObject(result);
                return json.Length > 100 ? json.Substring(0, 97) + "..." : json;
            }
            catch
            {
                return result.ToString();
            }
        }
    }
}