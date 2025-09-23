#if NET8_0_OR_GREATER || NETCOREAPP3_1_OR_GREATER
using System;
using Stardust.Paradox.Data.InMemory.Core;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Stardust.Paradox.Data.InMemory.Server
{
    /// <summary>
    /// GraphSON serializer for TinkerPop WebSocket protocol
    /// Supports GraphSON v1, v2, and v3 formats without external dependencies
    /// </summary>
    public class TinkerPopGraphSONSerializer : ITinkerPopSerializer
    {
        private readonly GraphSONVersion _version;
        private readonly JsonSerializerSettings _jsonSettings;

        public TinkerPopGraphSONSerializer(GraphSONVersion version = GraphSONVersion.V3)
        {
            _version = version;
            _jsonSettings = new JsonSerializerSettings
            {
                DateFormatHandling = DateFormatHandling.MicrosoftDateFormat,
                DateTimeZoneHandling = DateTimeZoneHandling.Utc,
                NullValueHandling = NullValueHandling.Ignore,
                Formatting = Formatting.None
            };

            ConfigureGraphSONSerializers();
        }

        /// <summary>
        /// Serialize a TinkerPop message to GraphSON format
        /// </summary>
        public string SerializeMessage(TinkerPopMessage message)
        {
            var messageObject = new
            {
                requestId = SerializeRequestId(message.RequestId),
                op = message.Op,
                processor = message.Processor,
                args = SerializeArgs(message.Args)
            };

            return JsonConvert.SerializeObject(messageObject, _jsonSettings);
        }

        /// <summary>
        /// Serialize a TinkerPop response to JSON with enhanced version-specific formatting
        /// </summary>
        public string SerializeResponse(TinkerPopResponse response)
        {
            try
            {
                // Create response object with exact TinkerPop specification format for Gremlin.Net v3.7.1 compatibility
                // Based on Apache TinkerPop reference implementation: 
                // https://github.com/apache/tinkerpop/blob/master/gremlin-driver/src/main/java/org/apache/tinkerpop/gremlin/driver/message/ResponseMessage.java
                var responseObject = new
                {
                    requestId = response.RequestId.ToString("D"), // Always use dashed UUID string format for Gremlin.Net compatibility
                    status = new
                    {
                        message = response.Status?.Message ?? "",
                        code = response.Status?.Code ?? TinkerPopStatusCodes.Success, // Use string code directly
                        attributes = CreateSimpleAttributes(response.Status?.Attributes)
                    },
                    result = new
                    {
                        data = CreateCompatibleData(response.Result?.Data),
                        meta = CreateSimpleMeta(response.Result?.Meta)
                    }
                };

                // Use minimal JSON serialization settings for maximum compatibility
                var settings = new JsonSerializerSettings
                {
                    NullValueHandling = NullValueHandling.Ignore,
                    DefaultValueHandling = DefaultValueHandling.Include, // Include defaults for TinkerPop compliance
                    ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
                    DateFormatHandling = DateFormatHandling.IsoDateFormat,
                    Formatting = Formatting.None
                    // Don't add custom converters for Gremlin.Net compatibility
                };

                var json = JsonConvert.SerializeObject(responseObject, settings);
                
                // Debug log to see what we're sending
                Console.WriteLine($"[DEBUG] Serialized TinkerPop response: {json}");
                
                return json;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DEBUG] Response serialization failed: {ex.Message}");
                
                // Fallback: create a minimal compliant response
                var fallbackResponse = new
                {
                    requestId = response.RequestId.ToString("D"),
                    status = new
                    {
                        code = 500,
                        message = $"Serialization error: {ex.Message}",
                        attributes = new { }
                    },
                    result = new
                    {
                        data = new object[0],
                        meta = new { }
                    }
                };

                var fallbackJson = JsonConvert.SerializeObject(fallbackResponse);
                Console.WriteLine($"[DEBUG] Fallback response: {fallbackJson}");
                return fallbackJson;
            }
        }

        /// <summary>
        /// Create simple attributes dictionary for Gremlin.Net compatibility
        /// </summary>
        private object CreateSimpleAttributes(Dictionary<string, object> attributes)
        {
            if (attributes == null || attributes.Count == 0) 
                return new { };
            
            var result = new Dictionary<string, object>();
            foreach (var kvp in attributes)
            {
                // Only include simple types that System.Text.Json can handle
                try
                {
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
                catch
                {
                    result[kvp.Key] = kvp.Value?.ToString() ?? "";
                }
            }
            
            return result;
        }

        /// <summary>
        /// Create compatible data array for Gremlin.Net
        /// </summary>
        private object[] CreateCompatibleData(List<object> data)
        {
            if (data == null) return new object[0];

            var result = new object[data.Count];
            for (int i = 0; i < data.Count; i++)
            {
                try
                {
                    result[i] = SerializeDataItem(data[i]);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[DEBUG] Failed to serialize data item {i}: {ex.Message}");
                    result[i] = data[i]?.ToString() ?? null;
                }
            }

            return result;
        }

        /// <summary>
        /// Serialize individual data items with Gremlin.Net compatibility
        /// </summary>
        private object SerializeDataItem(object item)
        {
            if (item == null) return null;

            return item switch
            {
                // Primitive types - keep as-is for Gremlin.Net compatibility
                string s => s,
                int i => i,
                long l => l,
                double d => d,
                float f => f,
                bool b => b,
                
                // Graph elements - serialize according to TinkerPop GraphSON specification
                InMemoryVertex vertex => SerializeVertexForGremlinNet(vertex),
                InMemoryEdge edge => SerializeEdgeForGremlinNet(edge),
                
                // Collections
                System.Collections.IEnumerable enumerable when !(item is string) => 
                    enumerable.Cast<object>().Select(SerializeDataItem).ToArray(),
                
                // Default: convert to string
                _ => item.ToString()
            };
        }

        /// <summary>
        /// Serialize vertex for Gremlin.Net compatibility (GraphSON v3 format)
        /// </summary>
        private object SerializeVertexForGremlinNet(InMemoryVertex vertex)
        {
            return new
            {
                @type = "g:Vertex",
                @value = new
                {
                    id = vertex.Id,
                    label = vertex.Label,
                    properties = SerializeVertexPropertiesForGremlinNet(vertex.Properties)
                }
            };
        }

        /// <summary>
        /// Serialize edge for Gremlin.Net compatibility (GraphSON v3 format)
        /// </summary>
        private object SerializeEdgeForGremlinNet(InMemoryEdge edge)
        {
            return new
            {
                @type = "g:Edge",
                @value = new
                {
                    id = edge.Id,
                    label = edge.Label,
                    inV = edge.InVertexId,
                    outV = edge.OutVertexId,
                    properties = SerializeEdgePropertiesForGremlinNet(edge.Properties)
                }
            };
        }

        /// <summary>
        /// Serialize vertex properties for Gremlin.Net (GraphSON v3 multi-property format)
        /// </summary>
        private object SerializeVertexPropertiesForGremlinNet(Dictionary<string, object> properties)
        {
            if (properties == null || properties.Count == 0) 
                return new { };

            var result = new Dictionary<string, object>();
            
            foreach (var kvp in properties)
            {
                try
                {
                    // GraphSON v3 vertex properties are arrays of property objects
                    result[kvp.Key] = new[]
                    {
                        new
                        {
                            id = Guid.NewGuid().ToString(),
                            value = SerializePropertyValue(kvp.Value)
                        }
                    };
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[DEBUG] Failed to serialize vertex property '{kvp.Key}': {ex.Message}");
                    result[kvp.Key] = new[] { new { id = Guid.NewGuid().ToString(), value = kvp.Value?.ToString() ?? "" } };
                }
            }

            return result;
        }

        /// <summary>
        /// Serialize edge properties for Gremlin.Net (simple key-value format)
        /// </summary>
        private object SerializeEdgePropertiesForGremlinNet(Dictionary<string, object> properties)
        {
            if (properties == null || properties.Count == 0) 
                return new { };

            var result = new Dictionary<string, object>();
            
            foreach (var kvp in properties)
            {
                try
                {
                    result[kvp.Key] = SerializePropertyValue(kvp.Value);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[DEBUG] Failed to serialize edge property '{kvp.Key}': {ex.Message}");
                    result[kvp.Key] = kvp.Value?.ToString() ?? "";
                }
            }

            return result;
        }

        /// <summary>
        /// Serialize property values with type annotations for Gremlin.Net
        /// </summary>
        private object SerializePropertyValue(object value)
        {
            return value switch
            {
                null => null,
                string s => s,
                int i => new { @type = "g:Int32", @value = i },
                long l => new { @type = "g:Int64", @value = l },
                double d => new { @type = "g:Double", @value = d },
                float f => new { @type = "g:Float", @value = f },
                bool b => new { @type = "g:Boolean", @value = b },
                DateTime dt => new { @type = "g:Date", @value = ((DateTimeOffset)dt).ToUnixTimeMilliseconds() },
                Guid guid => new { @type = "g:UUID", @value = guid.ToString("D") },
                _ => value.ToString()
            };
        }

        /// <summary>
        /// Create simple meta dictionary for Gremlin.Net compatibility
        /// </summary>
        private object CreateSimpleMeta(Dictionary<string, object> meta)
        {
            // For Gremlin.Net v3.7.1 compatibility, return empty object for meta
            // Complex meta serialization can cause compatibility issues
            return new { };
        }

        /// <summary>
        /// Deserialize a TinkerPop message from GraphSON format with enhanced version-specific parsing
        /// </summary>
        public TinkerPopMessage DeserializeMessage(string json)
        {
            // Validate input
            if (string.IsNullOrWhiteSpace(json))
            {
                throw new TinkerPopProtocolException("JSON input cannot be null or empty");
            }

            try
            {
                var jObject = JObject.Parse(json);
                
                var message = new TinkerPopMessage
                {
                    RequestId = ParseRequestId(jObject["requestId"]),
                    Op = jObject["op"]?.ToString() ?? TinkerPopOperations.Eval,
                    Processor = jObject["processor"]?.ToString() ?? "",
                    Args = DeserializeArgs(jObject["args"] as JObject)
                };

                // Enhanced validation for Gremlin.Net compatibility
                if (message.RequestId == Guid.Empty)
                {
                    message.RequestId = Guid.NewGuid();
                }

                // Handle operation variations
                if (string.IsNullOrEmpty(message.Op))
                {
                    message.Op = TinkerPopOperations.Eval;
                }

                return message;
            }
            catch (JsonReaderException ex)
            {
                throw new TinkerPopProtocolException($"Invalid JSON format: {ex.Message}", ex);
            }
            catch (JsonException ex)
            {
                throw new TinkerPopProtocolException($"JSON parsing error: {ex.Message}", ex);
            }
            catch (Exception ex) when (!(ex is TinkerPopProtocolException))
            {
                throw new TinkerPopProtocolException($"Failed to deserialize TinkerPop message: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Create an error response
        /// </summary>
        public TinkerPopResponse CreateErrorResponse(Guid requestId, int statusCode, string message, Exception exception = null)
        {
            var response = new TinkerPopResponse
            {
                RequestId = requestId,
                Status = new TinkerPopStatus
                {
                    Code = statusCode,
                    Message = message,
                    Attributes = new Dictionary<string, object>()
                }
            };

            if (exception != null)
            {
                response.Status.Attributes["stackTrace"] = exception.StackTrace;
                response.Status.Attributes["exceptionType"] = exception.GetType().Name;
            }

            return response;
        }

        /// <summary>
        /// Create a success response
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
                    Meta = meta ?? CreateResponseMetadata()
                }
            };
        }

        /// <summary>
        /// Get MIME type for the current GraphSON version
        /// </summary>
        public string GetMimeType()
        {
            return _version switch
            {
                GraphSONVersion.V1 => TinkerPopMimeTypes.GraphSONV1,
                GraphSONVersion.V2 => TinkerPopMimeTypes.GraphSONV2,
                GraphSONVersion.V3 => TinkerPopMimeTypes.GraphSONV3,
                _ => TinkerPopMimeTypes.Json
            };
        }

        // Private helper methods

        private object SerializeRequestId(Guid requestId)
        {
            // For maximum Gremlin.Net compatibility, always use the standard UUID format
            // Gremlin.Net v3.7.1 with System.Text.Json expects consistent UUID format
            return _version switch
            {
                GraphSONVersion.V1 => requestId.ToString("D"), // Always use dashed format
                GraphSONVersion.V2 or GraphSONVersion.V3 => requestId.ToString("D"), // For latest Gremlin.Net, use string UUID
                _ => requestId.ToString("D")
            };
        }

        private List<object> SerializeData(List<object> data)
        {
            if (data == null) return new List<object>();

            var serializedData = new List<object>();
            foreach (var item in data)
            {
                try
                {
                    serializedData.Add(SerializeValue(item));
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[DEBUG] Failed to serialize data item: {ex.Message}");
                    serializedData.Add(item?.ToString() ?? "null");
                }
            }

            return serializedData;
        }

        private object SerializeValue(object value)
        {
            if (value == null) return null;

            return _version switch
            {
                GraphSONVersion.V3 => SerializeValueV3(value),
                GraphSONVersion.V2 => SerializeValueV2(value),
                GraphSONVersion.V1 => SerializeValueV1(value),
                _ => SerializeValueV1(value)
            };
        }

        private object SerializeValueV1(object value)
        {
            return value;
        }

        private object SerializeValueV2(object value)
        {
            return value switch
            {
                null => null,
                int i => new { @type = "g:Int32", @value = i },
                long l => new { @type = "g:Int64", @value = l },
                float f => new { @type = "g:Float", @value = f },
                double d => new { @type = "g:Double", @value = d },
                DateTime dt => new { @type = "g:Date", @value = ((DateTimeOffset)dt).ToUnixTimeMilliseconds() },
                InMemoryVertex vertex => SerializeVertex(vertex),
                InMemoryEdge edge => SerializeEdge(edge),
                System.Collections.IEnumerable enumerable when !(value is string) => SerializeEnumerable(enumerable),
                _ => value
            };
        }

        private object SerializeValueV3(object value)
        {
            return value switch
            {
                null => null,
                int i => new { @type = "g:Int32", @value = i },
                long l => new { @type = "g:Int64", @value = l },
                float f => new { @type = "g:Float", @value = f },
                double d => new { @type = "g:Double", @value = d },
                bool b => new { @type = "g:Boolean", @value = b },
                string s => s,
                DateTime dt => new { @type = "g:Date", @value = ((DateTimeOffset)dt).ToUnixTimeMilliseconds() },
                Guid guid => new { @type = "g:UUID", @value = guid.ToString() },
                InMemoryVertex vertex => SerializeVertex(vertex),
                InMemoryEdge edge => SerializeEdge(edge),
                System.Collections.IEnumerable enumerable when !(value is string) => SerializeEnumerable(enumerable),
                _ => value
            };
        }

        private object SerializeEnumerable(System.Collections.IEnumerable enumerable)
        {
            var list = new List<object>();
            foreach (var item in enumerable)
            {
                list.Add(SerializeValue(item));
            }
            
            return _version switch
            {
                GraphSONVersion.V3 => new { @type = "g:List", @value = list },
                _ => list
            };
        }

        private object SerializeVertex(InMemoryVertex vertex)
        {
            var vertexValue = new
            {
                id = SerializeValue(vertex.Id),
                label = vertex.Label,
                properties = SerializeVertexProperties(vertex.Properties)
            };

            return _version switch
            {
                GraphSONVersion.V1 => vertexValue,
                _ => new { @type = "g:Vertex", @value = vertexValue }
            };
        }

        private object SerializeEdge(InMemoryEdge edge)
        {
            var edgeValue = new
            {
                id = SerializeValue(edge.Id),
                label = edge.Label,
                inV = SerializeValue(edge.InVertexId),
                outV = SerializeValue(edge.OutVertexId),
                properties = SerializeEdgeProperties(edge.Properties)
            };

            return _version switch
            {
                GraphSONVersion.V1 => edgeValue,
                _ => new { @type = "g:Edge", @value = edgeValue }
            };
        }

        private object SerializeVertexProperties(Dictionary<string, object> properties)
        {
            if (properties == null || properties.Count == 0) 
                return new { };

            var serializedProps = new Dictionary<string, object>();
            
            foreach (var kvp in properties)
            {
                try
                {
                    var propertyValue = _version switch
                    {
                        GraphSONVersion.V1 => SerializeValue(kvp.Value),
                        _ => new List<object>
                        {
                            new
                            {
                                id = Guid.NewGuid().ToString(),
                                value = SerializeValue(kvp.Value)
                            }
                        }
                    };
                    
                    serializedProps[kvp.Key] = propertyValue;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[DEBUG] Failed to serialize vertex property '{kvp.Key}': {ex.Message}");
                    serializedProps[kvp.Key] = kvp.Value?.ToString() ?? "";
                }
            }

            return serializedProps;
        }

        private object SerializeEdgeProperties(Dictionary<string, object> properties)
        {
            if (properties == null || properties.Count == 0) 
                return new { };

            var serializedProps = new Dictionary<string, object>();
            
            foreach (var kvp in properties)
            {
                try
                {
                    serializedProps[kvp.Key] = SerializeValue(kvp.Value);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[DEBUG] Failed to serialize edge property '{kvp.Key}': {ex.Message}");
                    serializedProps[kvp.Key] = kvp.Value?.ToString() ?? "";
                }
            }

            return serializedProps;
        }

        private object SerializeArgs(Dictionary<string, object> args)
        {
            if (args == null) return new { };

            var serializedArgs = new Dictionary<string, object>();
            
            foreach (var kvp in args)
            {
                if (kvp.Key == "gremlin" && kvp.Value is string)
                {
                    serializedArgs[kvp.Key] = kvp.Value;
                }
                else if (kvp.Key == "bindings" && kvp.Value is Dictionary<string, object> bindings)
                {
                    serializedArgs[kvp.Key] = SerializeBindings(bindings);
                }
                else
                {
                    serializedArgs[kvp.Key] = SerializeValue(kvp.Value);
                }
            }

            return serializedArgs;
        }

        private Dictionary<string, object> SerializeBindings(Dictionary<string, object> bindings)
        {
            var serializedBindings = new Dictionary<string, object>();
            
            foreach (var kvp in bindings)
            {
                serializedBindings[kvp.Key] = SerializeValue(kvp.Value);
            }

            return serializedBindings;
        }

        private object SerializeAttributes(Dictionary<string, object> attributes)
        {
            // For Gremlin.Net v3.7.1 compatibility, keep attributes simple
            if (attributes == null || attributes.Count == 0) 
                return new { };
            
            var serializedAttributes = new Dictionary<string, object>();
            foreach (var kvp in attributes)
            {
                // Keep simple values to avoid serialization issues
                try
                {
                    serializedAttributes[kvp.Key] = kvp.Value switch
                    {
                        string s => s,
                        int i => i,
                        long l => l,
                        double d => d,
                        bool b => b,
                        null => null,
                        _ => kvp.Value.ToString()
                    };
                }
                catch
                {
                    serializedAttributes[kvp.Key] = kvp.Value?.ToString() ?? "";
                }
            }
            
            return serializedAttributes;
        }

        private object SerializeMeta(Dictionary<string, object> meta)
        {
            // For Gremlin.Net v3.7.1 compatibility, keep meta simple
            if (meta == null || meta.Count == 0) 
                return new { };
            
            // Return simple dictionary format for better compatibility
            var simpleMeta = new Dictionary<string, object>();
            foreach (var kvp in meta)
            {
                try
                {
                    simpleMeta[kvp.Key] = kvp.Value switch
                    {
                        string s => s,
                        int i => i,
                        long l => l,
                        double d => d,
                        bool b => b,
                        null => null,
                        _ => kvp.Value.ToString()
                    };
                }
                catch
                {
                    simpleMeta[kvp.Key] = kvp.Value?.ToString() ?? "";
                }
            }
            
            return simpleMeta;
        }

        private object CreateVersionSpecificMeta(Dictionary<string, object> meta)
        {
            return _version switch
            {
                GraphSONVersion.V1 => meta,
                _ => new { @type = "g:Map", @value = ConvertToMapFormat(meta) }
            };
        }

        private object CreateVersionSpecificMap(Dictionary<string, object> map)
        {
            return _version switch
            {
                GraphSONVersion.V1 => map,
                _ => new { @type = "g:Map", @value = ConvertToMapFormat(map) }
            };
        }

        private List<object> ConvertToMapFormat(Dictionary<string, object> map)
        {
            var mapFormat = new List<object>();
            foreach (var kvp in map)
            {
                mapFormat.Add(kvp.Key);
                mapFormat.Add(SerializeValue(kvp.Value));
            }
            return mapFormat;
        }

        private Dictionary<string, object> CreateResponseMetadata()
        {
            return _version switch
            {
                GraphSONVersion.V1 => new Dictionary<string, object>(),
                _ => new Dictionary<string, object>
                {
                    ["@type"] = "g:Map",
                    ["@value"] = new List<object>()
                }
            };
        }

        private List<JsonConverter> GetConvertersForVersion()
        {
            var converters = new List<JsonConverter>();
            
            switch (_version)
            {
                case GraphSONVersion.V2:
                    converters.Add(new GraphSONV2Converter());
                    break;
                case GraphSONVersion.V3:
                    converters.Add(new GraphSONV3Converter());
                    break;
            }
            
            return converters;
        }

        private void ConfigureGraphSONSerializers()
        {
            switch (_version)
            {
                case GraphSONVersion.V2:
                    _jsonSettings.Converters.Add(new GraphSONV2Converter());
                    break;
                case GraphSONVersion.V3:
                    _jsonSettings.Converters.Add(new GraphSONV3Converter());
                    break;
            }
        }

        private Guid ParseRequestId(JToken requestIdToken)
        {
            if (requestIdToken == null) return Guid.NewGuid();
            
            try
            {
                if (requestIdToken is JObject obj && obj.ContainsKey("@value"))
                {
                    var typeValue = obj["@type"]?.ToString();
                    var value = obj["@value"]?.ToString();
                    
                    if (typeValue == "g:UUID" && !string.IsNullOrEmpty(value))
                    {
                        return Guid.TryParse(value, out var guid) ? guid : Guid.NewGuid();
                    }
                }
                
                var stringValue = requestIdToken.ToString();
                if (!string.IsNullOrEmpty(stringValue))
                {
                    if (Guid.TryParse(stringValue, out var guid))
                    {
                        return guid;
                    }
                    
                    if (stringValue.Length == 32)
                    {
                        try
                        {
                            var formatted = $"{stringValue.Substring(0, 8)}-{stringValue.Substring(8, 4)}-{stringValue.Substring(12, 4)}-{stringValue.Substring(16, 4)}-{stringValue.Substring(20, 12)}";
                            if (Guid.TryParse(formatted, out var formattedGuid))
                            {
                                return formattedGuid;
                            }
                        }
                        catch
                        {
                            // Ignore formatting errors
                        }
                    }
                }
            }
            catch
            {
                // Ignore parsing errors
            }
            
            return Guid.NewGuid();
        }

        private Dictionary<string, object> DeserializeArgs(JObject argsObject)
        {
            if (argsObject == null) return new Dictionary<string, object>();

            var args = new Dictionary<string, object>();
            
            foreach (var property in argsObject.Properties())
            {
                try
                {
                    if (property.Name == "bindings" && property.Value is JObject bindingsObj)
                    {
                        args[property.Name] = DeserializeBindings(bindingsObj);
                    }
                    else if (property.Name == "gremlin")
                    {
                        args[property.Name] = DeserializeValue(property.Value)?.ToString() ?? "";
                    }
                    else if (property.Name == "language")
                    {
                        args[property.Name] = DeserializeValue(property.Value)?.ToString() ?? "gremlin-groovy";
                    }
                    else
                    {
                        args[property.Name] = DeserializeValue(property.Value);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[DEBUG] Failed to deserialize arg '{property.Name}': {ex.Message}");
                    args[property.Name] = property.Value?.ToString() ?? "";
                }
            }

            return args;
        }

        private Dictionary<string, object> DeserializeBindings(JObject bindingsObject)
        {
            var bindings = new Dictionary<string, object>();
            
            foreach (var property in bindingsObject.Properties())
            {
                bindings[property.Name] = DeserializeValue(property.Value);
            }

            return bindings;
        }

        private object DeserializeValue(JToken token)
        {
            if (token == null) return null;

            try
            {
                if (token is JObject obj && obj.ContainsKey("@type") && obj.ContainsKey("@value"))
                {
                    var type = obj["@type"]?.ToString();
                    var value = obj["@value"];

                    return type switch
                    {
                        "g:Int32" => value?.ToObject<int>() ?? 0,
                        "g:Int64" => value?.ToObject<long>() ?? 0L,
                        "g:Float" => value?.ToObject<float>() ?? 0f,
                        "g:Double" => value?.ToObject<double>() ?? 0.0,
                        "g:Date" => value != null ? DateTimeOffset.FromUnixTimeMilliseconds(value.ToObject<long>()).DateTime : DateTime.MinValue,
                        "g:UUID" => !string.IsNullOrEmpty(value?.ToString()) && Guid.TryParse(value.ToString(), out var g) ? g : Guid.Empty,
                        "g:String" => value?.ToString() ?? "",
                        "g:Boolean" => value?.ToObject<bool>() ?? false,
                        "g:List" => DeserializeList(value as JArray),
                        "g:Map" => DeserializeMap(value as JArray),
                        _ => value?.ToObject<object>()
                    };
                }

                if (token is JArray array)
                {
                    return array.Select(DeserializeValue).ToList();
                }

                return token.ToObject<object>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DEBUG] Failed to deserialize value: {ex.Message}");
                return token?.ToString();
            }
        }

        private List<object> DeserializeList(JArray array)
        {
            if (array == null) return new List<object>();
            
            return array.Select(DeserializeValue).ToList();
        }

        private Dictionary<string, object> DeserializeMap(JArray array)
        {
            var map = new Dictionary<string, object>();
            
            if (array == null || array.Count % 2 != 0) return map;
            
            try
            {
                for (int i = 0; i < array.Count; i += 2)
                {
                    var key = DeserializeValue(array[i])?.ToString();
                    var value = DeserializeValue(array[i + 1]);
                    
                    if (!string.IsNullOrEmpty(key))
                    {
                        map[key] = value;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DEBUG] Failed to deserialize map: {ex.Message}");
            }
            
            return map;
        }
    }

    /// <summary>
    /// GraphSON version enumeration
    /// </summary>
    public enum GraphSONVersion
    {
        V1,
        V2,
        V3
    }

    /// <summary>
    /// Custom JSON converter for GraphSON V2
    /// </summary>
    public class GraphSONV2Converter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(DateTime) || objectType == typeof(int) || objectType == typeof(long) || objectType == typeof(double);
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            if (value is DateTime dateTime)
            {
                writer.WriteStartObject();
                writer.WritePropertyName("@type");
                writer.WriteValue("g:Date");
                writer.WritePropertyName("@value");
                writer.WriteValue(((DateTimeOffset)dateTime).ToUnixTimeMilliseconds());
                writer.WriteEndObject();
            }
            else if (value is int intValue)
            {
                writer.WriteStartObject();
                writer.WritePropertyName("@type");
                writer.WriteValue("g:Int32");
                writer.WritePropertyName("@value");
                writer.WriteValue(intValue);
                writer.WriteEndObject();
            }
            else if (value is long longValue)
            {
                writer.WriteStartObject();
                writer.WritePropertyName("@type");
                writer.WriteValue("g:Int64");
                writer.WritePropertyName("@value");
                writer.WriteValue(longValue);
                writer.WriteEndObject();
            }
            else if (value is double doubleValue)
            {
                writer.WriteStartObject();
                writer.WritePropertyName("@type");
                writer.WriteValue("g:Double");
                writer.WritePropertyName("@value");
                writer.WriteValue(doubleValue);
                writer.WriteEndObject();
            }
            else
            {
                // For other types, write the raw value to avoid recursion
                writer.WriteValue(value);
            }
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            return serializer.Deserialize(reader, objectType);
        }
    }

    /// <summary>
    /// Custom JSON converter for GraphSON V3
    /// </summary>
    public class GraphSONV3Converter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(DateTime) || objectType == typeof(int) || objectType == typeof(long) || 
                   objectType == typeof(double) || objectType == typeof(Guid) || objectType == typeof(bool);
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            if (value is DateTime dateTime)
            {
                writer.WriteStartObject();
                writer.WritePropertyName("@type");
                writer.WriteValue("g:Date");
                writer.WritePropertyName("@value");
                writer.WriteValue(((DateTimeOffset)dateTime).ToUnixTimeMilliseconds());
                writer.WriteEndObject();
            }
            else if (value is Guid guid)
            {
                writer.WriteStartObject();
                writer.WritePropertyName("@type");
                writer.WriteValue("g:UUID");
                writer.WritePropertyName("@value");
                writer.WriteValue(guid.ToString());
                writer.WriteEndObject();
            }
            else if (value is int intValue)
            {
                writer.WriteStartObject();
                writer.WritePropertyName("@type");
                writer.WriteValue("g:Int32");
                writer.WritePropertyName("@value");
                writer.WriteValue(intValue);
                writer.WriteEndObject();
            }
            else if (value is long longValue)
            {
                writer.WriteStartObject();
                writer.WritePropertyName("@type");
                writer.WriteValue("g:Int64");
                writer.WritePropertyName("@value");
                writer.WriteValue(longValue);
                writer.WriteEndObject();
            }
            else if (value is double doubleValue)
            {
                writer.WriteStartObject();
                writer.WritePropertyName("@type");
                writer.WriteValue("g:Double");
                writer.WritePropertyName("@value");
                writer.WriteValue(doubleValue);
                writer.WriteEndObject();
            }
            else if (value is bool boolValue)
            {
                writer.WriteStartObject();
                writer.WritePropertyName("@type");
                writer.WriteValue("g:Boolean");
                writer.WritePropertyName("@value");
                writer.WriteValue(boolValue);
                writer.WriteEndObject();
            }
            else
            {
                // For other types, write the raw value to avoid recursion
                writer.WriteValue(value);
            }
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            return serializer.Deserialize(reader, objectType);
        }
    }

    /// <summary>
    /// Exception thrown when TinkerPop protocol errors occur
    /// </summary>
    public class TinkerPopProtocolException : Exception
    {
        public TinkerPopProtocolException(string message) : base(message) { }
        public TinkerPopProtocolException(string message, Exception innerException) : base(message, innerException) { }
    }
}

#endif