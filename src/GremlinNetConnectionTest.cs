using System;
using System.Threading.Tasks;
using Stardust.Paradox.Data.InMemory;
using Stardust.Paradox.Data.InMemory.Demo;

/// <summary>
/// Test for Gremlin.Net client connection and query execution
/// </summary>
class GremlinNetConnectionTest
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("?? Gremlin.Net Connection Test");
        Console.WriteLine("=" + new string('=', 40));
        Console.WriteLine("Testing Gremlin.Net client connection and query execution with timeouts.\n");

        try
        {
            await TestGremlinNetClientAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"? Test failed: {ex.Message}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"   Inner: {ex.InnerException.Message}");
            }
        }

        Console.WriteLine("\nPress any key to exit...");
        Console.ReadKey();
    }

    static async Task TestGremlinNetClientAsync()
    {
        // Start the server
        Console.WriteLine("?? Starting Gremlin Server...");
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
            // Give server time to fully start
            Console.WriteLine("? Waiting for server to fully initialize...");
            await Task.Delay(2000);

            // Load some test data
            Console.WriteLine("?? Loading test data...");
            var scenario = new SocialCommerceScenario(server.Connector);
            await scenario.LoadScenarioAsync();
            Console.WriteLine("? Test data loaded");

            // Test Gremlin.Net client with timeout handling
            Console.WriteLine("\n?? Testing Gremlin.Net Client...");
            
            IGremlinQueryClient client = null;
            try
            {
                Console.WriteLine("   Creating Gremlin.Net client...");
                client = GremlinClientFactory.CreateGremlinNetClientAsync("localhost", 8183);
                Console.WriteLine($"   ? Client created: {client.Protocol}");

                // Test queries with timeouts
                var testQueries = new[]
                {
                    ("Simple count", "g.V().count()"),
                    ("Vertex query", "g.V().limit(3)"),
                    ("Label query", "g.V().label().dedup()"),
                    ("Property query", "g.V().hasLabel('user').values('name').limit(5)")
                };

                foreach (var (description, query) in testQueries)
                {
                    Console.WriteLine($"\n   ?? {description}: {query}");
                    
                    try
                    {
                        using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(10));
                        var startTime = DateTime.Now;
                        
                        var task = client.ExecuteAsync(query);
                        var results = await task.WaitAsync(cts.Token);
                        
                        var endTime = DateTime.Now;
                        var duration = (endTime - startTime).TotalMilliseconds;
                        
                        var resultsList = results.ToList();
                        Console.WriteLine($"   ? Success: {resultsList.Count} results in {duration:F1}ms");
                        
                        // Show first few results
                        for (int i = 0; i < Math.Min(3, resultsList.Count); i++)
                        {
                            var formatted = FormatResult(resultsList[i]);
                            Console.WriteLine($"      [{i + 1}] {formatted}");
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        Console.WriteLine($"   ? Timeout: Query timed out after 10 seconds");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"   ? Failed: {ex.Message}");
                    }
                }

                Console.WriteLine("\n?? Gremlin.Net client test completed successfully!");
            }
            finally
            {
                client?.Dispose();
            }
        }
        finally
        {
            await GremlinDatabase.StopAsync(server);
            Console.WriteLine("\n?? Server stopped");
        }
    }

    static string FormatResult(dynamic result)
    {
        if (result == null) return "null";

        try
        {
            if (result is string str) return $"\"{str}\"";
            if (result is int || result is long || result is double || result is float || result is decimal)
                return result.ToString();
            if (result is bool boolean) return boolean.ToString().ToLower();

            // Handle JSON objects
            if (result is Newtonsoft.Json.Linq.JObject jObj)
            {
                if (jObj.ContainsKey("id") && jObj.ContainsKey("label"))
                {
                    var id = jObj["id"]?.ToString() ?? "?";
                    var label = jObj["label"]?.ToString() ?? "?";
                    return $"v[{id}:{label}]";
                }
            }

            var json = Newtonsoft.Json.JsonConvert.SerializeObject(result, Newtonsoft.Json.Formatting.None);
            return json.Length > 80 ? json.Substring(0, 77) + "..." : json;
        }
        catch
        {
            return result.ToString();
        }
    }
}