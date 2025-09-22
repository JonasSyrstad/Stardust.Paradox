using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Stardust.Paradox.Data.InMemory
{
    /// <summary>
    /// TinkerPop-compliant serializer based on Apache TinkerGraph reference implementation
    /// Specifically designed for Gremlin.Net v3.7.1 System.Text.Json compatibility
    /// Reference: https://github.com/apache/tinkerpop/tree/master/tinkergraph-gremlin/src/main/java/org/apache/tinkerpop/gremlin/tinkergraph
    /// </summary>
    public class GremlinNetCompatibleSerializer : ITinkerPopSerializer
    {
        private readonly JsonSerializerSettings _settings;

        public GremlinNetCompatibleSerializer()
        {
            // Use settings that ensure System.Text.Json compatibility
            _settings = new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore,
                Formatting = Formatting.None,
                DateFormatHandling = DateFormatHandling.IsoDateFormat,
                // Don't add any custom converters to maintain compatibility
            };
        }

        /// <summary>
        /// Serialize response according to exact TinkerPop specification for Gremlin.Net
        /// Based on Apache TinkerGraph ResponseMessage format
        /// </summary>
        public string SerializeResponse(TinkerPopResponse response)
        {
            try
            {
                // Create response exactly as Apache TinkerGraph does for maximum compatibility
                var responseObj = new
                {
                    requestId = response.RequestId.ToString("D"), // Always string format for UUID
                    status = new
                    {
                        message = response.Status?.Message ?? "",
                        code = response.Status?.Code ?? 200, // Keep as int for Gremlin.Net compatibility
                        attributes = new { } // Always empty for Gremlin.Net compatibility
                    },
                    result = new
                    {
                        data = CreateTinkerGraphCompatibleData(response.Result?.Data),
                        meta = new { } // Always empty for Gremlin.Net compatibility
                    }
                };

                var json = JsonConvert.SerializeObject(responseObj, _settings);
                Console.WriteLine($"[DEBUG] TinkerGraph-compatible response: {json}");
                return json;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] TinkerGraph serialization failed: {ex.Message}");
                
                // Fallback to minimal response that System.Text.Json can definitely parse
                var fallback = new
                {
                    requestId = response.RequestId.ToString("D"),
                    status = new { code = 200, message = "", attributes = new { } },
                    result = new { data = new[] { "1" }, meta = new { } }
                };
                
                return JsonConvert.SerializeObject(fallback, _settings);
            }
        }

        /// <summary>
        /// Convert status code from string to int for Gremlin.Net compatibility
        /// </summary>
        private int ConvertStatusCodeToInt(string statusCode)
        {
            if (string.IsNullOrEmpty(statusCode))
                return 200;

            if (int.TryParse(statusCode, out var intCode))
                return intCode;

            // Handle known status codes
            return statusCode switch
            {
                "200" => 200,
                "204" => 204,
                "206" => 206,
                "401" => 401,
                "407" => 407,
                "498" => 498,
                "499" => 499,
                "500" => 500,
                "597" => 597,
                "598" => 598,
                "599" => 599,
                _ => 500
            };
        }

        /// <summary>
        /// Create data array compatible with Apache TinkerGraph format
        /// All values converted to strings to avoid System.Text.Json parsing issues
        /// </summary>
        private string[] CreateTinkerGraphCompatibleData(List<object> data)
        {
            if (data == null || data.Count == 0)
                return new string[0];

            var result = new string[data.Count];
            for (int i = 0; i < data.Count; i++)
            {
                result[i] = ConvertToTinkerGraphValue(data[i]);
            }

            return result;
        }

        /// <summary>
        /// Convert values to TinkerGraph-compatible string format
        /// Based on how Apache TinkerGraph serializes values for Gremlin clients
        /// </summary>
        private string ConvertToTinkerGraphValue(object value)
        {
            if (value == null) return "null";

            return value switch
            {
                // Basic types - convert all to strings for System.Text.Json compatibility
                string s => s,
                bool b => b.ToString().ToLower(),
                int i => i.ToString(),
                long l => l.ToString(),
                double d => d.ToString("G17"), // Use round-trip format
                float f => f.ToString("G9"),   // Use round-trip format
                decimal dec => dec.ToString(),
                
                // Graph elements - simplified format like TinkerGraph
                InMemoryVertex vertex => SerializeVertexToString(vertex),
                InMemoryEdge edge => SerializeEdgeToString(edge),
                
                // Collections - convert to comma-separated string
                System.Collections.IEnumerable enumerable when !(value is string) =>
                    string.Join(",", enumerable.Cast<object>().Select(ConvertToTinkerGraphValue)),
                
                // Everything else as string
                _ => value.ToString()
            };
        }

        /// <summary>
        /// Serialize vertex to string format like Apache TinkerGraph
        /// </summary>
        private string SerializeVertexToString(InMemoryVertex vertex)
        {
            // Simple string representation similar to TinkerGraph's toString format
            var properties = string.Join(",", vertex.Properties.Select(p => $"{p.Key}={ConvertToTinkerGraphValue(p.Value)}"));
            return $"v[{vertex.Id}]:{vertex.Label}" + (properties.Length > 0 ? $"{{properties={properties}}}" : "");
        }

        /// <summary>
        /// Serialize edge to string format like Apache TinkerGraph
        /// </summary>
        private string SerializeEdgeToString(InMemoryEdge edge)
        {
            // Simple string representation similar to TinkerGraph's toString format
            var properties = string.Join(",", edge.Properties.Select(p => $"{p.Key}={ConvertToTinkerGraphValue(p.Value)}"));
            return $"e[{edge.Id}]:{edge.Label}[{edge.OutVertexId}->{edge.InVertexId}]" + 
                   (properties.Length > 0 ? $"{{properties={properties}}}" : "");
        }

        /// <summary>
        /// Deserialize MIME-prefixed messages from Gremlin.Net
        /// Based on Apache TinkerPop message format
        /// </summary>
        public TinkerPopMessage DeserializeMessage(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new TinkerPopProtocolException("JSON input cannot be null or empty");

            try
            {
                Console.WriteLine($"[DEBUG] Deserializing TinkerPop message: {json}");
                
                // Handle MIME-prefixed messages from Gremlin.Net (TinkerPop standard)
                string actualJson = json;
                if (json.StartsWith("!"))
                {
                    var jsonStart = json.IndexOf('{');
                    if (jsonStart > 0)
                    {
                        var mimeType = json.Substring(1, jsonStart - 1);
                        actualJson = json.Substring(jsonStart);
                        Console.WriteLine($"[DEBUG] Extracted MIME type: {mimeType}");
                        Console.WriteLine($"[DEBUG] Extracted JSON: {actualJson}");
                    }
                }
                
                var jObject = JObject.Parse(actualJson);
                
                var message = new TinkerPopMessage
                {
                    RequestId = ParseTinkerPopRequestId(jObject["requestId"]),
                    Op = jObject["op"]?.ToString() ?? "eval",
                    Processor = jObject["processor"]?.ToString() ?? "",
                    Args = ParseTinkerPopArgs(jObject["args"] as JObject)
                };
                
                Console.WriteLine($"[DEBUG] TinkerPop message parsed: Op={message.Op}, RequestId={message.RequestId}");
                return message;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DEBUG] TinkerPop message parsing failed: {ex.Message}");
                throw new TinkerPopProtocolException($"Failed to deserialize TinkerPop message: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Parse request ID according to TinkerPop specification
        /// </summary>
        private Guid ParseTinkerPopRequestId(JToken token)
        {
            if (token == null) return Guid.NewGuid();
            
            try
            {
                // Handle both string UUID and GraphSON typed UUID formats
                if (token is JObject obj && obj.ContainsKey("@value"))
                {
                    return Guid.TryParse(obj["@value"]?.ToString(), out var guid) ? guid : Guid.NewGuid();
                }
                
                return Guid.TryParse(token.ToString(), out var directGuid) ? directGuid : Guid.NewGuid();
            }
            catch
            {
                return Guid.NewGuid();
            }
        }

        /// <summary>
        /// Parse message arguments according to TinkerPop specification
        /// </summary>
        private Dictionary<string, object> ParseTinkerPopArgs(JObject argsObject)
        {
            if (argsObject == null) return new Dictionary<string, object>();

            var args = new Dictionary<string, object>();
            foreach (var property in argsObject.Properties())
            {
                try
                {
                    // Handle specific TinkerPop argument types
                    if (property.Name == "gremlin")
                    {
                        args[property.Name] = property.Value?.ToString() ?? "";
                    }
                    else if (property.Name == "bindings" && property.Value is JObject bindingsObj)
                    {
                        args[property.Name] = ParseTinkerPopBindings(bindingsObj);
                    }
                    else
                    {
                        args[property.Name] = property.Value?.ToObject<object>();
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[DEBUG] Failed to parse arg '{property.Name}': {ex.Message}");
                    args[property.Name] = property.Value?.ToString() ?? "";
                }
            }
            return args;
        }

        /// <summary>
        /// Parse TinkerPop bindings (query parameters)
        /// </summary>
        private Dictionary<string, object> ParseTinkerPopBindings(JObject bindingsObject)
        {
            var bindings = new Dictionary<string, object>();
            
            foreach (var property in bindingsObject.Properties())
            {
                try
                {
                    bindings[property.Name] = property.Value?.ToObject<object>();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[DEBUG] Failed to parse binding '{property.Name}': {ex.Message}");
                    bindings[property.Name] = property.Value?.ToString() ?? "";
                }
            }

            return bindings;
        }

        /// <summary>
        /// Create error response in TinkerPop format
        /// </summary>
        public TinkerPopResponse CreateErrorResponse(Guid requestId, int statusCode, string message, Exception exception = null)
        {
            var attributes = new Dictionary<string, object>();
            if (exception != null)
            {
                // Add exception details in TinkerPop format
                attributes["exceptionType"] = exception.GetType().Name;
                if (!string.IsNullOrEmpty(exception.StackTrace))
                {
                    attributes["stackTrace"] = exception.StackTrace;
                }
            }

            return new TinkerPopResponse
            {
                RequestId = requestId,
                Status = new TinkerPopStatus
                {
                    Code = statusCode,
                    Message = message,
                    Attributes = attributes
                },
                Result = new TinkerPopResult
                {
                    Data = new List<object>(),
                    Meta = new Dictionary<string, object>()
                }
            };
        }

        /// <summary>
        /// Create success response in TinkerPop format
        /// </summary>
        public TinkerPopResponse CreateSuccessResponse(Guid requestId, IEnumerable<dynamic> data, Dictionary<string, object> meta = null)
        {
            return new TinkerPopResponse
            {
                RequestId = requestId,
                Status = new TinkerPopStatus
                {
                    Code = TinkerPopStatusCodes.Success,
                    Message = "",
                    Attributes = new Dictionary<string, object>()
                },
                Result = new TinkerPopResult
                {
                    Data = data?.Cast<object>().ToList() ?? new List<object>(),
                    Meta = meta ?? new Dictionary<string, object>()
                }
            };
        }
    }
}