using System;
using System.Linq;
using System.Threading.Tasks;
using Stardust.Paradox.Data.InMemory;
using Stardust.Paradox.Data.InMemory.Demo;

namespace VertexPropertiesTest
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("?? Testing vertex properties display in demo app...");

            try
            {
                // Start server with Direct protocol (simplest)
                var server = await GremlinDatabase.StartAsync(options =>
                {
                    options.Host = "localhost";
                    options.Port = 8182;
                    options.EnableTcp = false;
                    options.EnableWebSocket = false;
                    options.EnableHttp = false;
                    options.EnableLogging = false;
                });

                Console.WriteLine("? Server started");

                // Load the social commerce scenario
                var scenario = new SocialCommerceScenario(server.Connector);
                await scenario.LoadScenarioAsync();

                Console.WriteLine("? Scenario loaded");

                // Create a direct client
                var client = await GremlinClientFactory.CreateClientAsync(WireProtocol.Direct, server.Connector);

                Console.WriteLine("? Client created");

                // Test option 5: All products (with properties)
                Console.WriteLine("\n?? Testing Query: g.V().hasLabel('product')");
                Console.WriteLine("This simulates what option 5 in the demo should show...");

                var products = await client.ExecuteAsync("g.V().hasLabel('product')");
                var productsList = products.ToList();

                Console.WriteLine($"\n?? Found {productsList.Count} products:");
                
                for (int i = 0; i < Math.Min(productsList.Count, 5); i++)
                {
                    var product = productsList[i];
                    Console.WriteLine($"Raw product type: {product?.GetType().FullName}");
                    
                    // Test our formatting
                    var formatted = FormatResult(product);
                    Console.WriteLine($"   [{i + 1}] {formatted}");
                    
                    // Also show the raw object structure for debugging
                    if (product?.GetType().Name == "GremlinResponseObject")
                    {
                        try
                        {
                            var type = product.GetType();
                            var getMethod = type.GetMethod("Get");
                            if (getMethod != null)
                            {
                                var id = getMethod.Invoke(product, new object[] { "id" });
                                var label = getMethod.Invoke(product, new object[] { "label" });
                                var properties = getMethod.Invoke(product, new object[] { "properties" });
                                
                                Console.WriteLine($"      Debug - ID: {id}, Label: {label}");
                                Console.WriteLine($"      Debug - Properties type: {properties?.GetType()}");
                                Console.WriteLine($"      Debug - Properties content: {properties}");
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"      Debug error: {ex.Message}");
                        }
                    }
                }

                client?.Dispose();
                await GremlinDatabase.StopAsync(server);
                
                Console.WriteLine("\n? Test completed successfully!");
                Console.WriteLine("\nThe vertex properties should now be visible in the demo app when running option 5.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"? Error: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }

            Console.WriteLine("\nPress any key to exit...");
            Console.ReadKey();
        }

        // Copy the enhanced FormatResult method from the demo
        private static string FormatResult(dynamic result)
        {
            if (result == null)
                return "null";

            try
            {
                // Handle common result types with enhanced formatting
                if (result is string str)
                    return $"\"{str}\"";
                
                if (result is int || result is long || result is double || result is float || result is decimal)
                    return result.ToString();
                
                if (result is bool boolean)
                    return boolean.ToString().ToLower();

                // **ENHANCED**: Handle GremlinResponseObject specifically for vertex property display
                if (result.GetType().Name == "GremlinResponseObject")
                {
                    try
                    {
                        // Use reflection to safely access properties
                        var type = result.GetType();
                        var getMethod = type.GetMethod("Get");
                        
                        if (getMethod != null)
                        {
                            var id = getMethod.Invoke(result, new object[] { "id" });
                            var label = getMethod.Invoke(result, new object[] { "label" });
                            var objType = getMethod.Invoke(result, new object[] { "type" });
                            var properties = getMethod.Invoke(result, new object[] { "properties" });
                            
                            if (objType?.ToString() == "vertex")
                            {
                                return FormatVertexSimple(id?.ToString(), label?.ToString(), properties);
                            }
                            else if (objType?.ToString() == "edge")
                            {
                                var outV = getMethod.Invoke(result, new object[] { "outV" });
                                var inV = getMethod.Invoke(result, new object[] { "inV" });
                                return FormatEdgeSimple(id?.ToString(), label?.ToString(), outV?.ToString(), inV?.ToString(), properties);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[DEBUG] Error processing GremlinResponseObject: {ex.Message}");
                        // Fall through to JSON serialization
                    }
                }

                // Fallback to string representation
                return result.ToString();
            }
            catch
            {
                return result.ToString();
            }
        }

        // **SIMPLIFIED**: Simple vertex formatter that works with any object type
        private static string FormatVertexSimple(string id, string label, dynamic properties)
        {
            try
            {
                var propList = new System.Collections.Generic.List<string>();
                
                if (properties != null)
                {
                    // Simple string representation of properties
                    var propsString = properties.ToString();
                    if (!string.IsNullOrEmpty(propsString) && propsString != "{}")
                    {
                        // If it's a JSON string, try to parse some basic properties
                        if (propsString.StartsWith("{"))
                        {
                            try
                            {
                                var propsJson = Newtonsoft.Json.JsonConvert.DeserializeObject<System.Collections.Generic.Dictionary<string, object>>(propsString);
                                if (propsJson != null)
                                {
                                    foreach (var prop in propsJson.Take(3))
                                    {
                                        var value = prop.Value?.ToString();
                                        if (value?.Length > 20)
                                        {
                                            value = value.Substring(0, 17) + "...";
                                        }
                                        propList.Add($"{prop.Key}={value}");
                                    }
                                    
                                    if (propsJson.Count > 3)
                                    {
                                        propList.Add($"+{propsJson.Count - 3} more");
                                    }
                                }
                            }
                            catch
                            {
                                // If JSON parsing fails, just show truncated properties
                                var truncated = propsString.Length > 30 ? propsString.Substring(0, 27) + "..." : propsString;
                                propList.Add($"props: {truncated}");
                            }
                        }
                        else
                        {
                            // Non-JSON properties, just show truncated
                            var truncated = propsString.Length > 20 ? propsString.Substring(0, 17) + "..." : propsString;
                            propList.Add($"props: {truncated}");
                        }
                    }
                }
                
                if (propList.Count > 0)
                {
                    return $"v[{id ?? "?"}:{label ?? "vertex"}] {{{string.Join(", ", propList)}}}";
                }
                
                return $"v[{id ?? "?"}:{label ?? "vertex"}]";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DEBUG] Error formatting vertex: {ex.Message}");
                return $"v[{id ?? "?"}:{label ?? "vertex"}]";
            }
        }

        // **SIMPLIFIED**: Simple edge formatter
        private static string FormatEdgeSimple(string id, string label, string outV, string inV, dynamic properties)
        {
            try
            {
                var propDisplay = "";
                if (properties != null)
                {
                    var propsString = properties.ToString();
                    if (!string.IsNullOrEmpty(propsString) && propsString != "{}")
                    {
                        var truncated = propsString.Length > 20 ? propsString.Substring(0, 17) + "..." : propsString;
                        propDisplay = $" {{{truncated}}}";
                    }
                }
                
                return $"e[{id ?? "?"}:{label ?? "edge"}][{outV ?? "?"}?{inV ?? "?"}]{propDisplay}";
            }
            catch
            {
                return $"e[{id ?? "?"}:{label ?? "edge"}][{outV ?? "?"}?{inV ?? "?"}]";
            }
        }
    }
}