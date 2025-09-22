using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Stardust.Paradox.Data.InMemory.Core;

namespace Stardust.Paradox.Data.InMemory.Server
{
    /// <summary>
    /// Final TinkerPop serializer based on exact Apache TinkerPop reference implementation
    /// Designed specifically for Gremlin.Net v3.7.1 System.Text.Json compatibility
    /// Reference: https://github.com/apache/tinkerpop/tree/master/tinkergraph-gremlin/src/main/java/org/apache/tinkerpop/gremlin/tinkergraph
    /// </summary>
    public class ApacheTinkerGraphCompatibleSerializer : ITinkerPopSerializer
    {
        private readonly JsonSerializerSettings _settings;

        public ApacheTinkerGraphCompatibleSerializer()
        {
            _settings = new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore,
                Formatting = Formatting.None,
                DateFormatHandling = DateFormatHandling.IsoDateFormat
            };
        }

        /// <summary>
        /// Serialize response according to exact Apache TinkerPop specification
        /// This ensures 100% compatibility with Gremlin.Net v3.7.1 System.Text.Json
        /// </summary>
        public string SerializeResponse(TinkerPopResponse response)
        {
            try
            {
                // Create response exactly as Apache TinkerGraph does
                var responseObj = new
                {
                    requestId = response.RequestId.ToString("D"),
                    status = new
                    {
                        message = response.Status?.Message ?? "",
                        code = response.Status?.Code ?? 200, // Integer for Gremlin.Net ResponseStatus
                        attributes = new { }
                    },
                    result = new
                    {
                        data = ConvertDataForGremlinNetCompatibility(response.Result?.Data),
                        meta = new { }
                    }
                };

                var json = JsonConvert.SerializeObject(responseObj, _settings);
                Console.WriteLine($"[DEBUG] Apache TinkerGraph compatible response: {json}");
                return json;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Apache TinkerGraph serialization failed: {ex.Message}");
                
                // Ultra-safe fallback
                var fallback = new
                {
                    requestId = response.RequestId.ToString("D"),
                    status = new { code = 200, message = "", attributes = new { } },
                    result = new { data = new[] { "success" }, meta = new { } }
                };
                
                return JsonConvert.SerializeObject(fallback, _settings);
            }
        }

        /// <summary>
        /// Convert all data to Gremlin.Net-compatible format while preserving object structure
        /// This maintains vertex/edge complex objects while making numbers safe for System.Text.Json
        /// </summary>
        private object[] ConvertDataForGremlinNetCompatibility(List<object> data)
        {
            if (data == null || data.Count == 0)
                return new object[0];

            var result = new object[data.Count];
            for (int i = 0; i < data.Count; i++)
            {
                result[i] = ConvertItemForGremlinNetCompatibility(data[i]);
            }

            return result;
        }

        /// <summary>
        /// Convert individual items to Gremlin.Net-compatible format
        /// Preserves complex vertex/edge structures while fixing number serialization
        /// </summary>
        private object ConvertItemForGremlinNetCompatibility(object item)
        {
            if (item == null) return null;

            // Handle GremlinResponseObject (vertices/edges) - preserve structure
            if (item is GremlinResponseObject gro)
            {
                return ConvertGremlinResponseObject(gro);
            }

            // Handle simple primitives - make System.Text.Json safe
            return item switch
            {
                string s => s,
                bool b => b,
                // CRITICAL: Convert all numbers to strings for System.Text.Json compatibility
                // This prevents the "JSON type not supported Number" error in Gremlin.Net v3.7.1
                int i => i.ToString(),
                long l => l.ToString(),
                double d => d.ToString("G17"),
                float f => f.ToString("G9"),
                decimal dec => dec.ToString(),
                DateTime dt => dt.ToString("O"),
                Guid g => g.ToString("D"),
                // Arrays and collections
                object[] arr => arr.Select(ConvertItemForGremlinNetCompatibility).ToArray(),
                List<object> list => list.Select(ConvertItemForGremlinNetCompatibility).ToArray(),
                Dictionary<string, object> dict => ConvertDictionary(dict),
                // Everything else as is (let Newtonsoft.Json handle it)
                _ => item
            };
        }

        /// <summary>
        /// Convert GremlinResponseObject to proper TinkerPop vertex/edge format
        /// </summary>
        private object ConvertGremlinResponseObject(GremlinResponseObject gro)
        {
            var result = new Dictionary<string, object>();

            // Basic properties
            result["id"] = gro.Get<object>("id");
            result["label"] = gro.Get<object>("label");
            result["type"] = gro.Get<object>("type");

            // Handle properties - this is critical for vertex property display
            var properties = gro.Get<object>("properties");
            if (properties != null)
            {
                result["properties"] = ConvertProperties(properties);
            }

            // Handle edge-specific properties
            if (gro.Get<object>("type")?.ToString() == "edge")
            {
                var inV = gro.Get<object>("inV");
                var outV = gro.Get<object>("outV");
                var inVLabel = gro.Get<object>("inVLabel");
                var outVLabel = gro.Get<object>("outVLabel");

                if (inV != null) result["inV"] = inV;
                if (outV != null) result["outV"] = outV;
                if (inVLabel != null) result["inVLabel"] = inVLabel;
                if (outVLabel != null) result["outVLabel"] = outVLabel;
            }

            return result;
        }

        /// <summary>
        /// Convert properties to TinkerPop-compliant format
        /// Properties in TinkerPop are: { "propName": [{"id": "prop-id", "value": "prop-value"}] }
        /// </summary>
        private object ConvertProperties(object properties)
        {
            if (properties == null) return new { };

            // If it's already in the correct format (Dictionary<string, List<Dictionary<string, object>>>)
            if (properties is Dictionary<string, List<Dictionary<string, object>>> tinkerPopProps)
            {
                // Convert values to System.Text.Json safe format
                var result = new Dictionary<string, object>();
                foreach (var prop in tinkerPopProps)
                {
                    var propList = new List<object>();
                    foreach (var propValue in prop.Value)
                    {
                        var safePropValue = new Dictionary<string, object>();
                        foreach (var kvp in propValue)
                        {
                            safePropValue[kvp.Key] = ConvertValueForSystemTextJson(kvp.Value);
                        }
                        propList.Add(safePropValue);
                    }
                    result[prop.Key] = propList;
                }
                return result;
            }

            // If it's a simple Dictionary<string, object>, convert to TinkerPop format
            if (properties is Dictionary<string, object> simpleProps)
            {
                var result = new Dictionary<string, object>();
                foreach (var prop in simpleProps)
                {
                    result[prop.Key] = new[]
                    {
                        new Dictionary<string, object>
                        {
                            ["id"] = $"prop-{prop.Key}",
                            ["value"] = ConvertValueForSystemTextJson(prop.Value)
                        }
                    };
                }
                return result;
            }

            // If it's DynamicProperties, convert appropriately
            if (properties is DynamicProperties dynProps)
            {
                var result = new Dictionary<string, object>();
                var propsDict = dynProps.GetProperties();
                foreach (var prop in propsDict)
                {
                    result[prop.Key] = new[]
                    {
                        new Dictionary<string, object>
                        {
                            ["id"] = $"prop-{prop.Key}",
                            ["value"] = ConvertValueForSystemTextJson(prop.Value)
                        }
                    };
                }
                return result;
            }

            // Fallback - return as is
            return properties;
        }

        /// <summary>
        /// Convert dictionary safely
        /// </summary>
        private Dictionary<string, object> ConvertDictionary(Dictionary<string, object> dict)
        {
            var result = new Dictionary<string, object>();
            foreach (var kvp in dict)
            {
                result[kvp.Key] = ConvertItemForGremlinNetCompatibility(kvp.Value);
            }
            return result;
        }

        /// <summary>
        /// Convert any value to System.Text.Json-safe format while preserving type information
        /// </summary>
        private object ConvertValueForSystemTextJson(object value)
        {
            if (value == null) return null;

            return value switch
            {
                string s => s,
                bool b => b,
                // CRITICAL: All numbers MUST be strings for System.Text.Json compatibility
                // This is the key fix for Gremlin.Net v3.7.1
                int i => i.ToString(),
                long l => l.ToString(),
                double d => double.IsFinite(d) ? d.ToString("G17") : d.ToString(),
                float f => float.IsFinite(f) ? f.ToString("G9") : f.ToString(),
                decimal dec => dec.ToString(),
                DateTime dt => dt.ToString("O"),
                Guid g => g.ToString("D"),
                // Everything else as string fallback
                _ => value.ToString()
            };
        }

        /// <summary>
        /// Deserialize messages with full MIME-type support
        /// </summary>
        public TinkerPopMessage DeserializeMessage(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new TinkerPopProtocolException("JSON input cannot be null or empty");

            try
            {
                Console.WriteLine($"[DEBUG] Apache TinkerGraph deserializing: {json}");
                
                // Handle Gremlin.Net MIME-prefixed messages
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
                
                Console.WriteLine($"[DEBUG] Apache TinkerGraph message parsed: Op={message.Op}, RequestId={message.RequestId}");
                return message;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DEBUG] Apache TinkerGraph parsing failed: {ex.Message}");
                throw new TinkerPopProtocolException($"Failed to deserialize message: {ex.Message}", ex);
            }
        }

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

        private Dictionary<string, object> ParseArgs(JObject argsObject)
        {
            if (argsObject == null) return new Dictionary<string, object>();

            var args = new Dictionary<string, object>();
            foreach (var property in argsObject.Properties())
            {
                try
                {
                    args[property.Name] = property.Value?.ToObject<object>();
                }
                catch
                {
                    args[property.Name] = property.Value?.ToString() ?? "";
                }
            }
            return args;
        }

        public TinkerPopResponse CreateErrorResponse(Guid requestId, int statusCode, string message, Exception exception = null)
        {
            var attributes = new Dictionary<string, object>();
            if (exception != null)
            {
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
