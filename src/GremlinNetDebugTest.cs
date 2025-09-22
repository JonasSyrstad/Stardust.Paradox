using System;
using System.Threading.Tasks;
using System.Linq;
using Stardust.Paradox.Data.InMemory;
using Stardust.Paradox.Data.InMemory.Demo;

class GremlinNetDebugTest
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("?? Gremlin.Net Debug Test with Extensive Logging");
        Console.WriteLine("=" + new string('=', 60));

        // Start server with comprehensive debugging
        Console.WriteLine("Starting server with debug logging...");
        var server = await GremlinDatabase.StartAsync(options =>
        {
            options.Host = "localhost";
            options.Port = 8190;        
            options.HttpPort = 8191;    
            options.EnableTcp = true;
            options.EnableWebSocket = true;
            options.EnableHttp = false;
            options.EnableLogging = true;
            options.EnableDebugLogging = true; // Enable full debug logging
        });

        try
        {
            Console.WriteLine("Server started, waiting 3 seconds...");
            await Task.Delay(3000);

            Console.WriteLine("\n=== Creating Gremlin.Net Client ===");
            var client = GremlinClientFactory.CreateGremlinNetClientAsync("localhost", 8191);
            Console.WriteLine($"Client created: {client.Protocol}");

            Console.WriteLine("\n=== Testing Simple Query ===");
            Console.WriteLine("About to execute: g.inject(42)");
            
            try
            {
                // Create a task with timeout to see exactly where it hangs
                var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(15));
                var task = Task.Run(async () => 
                {
                    Console.WriteLine("[TASK] About to call client.ExecuteAsync");
                    var result = await client.ExecuteAsync("g.inject(42)");
                    Console.WriteLine("[TASK] client.ExecuteAsync completed");
                    return result;
                }, cts.Token);
                
                Console.WriteLine("Waiting for query result...");
                var result = await task;
                var resultsList = result.ToList();
                Console.WriteLine($"? SUCCESS: {resultsList.Count} results: {string.Join(", ", resultsList)}");
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("? TIMEOUT: Query timed out after 15 seconds");
                Console.WriteLine("This indicates the hang is occurring during Gremlin.Net -> Server communication");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"? ERROR: {ex.GetType().FullName}: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Inner: {ex.InnerException.GetType().FullName}: {ex.InnerException.Message}");
                }
            }

            client?.Dispose();
        }
        finally
        {
            await GremlinDatabase.StopAsync(server);
            Console.WriteLine("Server stopped");
        }

        Console.WriteLine("\n=== Analysis ===");
        Console.WriteLine("Check the debug output above to see:");
        Console.WriteLine("1. Whether the Gremlin.Net client connects to the WebSocket");
        Console.WriteLine("2. Whether the server receives any messages");
        Console.WriteLine("3. Where exactly the communication breaks down");
    }
}