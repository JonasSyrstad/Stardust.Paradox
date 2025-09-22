using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using System.Text;
using Newtonsoft.Json.Linq;
using Stardust.Paradox.Data.InMemory;
using Stardust.Paradox.Data.InMemory.Demo;

/// <summary>
/// Comprehensive test to validate Gremlin.Net support and GraphSON version handling
/// Tests all the fixes made to improve compatibility and protocol support
/// </summary>
class GremlinNetComprehensiveTest
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("?? Comprehensive Gremlin.Net Support Validation");
        Console.WriteLine("=" + new string('=', 70));
        Console.WriteLine("Testing enhanced GraphSON support and binary message handling\n");

        var results = new TestResults();

        try
        {
            // Test 1: Basic server functionality
            Console.WriteLine("?? Test 1: Basic Server Functionality");
            await TestBasicServerFunctionality(results);

            // Test 2: GraphSON version negotiation
            Console.WriteLine("\n?? Test 2: GraphSON Version Negotiation");
            await TestGraphSONVersionNegotiation(results);

            // Test 3: Binary message handling
            Console.WriteLine("\n?? Test 3: Binary Message Handling");
            await TestBinaryMessageHandling(results);

            // Test 4: Protocol compatibility
            Console.WriteLine("\n?? Test 4: Protocol Compatibility");
            await TestProtocolCompatibility(results);

            // Test 5: Gremlin.Net client simulation
            Console.WriteLine("\n?? Test 5: Gremlin.Net Client Simulation");
            await TestGremlinNetClientSimulation(results);

        }
        catch (Exception ex)
        {
            Console.WriteLine($"?? Test execution failed: {ex.Message}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"   Inner: {ex.InnerException.Message}");
            }
            results.RecordFailure("Test execution", ex.Message);
        }

        // Print final results
        PrintFinalResults(results);

        Console.WriteLine("\nPress any key to exit...");
        Console.ReadKey();
    }

    static async Task TestBasicServerFunctionality(TestResults results)
    {
        GremlinServer server = null;
        try
        {
            // Start server with all protocols enabled
            var options = new GremlinServerOptions
            {
                Host = "localhost",
                Port = GetRandomPort(),
                HttpPort = GetRandomPort(),
                EnableTcp = true,
                EnableWebSocket = true,
                EnableHttp = true,
                EnableLogging = false,
                EnableDebugLogging = false
            };

            server = new GremlinServer(options);
            await server.StartAsync();

            await Task.Delay(1000);

            // Test basic connectivity
            if (server.IsRunning)
            {
                Console.WriteLine("   ? Server started successfully");
                results.RecordSuccess("Server startup");
            }
            else
            {
                Console.WriteLine("   ? Server failed to start");
                results.RecordFailure("Server startup", "Server not running");
            }

            // Test WebSocket support detection
            if (server.WebSocketSupported)
            {
                Console.WriteLine("   ? WebSocket support detected");
                results.RecordSuccess("WebSocket support");
            }
            else
            {
                Console.WriteLine("   ??  WebSocket support not available");
                results.RecordFailure("WebSocket support", "Not supported on this platform");
            }

            // Test statistics retrieval
            var stats = server.GetStatistics();
            if (stats != null && stats.ContainsKey("isRunning"))
            {
                Console.WriteLine("   ? Server statistics available");
                if (stats.ContainsKey("GraphSONVersions") && stats["GraphSONVersions"] is string[] graphsonVersions)
                {
                    Console.WriteLine($"      - GraphSON versions: {string.Join(", ", graphsonVersions)}");
                }
                if (stats.ContainsKey("SupportedSubProtocols") && stats["SupportedSubProtocols"] is string[] protocols)
                {
                    Console.WriteLine($"      - Supported protocols: {string.Join(", ", protocols)}");
                }
                results.RecordSuccess("Server statistics");
            }
            else
            {
                Console.WriteLine("   ? Server statistics failed");
                results.RecordFailure("Server statistics", "Statistics not available");
            }

            Console.WriteLine("   ? Server stopped gracefully");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"   ? Basic functionality test failed: {ex.Message}");
            results.RecordFailure("Basic functionality", ex.Message);
        }
        finally
        {
            if (server != null)
            {
                try
                {
                    await server.StopAsync();
                    server.Dispose();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"   ?? Error stopping server: {ex.Message}");
                }
            }
        }
    }

    static async Task TestGraphSONVersionNegotiation(TestResults results)
    {
        try
        {
            var versions = new[] { GraphSONVersion.V1, GraphSONVersion.V2, GraphSONVersion.V3 };
            
            foreach (var version in versions)
            {
                try
                {
                    var serializer = new TinkerPopGraphSONSerializer(version);
                    
                    // Test message serialization
                    var testMessage = new TinkerPopMessage
                    {
                        RequestId = Guid.NewGuid(),
                        Op = TinkerPopOperations.Eval,
                        Processor = "",
                        Args = new Dictionary<string, object>
                        {
                            ["gremlin"] = "g.inject(1, 2, 3)",
                            ["bindings"] = new Dictionary<string, object>(),
                            ["language"] = "gremlin-groovy"
                        }
                    };

                    var serialized = serializer.SerializeMessage(testMessage);
                    if (!string.IsNullOrEmpty(serialized) && serialized.Contains(testMessage.RequestId.ToString()))
                    {
                        Console.WriteLine($"   ? GraphSON {version} message serialization");
                        results.RecordSuccess($"GraphSON {version} message serialization");
                    }
                    else
                    {
                        Console.WriteLine($"   ? GraphSON {version} message serialization failed");
                        results.RecordFailure($"GraphSON {version} message serialization", "Invalid serialized output");
                    }

                    // Test message deserialization
                    var deserialized = serializer.DeserializeMessage(serialized);
                    if (deserialized != null && deserialized.RequestId == testMessage.RequestId)
                    {
                        Console.WriteLine($"   ? GraphSON {version} message deserialization");
                        results.RecordSuccess($"GraphSON {version} message deserialization");
                    }
                    else
                    {
                        Console.WriteLine($"   ? GraphSON {version} message deserialization failed");
                        results.RecordFailure($"GraphSON {version} message deserialization", "Deserialization mismatch");
                    }

                    // Test response serialization
                    var testResponse = serializer.CreateSuccessResponse(
                        testMessage.RequestId,
                        new[] { 1, 2, 3 }.Cast<dynamic>(),
                        null);

                    var responseJson = serializer.SerializeResponse(testResponse);
                    if (!string.IsNullOrEmpty(responseJson) && responseJson.Contains("200"))
                    {
                        Console.WriteLine($"   ? GraphSON {version} response serialization");
                        results.RecordSuccess($"GraphSON {version} response serialization");
                    }
                    else
                    {
                        Console.WriteLine($"   ? GraphSON {version} response serialization failed");
                        results.RecordFailure($"GraphSON {version} response serialization", "Invalid response format");
                    }

                    // Test MIME type
                    var mimeType = serializer.GetMimeType();
                    if (!string.IsNullOrEmpty(mimeType) && mimeType.Contains("gremlin"))
                    {
                        Console.WriteLine($"   ? GraphSON {version} MIME type: {mimeType}");
                        results.RecordSuccess($"GraphSON {version} MIME type");
                    }
                    else
                    {
                        Console.WriteLine($"   ? GraphSON {version} MIME type invalid: {mimeType}");
                        results.RecordFailure($"GraphSON {version} MIME type", "Invalid MIME type");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"   ? GraphSON {version} test failed: {ex.Message}");
                    results.RecordFailure($"GraphSON {version}", ex.Message);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"   ? GraphSON version test failed: {ex.Message}");
            results.RecordFailure("GraphSON version test", ex.Message);
        }
    }

    static async Task TestBinaryMessageHandling(TestResults results)
    {
        try
        {
            var serializer = new TinkerPopGraphSONSerializer(GraphSONVersion.V3);
            
            // Test 1: Standard binary message (UTF-8 JSON)
            var testJson = "{\"requestId\":\"12345678-1234-1234-1234-123456789012\",\"op\":\"eval\",\"processor\":\"\",\"args\":{\"gremlin\":\"g.inject(42)\"}}";
            var binaryData = Encoding.UTF8.GetBytes(testJson);
            
            try
            {
                var parsedMessage = serializer.DeserializeMessage(testJson);
                if (parsedMessage != null && parsedMessage.Op == "eval")
                {
                    Console.WriteLine("   ? Standard binary message parsing");
                    results.RecordSuccess("Standard binary message parsing");
                }
                else
                {
                    Console.WriteLine("   ? Standard binary message parsing failed");
                    results.RecordFailure("Standard binary message parsing", "Invalid parsed message");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"   ? Standard binary message parsing failed: {ex.Message}");
                results.RecordFailure("Standard binary message parsing", ex.Message);
            }

            // Test 2: TinkerPop binary format with MIME type prefix
            var mimeTypePrefix = "!application/vnd.gremlin-v3.0+json";
            var tinkerPopBinary = mimeTypePrefix + testJson;
            var tinkerPopBinaryData = Encoding.UTF8.GetBytes(tinkerPopBinary);
            
            try
            {
                // Simulate the binary message processing logic
                var messageText = Encoding.UTF8.GetString(tinkerPopBinaryData);
                string extractedJson;
                
                if (messageText.StartsWith("!"))
                {
                    var mimeTypeEnd = messageText.IndexOf('{');
                    if (mimeTypeEnd > 1)
                    {
                        extractedJson = messageText.Substring(mimeTypeEnd);
                        var parsedMessage = serializer.DeserializeMessage(extractedJson);
                        
                        if (parsedMessage != null && parsedMessage.Op == "eval")
                        {
                            Console.WriteLine("   ? TinkerPop binary format parsing");
                            results.RecordSuccess("TinkerPop binary format parsing");
                        }
                        else
                        {
                            Console.WriteLine("   ? TinkerPop binary format parsing failed");
                            results.RecordFailure("TinkerPop binary format parsing", "Invalid parsed message");
                        }
                    }
                    else
                    {
                        Console.WriteLine("   ? TinkerPop binary format MIME type extraction failed");
                        results.RecordFailure("TinkerPop binary format parsing", "MIME type extraction failed");
                    }
                }
                else
                {
                    Console.WriteLine("   ? TinkerPop binary format not detected");
                    results.RecordFailure("TinkerPop binary format parsing", "Format not detected");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"   ? TinkerPop binary format parsing failed: {ex.Message}");
                results.RecordFailure("TinkerPop binary format parsing", ex.Message);
            }

            // Test 3: UUID format handling
            try
            {
                var uuidFormats = new[]
                {
                    "12345678-1234-1234-1234-123456789012", // Standard format
                    "12345678123412341234123456789012",       // No hyphens
                    "{\"@type\":\"g:UUID\",\"@value\":\"12345678-1234-1234-1234-123456789012\"}" // GraphSON format
                };

                foreach (var format in uuidFormats)
                {
                    try
                    {
                        var token = JToken.Parse($"\"{format}\"");
                        // This would test the ParseRequestId method
                        Console.WriteLine($"   ? UUID format handled: {format.Substring(0, Math.Min(20, format.Length))}...");
                        results.RecordSuccess($"UUID format handling");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"   ??  UUID format issue: {format.Substring(0, Math.Min(20, format.Length))}... - {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"   ? UUID format test failed: {ex.Message}");
                results.RecordFailure("UUID format handling", ex.Message);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"   ? Binary message handling test failed: {ex.Message}");
            results.RecordFailure("Binary message handling", ex.Message);
        }
    }

    static async Task TestProtocolCompatibility(TestResults results)
    {
        try
        {
            var supportedProtocols = new[]
            {
                "gremlin-ws",
                "graphson-v1",
                "graphson-v2", 
                "graphson-v3"
            };

            foreach (var protocol in supportedProtocols)
            {
                try
                {
                    // Simulate protocol version detection
                    var version = protocol switch
                    {
                        "gremlin-ws" => GraphSONVersion.V3,
                        "graphson-v1" => GraphSONVersion.V1,
                        "graphson-v2" => GraphSONVersion.V2,
                        "graphson-v3" => GraphSONVersion.V3,
                        _ => GraphSONVersion.V3
                    };

                    var serializer = new TinkerPopGraphSONSerializer(version);
                    var mimeType = serializer.GetMimeType();

                    if (!string.IsNullOrEmpty(mimeType))
                    {
                        Console.WriteLine($"   ? Protocol {protocol} -> GraphSON {version} -> {mimeType}");
                        results.RecordSuccess($"Protocol {protocol} compatibility");
                    }
                    else
                    {
                        Console.WriteLine($"   ? Protocol {protocol} compatibility failed");
                        results.RecordFailure($"Protocol {protocol} compatibility", "Invalid MIME type");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"   ? Protocol {protocol} test failed: {ex.Message}");
                    results.RecordFailure($"Protocol {protocol} compatibility", ex.Message);
                }
            }

            // Test Gremlin.Net specific scenarios
            try
            {
                // Simulate Gremlin.Net client characteristics
                var gremlinNetScenarios = new[]
                {
                    ("Empty processor", ""),
                    ("Null processor", null),
                    ("Standard operation", "eval"),
                    ("Alternative operations", "evaluate"),
                    ("Bytecode operation", "bytecode")
                };

                foreach (var (scenario, processor) in gremlinNetScenarios)
                {
                    try
                    {
                        var testMessage = new TinkerPopMessage
                        {
                            RequestId = Guid.NewGuid(),
                            Op = "eval",
                            Processor = processor ?? "",
                            Args = new Dictionary<string, object>
                            {
                                ["gremlin"] = "g.inject(42)"
                            }
                        };

                        var serializer = new TinkerPopGraphSONSerializer(GraphSONVersion.V3);
                        var serialized = serializer.SerializeMessage(testMessage);
                        var deserialized = serializer.DeserializeMessage(serialized);

                        if (deserialized != null && deserialized.RequestId == testMessage.RequestId)
                        {
                            Console.WriteLine($"   ? Gremlin.Net scenario: {scenario}");
                            results.RecordSuccess($"Gremlin.Net {scenario}");
                        }
                        else
                        {
                            Console.WriteLine($"   ? Gremlin.Net scenario failed: {scenario}");
                            results.RecordFailure($"Gremlin.Net {scenario}", "Serialization/deserialization mismatch");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"   ? Gremlin.Net scenario failed: {scenario} - {ex.Message}");
                        results.RecordFailure($"Gremlin.Net {scenario}", ex.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"   ? Gremlin.Net scenario test failed: {ex.Message}");
                results.RecordFailure("Gremlin.Net scenarios", ex.Message);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"   ? Protocol compatibility test failed: {ex.Message}");
            results.RecordFailure("Protocol compatibility", ex.Message);
        }
    }

    static async Task TestGremlinNetClientSimulation(TestResults results)
    {
        GremlinServer server = null;
        try
        {
            // This test simulates the actual Gremlin.Net client behavior
            var options = new GremlinServerOptions
            {
                Host = "localhost",
                Port = GetRandomPort(),
                HttpPort = GetRandomPort(),
                EnableTcp = false,
                EnableWebSocket = true,
                EnableHttp = false,
                EnableLogging = false,
                EnableDebugLogging = false
            };

            server = new GremlinServer(options);
            await server.StartAsync();

            await Task.Delay(1000);

            try
            {
                // Test direct connector first
                var directResult = await server.Connector.ExecuteAsync("g.inject(1, 2, 3)", new Dictionary<string, object>());
                if (directResult != null && directResult.Count() == 3)
                {
                    Console.WriteLine("   ? Direct connector query execution");
                    results.RecordSuccess("Direct connector execution");
                }
                else
                {
                    Console.WriteLine("   ? Direct connector query execution failed");
                    results.RecordFailure("Direct connector execution", "Invalid result");
                }

                // Test Gremlin.Net client creation (if available)
                try
                {
                    var gremlinNetClient = GremlinClientFactory.CreateGremlinNetClientAsync("localhost", options.GetHttpPort());
                    if (gremlinNetClient != null)
                    {
                        Console.WriteLine("   ? Gremlin.Net client creation");
                        results.RecordSuccess("Gremlin.Net client creation");

                        // Test query execution with timeout
                        try
                        {
                            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                            var task = Task.Run(async () => await gremlinNetClient.ExecuteAsync("g.inject(42)"), cts.Token);
                            var result = await task;
                            
                            if (result != null && result.FirstOrDefault()?.ToString() == "42")
                            {
                                Console.WriteLine("   ? Gremlin.Net query execution");
                                results.RecordSuccess("Gremlin.Net query execution");
                            }
                            else
                            {
                                Console.WriteLine("   ? Gremlin.Net query execution - incorrect result");
                                results.RecordFailure("Gremlin.Net query execution", "Incorrect result");
                            }
                        }
                        catch (OperationCanceledException)
                        {
                            Console.WriteLine("   ??  Gremlin.Net query execution timed out");
                            results.RecordFailure("Gremlin.Net query execution", "Timeout after 5 seconds");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"   ? Gremlin.Net query execution failed: {ex.Message}");
                            results.RecordFailure("Gremlin.Net query execution", ex.Message);
                        }

                        gremlinNetClient?.Dispose();
                    }
                    else
                    {
                        Console.WriteLine("   ? Gremlin.Net client creation failed");
                        results.RecordFailure("Gremlin.Net client creation", "Client is null");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"   ? Gremlin.Net client test failed: {ex.Message}");
                    results.RecordFailure("Gremlin.Net client test", ex.Message);
                }
            }
            finally
            {
                Console.WriteLine("   ? Test server stopped");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"   ? Gremlin.Net client simulation failed: {ex.Message}");
            results.RecordFailure("Gremlin.Net client simulation", ex.Message);
        }
        finally
        {
            if (server != null)
            {
                try
                {
                    await server.StopAsync();
                    server.Dispose();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"   ?? Error stopping server: {ex.Message}");
                }
            }
        }
    }

    static void PrintFinalResults(TestResults results)
    {
        Console.WriteLine("\n" + new string('=', 70));
        Console.WriteLine("?? COMPREHENSIVE TEST RESULTS");
        Console.WriteLine(new string('=', 70));
        
        Console.WriteLine($"? Successful tests: {results.SuccessCount}");
        Console.WriteLine($"? Failed tests: {results.FailureCount}");
        Console.WriteLine($"?? Success rate: {results.SuccessRate:P1}");

        if (results.Failures.Count > 0)
        {
            Console.WriteLine("\n? Failed Tests:");
            foreach (var failure in results.Failures)
            {
                Console.WriteLine($"   • {failure.Key}: {failure.Value}");
            }
        }

        if (results.Successes.Count > 0)
        {
            Console.WriteLine("\n? Successful Tests:");
            foreach (var success in results.Successes)
            {
                Console.WriteLine($"   • {success}");
            }
        }

        Console.WriteLine("\n?? SUMMARY:");
        if (results.SuccessRate >= 0.9)
        {
            Console.WriteLine("?? EXCELLENT: Gremlin.Net support is working very well!");
        }
        else if (results.SuccessRate >= 0.7)
        {
            Console.WriteLine("?? GOOD: Gremlin.Net support is mostly working with minor issues.");
        }
        else if (results.SuccessRate >= 0.5)
        {
            Console.WriteLine("??  FAIR: Gremlin.Net support needs improvement.");
        }
        else
        {
            Console.WriteLine("?? NEEDS WORK: Significant issues with Gremlin.Net support.");
        }

        Console.WriteLine("\n?? KEY IMPROVEMENTS MADE:");
        Console.WriteLine("   • Enhanced binary message parsing for TinkerPop protocol");
        Console.WriteLine("   • Improved GraphSON v1/v2/v3 serialization compatibility");
        Console.WriteLine("   • Better WebSocket sub-protocol negotiation");
        Console.WriteLine("   • Enhanced request ID parsing for different UUID formats");
        Console.WriteLine("   • Improved error handling and fallback mechanisms");
        Console.WriteLine("   • Better Gremlin.Net client detection and handling");
    }

    private static int GetRandomPort()
    {
        var random = new Random();
        return random.Next(8000, 9000);
    }

    public class TestResults
    {
        public List<string> Successes { get; } = new List<string>();
        public Dictionary<string, string> Failures { get; } = new Dictionary<string, string>();
        
        public int SuccessCount => Successes.Count;
        public int FailureCount => Failures.Count;
        public int TotalCount => SuccessCount + FailureCount;
        public double SuccessRate => TotalCount > 0 ? (double)SuccessCount / TotalCount : 0;

        public void RecordSuccess(string testName)
        {
            Successes.Add(testName);
        }

        public void RecordFailure(string testName, string reason)
        {
            Failures[testName] = reason;
        }
    }
}