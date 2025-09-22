using System;
using System.Threading.Tasks;
using Stardust.Paradox.Data.InMemory;
using Stardust.Paradox.Data.InMemory.Demo;

/// <summary>
/// Demonstration of Gremlin query execution and response display across all protocols
/// </summary>
class QueryExecutionDemo
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("?? Gremlin Query Execution and Response Display Demo");
        Console.WriteLine("=" + new string('=', 60));

        try
        {
            await DemoQueryExecutionAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"? Demo failed: {ex.Message}");
        }

        Console.WriteLine("\nPress any key to exit...");
        Console.ReadKey();
    }

    static async Task DemoQueryExecutionAsync()
    {
        // Start the Gremlin server
        Console.WriteLine("\n?? Starting Gremlin Server...");
        var server = await GremlinDatabase.StartAsync(options =>
        {
            options.Host = "localhost";
            options.Port = 8182;
            options.HttpPort = 8183;
            options.EnableTcp = true;
            options.EnableWebSocket = true;
            options.EnableHttp = true;
            options.EnableLogging = false;
        });

        try
        {
            // Load sample data
            Console.WriteLine("?? Loading sample data...");
            var scenario = new SocialCommerceScenario(server.Connector);
            await scenario.LoadScenarioAsync();
            Console.WriteLine("? Sample data loaded successfully");

            // Test each protocol
            await TestProtocolAsync("Direct In-Process", WireProtocol.Direct, server);
            await TestProtocolAsync("TCP", WireProtocol.TCP, server);
            
            if (server.WebSocketSupported)
            {
                await TestProtocolAsync("WebSocket", WireProtocol.WebSocket, server);
                await TestProtocolAsync("HTTP REST", WireProtocol.HTTP, server);
            }
            else
            {
                Console.WriteLine("?? WebSocket protocols not available on this platform");
            }
        }
        finally
        {
            await GremlinDatabase.StopAsync(server);
            Console.WriteLine("?? Server stopped");
        }
    }

    static async Task TestProtocolAsync(string protocolName, WireProtocol protocol, GremlinServer server)
    {
        Console.WriteLine($"\n{new string('=', 50)}");
        Console.WriteLine($"?? Testing {protocolName} Protocol");
        Console.WriteLine($"{new string('=', 50)}");

        IGremlinQueryClient client = null;
        try
        {
            // Create client for the specific protocol
            client = await GremlinClientFactory.CreateClientAsync(protocol, server.Connector, "localhost", 8182, 8183);
            Console.WriteLine($"? Connected via {client.Protocol}");

            // Execute a variety of queries to demonstrate response formatting
            var queries = new[]
            {
                ("Count vertices", "g.V().count()"),
                ("Get vertex labels", "g.V().label().dedup()"),
                ("Find users", "g.V().hasLabel('user').limit(3)"),
                ("User properties", "g.V().hasLabel('user').limit(2).propertyMap()"),
                ("Find edges", "g.E().hasLabel('purchased').limit(3)"),
                ("Complex traversal", "g.V().hasLabel('user').out('purchased').hasLabel('product').values('name').limit(5)")
            };

            foreach (var (description, query) in queries)
            {
                Console.WriteLine($"\n?? {description}:");
                Console.WriteLine($"?? Query: {query}");
                
                await ExecuteAndDisplayQueryAsync(client, query);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"? {protocolName} protocol failed: {ex.Message}");
        }
        finally
        {
            client?.Dispose();
        }
    }

    static async Task ExecuteAndDisplayQueryAsync(IGremlinQueryClient client, string query)
    {
        var startTime = DateTime.Now;
        
        try
        {
            var results = await client.ExecuteAsync(query);
            var endTime = DateTime.Now;
            var duration = (endTime - startTime).TotalMilliseconds;

            var resultsList = results.ToList();
            
            Console.WriteLine($"??  Executed in {duration:F1}ms");
            Console.WriteLine($"?? Results ({resultsList.Count} items):");
            
            if (resultsList.Count == 0)
            {
                Console.WriteLine("   (No results)");
            }
            else
            {
                for (int i = 0; i < Math.Min(resultsList.Count, 5); i++)
                {
                    var result = resultsList[i];
                    var formatted = FormatResult(result);
                    Console.WriteLine($"   [{i + 1}] {formatted}");
                }
                
                if (resultsList.Count > 5)
                {
                    Console.WriteLine($"   ... and {resultsList.Count - 5} more results");
                }
            }
        }
        catch (Exception ex)
        {
            var endTime = DateTime.Now;
            var duration = (endTime - startTime).TotalMilliseconds;
            Console.WriteLine($"? Query failed after {duration:F1}ms: {ex.Message}");
        }
    }

    static string FormatResult(dynamic result)
    {
        if (result == null)
            return "null";

        try
        {
            // Handle common types
            if (result is string str)
                return $"\"{str}\"";
            
            if (result is int || result is long || result is double || result is float || result is decimal)
                return result.ToString();
            
            if (result is bool boolean)
                return boolean.ToString().ToLower();

            // Handle JSON objects (common in GraphSON responses)
            if (result is Newtonsoft.Json.Linq.JObject jObj)
            {
                // Handle GraphSON vertex format
                if (jObj.ContainsKey("@type") && jObj["@type"]?.ToString() == "g:Vertex")
                {
                    var id = jObj["@value"]?["id"]?.ToString() ?? "?";
                    var label = jObj["@value"]?["label"]?.ToString() ?? "vertex";
                    return $"v[{id}:{label}]";
                }
                
                // Handle GraphSON edge format
                if (jObj.ContainsKey("@type") && jObj["@type"]?.ToString() == "g:Edge")
                {
                    var id = jObj["@value"]?["id"]?.ToString() ?? "?";
                    var label = jObj["@value"]?["label"]?.ToString() ?? "edge";
                    var outV = jObj["@value"]?["outV"]?.ToString() ?? "?";
                    var inV = jObj["@value"]?["inV"]?.ToString() ?? "?";
                    return $"e[{id}:{label}][{outV}?{inV}]";
                }
                
                // Handle simple vertex format
                if (jObj.ContainsKey("id") && jObj.ContainsKey("label"))
                {
                    var id = jObj["id"]?.ToString() ?? "?";
                    var label = jObj["label"]?.ToString() ?? "vertex";
                    return $"v[{id}:{label}]";
                }
            }

            // Try JSON serialization for complex objects
            var json = Newtonsoft.Json.JsonConvert.SerializeObject(result, Newtonsoft.Json.Formatting.None);
            
            // Truncate long results
            if (json.Length > 100)
            {
                return json.Substring(0, 97) + "...";
            }
            
            return json;
        }
        catch
        {
            return result.ToString();
        }
    }
}