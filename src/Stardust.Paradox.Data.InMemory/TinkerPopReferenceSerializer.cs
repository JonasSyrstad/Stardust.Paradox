using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Stardust.Paradox.Data.InMemory
{
    /// <summary>
    /// TinkerPop-compliant GraphSON serializer based on Apache TinkerPop reference implementation
    /// Specifically designed for Gremlin.Net v3.7.1+ compatibility with System.Text.Json
    /// </summary>
    public class TinkerPopReferenceSerializer : ITinkerPopSerializer
    {
        private readonly GraphSONVersion _version;

        public TinkerPopReferenceSerializer(GraphSONVersion version = GraphSONVersion.V3)
        {
            _version = version;
        }

        /// <summary>
        /// Serialize response according to exact TinkerPop ResponseMessage specification
        /// Reference: https://github.com/apache/tinkerpop/blob/master/gremlin-driver/src/main/java/org/apache/tinkerpop/gremlin/driver/message/ResponseMessage.java
        /// </summary>
        public string SerializeResponse(TinkerPopResponse response)
        {
            try
            {
                // Create exact TinkerPop ResponseMessage structure for Gremlin.Net compatibility
                var responseMessage = new Dictionary<string, object>
                {
                    ["requestId"] = response.RequestId.ToString("D"), // UUID as string without type wrapper
                    ["status"] = CreateStatusObject(response.Status),
                    ["result"] = CreateResultObject(response.Result)
                };

                // Use standard JSON serialization settings that match Gremlin.Net expectations
                var settings = new JsonSerializerSettings
                {
                    NullValueHandling = NullValueHandling.Ignore,
                    Formatting = Formatting.None,
                    DateFormatHandling = DateFormatHandling.IsoDateFormat
                };

                var json = JsonConvert.SerializeObject(responseMessage, settings);
                Console.WriteLine($"[DEBUG] TinkerPop response JSON: {json}");
                return json;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Failed to serialize TinkerPop response: {ex.Message}");
                throw new TinkerPopProtocolException($"Response serialization failed: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Create status object according to TinkerPop ResponseStatus specification
        /// </summary>
        private Dictionary<string, object> CreateStatusObject(TinkerPopStatus status)
        {
            var statusObj = new Dictionary<string, object>
            {
                ["message"] = status?.Message ?? "",
                ["code"] = status?.Code ?? TinkerPopStatusCodes.Success, // Now string-based
                ["attributes"] = CreateAttributesObject(status?.Attributes)
            };

            return statusObj;
        }

        /// <summary>
        /// Create result object according to TinkerPop ResponseResult specification
        /// </summary>
        private Dictionary<string, object> CreateResultObject(TinkerPopResult result)
        {
            var resultObj = new Dictionary<string, object>
            {
                ["data"] = CreateDataArray(result?.Data),
                ["meta"] = CreateMetaObject(result?.Meta)
            };

            return resultObj;
        }

        /// <summary>
        /// Create attributes object - keep simple for Gremlin.Net compatibility
        /// </summary>
        private Dictionary<string, object> CreateAttributesObject(Dictionary<string, object> attributes)
        {
            if (attributes == null || attributes.Count == 0)
                return new Dictionary<string, object>();

            var result = new Dictionary<string, object>();
            foreach (var kvp in attributes)
            {
                // Only include primitive types to avoid System.Text.Json parsing issues
                result[kvp.Key] = kvp.Value switch
                {
                    string s => s,
                    int i => i,
                    long l => l,
                    double d => d,
                    float f => f,
                    bool b => b,
                    null => null,
                    _ => kvp.Value.ToString()
                };
            }

            return result;
        }

        /// <summary>
        /// Create data array with proper GraphSON formatting
        /// </summary>
        private object[] CreateDataArray(List<object> data)
        {
            if (data == null || data.Count == 0)
                return new object[0];

            var result = new object[data.Count];
            for (int i = 0; i < data.Count; i++)
            {
                result[i] = SerializeDataValue(data[i]);
            }

            return result;
        }

        /// <summary>
        /// Create meta object - keep empty for maximum compatibility
        /// </summary>
        private Dictionary<string, object> CreateMetaObject(Dictionary<string, object> meta)
        {
            // For maximum Gremlin.Net compatibility, return empty meta object
            return new Dictionary<string, object>();
        }

        /// <summary>
        /// Serialize data values according to TinkerPop GraphSON specification
        /// </summary>
        private object SerializeDataValue(object value)
        {
            if (value == null) return null;

            return value switch
            {
                // Primitive types - no type wrapping for basic types in GraphSON v3
                string s => s,
                bool b => b,
                
                // For Gremlin.Net compatibility, don't use GraphSON type wrappers for simple numbers
                // Instead, send them as plain JSON numbers which System.Text.Json can handle
                int i => i,
                long l => l,
                double d => d,
                float f => f,
                
                // Special types
                DateTime dt => ((DateTimeOffset)dt).ToUnixTimeMilliseconds(),
                Guid guid => guid.ToString("D"),
                
                // Graph elements
                InMemoryVertex vertex => SerializeVertex(vertex),
                InMemoryEdge edge => SerializeEdge(edge),
                
                // Collections
                System.Collections.IEnumerable enumerable when !(value is string) => 
                    SerializeCollection(enumerable),
                
                // Default fallback
                _ => value.ToString()
            };
        }

        /// <summary>
        /// Serialize vertex according to TinkerPop Vertex specification
        /// Simplified for maximum Gremlin.Net v3.7.1 compatibility
        /// </summary>
        private Dictionary<string, object> SerializeVertex(InMemoryVertex vertex)
        {
            // Simple vertex format without GraphSON type wrapping for better compatibility
            return new Dictionary<string, object>
            {
                ["id"] = vertex.Id,
                ["label"] = vertex.Label,
                ["type"] = "vertex",
                ["properties"] = SerializeVertexProperties(vertex.Properties)
            };
        }

        /// <summary>
        /// Serialize edge according to TinkerPop Edge specification
        /// Simplified for maximum Gremlin.Net v3.7.1 compatibility
        /// </summary>
        private Dictionary<string, object> SerializeEdge(InMemoryEdge edge)
        {
            // Simple edge format without GraphSON type wrapping for better compatibility
            return new Dictionary<string, object>
            {
                ["id"] = edge.Id,
                ["label"] = edge.Label,
                ["type"] = "edge",
                ["inV"] = edge.InVertexId,
                ["outV"] = edge.OutVertexId,
                ["properties"] = SerializeEdgeProperties(edge.Properties)
            };
        }

        /// <summary>
        /// Serialize vertex properties (simplified format for Gremlin.Net compatibility)
        /// </summary>
        private Dictionary<string, object> SerializeVertexProperties(Dictionary<string, object> properties)
        {
            if (properties == null || properties.Count == 0)
                return new Dictionary<string, object>();

            var result = new Dictionary<string, object>();
            foreach (var kvp in properties)
            {
                // Simple property format for maximum compatibility
                result[kvp.Key] = SerializePropertyValue(kvp.Value);
            }

            return result;
        }

        /// <summary>
        /// Serialize edge properties (simple key-value format)
        /// </summary>
        private Dictionary<string, object> SerializeEdgeProperties(Dictionary<string, object> properties)
        {
            if (properties == null || properties.Count == 0)
                return new Dictionary<string, object>();

            var result = new Dictionary<string, object>();
            foreach (var kvp in properties)
            {
                result[kvp.Key] = SerializePropertyValue(kvp.Value);
            }

            return result;
        }

        /// <summary>
        /// Serialize property values with appropriate type annotations
        /// </summary>
        private object SerializePropertyValue(object value)
        {
            return value switch
            {
                null => null,
                string s => s,
                bool b => b,
                // Use plain JSON numbers for System.Text.Json compatibility
                int i => i,
                long l => l,
                double d => d,
                float f => f,
                DateTime dt => ((DateTimeOffset)dt).ToUnixTimeMilliseconds(),
                Guid guid => guid.ToString("D"),
                _ => value.ToString()
            };
        }

        /// <summary>
        /// Serialize collections as simple arrays for Gremlin.Net compatibility
        /// </summary>
        private object[] SerializeCollection(System.Collections.IEnumerable enumerable)
        {
            var list = new List<object>();
            foreach (var item in enumerable)
            {
                list.Add(SerializeDataValue(item));
            }

            return list.ToArray();
        }

        /// <summary>
        /// Deserialize message from Gremlin.Net client with MIME-type prefix support
        /// </summary>
        public TinkerPopMessage DeserializeMessage(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new TinkerPopProtocolException("JSON input cannot be null or empty");

            try
            {
                // Handle TinkerPop MIME-type prefixed messages (e.g., from Gremlin.Net)
                // Format: "!application/vnd.gremlin-v3.0+json{actual json}"
                string actualJson = json;
                if (json.StartsWith("!"))
                {
                    Console.WriteLine($"[DEBUG] Detected MIME-prefixed message");
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
                
                return new TinkerPopMessage
                {
                    RequestId = ParseRequestId(jObject["requestId"]),
                    Op = jObject["op"]?.ToString() ?? TinkerPopOperations.Eval,
                    Processor = jObject["processor"]?.ToString() ?? "",
                    Args = DeserializeArgs(jObject["args"] as JObject)
                };
            }
            catch (Exception ex)
            {
                throw new TinkerPopProtocolException($"Failed to deserialize message: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Parse request ID from various formats
        /// </summary>
        private Guid ParseRequestId(JToken token)
        {
            if (token == null) return Guid.NewGuid();

            try
            {
                // Handle both plain string and typed UUID formats
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
        /// Deserialize message arguments
        /// </summary>
        private Dictionary<string, object> DeserializeArgs(JObject argsObject)
        {
            if (argsObject == null) return new Dictionary<string, object>();

            var args = new Dictionary<string, object>();
            foreach (var property in argsObject.Properties())
            {
                args[property.Name] = property.Name switch
                {
                    "gremlin" => property.Value?.ToString() ?? "",
                    "bindings" => DeserializeBindings(property.Value as JObject),
                    "language" => property.Value?.ToString() ?? "gremlin-groovy",
                    _ => property.Value?.ToObject<object>()
                };
            }

            return args;
        }

        /// <summary>
        /// Deserialize bindings object
        /// </summary>
        private Dictionary<string, object> DeserializeBindings(JObject bindingsObject)
        {
            if (bindingsObject == null) return new Dictionary<string, object>();

            var bindings = new Dictionary<string, object>();
            foreach (var property in bindingsObject.Properties())
            {
                bindings[property.Name] = property.Value?.ToObject<object>();
            }

            return bindings;
        }

        /// <summary>
        /// Create error response
        /// </summary>
        public TinkerPopResponse CreateErrorResponse(Guid requestId, int statusCode, string message, Exception exception = null)
        {
            var attributes = new Dictionary<string, object>();
            if (exception != null)
            {
                attributes["exceptionType"] = exception.GetType().Name;
                attributes["stackTrace"] = exception.StackTrace ?? "";
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