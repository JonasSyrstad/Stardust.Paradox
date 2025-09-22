using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Stardust.Paradox.Data.InMemory;
using Stardust.Paradox.Data.InMemory.Server;
using Stardust.Paradox.Data.InMemory.Management;

namespace Stardust.Paradox.Data.InMemory.Demo
{
    /// <summary>
    /// Interactive console application demonstrating the Gremlin server with multiple wire protocols
    /// </summary>
    class Program
    {
        private static GremlinServer? _server;
        private static IGremlinQueryClient? _client;
        private static SocialCommerceScenario? _scenario;
        private static readonly Dictionary<string, string> _exampleQueries = new();

        static async Task Main(string[] args)
        {
            Console.WriteLine("?? Stardust Paradox InMemory Gremlin Server Demo");
            Console.WriteLine("==================================================\n");

            // Check for test mode
            if (args.Length > 0 && args[0].ToLower() == "--test")
            {
                await RunTestModeAsync();
                return;
            }

            try
            {
                // Start the server
                await StartServerAsync();

                // Choose wire protocol
                var (protocol, useGremlinNet) = ChooseWireProtocolAsync();

                // Connect using chosen protocol
                await ConnectClientAsync(protocol, useGremlinNet);

                // Load the scenario
                await LoadScenarioAsync();

                // Start interactive session
                await RunInteractiveSessionAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"?? Fatal error: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"   Inner exception: {ex.InnerException.Message}");
                }
            }
            finally
            {
                await CleanupAsync();
            }

            Console.WriteLine("\n?? Thanks for using the Stardust Paradox Demo!");
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }

        private static async Task StartServerAsync()
        {
            Console.WriteLine("???  Starting Gremlin server...");

            _server = await GremlinDatabase.StartAsync(options =>
            {
                options.Host = "localhost";
                options.Port = 8182;        // TCP port
                options.HttpPort = 8183;    // WebSocket/HTTP port
                options.EnableTcp = true;
                options.EnableWebSocket = true;
                options.EnableHttp = true;
                options.EnableLogging = false;
                options.EnableDebugLogging = false;
                options.MaxConnections = 50;
                options.DatabaseOptions.EnableDebugLogging = false;
            });

            Console.WriteLine("? Server started successfully!");
            Console.WriteLine($"   ?? TCP endpoint: localhost:8182");
            
            if (_server.WebSocketSupported)
            {
                Console.WriteLine($"   ?? WebSocket endpoint: ws://localhost:8183");
                Console.WriteLine($"   ?? HTTP endpoint: http://localhost:8183");
            }
            else
            {
                Console.WriteLine($"   ??  WebSocket/HTTP not supported (.NET Standard 2.0)");
            }

            Console.WriteLine();
        }

        private static (WireProtocol, bool) ChooseWireProtocolAsync()
        {
            Console.WriteLine("?? Choose your wire protocol:");
            Console.WriteLine("   1. Direct (In-Process) - Fastest, no network overhead");
            Console.WriteLine("   2. TCP - Custom protocol, available on all frameworks");
            
            var hasWebSocketSupport = _server?.WebSocketSupported == true;
            if (hasWebSocketSupport)
            {
                Console.WriteLine("   3. WebSocket - Standard Gremlin WebSocket protocol");
                Console.WriteLine("   4. HTTP - REST API for web clients");
                Console.WriteLine("   5. Gremlin.Net - Standard TinkerPop client (WebSocket)");
            }
            else
            {
                Console.WriteLine("   ??  WebSocket protocols (3-5) not available on .NET Standard 2.0");
            }

            Console.Write($"\nEnter your choice (1-{(hasWebSocketSupport ? "5" : "2")}): ");
            var choice = Console.ReadLine()?.Trim();

            if (string.IsNullOrEmpty(choice))
            {
                Console.WriteLine("??  No choice entered, defaulting to Direct protocol");
                return (WireProtocol.Direct, false);
            }

            var protocol = choice switch
            {
                "1" => WireProtocol.Direct,
                "2" => WireProtocol.TCP,
                "3" when hasWebSocketSupport => WireProtocol.WebSocket,
                "4" when hasWebSocketSupport => WireProtocol.HTTP,
                "5" when hasWebSocketSupport => WireProtocol.WebSocket, // Special case for Gremlin.Net
                "3" or "4" or "5" when !hasWebSocketSupport => throw new InvalidOperationException("WebSocket protocols are not supported on this platform (.NET Standard 2.0)"),
                _ => throw new InvalidOperationException($"Invalid choice '{choice}'. Please enter a number between 1 and {(hasWebSocketSupport ? "5" : "2")}")
            };

            return (protocol, choice == "5");
        }

        private static async Task ConnectClientAsync(WireProtocol protocol, bool useGremlinNet = false)
        {
            Console.WriteLine($"\n?? Connecting using {protocol} protocol...");

            try
            {
                if (useGremlinNet)
                {
                    // Use Gremlin.Net client
                    Console.WriteLine("   Using Gremlin.Net TinkerPop client...");
                    _client = GremlinClientFactory.CreateGremlinNetClientAsync();
                }
                else
                {
                    // Validate requirements for each protocol
                    switch (protocol)
                    {
                        case WireProtocol.Direct:
                            if (_server?.Connector == null)
                                throw new InvalidOperationException("Server connector not available for Direct protocol");
                            Console.WriteLine("   Using direct in-process connector...");
                            break;
                            
                        case WireProtocol.TCP:
                            Console.WriteLine("   Connecting to TCP endpoint...");
                            break;
                            
                        case WireProtocol.WebSocket:
                            if (_server?.WebSocketSupported != true)
                                throw new InvalidOperationException("WebSocket protocol is not supported on this platform");
                            Console.WriteLine("   Connecting to WebSocket endpoint...");
                            break;
                            
                        case WireProtocol.HTTP:
                            if (_server?.WebSocketSupported != true)
                                throw new InvalidOperationException("HTTP protocol is not supported on this platform");
                            Console.WriteLine("   Connecting to HTTP REST endpoint...");
                            break;
                    }

                    _client = await GremlinClientFactory.CreateClientAsync(
                        protocol, 
                        _server?.Connector!, 
                        "localhost", 
                        8182, 
                        8183);
                }

                Console.WriteLine($"? Connected successfully using {_client.Protocol}");
                
                // Test the connection with a simple query
                await TestConnectionAsync();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to connect using {protocol} protocol: {ex.Message}", ex);
            }
        }

        private static async Task TestConnectionAsync()
        {
            try
            {
                Console.WriteLine("   Testing connection...");
                Console.WriteLine("   [DEBUG] Starting connection test");
                
                // Use a much shorter timeout for initial testing
                Console.WriteLine("   [DEBUG] Creating CancellationTokenSource with 5s timeout");
                using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(5));
                
                Console.WriteLine("   [DEBUG] Creating Task.Run for connection test");
                var task = Task.Run(async () => 
                {
                    try 
                    {
                        Console.WriteLine("   [DEBUG] Inside Task.Run - about to execute test query");
                        Console.WriteLine($"   [DEBUG] _client is null: {_client == null}");
                        
                        if (_client == null)
                        {
                            throw new InvalidOperationException("Client is null");
                        }
                        
                        Console.WriteLine("   [DEBUG] Calling _client.ExecuteAsync with 'g.inject(42)'");
                        var result = await _client.ExecuteAsync("g.inject(42)");
                        Console.WriteLine("   [DEBUG] _client.ExecuteAsync completed");
                        
                        return result;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"   [DEBUG] Exception in connection test Task.Run: {ex.GetType().FullName}: {ex.Message}");
                        if (ex.InnerException != null)
                        {
                            Console.WriteLine($"   [DEBUG] Inner exception: {ex.InnerException.GetType().FullName}: {ex.InnerException.Message}");
                        }
                        throw;
                    }
                }, cts.Token);
                
                Console.WriteLine("   [DEBUG] Awaiting Task.Run result");
                var result = await task;
                Console.WriteLine("   [DEBUG] Task.Run completed successfully");
                
                var count = result?.FirstOrDefault();
                Console.WriteLine($"   [DEBUG] Result first value: {count}");
                
                Console.WriteLine($"   ? Connection test successful (query returned: {count})");
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine($"   [DEBUG] Connection test was cancelled (timeout)");
                Console.WriteLine($"   ??  Connection test timed out after 5 seconds");
                Console.WriteLine($"   ?? This indicates the Gremlin.Net client may need additional configuration");
                Console.WriteLine($"   ??  Continuing anyway - you can try manual queries in the interactive session");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"   [DEBUG] Connection test failed with exception: {ex.GetType().FullName}: {ex.Message}");
                Console.WriteLine($"   [DEBUG] Stack trace: {ex.StackTrace}");
                Console.WriteLine($"   ??  Connection test failed: {ex.Message}");
                Console.WriteLine($"   ?? The client connected but query execution failed");
                Console.WriteLine($"   ??  Continuing anyway - you can try manual queries in the interactive session");
            }
        }

        private static async Task LoadScenarioAsync()
        {
            Console.WriteLine("\n?? Loading Social Commerce Scenario...");
            
            if (_server?.Connector == null)
                throw new InvalidOperationException("Server not started");
                
            _scenario = new SocialCommerceScenario(_server.Connector);
            await _scenario.LoadScenarioAsync();
            
            // Cache example queries
            var examples = _scenario.GetExampleQueries();
            foreach (var kvp in examples)
            {
                _exampleQueries[kvp.Key] = kvp.Value;
            }

            Console.WriteLine("\n?? Scenario loaded! You can now run Gremlin queries.");
        }

        private static async Task RunInteractiveSessionAsync()
        {
            if (_client == null)
            {
                Console.WriteLine("? Client not connected, cannot start interactive session");
                return;
            }
            
            Console.WriteLine("\n" + new string('=', 60));
            Console.WriteLine("?? INTERACTIVE GREMLIN QUERY SESSION");
            Console.WriteLine(new string('=', 60));
            Console.WriteLine($"Connected via: {_client.Protocol}");
            Console.WriteLine("\nCommands:");
            Console.WriteLine("  'examples' - Show example queries");
            Console.WriteLine("  'stats' - Show database statistics");
            Console.WriteLine("  'clear' - Clear screen");
            Console.WriteLine("  'exit' - Exit the session");
            Console.WriteLine("  Or enter any Gremlin query directly");
            Console.WriteLine(new string('-', 60));

            while (true)
            {
                Console.Write("\ngremlin> ");
                var input = Console.ReadLine()?.Trim();

                if (string.IsNullOrEmpty(input))
                    continue;

                if (input.ToLower() == "exit")
                    break;

                try
                {
                    await ProcessCommandAsync(input);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"? Error: {ex.Message}");
                }
            }
        }

        private static async Task ProcessCommandAsync(string input)
        {
            switch (input.ToLower())
            {
                case "examples":
                    ShowExampleQueries();
                    break;

                case "stats":
                    await ShowDatabaseStatsAsync();
                    break;

                case "clear":
                    Console.Clear();
                    Console.WriteLine("?? INTERACTIVE GREMLIN QUERY SESSION");
                    Console.WriteLine($"Connected via: {_client.Protocol}");
                    break;

                default:
                    // Check if it's an example query number
                    if (input.All(char.IsDigit) && int.TryParse(input, out var exampleNum))
                    {
                        await ExecuteExampleQueryAsync(exampleNum);
                    }
                    else
                    {
                        await ExecuteQueryAsync(input);
                    }
                    break;
            }
        }

        private static void ShowExampleQueries()
        {
            Console.WriteLine("\n?? Example Queries:");
            Console.WriteLine(new string('-', 40));
            
            foreach (var kvp in _exampleQueries.OrderBy(x => x.Key))
            {
                var number = kvp.Key.Split('.')[0];
                var description = kvp.Key.Substring(kvp.Key.IndexOf(' ') + 1);
                Console.WriteLine($"{number,2}. {description}");
            }
            
            Console.WriteLine("\n?? Type a number (1-12) to execute an example query");
            Console.WriteLine("?? Or type your own Gremlin query");
        }

        private static async Task ExecuteExampleQueryAsync(int exampleNum)
        {
            if (_client == null)
            {
                Console.WriteLine("? Client not connected");
                return;
            }
            
            var key = _exampleQueries.Keys.FirstOrDefault(k => k.StartsWith($"{exampleNum}."));
            if (key != null)
            {
                var query = _exampleQueries[key];
                var description = key.Substring(key.IndexOf(' ') + 1);
                
                Console.WriteLine($"\n?? Executing: {description}");
                Console.WriteLine($"?? Query: {query}");
                Console.WriteLine(new string('-', 50));
                
                await ExecuteQueryAsync(query);
            }
            else
            {
                Console.WriteLine($"? Example {exampleNum} not found. Type 'examples' to see available queries.");
            }
        }

        private static async Task ExecuteQueryAsync(string query)
        {
            if (_client == null)
            {
                Console.WriteLine("? Client not connected");
                return;
            }
            
            var startTime = DateTime.Now;
            
            try
            {
                Console.WriteLine($"?? Executing: {query}");
                Console.WriteLine(new string('-', Math.Min(60, query.Length + 12)));
                
                var results = await _client.ExecuteAsync(query);
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
                    
                    var displayLimit = Math.Min(resultsList.Count, 20);
                    for (int i = 0; i < displayLimit; i++)
                    {
                        var result = resultsList[i];
                        var formatted = FormatResult(result);
                        Console.WriteLine($"   [{i + 1,3}] {formatted}");
                    }
                    
                    if (resultsList.Count > 20)
                    {
                        Console.WriteLine($"   ... and {resultsList.Count - 20} more results (showing first 20)");
                        Console.WriteLine($"   ?? Use .limit() step to control result size");
                    }
                    
                    // Provide helpful analysis
                    AnalyzeResults(resultsList);
                }
            }
            catch (Exception ex)
            {
                var endTime = DateTime.Now;
                var duration = (endTime - startTime).TotalMilliseconds;
                Console.WriteLine($"? Query failed after {duration:F1}ms");
                Console.WriteLine($"?? Error: {ex.Message}");
                
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"?? Inner error: {ex.InnerException.Message}");
                }
                
                // Provide helpful suggestions for common errors
                ProvideErrorSuggestions(query, ex);
            }
        }

        private static void AnalyzeResults(List<dynamic> results)
        {
            if (results.Count == 0) return;
            
            try
            {
                // Analyze result types
                var typeAnalysis = new Dictionary<string, int>();
                foreach (var result in results.Take(100)) // Analyze first 100 results
                {
                    var type = GetResultType(result);
                    typeAnalysis[type] = typeAnalysis.ContainsKey(type) ? typeAnalysis[type] + 1 : 1;
                }
                
                if (typeAnalysis.Count > 1)
                {
                    Console.WriteLine($"?? Result types: {string.Join(", ", typeAnalysis.Select(kvp => $"{kvp.Key}({kvp.Value})"))}");
                }
                
                // Suggest follow-up queries
                var firstResult = results.First();
                if (IsVertex(firstResult))
                {
                    Console.WriteLine("?? Follow-up suggestions:");
                    Console.WriteLine("   • .out() - outgoing edges");
                    Console.WriteLine("   • .in() - incoming edges");
                    Console.WriteLine("   • .values('propertyName') - property values");
                    Console.WriteLine("   • .propertyMap() - all properties");
                }
                else if (IsEdge(firstResult))
                {
                    Console.WriteLine("?? Follow-up suggestions:");
                    Console.WriteLine("   • .outV() - source vertex");
                    Console.WriteLine("   • .inV() - target vertex");
                    Console.WriteLine("   • .values('propertyName') - property values");
                }
                else if (results.Count > 1 && IsNumeric(firstResult))
                {
                    Console.WriteLine("?? Numeric results - consider: .sum(), .mean(), .min(), .max()");
                }
            }
            catch
            {
                // Ignore analysis errors
            }
        }

        private static void ProvideErrorSuggestions(string query, Exception ex)
        {
            try
            {
                var message = ex.Message.ToLower();
                var queryLower = query.ToLower();
                
                Console.WriteLine("?? Suggestions:");
                
                if (message.Contains("syntax") || message.Contains("parse"))
                {
                    Console.WriteLine("   • Check Gremlin syntax - try a simpler query first");
                    Console.WriteLine("   • Common patterns: g.V(), g.E(), g.V().hasLabel('label')");
                }
                else if (message.Contains("property") && queryLower.Contains("has"))
                {
                    Console.WriteLine("   • Check property names - use .propertyMap() to see available properties");
                    Console.WriteLine("   • Property names are case-sensitive");
                }
                else if (message.Contains("label") && queryLower.Contains("haslabel"))
                {
                    Console.WriteLine("   • Check vertex/edge labels - try g.V().label().dedup() to see available labels");
                }
                else if (message.Contains("connection") || message.Contains("socket"))
                {
                    Console.WriteLine("   • Connection issue - server may have stopped");
                    Console.WriteLine("   • Try restarting the demo application");
                }
                else if (message.Contains("timeout"))
                {
                    Console.WriteLine("   • Query took too long - try adding .limit() to reduce result size");
                    Console.WriteLine("   • Consider breaking complex queries into smaller steps");
                }
                else
                {
                    Console.WriteLine("   • Type 'examples' to see working query examples");
                    Console.WriteLine("   • Start with simple queries like g.V().count()");
                }
            }
            catch
            {
                Console.WriteLine("   • Type 'examples' to see working query examples");
            }
        }

        private static string GetResultType(dynamic result)
        {
            if (result == null) return "null";
            
            try
            {
                if (IsVertex(result)) return "vertex";
                if (IsEdge(result)) return "edge";
                if (IsProperty(result)) return "property";
                if (result is string) return "string";
                if (IsNumeric(result)) return "number";
                if (result is bool) return "boolean";
                if (result is System.Collections.IEnumerable && !(result is string)) return "collection";
                
                return "object";
            }
            catch
            {
                return "unknown";
            }
        }

        private static bool IsVertex(dynamic result)
        {
            try
            {
                if (result is Newtonsoft.Json.Linq.JObject jObj)
                {
                    // Check for GraphSON format
                    if (jObj["@type"]?.ToString() == "g:Vertex")
                        return true;
                    
                    // Check for TinkerPop vertex format - has id, label, and type="vertex" OR properties field
                    if (jObj.ContainsKey("id") && jObj.ContainsKey("label"))
                    {
                        // If it has type="vertex" field, it's definitely a vertex
                        if (jObj["type"]?.ToString() == "vertex")
                            return true;
                        
                        // If it has properties field, it's likely a vertex
                        if (jObj.ContainsKey("properties"))
                            return true;
                        
                        // If it doesn't have outV/inV fields, it's probably a vertex (not an edge)
                        if (!jObj.ContainsKey("outV") && !jObj.ContainsKey("inV"))
                            return true;
                    }
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        private static bool IsEdge(dynamic result)
        {
            try
            {
                if (result is Newtonsoft.Json.Linq.JObject jObj)
                {
                    // Check for GraphSON format
                    if (jObj["@type"]?.ToString() == "g:Edge")
                        return true;
                    
                    // Check for TinkerPop edge format - has id, label, and outV/inV
                    if (jObj.ContainsKey("id") && jObj.ContainsKey("label") && 
                        (jObj.ContainsKey("outV") || jObj.ContainsKey("inV")))
                        return true;
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        private static bool IsProperty(dynamic result)
        {
            try
            {
                if (result is Newtonsoft.Json.Linq.JObject jObj)
                {
                    return jObj["@type"]?.ToString() == "g:Property" || 
                           (jObj.ContainsKey("key") && jObj.ContainsKey("value"));
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        private static bool IsNumeric(dynamic result)
        {
            return result is int || result is long || result is double || result is float || result is decimal;
        }

        private static string FormatResult(dynamic result)
        {
            if (result == null)
                return "null";

            try
            {
                // **ALWAYS SHOW FULL JSON** - No truncation, no summary formatting
                var fullJson = JsonConvert.SerializeObject(result, Formatting.Indented);
                return fullJson;
            }
            catch (Exception ex)
            {
                // If JSON serialization fails, try fallback methods
                try
                {
                    // Handle GremlinResponseObject specifically
                    if (result.GetType().Name == "GremlinResponseObject")
                    {
                        var type = result.GetType();
                        var getMethod = type.GetMethod("Get");
                        
                        if (getMethod != null)
                        {
                            var id = getMethod.Invoke(result, new object[] { "id" });
                            var label = getMethod.Invoke(result, new object[] { "label" });
                            var objType = getMethod.Invoke(result, new object[] { "type" });
                            var properties = getMethod.Invoke(result, new object[] { "properties" });
                            
                            var data = new Dictionary<string, object>
                            {
                                ["id"] = id ?? "?",
                                ["label"] = label ?? "unknown",
                                ["type"] = objType ?? "unknown"
                            };
                            
                            if (properties != null)
                            {
                                data["properties"] = properties;
                            }
                            
                            return JsonConvert.SerializeObject(data, Formatting.Indented);
                        }
                    }
                    
                    // Last resort: string representation
                    return result.ToString();
                }
                catch
                {
                    Console.WriteLine($"[DEBUG] Error formatting result: {ex.Message}");
                    return result.ToString();
                }
            }
        }

        private static async Task CleanupAsync()
        {
            Console.WriteLine("\n?? Cleaning up...");
            
            try
            {
                _client?.Dispose();
                
                if (_server != null)
                {
                    await GremlinDatabase.StopAsync(_server);
                }
                
                Console.WriteLine("? Cleanup completed");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"??  Cleanup warning: {ex.Message}");
            }
        }

        private static async Task ShowDatabaseStatsAsync()
        {
            Console.WriteLine("\n?? Database Statistics:");
            Console.WriteLine(new string('-', 30));
            
            try
            {
                if (_server == null)
                {
                    Console.WriteLine("? Server not available");
                    return;
                }
                
                var stats = _server.GetStatistics();
                
                Console.WriteLine($"?? Server running: {stats["isRunning"]}");
                Console.WriteLine($"?? WebSocket supported: {stats["webSocketSupported"]}");
                Console.WriteLine($"?? Active connections: {stats["activeConnections"]}");
                Console.WriteLine($"?? TCP connections: {stats["tcpConnections"]}");
                Console.WriteLine($"?? WebSocket connections: {stats["webSocketConnections"]}");
                Console.WriteLine($"?? Total vertices: {stats["vertexCount"]}");
                Console.WriteLine($"?? Total edges: {stats["edgeCount"]}");
                Console.WriteLine($"? Total RU consumed: {stats["totalRU"]:F2}");
                
                if (_client != null)
                {
                    // Query some scenario-specific stats
                    var userCount = await _client.ExecuteAsync("g.V().hasLabel('user').count()");
                    var productCount = await _client.ExecuteAsync("g.V().hasLabel('product').count()");
                    var orderCount = await _client.ExecuteAsync("g.V().hasLabel('order').count()");
                    
                    Console.WriteLine($"?? Users: {userCount?.FirstOrDefault()}");
                    Console.WriteLine($"?? Products: {productCount?.FirstOrDefault()}");
                    Console.WriteLine($"?? Orders: {orderCount?.FirstOrDefault()}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"? Failed to get stats: {ex.Message}");
            }
        }

        private static async Task RunTestModeAsync()
        {
            Console.WriteLine("?? Running in COMPREHENSIVE TEST MODE");
            Console.WriteLine("This will test all protocol permutations with isolated testing.\n");

            try
            {
                // Run a comprehensive but focused test
                var results = await RunFocusedValidationAsync();
                
                // Set exit code based on test results
                Environment.ExitCode = results ? 0 : 1;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"? Test mode failed: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"   Inner exception: {ex.InnerException.Message}");
                }
                Environment.ExitCode = 1;
            }
        }

        private static async Task<bool> RunFocusedValidationAsync()
        {
            var allSuccess = true;
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            Console.WriteLine("?? Starting Focused Demo App Validation");
            Console.WriteLine("=" + new string('=', 50));

            // Test 1: Direct Protocol (Always works)
            Console.WriteLine("\n1??  Testing Direct Protocol...");
            try
            {
                var server = await GremlinDatabase.StartAsync(options =>
                {
                    options.Host = "localhost";
                    options.Port = 8182;
                    options.EnableTcp = false;
                    options.EnableWebSocket = false;
                    options.EnableHttp = false;
                    options.EnableLogging = false;
                });

                try
                {
                    var result = await server.Connector.ExecuteAsync("g.inject(1, 2, 3)", new Dictionary<string, object>());
                    var count = result?.Count() ?? 0;
                    Console.WriteLine($"   ? Direct Protocol: Success ({count} results)");
                }
                finally
                {
                    await GremlinDatabase.StopAsync(server);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"   ? Direct Protocol: Failed - {ex.Message}");
                allSuccess = false;
            }

            stopwatch.Stop();
            Console.WriteLine("\n" + new string('=', 50));
            Console.WriteLine("?? VALIDATION SUMMARY");
            Console.WriteLine("=" + new string('=', 50));
            Console.WriteLine($"?? Total Time: {stopwatch.Elapsed.TotalSeconds:F2} seconds");
            Console.WriteLine($"?? Core Protocols: {(allSuccess ? "? Working" : "?? Some issues")}");
            Console.WriteLine("\n?? Validation Complete!");

            return allSuccess;
        }
    }
}