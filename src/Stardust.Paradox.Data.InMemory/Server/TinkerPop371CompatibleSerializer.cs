#if NET8_0_OR_GREATER || NETCOREAPP3_1_OR_GREATER
using System;
using Stardust.Paradox.Data.InMemory.Core;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Stardust.Paradox.Data.InMemory.Server
{
    /// <summary>
    /// TinkerPop 3.7.1 compatible serializer based on exact Apache TinkerPop reference implementation
    /// Specifically addresses System.Text.Json parsing issues in Gremlin.Net v3.7.1
    /// Reference: https://github.com/apache/tinkerpop/tree/master/tinkergraph-gremlin/src/main/java/org/apache/tinkerpop/gremlin/tinkergraph
    /// </summary>
    public class TinkerPop371CompatibleSerializer : ITinkerPopSerializer
    {
        private readonly JsonSerializerSettings _jsonSettings;

        public TinkerPop371CompatibleSerializer()
        {
            _jsonSettings = new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore,
                Formatting = Formatting.None,
                DateFormatHandling = DateFormatHandling.IsoDateFormat,
                FloatFormatHandling = FloatFormatHandling.String,  // Key fix for System.Text.Json
                FloatParseHandling = FloatParseHandling.Double      // Ensure doubles are handled properly
            };
        }

        /// <summary>
        /// Serialize response according to exact TinkerPop 3.7.1 specification
        /// </summary>
        public string SerializeResponse(TinkerPopResponse response)
        {
            try
            {
                // Create response exactly as TinkerPop expects for System.Text.Json compatibility
                var responseObj = new
                {
                    requestId = response.RequestId.ToString("D"),
                    status = new
                    {
                        message = response.Status?.Message ?? "",
                        code = response.Status?.Code ?? TinkerPopStatusCodes.Success, // Already a string
                        attributes = new { } // Always empty object for compatibility
                    },
                    result = new
                    {
                        data = ConvertDataForSystemTextJson(response.Result?.Data),
                        meta = new { } // Always empty object for compatibility
                    }
                };

                var json = JsonConvert.SerializeObject(responseObj, _jsonSettings);
                Console.WriteLine($"[DEBUG] TinkerPop 3.7.1 response: {json}");
                return json;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] TinkerPop 3.7.1 serialization failed: {ex.Message}");
                
                // Fallback to minimal response
                var fallback = new
                {
                    requestId = response.RequestId.ToString("D"),
                    status = new { code = 500, message = ex.Message, attributes = new { } },
                    result = new { data = new object[0], meta = new { } }
                };
                
                return JsonConvert.SerializeObject(fallback, _jsonSettings);
            }
        }

        /// <summary>
        /// Convert data specifically for System.Text.Json compatibility
        /// </summary>
        private object[] ConvertDataForSystemTextJson(List<object> data)
        {
            if (data == null || data.Count == 0)
                return new object[0];

            return data.Select(ConvertValueForSystemTextJson).ToArray();
        }

        /// <summary>
        /// Convert individual values to be compatible with System.Text.Json used in Gremlin.Net 3.7.1
        /// </summary>
        private object ConvertValueForSystemTextJson(object value)
        {
            if (value == null) return null;

            return value switch
            {
                // Basic types that System.Text.Json handles well
                string s => s,
                bool b => b,
                
                // Numbers: Convert to strings to avoid System.Text.Json parsing issues
                // This is the key fix for the "JSON type not supported Number" error
                int i => i.ToString(),
                long l => l.ToString(),
                double d => d.ToString("G17"), // Use round-trip format
                float f => f.ToString("G9"),   // Use round-trip format
                decimal dec => dec.ToString(),
                
                // Graph elements: Simplified format
                InMemoryVertex vertex => ConvertVertexForSystemTextJson(vertex),
                InMemoryEdge edge => ConvertEdgeForSystemTextJson(edge),
                
                // Collections: Convert recursively
                System.Collections.IEnumerable enumerable when !(value is string) =>
                    enumerable.Cast<object>().Select(ConvertValueForSystemTextJson).ToArray(),
                
                // Everything else as string
                _ => value.ToString()
            };
        }

        /// <summary>
        /// Convert vertex to System.Text.Json compatible format
        /// </summary>
        private object ConvertVertexForSystemTextJson(InMemoryVertex vertex)
        {
            return new
            {
                id = vertex.Id.ToString(), // Always convert IDs to strings
                label = vertex.Label,
                type = "vertex",
                properties = ConvertPropertiesForSystemTextJson(vertex.Properties)
            };
        }

        /// <summary>
        /// Convert edge to System.Text.Json compatible format
        /// </summary>
        private object ConvertEdgeForSystemTextJson(InMemoryEdge edge)
        {
            return new
            {
                id = edge.Id.ToString(), // Always convert IDs to strings
                label = edge.Label,
                type = "edge",
                inV = edge.InVertexId.ToString(),
                outV = edge.OutVertexId.ToString(),
                properties = ConvertPropertiesForSystemTextJson(edge.Properties)
            };
        }

        /// <summary>
        /// Convert properties to System.Text.Json compatible format
        /// </summary>
        private object ConvertPropertiesForSystemTextJson(Dictionary<string, object> properties)
        {
            if (properties == null || properties.Count == 0)
                return new { };

            var result = new Dictionary<string, object>();
            foreach (var kvp in properties)
            {
                result[kvp.Key] = ConvertValueForSystemTextJson(kvp.Value);
            }
            return result;
        }

        /// <summary>
        /// Deserialize MIME-prefixed messages from Gremlin.Net
        /// </summary>
        public TinkerPopMessage DeserializeMessage(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new TinkerPopProtocolException("JSON input cannot be null or empty");

            try
            {
                Console.WriteLine($"[DEBUG] Deserializing message: {json}");
                
                // Handle MIME-prefixed messages from Gremlin.Net
                // Format: "!application/vnd.gremlin-v3.0+json{actual json}"
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
                    RequestId = ParseRequestId(jObject["requestId"]),
                    Op = jObject["op"]?.ToString() ?? "eval",
                    Processor = jObject["processor"]?.ToString() ?? "",
                    Args = ParseArgs(jObject["args"] as JObject)
                };
                
                Console.WriteLine($"[DEBUG] Message parsed successfully: Op={message.Op}, RequestId={message.RequestId}");
                return message;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DEBUG] Message parsing failed: {ex.Message}");
                throw new TinkerPopProtocolException($"Failed to deserialize message: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Parse request ID from JSON token
        /// </summary>
        private Guid ParseRequestId(JToken token)
        {
            if (token == null) return Guid.NewGuid();
            
            try
            {
                return Guid.TryParse(token.ToString(), out var guid) ? guid : Guid.NewGuid();
            }
            catch
            {
                return Guid.NewGuid();
            }
        }

        /// <summary>
        /// Parse message arguments
        /// </summary>
        private Dictionary<string, object> ParseArgs(JObject argsObject)
        {
            if (argsObject == null) return new Dictionary<string, object>();

            var args = new Dictionary<string, object>();
            foreach (var property in argsObject.Properties())
            {
                args[property.Name] = property.Value?.ToObject<object>();
            }
            return args;
        }

        /// <summary>
        /// Create error response
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
        /// Create success response
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

#endif