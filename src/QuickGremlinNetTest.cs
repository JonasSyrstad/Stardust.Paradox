using System;
using System.Threading.Tasks;
using System.Linq;
using Stardust.Paradox.Data.InMemory;
using Stardust.Paradox.Data.InMemory.Demo;

class QuickGremlinNetTest
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("?? Enhanced Gremlin.Net Debug Test");
        Console.WriteLine("Testing the exact issue with Gremlin.Net connection with extensive logging...\n");

        // Start server with different ports to avoid conflicts
        Console.WriteLine("Starting server...");
        var server = await GremlinDatabase.StartAsync(options =>
        {
            options.Host = "localhost";
            options.Port = 8187;        
            options.HttpPort = 8188;    
            options.EnableTcp = true;
            options.EnableWebSocket = true;
            options.EnableHttp = false;
            options.EnableLogging = true;
            options.EnableDebugLogging = true; // Enable debug logging
        });

        try
        {
            Console.WriteLine("Waiting for server to start...");
            await Task.Delay(2000); // Let server start

            Console.WriteLine("\n=== STEP 1: Creating Gremlin.Net client ===");
            IGremlinQueryClient client = null;
            
            try
            {
                client = GremlinClientFactory.CreateGremlinNetClientAsync("localhost", 8188);
                Console.WriteLine($"? Client created: {client.Protocol}");
                Console.WriteLine($"   Client IsConnected: {client.IsConnected}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"? Client creation failed: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"   Inner: {ex.InnerException.Message}");
                }
                return;
            }

            Console.WriteLine("\n=== STEP 2: Testing WebSocket endpoint directly ===");
            try
            {
                using var testWs = new System.Net.WebSockets.ClientWebSocket();
                testWs.Options.AddSubProtocol("gremlin-ws");
                
                var testCts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(5));
                await testWs.ConnectAsync(new Uri("ws://localhost:8188/"), testCts.Token);
                
                Console.WriteLine($"? Direct WebSocket connection successful: {testWs.State}");
                await testWs.CloseAsync(System.Net.WebSockets.WebSocketCloseStatus.NormalClosure, "Test", testCts.Token);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"? Direct WebSocket test failed: {ex.Message}");
            }

            Console.WriteLine("\n=== STEP 3: Testing simple query with timeout ===");
            var queryTests = new[]
            {
                ("g.inject(42)", "Simple inject test"),
                ("g.V().limit(0).count()", "Empty vertex count"),
                ("1+1", "Simple math expression")
            };

            foreach (var (query, description) in queryTests)
            {
                Console.WriteLine($"\n--- Testing: {description} ---");
                Console.WriteLine($"Query: {query}");
                
                var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(10));
                try
                {
                    Console.WriteLine("Creating task...");
                    var task = Task.Run(async () => 
                    {
                        Console.WriteLine($"[TASK] Starting query execution for: {query}");
                        var result = await client.ExecuteAsync(query);
                        Console.WriteLine($"[TASK] Query completed");
                        return result;
                    }, cts.Token);
                    
                    Console.WriteLine("Awaiting task...");
                    var result = await task;
                    var resultsList = result.ToList();
                    Console.WriteLine($"? Success! Got {resultsList.Count} results: {string.Join(", ", resultsList)}");
                }
                catch (OperationCanceledException)
                {
                    Console.WriteLine("? Query timed out after 10 seconds");
                    Console.WriteLine("   This indicates the query is hanging in Gremlin.Net");
                    break; // Don't continue with other tests
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"? Query failed: {ex.GetType().FullName}: {ex.Message}");
                    if (ex.InnerException != null)
                    {
                        Console.WriteLine($"   Inner: {ex.InnerException.GetType().FullName}: {ex.InnerException.Message}");
                    }
                }
                
                // Small delay between tests
                await Task.Delay(1000);
            }

            Console.WriteLine("\n=== STEP 4: Testing disposal ===");
            try
            {
                client?.Dispose();
                Console.WriteLine("? Client disposed successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"? Client disposal failed: {ex.Message}");
            }
        }
        finally
        {
            Console.WriteLine("\n=== CLEANUP ===");
            try
            {
                await GremlinDatabase.StopAsync(server);
                Console.WriteLine("? Server stopped successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"? Server stop failed: {ex.Message}");
            }
        }

        Console.WriteLine("\n=== TEST COMPLETE ===");
        Console.WriteLine("Check the debug output above to identify where the hang occurs.");
    }
}