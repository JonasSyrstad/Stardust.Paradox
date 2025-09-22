using System;
using System.Threading.Tasks;
using Stardust.Paradox.Data.InMemory;
using Stardust.Paradox.Data.InMemory.Demo;

/// <summary>
/// Diagnostic test for Gremlin.Net connection issues
/// </summary>
class GremlinNetDiagnosticTest
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("?? Gremlin.Net Connection Diagnostic Test");
        Console.WriteLine("=" + new string('=', 50));
        Console.WriteLine("Diagnosing Gremlin.Net client connection issues...\n");

        try
        {
            await DiagnoseGremlinNetIssuesAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"? Diagnostic test failed: {ex.Message}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"   Inner: {ex.InnerException.Message}");
            }
            Console.WriteLine($"   Stack: {ex.StackTrace}");
        }

        Console.WriteLine("\nPress any key to exit...");
        Console.ReadKey();
    }

    static async Task DiagnoseGremlinNetIssuesAsync()
    {
        GremlinServer server = null;
        IGremlinQueryClient client = null;

        try
        {
            // Start server on different ports to avoid conflicts
            Console.WriteLine("?? Starting Gremlin Server (diagnostic mode)...");
            server = await GremlinDatabase.StartAsync(options =>
            {
                options.Host = "localhost";
                options.Port = 8185;        // Different TCP port to avoid conflicts
                options.HttpPort = 8186;    // Different HTTP/WebSocket port
                options.EnableTcp = true;
                options.EnableWebSocket = true;
                options.EnableHttp = false;
                options.EnableLogging = true;
                options.EnableDebugLogging = true;
                options.MaxConnections = 10;
            });

            Console.WriteLine("? Server started successfully");
            
            // Give server extra time to start
            Console.WriteLine("? Waiting for server to be fully ready...");
            await Task.Delay(3000);

            // Test Step 1: Check if WebSocket endpoint is accessible
            Console.WriteLine("\n?? Step 1: Testing WebSocket endpoint accessibility...");
            try
            {
                using var testClient = new System.Net.WebSockets.ClientWebSocket();
                testClient.Options.AddSubProtocol("gremlin-ws");
                
                var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(5));
                await testClient.ConnectAsync(new Uri("ws://localhost:8186/"), cts.Token);
                
                if (testClient.State == System.Net.WebSockets.WebSocketState.Open)
                {
                    Console.WriteLine("   ? WebSocket endpoint is accessible");
                    await testClient.CloseAsync(System.Net.WebSockets.WebSocketCloseStatus.NormalClosure, "Test", cts.Token);
                }
                else
                {
                    Console.WriteLine($"   ? WebSocket endpoint connection failed: {testClient.State}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"   ? WebSocket endpoint test failed: {ex.Message}");
            }

            // Test Step 2: Try creating Gremlin.Net client
            Console.WriteLine("\n?? Step 2: Creating Gremlin.Net client...");
            try
            {
                client = GremlinClientFactory.CreateGremlinNetClientAsync("localhost", 8186);
                Console.WriteLine($"   ? Gremlin.Net client created: {client.Protocol}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"   ? Gremlin.Net client creation failed: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"      Inner: {ex.InnerException.Message}");
                }
                return; // Can't continue without client
            }

            // Test Step 3: Simple query with detailed error tracking
            Console.WriteLine("\n?? Step 3: Testing simple query execution...");
            try
            {
                Console.WriteLine("   Executing: g.inject(42)");
                
                var startTime = DateTime.Now;
                var results = await client.ExecuteAsync("g.inject(42)");
                var endTime = DateTime.Now;
                
                var resultsList = results.ToList();
                Console.WriteLine($"   ? Query executed successfully in {(endTime - startTime).TotalMilliseconds:F1}ms");
                Console.WriteLine($"   ?? Results: {resultsList.Count} items");
                
                foreach (var result in resultsList.Take(3))
                {
                    Console.WriteLine($"      - {result}");
                }
            }
            catch (TimeoutException tex)
            {
                Console.WriteLine($"   ? Query timed out: {tex.Message}");
            }
            catch (System.Net.WebSockets.WebSocketException wsex)
            {
                Console.WriteLine($"   ?? WebSocket error: {wsex.Message}");
                Console.WriteLine($"      WebSocket Error Code: {wsex.WebSocketErrorCode}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"   ? Query execution failed: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"      Inner: {ex.InnerException.Message}");
                }
            }

            // Test Step 4: Test with sample data
            Console.WriteLine("\n?? Step 4: Testing with sample data...");
            try
            {
                // Load sample data directly through server
                Console.WriteLine("   Loading sample data...");
                var scenario = new SocialCommerceScenario(server.Connector);
                await scenario.LoadScenarioAsync();
                Console.WriteLine("   ? Sample data loaded");

                // Test vertex count query
                Console.WriteLine("   Testing vertex count query: g.V().count()");
                var countResults = await client.ExecuteAsync("g.V().count()");
                var count = countResults?.FirstOrDefault();
                Console.WriteLine($"   ? Vertex count: {count}");

                // Test vertex retrieval
                Console.WriteLine("   Testing vertex retrieval: g.V().limit(2)");
                var vertexResults = await client.ExecuteAsync("g.V().limit(2)");
                var vertices = vertexResults.ToList();
                Console.WriteLine($"   ? Retrieved {vertices.Count} vertices");
                
                foreach (var vertex in vertices)
                {
                    Console.WriteLine($"      - {vertex}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"   ? Sample data test failed: {ex.Message}");
            }

            Console.WriteLine("\n?? Diagnostic test completed!");
        }
        finally
        {
            try
            {
                client?.Dispose();
                Console.WriteLine("? Client disposed");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"?? Client disposal error: {ex.Message}");
            }

            try
            {
                if (server != null)
                {
                    await GremlinDatabase.StopAsync(server);
                    Console.WriteLine("? Server stopped");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"?? Server stop error: {ex.Message}");
            }
        }
    }
}