using System;
using System.Linq;
using System.Threading.Tasks;
using Stardust.Paradox.Data.InMemory;
using System.Collections.Generic;

namespace GremlinNetComprehensiveTestApp
{
    class DebugErrorHandlingTest
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("?? DEBUG ERROR HANDLING TEST");
            Console.WriteLine("=" + new string('=', 50));
            
            var options = new GremlinServerOptions
            {
                Host = "localhost",
                Port = 8765,
                HttpPort = 8766,
                EnableTcp = true,
                EnableWebSocket = true,
                EnableHttp = true,
                EnableLogging = false,
                EnableDebugLogging = false
            };

            var server = new GremlinServer(options);
            await server.StartAsync();
            await Task.Delay(1000);

            var errorTests = new List<(string query, bool expectError, string description)>
            {
                ("g.inject(42)", false, "Valid query should succeed"),
                ("invalid_syntax_error", true, "Invalid syntax should error"),
                ("g.nonExistentMethod()", true, "Non-existent method should error"),
                ("g.inject().invalidChain()", true, "Invalid method chain should error"),
                ("", true, "Empty query should error"),
                ("null", true, "Null query should error")
            };

            var correctErrorHandling = 0;
            foreach (var test in errorTests)
            {
                Console.WriteLine($"\nTesting: {test.description}");
                Console.WriteLine($"Query: '{test.query}'");
                Console.WriteLine($"Expect error: {test.expectError}");
                
                try
                {
                    var result = await server.Connector.ExecuteAsync(test.query, new Dictionary<string, object>());
                    
                    Console.WriteLine($"Result: {(result == null ? "null" : $"Count={result.Count()}")}");
                    
                    if (!test.expectError && result != null)
                    {
                        correctErrorHandling++;
                        Console.WriteLine("? PASSED - Expected success, got result");
                    }
                    else if (test.expectError)
                    {
                        Console.WriteLine("? FAILED - Expected error but got result");
                    }
                    else
                    {
                        Console.WriteLine("? FAILED - Got null result unexpectedly");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Exception: {ex.GetType().Name}: {ex.Message}");
                    
                    if (test.expectError)
                    {
                        correctErrorHandling++;
                        Console.WriteLine("? PASSED - Expected error, got exception");
                    }
                    else
                    {
                        Console.WriteLine("? FAILED - Unexpected error");
                    }
                }
            }

            var errorHandlingRate = (double)correctErrorHandling / errorTests.Count;
            Console.WriteLine($"\nFinal: {correctErrorHandling}/{errorTests.Count} tests passed ({errorHandlingRate:P0})");
            Console.WriteLine($"Required: 85% ({0.85:P0})");
            Console.WriteLine($"Result: {(errorHandlingRate >= 0.85 ? "PASS" : "FAIL")}");

            await server.StopAsync();
            server.Dispose();
        }
    }
}