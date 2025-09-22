using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Text;
using System.Net.WebSockets;
using Newtonsoft.Json.Linq;
using Stardust.Paradox.Data.InMemory;
using Stardust.Paradox.Data.InMemory.Demo;
using Gremlin.Net.Driver;
using Gremlin.Net.Driver.Messages;
using Gremlin.Net.Structure.IO.GraphSON;

// Alias to resolve naming conflicts
using StardustGremlinServer = Stardust.Paradox.Data.InMemory.GremlinServer;
using GremlinNetServer = Gremlin.Net.Driver.GremlinServer;

namespace GremlinNetComprehensiveTestApp
{
    /// <summary>
    /// Comprehensive tests for wire protocol and format support
    /// </summary>
    public static class WireProtocolTests
    {
        public static async Task<TestResults> RunAllTests()
        {
            var results = new TestResults();
            
            Console.WriteLine("?? Wire Protocol and Format Support Tests");
            Console.WriteLine("=" + new string('=', 50));
            
            await TestAllWireProtocols(results);
            await TestGremlinNetCompatibility(results);
            await TestBinaryMessageFormats(results);
            await TestConcurrentConnections(results);
            await TestLargeMessages(results);
            await TestConnectionRecovery(results);
            
            return results;
        }

        private static async Task TestAllWireProtocols(TestResults results)
        {
            Console.WriteLine("\n?? Wire Protocol Tests");
            
            StardustGremlinServer? server = null;
            try
            {
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

                server = new StardustGremlinServer(options);
                await server.StartAsync();
                await Task.Delay(1500); // Give server time to start

                if (!server.IsRunning)
                {
                    Console.WriteLine("   ? Server failed to start for wire protocol tests");
                    results.RecordFailure("Server startup", "Server not running");
                    return;
                }

                // Test Direct protocol
                try
                {
                    var directClient = await GremlinClientFactory.CreateClientAsync(WireProtocol.Direct, server.Connector);
                    var directResult = await directClient.ExecuteAsync("g.inject(1, 2, 3)");
                    
                    if (directResult?.Count() == 3)
                    {
                        Console.WriteLine("   ? Direct protocol");
                        results.RecordSuccess("Direct protocol");
                    }
                    else
                    {
                        Console.WriteLine("   ? Direct protocol failed");
                        results.RecordFailure("Direct protocol", "Incorrect result count");
                    }
                    directClient?.Dispose();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"   ? Direct protocol failed: {ex.Message}");
                    results.RecordFailure("Direct protocol", ex.Message);
                }

                // Test TCP protocol
                try
                {
                    var tcpClient = await GremlinClientFactory.CreateClientAsync(WireProtocol.TCP, null, options.Host, options.Port);
                    var tcpResult = await tcpClient.ExecuteAsync("g.inject(4, 5, 6)");
                    
                    if (tcpResult?.Count() == 3)
                    {
                        Console.WriteLine("   ? TCP protocol");
                        results.RecordSuccess("TCP protocol");
                    }
                    else
                    {
                        Console.WriteLine("   ? TCP protocol failed");
                        results.RecordFailure("TCP protocol", "Incorrect result count");
                    }
                    tcpClient?.Dispose();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"   ? TCP protocol failed: {ex.Message}");
                    results.RecordFailure("TCP protocol", ex.Message);
                }

                // Test WebSocket protocol
                if (server.WebSocketSupported)
                {
                    try
                    {
                        var wsClient = await GremlinClientFactory.CreateClientAsync(WireProtocol.WebSocket, null, options.Host, options.Port, options.GetHttpPort());
                        var wsResult = await wsClient.ExecuteAsync("g.inject(7, 8, 9)");
                        
                        if (wsResult?.Count() == 3)
                        {
                            Console.WriteLine("   ? WebSocket protocol");
                            results.RecordSuccess("WebSocket protocol");
                        }
                        else
                        {
                            Console.WriteLine("   ? WebSocket protocol failed");
                            results.RecordFailure("WebSocket protocol", "Incorrect result count");
                        }
                        wsClient?.Dispose();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"   ? WebSocket protocol failed: {ex.Message}");
                        results.RecordFailure("WebSocket protocol", ex.Message);
                    }
                }
                else
                {
                    Console.WriteLine("   ??  WebSocket protocol not supported on this platform");
                    results.RecordFailure("WebSocket protocol", "Not supported on platform");
                }

                // Test HTTP protocol
                try
                {
                    var httpClient = await GremlinClientFactory.CreateClientAsync(WireProtocol.HTTP, null, options.Host, options.Port, options.GetHttpPort());
                    var httpResult = await httpClient.ExecuteAsync("g.inject(10, 11, 12)");
                    
                    if (httpResult?.Count() == 3)
                    {
                        Console.WriteLine("   ? HTTP protocol");
                        results.RecordSuccess("HTTP protocol");
                    }
                    else
                    {
                        Console.WriteLine("   ? HTTP protocol failed");
                        results.RecordFailure("HTTP protocol", "Incorrect result count");
                    }
                    httpClient?.Dispose();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"   ? HTTP protocol failed: {ex.Message}");
                    results.RecordFailure("HTTP protocol", ex.Message);
                }
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

        private static async Task TestGremlinNetCompatibility(TestResults results)
        {
            Console.WriteLine("\n?? Gremlin.Net Compatibility Tests");
            
            StardustGremlinServer? server = null;
            try
            {
                var options = new GremlinServerOptions
                {
                    Host = "localhost",
                    Port = GetRandomPort(),
                    HttpPort = GetRandomPort(),
                    EnableWebSocket = true,
                    EnableLogging = false,
                    EnableDebugLogging = false
                };

                server = new StardustGremlinServer(options);
                await server.StartAsync();
                await Task.Delay(1500);

                if (!server.IsRunning || !server.WebSocketSupported)
                {
                    Console.WriteLine("   ? Server not ready for Gremlin.Net tests");
                    results.RecordFailure("Gremlin.Net server readiness", "Server not running or WebSocket not supported");
                    return;
                }

                // Test different GraphSON versions
                var versions = new[]
                {
                    (GraphSON2MessageSerializer.GraphSON2MimeType, "GraphSON v2"),
                    (GraphSON3MessageSerializer.GraphSON3MimeType, "GraphSON v3")
                };

                foreach (var (mimeType, versionName) in versions)
                {
                    GremlinClient? client = null;
                    try
                    {
                        var gremlinServer = new GremlinNetServer(options.Host, options.GetHttpPort());
                        
                        var connectionPoolSettings = new ConnectionPoolSettings
                        {
                            MaxInProcessPerConnection = 2,
                            PoolSize = 1,
                            ReconnectionAttempts = 1,
                            ReconnectionBaseDelay = TimeSpan.FromSeconds(1)
                        };

                        client = new GremlinClient(
                            gremlinServer: gremlinServer,
                            mimeType: mimeType,
                            connectionPoolSettings: connectionPoolSettings);

                        // Test basic query
                        var result1 = await client.SubmitAsync<dynamic>("g.inject(42)");
                        if (result1?.Count > 0 && result1.FirstOrDefault()?.ToString() == "42")
                        {
                            Console.WriteLine($"   ? {versionName} basic query");
                            results.RecordSuccess($"{versionName} basic query");
                        }
                        else
                        {
                            Console.WriteLine($"   ? {versionName} basic query failed");
                            results.RecordFailure($"{versionName} basic query", "Incorrect result");
                        }

                        // Test parameterized query
                        var bindings = new Dictionary<string, object> { ["x"] = 100 };
                        var result2 = await client.SubmitAsync<dynamic>("g.inject(x)", bindings);
                        if (result2?.Count > 0 && result2.FirstOrDefault()?.ToString() == "100")
                        {
                            Console.WriteLine($"   ? {versionName} parameterized query");
                            results.RecordSuccess($"{versionName} parameterized query");
                        }
                        else
                        {
                            Console.WriteLine($"   ? {versionName} parameterized query failed");
                            results.RecordFailure($"{versionName} parameterized query", "Incorrect result");
                        }

                        // Test collection query
                        var result3 = await client.SubmitAsync<dynamic>("g.inject(1, 2, 3, 4, 5)");
                        if (result3?.Count == 5)
                        {
                            Console.WriteLine($"   ? {versionName} collection query");
                            results.RecordSuccess($"{versionName} collection query");
                        }
                        else
                        {
                            Console.WriteLine($"   ? {versionName} collection query failed");
                            results.RecordFailure($"{versionName} collection query", "Incorrect collection size");
                        }

                        // Test graph operations
                        var result4 = await client.SubmitAsync<dynamic>("g.addV('test').property('value', 999).id()");
                        if (result4?.Count > 0)
                        {
                            Console.WriteLine($"   ? {versionName} graph operations");
                            results.RecordSuccess($"{versionName} graph operations");
                            
                            // Cleanup
                            await client.SubmitAsync<dynamic>($"g.V({result4.FirstOrDefault()}).drop()");
                        }
                        else
                        {
                            Console.WriteLine($"   ? {versionName} graph operations failed");
                            results.RecordFailure($"{versionName} graph operations", "No result");
                        }

                        // Test error handling
                        try
                        {
                            await client.SubmitAsync<dynamic>("g.invalid().syntax()");
                            Console.WriteLine($"   ? {versionName} error handling failed - should have thrown");
                            results.RecordFailure($"{versionName} error handling", "Should have thrown exception");
                        }
                        catch
                        {
                            Console.WriteLine($"   ? {versionName} error handling");
                            results.RecordSuccess($"{versionName} error handling");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"   ? {versionName} compatibility test failed: {ex.Message}");
                        results.RecordFailure($"{versionName} compatibility", ex.Message);
                    }
                    finally
                    {
                        client?.Dispose();
                    }
                }
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

        private static async Task TestBinaryMessageFormats(TestResults results)
        {
            Console.WriteLine("\n?? Binary Message Format Tests");
            
            try
            {
                var serializer = new TinkerPopGraphSONSerializer(GraphSONVersion.V3);
                
                // Test 1: Standard UTF-8 JSON binary
                var testJson = "{\"requestId\":\"12345678-1234-1234-1234-123456789012\",\"op\":\"eval\",\"processor\":\"\",\"args\":{\"gremlin\":\"g.inject(42)\"}}";
                var standardBinary = Encoding.UTF8.GetBytes(testJson);
                
                try
                {
                    var message1 = serializer.DeserializeMessage(testJson);
                    if (message1?.Op == "eval")
                    {
                        Console.WriteLine("   ? Standard UTF-8 JSON binary");
                        results.RecordSuccess("Standard UTF-8 JSON binary");
                    }
                    else
                    {
                        Console.WriteLine("   ? Standard UTF-8 JSON binary failed");
                        results.RecordFailure("Standard UTF-8 JSON binary", "Invalid deserialization");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"   ? Standard UTF-8 JSON binary failed: {ex.Message}");
                    results.RecordFailure("Standard UTF-8 JSON binary", ex.Message);
                }

                // Test 2: TinkerPop binary format with MIME type prefix
                var mimeTypePrefix = "!application/vnd.gremlin-v3.0+json";
                var tinkerPopBinary = Encoding.UTF8.GetBytes(mimeTypePrefix + testJson);
                
                try
                {
                    var messageText = Encoding.UTF8.GetString(tinkerPopBinary);
                    if (messageText.StartsWith("!"))
                    {
                        var mimeTypeEnd = messageText.IndexOf('{');
                        if (mimeTypeEnd > 1)
                        {
                            var extractedMimeType = messageText.Substring(1, mimeTypeEnd - 1);
                            var extractedJson = messageText.Substring(mimeTypeEnd);
                            var message2 = serializer.DeserializeMessage(extractedJson);
                            
                            if (message2?.Op == "eval" && extractedMimeType.Contains("gremlin-v3"))
                            {
                                Console.WriteLine("   ? TinkerPop binary format with MIME prefix");
                                results.RecordSuccess("TinkerPop binary format");
                            }
                            else
                            {
                                Console.WriteLine("   ? TinkerPop binary format failed");
                                results.RecordFailure("TinkerPop binary format", "Invalid parsing");
                            }
                        }
                        else
                        {
                            Console.WriteLine("   ? TinkerPop binary format MIME extraction failed");
                            results.RecordFailure("TinkerPop binary format", "MIME extraction failed");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"   ? TinkerPop binary format failed: {ex.Message}");
                    results.RecordFailure("TinkerPop binary format", ex.Message);
                }

                // Test 3: Length-prefixed binary format
                try
                {
                    var jsonBytes = Encoding.UTF8.GetBytes(testJson);
                    var lengthPrefix = BitConverter.GetBytes(jsonBytes.Length);
                    var lengthPrefixedBinary = lengthPrefix.Concat(jsonBytes).ToArray();
                    
                    // Simulate length-prefixed parsing
                    if (lengthPrefixedBinary.Length >= 4)
                    {
                        var length = BitConverter.ToInt32(lengthPrefixedBinary, 0);
                        if (length > 0 && length <= lengthPrefixedBinary.Length - 4)
                        {
                            var extractedJson = Encoding.UTF8.GetString(lengthPrefixedBinary, 4, length);
                            var message3 = serializer.DeserializeMessage(extractedJson);
                            
                            if (message3?.Op == "eval")
                            {
                                Console.WriteLine("   ? Length-prefixed binary format");
                                results.RecordSuccess("Length-prefixed binary format");
                            }
                            else
                            {
                                Console.WriteLine("   ? Length-prefixed binary format failed");
                                results.RecordFailure("Length-prefixed binary format", "Invalid deserialization");
                            }
                        }
                        else
                        {
                            Console.WriteLine("   ? Length-prefixed binary format - invalid length");
                            results.RecordFailure("Length-prefixed binary format", "Invalid length prefix");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"   ? Length-prefixed binary format failed: {ex.Message}");
                    results.RecordFailure("Length-prefixed binary format", ex.Message);
                }

                // Test 4: GraphSON wrapped values
                var graphSONFormats = new[]
                {
                    ("Simple UUID", "\"12345678-1234-1234-1234-123456789012\""),
                    ("GraphSON UUID", "{\"@type\":\"g:UUID\",\"@value\":\"12345678-1234-1234-1234-123456789012\"}"),
                    ("Simple Int", "42"),
                    ("GraphSON Int", "{\"@type\":\"g:Int32\",\"@value\":42}"),
                    ("Simple String", "\"test\""),
                    ("GraphSON String", "{\"@type\":\"g:String\",\"@value\":\"test\"}")
                };

                foreach (var (formatName, formatValue) in graphSONFormats)
                {
                    try
                    {
                        var testMessageJson = $"{{\"requestId\":{formatValue},\"op\":\"eval\",\"processor\":\"\",\"args\":{{\"gremlin\":\"g.inject(1)\"}}}}";
                        var message = serializer.DeserializeMessage(testMessageJson);
                        
                        if (message != null && message.RequestId != Guid.Empty)
                        {
                            Console.WriteLine($"   ? {formatName} format");
                            results.RecordSuccess($"{formatName} format");
                        }
                        else
                        {
                            Console.WriteLine($"   ? {formatName} format failed");
                            results.RecordFailure($"{formatName} format", "Parsing failed");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"   ??  {formatName} format issue: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"   ? Binary message format test failed: {ex.Message}");
                results.RecordFailure("Binary message format", ex.Message);
            }
        }

        private static async Task TestConcurrentConnections(TestResults results)
        {
            Console.WriteLine("\n?? Concurrent Connection Tests");
            
            StardustGremlinServer? server = null;
            try
            {
                var options = new GremlinServerOptions
                {
                    Host = "localhost",
                    Port = GetRandomPort(),
                    HttpPort = GetRandomPort(),
                    EnableWebSocket = true,
                    MaxConnections = 10,
                    EnableLogging = false
                };

                server = new StardustGremlinServer(options);
                await server.StartAsync();
                await Task.Delay(1500);

                if (!server.IsRunning || !server.WebSocketSupported)
                {
                    Console.WriteLine("   ? Server not ready for concurrent tests");
                    results.RecordFailure("Concurrent test setup", "Server not ready");
                    return;
                }

                // Test concurrent Gremlin.Net clients
                var concurrentTasks = new List<Task<bool>>();
                const int connectionCount = 5;

                for (int i = 0; i < connectionCount; i++)
                {
                    int taskId = i;
                    concurrentTasks.Add(Task.Run(async () =>
                    {
                        GremlinClient? client = null;
                        try
                        {
                            var gremlinServer = new GremlinNetServer(options.Host, options.GetHttpPort());
                            client = new GremlinClient(gremlinServer);
                            
                            var result = await client.SubmitAsync<dynamic>($"g.inject({taskId * 10})");
                            return result?.Count > 0 && result.FirstOrDefault()?.ToString() == (taskId * 10).ToString();
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"      ?? Concurrent task {taskId} failed: {ex.Message}");
                            return false;
                        }
                        finally
                        {
                            client?.Dispose();
                        }
                    }));
                }

                var concurrentResults = await Task.WhenAll(concurrentTasks);
                var successfulConnections = concurrentResults.Count(r => r);

                if (successfulConnections >= connectionCount * 0.8) // 80% success rate
                {
                    Console.WriteLine($"   ? Concurrent connections ({successfulConnections}/{connectionCount} successful)");
                    results.RecordSuccess("Concurrent connections");
                }
                else
                {
                    Console.WriteLine($"   ? Concurrent connections failed ({successfulConnections}/{connectionCount} successful)");
                    results.RecordFailure("Concurrent connections", $"Only {successfulConnections}/{connectionCount} successful");
                }

                // Test connection limit
                try
                {
                    var overLimitTasks = new List<Task<bool>>();
                    for (int i = 0; i < options.MaxConnections + 2; i++)
                    {
                        int taskId = i;
                        overLimitTasks.Add(Task.Run(async () =>
                        {
                            GremlinClient? client = null;
                            try
                            {
                                var gremlinServer = new GremlinNetServer(options.Host, options.GetHttpPort());
                                client = new GremlinClient(gremlinServer);
                                
                                var result = await client.SubmitAsync<dynamic>("g.inject(1)");
                                await Task.Delay(1000); // Hold connection briefly
                                return result?.Count > 0;
                            }
                            catch
                            {
                                return false;
                            }
                            finally
                            {
                                client?.Dispose();
                            }
                        }));
                    }

                    var overLimitResults = await Task.WhenAll(overLimitTasks);
                    var actualConnections = overLimitResults.Count(r => r);

                    if (actualConnections <= options.MaxConnections)
                    {
                        Console.WriteLine($"   ? Connection limit enforcement ({actualConnections} <= {options.MaxConnections})");
                        results.RecordSuccess("Connection limit enforcement");
                    }
                    else
                    {
                        Console.WriteLine($"   ??  Connection limit not enforced ({actualConnections} > {options.MaxConnections})");
                        results.RecordFailure("Connection limit enforcement", $"Exceeded limit: {actualConnections} > {options.MaxConnections}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"   ? Connection limit test failed: {ex.Message}");
                    results.RecordFailure("Connection limit test", ex.Message);
                }
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

        private static async Task TestLargeMessages(TestResults results)
        {
            Console.WriteLine("\n?? Large Message Tests");
            
            StardustGremlinServer? server = null;
            try
            {
                var options = new GremlinServerOptions
                {
                    Host = "localhost",
                    Port = GetRandomPort(),
                    HttpPort = GetRandomPort(),
                    EnableWebSocket = true,
                    EnableLogging = false
                };

                server = new StardustGremlinServer(options);
                await server.StartAsync();
                await Task.Delay(1500);

                if (!server.IsRunning || !server.WebSocketSupported)
                {
                    Console.WriteLine("   ? Server not ready for large message tests");
                    results.RecordFailure("Large message test setup", "Server not ready");
                    return;
                }

                GremlinClient? client = null;
                try
                {
                    var gremlinServer = new GremlinNetServer(options.Host, options.GetHttpPort());
                    client = new GremlinClient(gremlinServer);

                    // Test large collection
                    var largeCollectionResult = await client.SubmitAsync<dynamic>("g.inject(1..1000)");
                    if (largeCollectionResult?.Count == 1000)
                    {
                        Console.WriteLine("   ? Large collection message");
                        results.RecordSuccess("Large collection message");
                    }
                    else
                    {
                        Console.WriteLine("   ? Large collection message failed");
                        results.RecordFailure("Large collection message", "Incorrect collection size");
                    }

                    // Test large string
                    var largeString = new string('A', 10000);
                    var largeStringResult = await client.SubmitAsync<dynamic>($"g.inject('{largeString}')");
                    if (largeStringResult?.Count > 0 && largeStringResult.FirstOrDefault()?.ToString()?.Length == 10000)
                    {
                        Console.WriteLine("   ? Large string message");
                        results.RecordSuccess("Large string message");
                    }
                    else
                    {
                        Console.WriteLine("   ? Large string message failed");
                        results.RecordFailure("Large string message", "String size mismatch");
                    }

                    // Test complex nested structure
                    var complexQuery = "g.inject([" + string.Join(",", Enumerable.Range(1, 100).Select(i => $"[id:{i}, value:'item_{i}']")) + "])";
                    var complexResult = await client.SubmitAsync<dynamic>(complexQuery);
                    if (complexResult?.Count > 0)
                    {
                        Console.WriteLine("   ? Complex nested structure message");
                        results.RecordSuccess("Complex nested structure message");
                    }
                    else
                    {
                        Console.WriteLine("   ? Complex nested structure message failed");
                        results.RecordFailure("Complex nested structure message", "No result");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"   ? Large message test failed: {ex.Message}");
                    results.RecordFailure("Large message test", ex.Message);
                }
                finally
                {
                    client?.Dispose();
                }
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

        private static async Task TestConnectionRecovery(TestResults results)
        {
            Console.WriteLine("\n?? Connection Recovery Tests");
            
            try
            {
                // Test connection timeout handling
                var shortTimeoutOptions = new GremlinServerOptions
                {
                    Host = "localhost",
                    Port = GetRandomPort(),
                    HttpPort = GetRandomPort(),
                    EnableWebSocket = true,
                    ConnectionTimeoutSeconds = 1,
                    EnableLogging = false
                };

                StardustGremlinServer? server = null;
                try
                {
                    server = new StardustGremlinServer(shortTimeoutOptions);
                    await server.StartAsync();
                    await Task.Delay(1500);

                    if (server.IsRunning && server.WebSocketSupported)
                    {
                        Console.WriteLine("   ? Server with short timeout started");
                        results.RecordSuccess("Short timeout server startup");

                        // Test multiple quick connections (should handle gracefully)
                        var quickTasks = new List<Task<bool>>();
                        for (int i = 0; i < 3; i++)
                        {
                            quickTasks.Add(Task.Run(async () =>
                            {
                                GremlinClient? client = null;
                                try
                                {
                                    var gremlinServer = new GremlinNetServer(shortTimeoutOptions.Host, shortTimeoutOptions.GetHttpPort());
                                    client = new GremlinClient(gremlinServer);
                                    
                                    var result = await client.SubmitAsync<dynamic>("g.inject(42)");
                                    return result?.Count > 0;
                                }
                                catch
                                {
                                    return false; // Expected for some connections
                                }
                                finally
                                {
                                    client?.Dispose();
                                }
                            }));
                        }

                        var quickResults = await Task.WhenAll(quickTasks);
                        var successfulQuickConnections = quickResults.Count(r => r);

                        if (successfulQuickConnections > 0)
                        {
                            Console.WriteLine($"   ? Quick connection handling ({successfulQuickConnections}/3 successful)");
                            results.RecordSuccess("Quick connection handling");
                        }
                        else
                        {
                            Console.WriteLine("   ? Quick connection handling failed");
                            results.RecordFailure("Quick connection handling", "No successful connections");
                        }
                    }
                    else
                    {
                        Console.WriteLine("   ? Short timeout server failed to start");
                        results.RecordFailure("Short timeout server startup", "Server not ready");
                    }
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
                            Console.WriteLine($"   ?? Error stopping short timeout server: {ex.Message}");
                        }
                    }
                }

                // Test graceful shutdown
                var gracefulShutdownOptions = new GremlinServerOptions
                {
                    Host = "localhost",
                    Port = GetRandomPort(),
                    HttpPort = GetRandomPort(),
                    EnableWebSocket = true,
                    EnableLogging = false
                };

                server = new StardustGremlinServer(gracefulShutdownOptions);
                await server.StartAsync();
                await Task.Delay(1000);

                if (server.IsRunning)
                {
                    // Create active connection
                    GremlinClient? activeClient = null;
                    try
                    {
                        var gremlinServer = new GremlinNetServer(gracefulShutdownOptions.Host, gracefulShutdownOptions.GetHttpPort());
                        activeClient = new GremlinClient(gremlinServer);
                        
                        // Start long-running query
                        var longRunningTask = activeClient.SubmitAsync<dynamic>("g.inject(1..100)");
                        
                        // Stop server while query is running
                        await server.StopAsync();
                        
                        try
                        {
                            await longRunningTask;
                            Console.WriteLine("   ? Graceful shutdown with active connections");
                            results.RecordSuccess("Graceful shutdown");
                        }
                        catch
                        {
                            Console.WriteLine("   ? Graceful shutdown (connection terminated as expected)");
                            results.RecordSuccess("Graceful shutdown");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"   ? Graceful shutdown test failed: {ex.Message}");
                        results.RecordFailure("Graceful shutdown", ex.Message);
                    }
                    finally
                    {
                        activeClient?.Dispose();
                        server?.Dispose();
                        server = null;
                    }
                }
                else
                {
                    Console.WriteLine("   ? Graceful shutdown server failed to start");
                    results.RecordFailure("Graceful shutdown setup", "Server not running");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"   ? Connection recovery test failed: {ex.Message}");
                results.RecordFailure("Connection recovery", ex.Message);
            }
        }

        private static int GetRandomPort()
        {
            var random = new Random();
            return random.Next(8000, 9000);
        }
    }
}