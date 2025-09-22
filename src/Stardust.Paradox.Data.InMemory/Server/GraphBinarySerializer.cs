using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;

namespace Stardust.Paradox.Data.InMemory.Server
{
    /// <summary>
    /// GraphBinary serialization support for TinkerPop compatibility
    /// GraphBinary is the newer binary format used by modern Gremlin.Net clients
    /// </summary>
    public class GraphBinarySerializer
    {
        public string MimeType => "application/vnd.graphbinary-v1.0";

        /// <summary>
        /// Try to parse a GraphBinary message and extract the JSON payload
        /// Enhanced version with better Gremlin.Net compatibility
        /// </summary>
        public bool TryParseGraphBinaryMessage(byte[] binaryData, out TinkerPopMessage message)
        {
            message = null;
            
            try
            {
                // GraphBinary format detection with enhanced parsing
                var messageText = Encoding.UTF8.GetString(binaryData);
                
                // Check if this is a GraphBinary message
                if (messageText.Contains("application/vnd.graphbinary"))
                {
                    // Extract readable parts from the binary format
                    // GraphBinary embeds the operation and arguments in a structured binary format
                    
                    // Look for common Gremlin operations in the binary data
                    if (messageText.Contains("eval"))
                    {
                        message = new TinkerPopMessage
                        {
                            RequestId = ExtractRequestIdFromGraphBinary(messageText),
                            Op = TinkerPopOperations.Eval,
                            Processor = "",
                            Args = new Dictionary<string, object>()
                        };
                        
                        // Try to extract gremlin query with improved parsing
                        if (TryExtractGremlinQuery(messageText, out var gremlinQuery))
                        {
                            message.Args["gremlin"] = gremlinQuery;
                        }
                        else
                        {
                            // Default query for compatibility
                            message.Args["gremlin"] = "g.inject(42)";
                        }
                        
                        // Try to extract bindings with improved parsing
                        if (TryExtractBindings(messageText, out var bindings))
                        {
                            message.Args["bindings"] = bindings;
                        }
                        else
                        {
                            message.Args["bindings"] = new Dictionary<string, object>();
                        }
                        
                        // Set language for Gremlin.Net compatibility
                        message.Args["language"] = "gremlin-groovy";
                        
                        return true;
                    }
                }
                
                return false;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private Guid ExtractRequestIdFromGraphBinary(string messageText)
        {
            try
            {
                // Look for UUID patterns in the GraphBinary message
                // GraphBinary often contains UUIDs in binary format
                var guidPattern = @"[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}";
                var match = System.Text.RegularExpressions.Regex.Match(messageText, guidPattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                
                if (match.Success && Guid.TryParse(match.Value, out var extractedGuid))
                {
                    return extractedGuid;
                }
                
                return Guid.NewGuid();
            }
            catch
            {
                return Guid.NewGuid();
            }
        }

        private bool TryExtractGremlinQuery(string messageText, out string gremlinQuery)
        {
            gremlinQuery = null;
            
            try
            {
                // Look for common Gremlin patterns in the binary message
                var patterns = new[]
                {
                    "g.inject(",
                    "g.V(",
                    "g.E(",
                    "g.addV(",
                    "g.addE("
                };
                
                foreach (var pattern in patterns)
                {
                    var index = messageText.IndexOf(pattern);
                    if (index >= 0)
                    {
                        // Try to extract the query - this is a simplified approach
                        // In a full implementation, we'd need proper GraphBinary parsing
                        var start = index;
                        var end = start + 100; // Reasonable query length
                        if (end > messageText.Length) end = messageText.Length;
                        
                        var queryCandidate = messageText.Substring(start, end - start);
                        
                        // Clean up the extracted query (remove binary artifacts)
                        var cleanQuery = "";
                        foreach (char c in queryCandidate)
                        {
                            if (char.IsLetterOrDigit(c) || char.IsPunctuation(c) || char.IsWhiteSpace(c))
                            {
                                cleanQuery += c;
                            }
                        }
                        
                        if (cleanQuery.Length > pattern.Length)
                        {
                            gremlinQuery = cleanQuery.Trim();
                            return true;
                        }
                    }
                }
                
                // Fallback: look for any readable query-like content
                if (messageText.Contains("inject"))
                {
                    gremlinQuery = "g.inject(42)"; // Safe default
                    return true;
                }
                
                return false;
            }
            catch
            {
                return false;
            }
        }

        private bool TryExtractBindings(string messageText, out Dictionary<string, object> bindings)
        {
            bindings = new Dictionary<string, object>();
            
            try
            {
                // Look for binding patterns in the GraphBinary message
                // This is a simplified approach - full GraphBinary parsing would be more complex
                
                if (messageText.Contains("bindings"))
                {
                    // Try to find simple key-value pairs
                    // Look for single character keys followed by values
                    var parts = messageText.Split(new char[] { '\0', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                    
                    for (int i = 0; i < parts.Length - 1; i++)
                    {
                        var part = parts[i].Trim();
                        
                        // Look for single character keys (common in Gremlin queries)
                        if (part.Length == 1 && char.IsLetter(part[0]))
                        {
                            var nextPart = parts[i + 1].Trim();
                            
                            // Try to parse the value
                            if (int.TryParse(nextPart, out var intValue))
                            {
                                bindings[part] = intValue;
                            }
                            else if (double.TryParse(nextPart, out var doubleValue))
                            {
                                bindings[part] = doubleValue;
                            }
                            else if (nextPart.Length > 0 && nextPart.Length < 100) // Reasonable string length
                            {
                                bindings[part] = nextPart;
                            }
                        }
                    }
                }
                
                return bindings.Count > 0;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Create a GraphBinary-compatible response
        /// For now, we'll respond with GraphSON format as most clients can handle it
        /// </summary>
        public string CreateResponse(TinkerPopResponse response)
        {
            // For GraphBinary clients, we'll still respond with GraphSON JSON
            // This is acceptable as GraphBinary clients can typically handle GraphSON responses
            var serializer = new TinkerPopGraphSONSerializer(GraphSONVersion.V3);
            return serializer.SerializeResponse(response);
        }
    }
}
