using System;
using System.Threading.Tasks;
using System.Linq;
using Stardust.Paradox.Data.InMemory;
using Stardust.Paradox.Data.InMemory.Demo;

class GremlinNetBinaryFixTest
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("?? Gremlin.Net Binary Message Fix Test");
        Console.WriteLine("=" + new string('=', 60));
        Console.WriteLine("Testing the binary message handling fix for Gremlin.Net\n");

        try
        {
            await TestGremlinNetBinaryHandlingAsync();
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

    static async Task TestGremlinNetBinaryHandlingAsync()
    {
        // Start a simple server
        Console.WriteLine("Starting server...");
        var server = await GremlinDatabase.StartAsync(options =>
        {
            options.Host = "localhost";
            options.Port = 8192;
            options.HttpPort = 8193;
            options.EnableTcp = true;
            options.EnableWebSocket = true;
            options.EnableHttp = false;
            options.EnableLogging = true;
            options.EnableDebugLogging = true;
        });

        try
        {
            await Task.Delay(2000);

            Console.WriteLine("\n=== Testing Gremlin.Net Client ===");
            
            // Create Gremlin.Net client
            var client = GremlinClientFactory.CreateGremlinNetClientAsync("localhost", 8193);
            Console.WriteLine($"Client created: {client.Protocol}");

            Console.WriteLine("\n=== Testing Query ===");
            
            try
            {
                // Test with a timeout
                var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(10));
                var task = Task.Run(async () => await client.ExecuteAsync("g.inject(42)"), cts.Token);
                
                var result = await task;
                var resultsList = result.ToList();
                
                Console.WriteLine($"? SUCCESS!");
                Console.WriteLine($"   Results: {resultsList.Count} items");
                Console.WriteLine($"   Values: {string.Join(", ", resultsList)}");
                
                // Test a few more queries
                var tests = new[]
                {
                    "g.inject(1, 2, 3)",
                    "1 + 1",
                    "g.V().limit(0).count()"
                };

                foreach (var testQuery in tests)
                {
                    try
                    {
                        Console.WriteLine($"\n   Testing: {testQuery}");
                        var testResult = await client.ExecuteAsync(testQuery);
                        var testList = testResult.ToList();
                        Console.WriteLine($"   ? Success: {testList.Count} results - {string.Join(", ", testList)}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"   ? Failed: {ex.Message}");
                    }
                }
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("? Query timed out - binary message handling may still have issues");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"? Query failed: {ex.GetType().FullName}: {ex.Message}");
            }

            client?.Dispose();
        }
        finally
        {
            await GremlinDatabase.StopAsync(server);
            Console.WriteLine("\nServer stopped");
        }

        Console.WriteLine("\n=== Test Complete ===");
        Console.WriteLine("If you see SUCCESS above, the binary message fix is working!");
        Console.WriteLine("If you see timeouts or failures, more work is needed on the binary message handling.");
    }
}