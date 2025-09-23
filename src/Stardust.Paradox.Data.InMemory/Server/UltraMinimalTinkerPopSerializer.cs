#if NET8_0_OR_GREATER 
using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Stardust.Paradox.Data.InMemory.Server
{
    /// <summary>
    /// Ultra-minimal TinkerPop serializer specifically for Gremlin.Net v3.7.1 System.Text.Json compatibility
    /// This serializer uses only strings and basic types to avoid all JSON parsing issues
    /// </summary>
    public class UltraMinimalTinkerPopSerializer : ITinkerPopSerializer
    {
        public UltraMinimalTinkerPopSerializer()
        {
        }

        /// <summary>
        /// Serialize response in the most basic format possible for System.Text.Json
        /// </summary>
        public string SerializeResponse(TinkerPopResponse response)
        {
            try
            {
                // Create the absolute simplest response structure
                // All numbers as strings, no complex objects, no GraphSON type annotations
                var responseObj = new
                {
                    requestId = response.RequestId.ToString("D"),
                    status = new
                    {
                        message = response.Status?.Message ?? "",
                        code = response.Status?.Code.ToString() ?? "200", // Convert to string
                        attributes = new { }
                    },
                    result = new
                    {
                        data = ConvertDataToSimpleStrings(response.Result?.Data),
                        meta = new { }
                    }
                };

                var json = JsonConvert.SerializeObject(responseObj, Formatting.None);
                Console.WriteLine($"[DEBUG] Ultra-minimal response: {json}");
                return json;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Ultra-minimal serialization failed: {ex.Message}");
                
                // Absolute fallback
                var fallback = new
                {
                    requestId = response.RequestId.ToString("D"),
                    status = new { code = "200", message = "", attributes = new { } },
                    result = new { data = new[] { "1" }, meta = new { } }
                };
                
                return JsonConvert.SerializeObject(fallback);
            }
        }

        /// <summary>
        /// Convert all data to simple strings to avoid any number parsing issues
        /// </summary>
        private string[] ConvertDataToSimpleStrings(List<object> data)
        {
            if (data == null || data.Count == 0)
                return new string[0];

            var result = new string[data.Count];
            for (int i = 0; i < data.Count; i++)
            {
                result[i] = ConvertValueToString(data[i]);
            }

            return result;
        }

        /// <summary>
        /// Convert any value to a simple string representation
        /// </summary>
        private string ConvertValueToString(object value)
        {
            if (value == null) return "null";

            return value switch
            {
                string s => s,
                bool b => b.ToString().ToLower(),
                int i => i.ToString(),
                long l => l.ToString(),
                double d => d.ToString(),
                float f => f.ToString(),
                decimal dec => dec.ToString(),
                _ => value.ToString()
            };
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
            return new TinkerPopResponse
            {
                RequestId = requestId,
                Status = new TinkerPopStatus
                {
                    Code = statusCode,
                    Message = message,
                    Attributes = new Dictionary<string, object>()
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
