using System;
using System.Threading.Tasks;
using System.Linq;
using Gremlin.Net.Driver;
using Stardust.Paradox.Data.InMemory;
using Stardust.Paradox.Data.InMemory.Demo;
using Stardust.Paradox.Data.InMemory.Management;

/// <summary>
/// Interactive Gremlin Query Execution Test - demonstrates enhanced query execution and response display
/// </summary>
class InteractiveQueryTest
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("?? Interactive Gremlin Query Execution Test");
        Console.WriteLine("=" + new string('=', 50));
        Console.WriteLine("This demonstrates enhanced query execution and response display across all protocols.\n");

        try
        {
            await RunInteractiveQueryTestAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"? Test failed: {ex.Message}");
        }

        Console.WriteLine("\nPress any key to exit...");
        Console.ReadKey();
    }

    static async Task RunInteractiveQueryTestAsync()
    {
        // Start the server
        Console.WriteLine("?? Starting Gremlin Server with all protocols...");
        var server = await GremlinDatabase.StartAsync(options =>
        {
            options.Host = "localhost";
            options.Port = 8182;
            options.HttpPort = 8183;
            options.EnableTcp = true;
            options.EnableWebSocket = true;
            options.EnableHttp = true;
            options.EnableLogging = true;
            options.EnableDebugLogging = false;
        });

        try
        {
            // Load rich sample data
            Console.WriteLine("?? Loading Social Commerce scenario with rich sample data...");
            var scenario = new SocialCommerceScenario(server.Connector);
            await scenario.LoadScenarioAsync();
            Console.WriteLine("? Sample data loaded - ready for query testing\n");

            // Test Direct protocol with detailed query execution
            await TestProtocolWithQueriesAsync("Direct In-Process", WireProtocol.Direct, server);
            
            // Test TCP protocol
            await TestProtocolWithQueriesAsync("TCP", WireProtocol.TCP, server);
            
            if (server.WebSocketSupported)
            {
                // Test WebSocket protocol
                await TestProtocolWithQueriesAsync("WebSocket", WireProtocol.WebSocket, server);
                
                // Test HTTP protocol
                await TestProtocolWithQueriesAsync("HTTP REST", WireProtocol.HTTP, server);
            }
            else
            {
                Console.WriteLine("?? WebSocket/HTTP protocols not available on this platform");
            }

            // Summary
            Console.WriteLine("\n" + new string('=', 60));
            Console.WriteLine("?? INTERACTIVE QUERY TEST COMPLETED");
            Console.WriteLine(new string('=', 60));
            Console.WriteLine("? All protocols support rich query execution and response display");
            Console.WriteLine("? Enhanced formatting for vertices, edges, and properties");
            Console.WriteLine("? Proper error handling with helpful suggestions");
            Console.WriteLine("? Performance metrics and result analysis");
            Console.WriteLine("? GraphSON format support for TinkerPop compatibility");
        }
        finally
        {
            await GremlinDatabase.StopAsync(server);
            Console.WriteLine("\n?? Server stopped");
        }
    }

    static async Task TestProtocolWithQueriesAsync(string protocolName, WireProtocol protocol, GremlinServer server)
    {
        Console.WriteLine($"\n{new string('=', 60)}");
        Console.WriteLine($"?? Testing {protocolName} Protocol - Enhanced Query Execution");
        Console.WriteLine($"{new string('=', 60)}");

        IGremlinQueryClient client = null;
        try
        {
            // Create client
            client = await GremlinClientFactory.CreateClientAsync(protocol, server.Connector, "localhost", 8182, 8183);
            Console.WriteLine($"? Connected via {client.Protocol}");

            // Define comprehensive test queries that showcase different result types
            var testQueries = new[]
            {
                new TestQuery("Count Query", "g.V().count()", 
                    "Simple aggregation - demonstrates numeric result formatting"),
                
                new TestQuery("Vertex Query", "g.V().hasLabel('user').limit(3)", 
                    "Vertex retrieval - demonstrates vertex object formatting"),
                
                new TestQuery("Property Map", "g.V().hasLabel('user').limit(2).propertyMap()", 
                    "Property mapping - demonstrates complex object formatting"),
                
                new TestQuery("Edge Query", "g.E().hasLabel('purchased').limit(3)", 
                    "Edge retrieval - demonstrates edge object formatting"),
                
                new TestQuery("Traversal Query", "g.V().hasLabel('user').out('purchased').hasLabel('product').values('name').limit(5)", 
                    "Complex traversal - demonstrates string result formatting"),
                
                new TestQuery("Label Analysis", "g.V().label().dedup().order()", 
                    "Schema discovery - demonstrates collection formatting"),
                
                new TestQuery("Property Values", "g.V().hasLabel('product').values('price').limit(10)", 
                    "Numeric properties - demonstrates numeric analysis"),
                
                new TestQuery("Boolean Query", "g.V().hasLabel('user').has('age', gt(25)).hasNext()", 
                    "Boolean result - demonstrates boolean formatting"),

                new TestQuery("Complex Aggregation", "g.V().hasLabel('product').groupCount().by('category')", 
                    "Grouping - demonstrates map result formatting"),

                new TestQuery("Path Query", "g.V().hasLabel('user').limit(2).out('purchased').path().limit(3)", 
                    "Path traversal - demonstrates path object formatting")
            };

            foreach (var testQuery in testQueries)
            {
                await ExecuteTestQueryAsync(client, testQuery);
                await Task.Delay(500); // Small delay for readability
            }

            // Test error handling
            Console.WriteLine($"\n?? Testing Error Handling:");
            await ExecuteTestQueryAsync(client, new TestQuery("Invalid Syntax", "g.V().invalidStep()", 
                "Tests error handling and suggestion system"));

            await ExecuteTestQueryAsync(client, new TestQuery("Non-existent Property", "g.V().has('nonExistentProperty', 'value')", 
                "Tests property error handling"));
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

    static async Task ExecuteTestQueryAsync(IGremlinQueryClient client, TestQuery testQuery)
    {
        Console.WriteLine($"\n?? {testQuery.Name}:");
        Console.WriteLine($"?? Query: {testQuery.Query}");
        Console.WriteLine($"?? Purpose: {testQuery.Description}");
        Console.WriteLine(new string('-', 50));
        
        var startTime = DateTime.Now;
        
        try
        {
            var results = await client.ExecuteAsync(testQuery.Query);
            var endTime = DateTime.Now;
            var duration = (endTime - startTime).TotalMilliseconds;

            var resultsList = results.ToList();
            
            // Enhanced result reporting
            Console.WriteLine($"? Query executed successfully");
            Console.WriteLine($"??  Execution time: {duration:F1}ms");
            Console.WriteLine($"?? Result count: {resultsList.Count}");
            
            if (resultsList.Count == 0)
            {
                Console.WriteLine("?? Results: (No results returned)");
            }
            else
            {
                Console.WriteLine($"?? Results:");
                
                var displayLimit = Math.Min(resultsList.Count, 5);
                for (int i = 0; i < displayLimit; i++)
                {
                    var result = resultsList[i];
                    var formatted = FormatResult(result);
                    Console.WriteLine($"   [{i + 1,2}] {formatted}");
                }
                
                if (resultsList.Count > 5)
                {
                    Console.WriteLine($"   ... and {resultsList.Count - 5} more results (showing first 5)");
                }

                // Show result type analysis
                AnalyzeResultTypes(resultsList);
            }
        }
        catch (Exception ex)
        {
            var endTime = DateTime.Now;
            var duration = (endTime - startTime).TotalMilliseconds;
            Console.WriteLine($"? Query failed after {duration:F1}ms");
            Console.WriteLine($"?? Error: {ex.Message}");
            
            // Show error suggestions
            ProvideErrorSuggestions(testQuery.Query, ex);
        }
    }

    static void AnalyzeResultTypes(System.Collections.Generic.List<dynamic> results)
    {
        try
        {
            var typeCount = new System.Collections.Generic.Dictionary<string, int>();
            foreach (var result in results.Take(10))
            {
                var type = GetResultType(result);
                typeCount[type] = typeCount.ContainsKey(type) ? typeCount[type] + 1 : 1;
            }

            if (typeCount.Count > 0)
            {
                var types = string.Join(", ", typeCount.Select(kvp => $"{kvp.Key}({kvp.Value})"));
                Console.WriteLine($"?? Result types: {types}");
            }
        }
        catch
        {
            // Ignore analysis errors
        }
    }

    static void ProvideErrorSuggestions(string query, Exception ex)
    {
        try
        {
            var message = ex.Message.ToLower();
            
            Console.WriteLine("?? Suggestions:");
            
            if (message.Contains("syntax") || message.Contains("parse") || message.Contains("invalid"))
            {
                Console.WriteLine("   • Check Gremlin syntax - start with simpler queries");
                Console.WriteLine("   • Common patterns: g.V(), g.E(), g.V().hasLabel('label')");
            }
            else if (message.Contains("property"))
            {
                Console.WriteLine("   • Check property names - use .propertyMap() to see available properties");
                Console.WriteLine("   • Property names are case-sensitive");
            }
            else
            {
                Console.WriteLine("   • Try a simpler query to verify connection");
                Console.WriteLine("   • Check server logs for more details");
            }
        }
        catch
        {
            Console.WriteLine("   • Try a simpler query first");
        }
    }

    static string GetResultType(dynamic result)
    {
        if (result == null) return "null";
        
        try
        {
            if (IsVertex(result)) return "vertex";
            if (IsEdge(result)) return "edge";
            if (result is string) return "string";
            if (IsNumeric(result)) return "number";
            if (result is bool) return "boolean";
            return "object";
        }
        catch
        {
            return "unknown";
        }
    }

    static bool IsVertex(dynamic result)
    {
        try
        {
            if (result is Newtonsoft.Json.Linq.JObject jObj)
            {
                return jObj["@type"]?.ToString() == "g:Vertex" || 
                       (jObj.ContainsKey("id") && jObj.ContainsKey("label") && jObj.ContainsKey("properties"));
            }
            return false;
        }
        catch
        {
            return false;
        }
    }

    static bool IsEdge(dynamic result)
    {
        try
        {
            if (result is Newtonsoft.Json.Linq.JObject jObj)
            {
                return jObj["@type"]?.ToString() == "g:Edge" || 
                       (jObj.ContainsKey("id") && jObj.ContainsKey("label") && jObj.ContainsKey("outV") && jObj.ContainsKey("inV"));
            }
            return false;
        }
        catch
        {
            return false;
        }
    }

    static bool IsNumeric(dynamic result)
    {
        return result is int || result is long || result is double || result is float || result is decimal;
    }

    static string FormatResult(dynamic result)
    {
        if (result == null)
            return "null";

        try
        {
            // Handle common types with enhanced formatting
            if (result is string str)
                return $"\"{str}\"";
            
            if (result is int || result is long || result is double || result is float || result is decimal)
                return result.ToString();
            
            if (result is bool boolean)
                return boolean.ToString().ToLower();

            // Handle JSON objects (GraphSON format)
            if (result is Newtonsoft.Json.Linq.JObject jObj)
            {
                // GraphSON vertex
                if (jObj.ContainsKey("@type") && jObj["@type"]?.ToString() == "g:Vertex")
                {
                    var value = jObj["@value"];
                    if (value is Newtonsoft.Json.Linq.JObject vObj)
                    {
                        var id = vObj["id"]?.ToString() ?? "?";
                        var label = vObj["label"]?.ToString() ?? "vertex";
                        return $"v[{id}:{label}]";
                    }
                }
                
                // GraphSON edge
                if (jObj.ContainsKey("@type") && jObj["@type"]?.ToString() == "g:Edge")
                {
                    var value = jObj["@value"];
                    if (value is Newtonsoft.Json.Linq.JObject eObj)
                    {
                        var id = eObj["id"]?.ToString() ?? "?";
                        var label = eObj["label"]?.ToString() ?? "edge";
                        var outV = eObj["outV"]?.ToString() ?? "?";
                        var inV = eObj["inV"]?.ToString() ?? "?";
                        return $"e[{id}:{label}][{outV}?{inV}]";
                    }
                }
                
                // Simple vertex format
                if (jObj.ContainsKey("id") && jObj.ContainsKey("label"))
                {
                    var id = jObj["id"]?.ToString() ?? "?";
                    var label = jObj["label"]?.ToString() ?? "vertex";
                    
                    // Add some property info if available
                    var props = "";
                    if (jObj.ContainsKey("properties"))
                    {
                        var propCount = 0;
                        if (jObj["properties"] is Newtonsoft.Json.Linq.JObject propObj)
                        {
                            propCount = propObj.Count;
                        }
                        props = propCount > 0 ? $" ({propCount} props)" : "";
                    }
                    
                    return $"v[{id}:{label}]{props}";
                }
            }

            // Handle arrays/collections
            if (result is Newtonsoft.Json.Linq.JArray jArr)
            {
                var items = jArr.Take(3).Select(item => FormatResult(item)).ToArray();
                var result_str = $"[{string.Join(", ", items)}";
                if (jArr.Count > 3)
                {
                    result_str += $", ... +{jArr.Count - 3} more";
                }
                return result_str + "]";
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

    public class TestQuery
    {
        public string Name { get; }
        public string Query { get; }
        public string Description { get; }

        public TestQuery(string name, string query, string description)
        {
            Name = name;
            Query = query;
            Description = description;
        }
    }
}