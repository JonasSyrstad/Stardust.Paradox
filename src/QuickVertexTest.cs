using System;
using System.Linq;
using System.Threading.Tasks;
using Stardust.Paradox.Data.InMemory;
using Stardust.Paradox.Data.InMemory.Demo;
using Newtonsoft.Json;

namespace QuickVertexTest
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("?? Quick test of vertex properties display...");

            try
            {
                // Start server
                var server = await GremlinDatabase.StartAsync(options =>
                {
                    options.Host = "localhost";
                    options.Port = 8182;
                    options.EnableTcp = false;
                    options.EnableWebSocket = false;
                    options.EnableHttp = false;
                    options.EnableLogging = false;
                });

                // Add a simple user directly
                await server.Connector.ExecuteAsync("g.addV('user').property('name', 'Test User').property('age', 30).property('city', 'Test City')", null);

                // Query it back
                var users = await server.Connector.ExecuteAsync("g.V().hasLabel('user')", null);
                
                Console.WriteLine($"\nFound {users.Count()} users:");
                foreach (var user in users)
                {
                    Console.WriteLine($"Raw result type: {user?.GetType().FullName}");
                    Console.WriteLine($"Raw JSON: {JsonConvert.SerializeObject(user, Formatting.Indented)}");
                    
                    // Test the formatting function manually
                    var formatted = FormatResult(user);
                    Console.WriteLine($"Formatted: {formatted}");
                }

                await GremlinDatabase.StopAsync(server);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }

            Console.WriteLine("\nPress any key to exit...");
            Console.ReadKey();
        }

        // Copy the FormatResult logic from the demo to test it
        private static string FormatResult(dynamic result)
        {
            if (result == null)
                return "null";

            try
            {
                if (result is string str)
                    return $"\"{str}\"";
                
                if (result is int || result is long || result is double || result is float || result is decimal)
                    return result.ToString();
                
                if (result is bool boolean)
                    return boolean.ToString().ToLower();

                if (result is Newtonsoft.Json.Linq.JObject jObj)
                {
                    if (IsVertex(result))
                    {
                        return FormatVertex(result);
                    }
                    else if (IsEdge(result))
                    {
                        return FormatEdge(result);
                    }
                    else
                    {
                        var formatted = JsonConvert.SerializeObject(jObj, Formatting.None);
                        return formatted.Length > 150 ? formatted.Substring(0, 147) + "..." : formatted;
                    }
                }

                var json = JsonConvert.SerializeObject(result, Formatting.None);
                if (json.Length > 200)
                {
                    return json.Substring(0, 197) + "...";
                }
                
                return json;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Formatting error: {ex.Message}");
                return result.ToString();
            }
        }

        private static bool IsVertex(dynamic result)
        {
            try
            {
                if (result is Newtonsoft.Json.Linq.JObject jObj)
                {
                    if (jObj["@type"]?.ToString() == "g:Vertex")
                        return true;
                    
                    if (jObj.ContainsKey("id") && jObj.ContainsKey("label"))
                    {
                        if (jObj["type"]?.ToString() == "vertex")
                            return true;
                        
                        if (jObj.ContainsKey("properties"))
                            return true;
                        
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
                    if (jObj["@type"]?.ToString() == "g:Edge")
                        return true;
                    
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

        private static string FormatVertex(dynamic vertex)
        {
            try
            {
                if (vertex is Newtonsoft.Json.Linq.JObject vObj)
                {
                    var id = vObj["id"]?.ToString() ?? "?";
                    var label = vObj["label"]?.ToString() ?? "vertex";
                    
                    var propertiesObj = vObj["properties"];
                    if (propertiesObj != null)
                    {
                        var properties = new System.Collections.Generic.List<string>();
                        
                        if (propertiesObj is Newtonsoft.Json.Linq.JObject propsDict)
                        {
                            foreach (var prop in propsDict)
                            {
                                var propName = prop.Key;
                                var propValue = ExtractPropertyValue(prop.Value);
                                if (propValue != null)
                                {
                                    properties.Add($"{propName}={propValue}");
                                }
                            }
                        }
                        
                        if (properties.Count > 0)
                        {
                            var propDisplay = string.Join(", ", properties.Take(3));
                            if (properties.Count > 3)
                            {
                                propDisplay += $", +{properties.Count - 3} more";
                            }
                            return $"v[{id}:{label}] {{{propDisplay}}}";
                        }
                    }
                    
                    return $"v[{id}:{label}]";
                }
                
                return vertex?.ToString() ?? "vertex[?]";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"FormatVertex error: {ex.Message}");
                return vertex?.ToString() ?? "vertex[?]";
            }
        }

        private static string FormatEdge(dynamic edge)
        {
            try
            {
                if (edge is Newtonsoft.Json.Linq.JObject eObj)
                {
                    var id = eObj["id"]?.ToString() ?? "?";
                    var label = eObj["label"]?.ToString() ?? "edge";
                    var outV = eObj["outV"]?.ToString() ?? "?";
                    var inV = eObj["inV"]?.ToString() ?? "?";
                    
                    return $"e[{id}:{label}][{outV}?{inV}]";
                }
                
                return edge?.ToString() ?? "edge[?]";
            }
            catch
            {
                return edge?.ToString() ?? "edge[?]";
            }
        }

        private static string ExtractPropertyValue(Newtonsoft.Json.Linq.JToken propToken)
        {
            try
            {
                if (propToken is Newtonsoft.Json.Linq.JArray propArray && propArray.Count > 0)
                {
                    var firstProp = propArray[0];
                    if (firstProp is Newtonsoft.Json.Linq.JObject propObj && propObj["value"] != null)
                    {
                        var value = propObj["value"];
                        return FormatPropertyValue(value);
                    }
                }
                
                return FormatPropertyValue(propToken);
            }
            catch
            {
                return propToken?.ToString();
            }
        }

        private static string FormatPropertyValue(Newtonsoft.Json.Linq.JToken value)
        {
            if (value == null) return "null";
            
            try
            {
                switch (value.Type)
                {
                    case Newtonsoft.Json.Linq.JTokenType.String:
                        var str = value.ToString();
                        return str.Length > 20 ? $"\"{str.Substring(0, 17)}...\"" : $"\"{str}\"";
                    case Newtonsoft.Json.Linq.JTokenType.Integer:
                    case Newtonsoft.Json.Linq.JTokenType.Float:
                        return value.ToString();
                    case Newtonsoft.Json.Linq.JTokenType.Boolean:
                        return value.ToString().ToLower();
                    default:
                        var str2 = value.ToString();
                        return str2.Length > 15 ? str2.Substring(0, 12) + "..." : str2;
                }
            }
            catch
            {
                return value.ToString();
            }
        }
    }
}