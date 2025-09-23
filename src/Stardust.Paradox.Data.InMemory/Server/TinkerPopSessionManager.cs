#if NET8_0_OR_GREATER || NETCOREAPP3_1_OR_GREATER
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Stardust.Paradox.Data.InMemory.Server
{
    /// <summary>
    /// WebSocket session manager for TinkerPop protocol
    /// Handles authentication, session state, and multi-user connections
    /// </summary>
    public class TinkerPopSessionManager
    {
        private readonly ConcurrentDictionary<string, TinkerPopSession> _sessions;
        private readonly GremlinServerOptions _serverOptions;
        private readonly InMemoryGremlinLanguageConnector _connector;

        public TinkerPopSessionManager(GremlinServerOptions serverOptions, InMemoryGremlinLanguageConnector connector)
        {
            _sessions = new ConcurrentDictionary<string, TinkerPopSession>();
            _serverOptions = serverOptions;
            _connector = connector;
        }

        /// <summary>
        /// Create a new TinkerPop session
        /// </summary>
        public TinkerPopSession CreateSession(string connectionId)
        {
            Console.WriteLine($"[DEBUG] TinkerPopSessionManager.CreateSession called for {connectionId}");
            
            var session = new TinkerPopSession
            {
                ConnectionId = connectionId,
                CreatedAt = DateTime.UtcNow,
                LastActivity = DateTime.UtcNow,
                IsAuthenticated = _serverOptions.Authentication == null, // Auto-authenticate if no auth required
                GraphSONVersion = GraphSONVersion.V3,
                Serializer = new ApacheTinkerGraphCompatibleSerializer(), // Use Apache TinkerGraph compatible serializer
                Configuration = new Dictionary<string, object>()
            };

            Console.WriteLine($"[DEBUG] Session created with ApacheTinkerGraphCompatibleSerializer");
            Console.WriteLine($"[DEBUG] Session.IsAuthenticated: {session.IsAuthenticated}");
            Console.WriteLine($"[DEBUG] Session.GraphSONVersion: {session.GraphSONVersion}");
            Console.WriteLine($"[DEBUG] Session.Serializer type: {session.Serializer.GetType().Name}");

            _sessions.TryAdd(connectionId, session);
            Console.WriteLine($"[DEBUG] Session added to collection, total sessions: {_sessions.Count}");
            
            return session;
        }

        /// <summary>
        /// Get an existing session
        /// </summary>
        public TinkerPopSession GetSession(string connectionId)
        {
            _sessions.TryGetValue(connectionId, out var session);
            return session;
        }

        /// <summary>
        /// Remove a session
        /// </summary>
        public void RemoveSession(string connectionId)
        {
            _sessions.TryRemove(connectionId, out _);
        }

        /// <summary>
        /// Process a TinkerPop message within a session context
        /// </summary>
        public async Task<TinkerPopResponse> ProcessMessageAsync(string connectionId, TinkerPopMessage message)
        {
            Console.WriteLine($"[DEBUG] TinkerPopSessionManager.ProcessMessageAsync called");
            Console.WriteLine($"[DEBUG] ConnectionId: {connectionId}");
            Console.WriteLine($"[DEBUG] Message.RequestId: {message.RequestId}");
            Console.WriteLine($"[DEBUG] Message.Op: '{message.Op}'");
            Console.WriteLine($"[DEBUG] Message.Processor: '{message.Processor}'");
            Console.WriteLine($"[DEBUG] Message.Args count: {message.Args?.Count ?? 0}");
            
            var session = GetSession(connectionId);
            if (session == null)
            {
                Console.WriteLine($"[DEBUG] Session not found for connectionId: {connectionId}");
                return new ApacheTinkerGraphCompatibleSerializer().CreateErrorResponse(
                    message.RequestId,
                    TinkerPopStatusCodes.ServerError,
                    "Session not found");
            }

            Console.WriteLine($"[DEBUG] Session found, IsAuthenticated: {session.IsAuthenticated}");

            // Update last activity
            session.LastActivity = DateTime.UtcNow;

            try
            {
                Console.WriteLine($"[DEBUG] Processing operation: '{message.Op}'");
                
                // Enhanced operation handling with better Gremlin.Net compatibility
                var response = message.Op switch
                {
                    TinkerPopOperations.Authentication => await ProcessAuthenticationAsync(session, message),
                    TinkerPopOperations.Eval => await ProcessEvaluationAsync(session, message),
                    TinkerPopOperations.Close => ProcessClose(session, message),
                    // Handle operation variations for different TinkerPop clients
                    "evaluate" => await ProcessEvaluationAsync(session, message),
                    "bytecode" => await ProcessBytecodeAsync(session, message),
                    "" when message.Args.ContainsKey("gremlin") => await ProcessEvaluationAsync(session, message), // Fallback for clients that don't set op
                    _ => session.Serializer.CreateErrorResponse(
                        message.RequestId,
                        TinkerPopStatusCodes.MalformedRequest,
                        $"Unsupported operation: {message.Op}")
                };
                
                Console.WriteLine($"[DEBUG] Operation processing completed, response status: {response.Status?.Code}");
                return response;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DEBUG] Exception in ProcessMessageAsync: {ex.GetType().FullName}: {ex.Message}");
                Console.WriteLine($"[DEBUG] Stack trace: {ex.StackTrace}");
                
                return session.Serializer.CreateErrorResponse(
                    message.RequestId,
                    TinkerPopStatusCodes.ServerError,
                    ex.Message,
                    ex);
            }
        }

        /// <summary>
        /// Process bytecode evaluation request (for Gremlin.Net traversals)
        /// </summary>
        private async Task<TinkerPopResponse> ProcessBytecodeAsync(TinkerPopSession session, TinkerPopMessage message)
        {
            if (!session.IsAuthenticated)
            {
                return session.Serializer.CreateErrorResponse(
                    message.RequestId,
                    TinkerPopStatusCodes.Unauthorized,
                    "Authentication required");
            }

            // For now, treat bytecode like regular Gremlin evaluation
            // In a full implementation, we would parse the bytecode and execute it
            var gremlin = message.Args.GetValueOrDefault("gremlin", "").ToString();
            if (string.IsNullOrEmpty(gremlin))
            {
                return session.Serializer.CreateErrorResponse(
                    message.RequestId,
                    TinkerPopStatusCodes.InvalidRequestArguments,
                    "Missing gremlin or bytecode argument");
            }

            // Use the same processing as regular evaluation
            return await ProcessEvaluationAsync(session, message);
        }

        /// <summary>
        /// Process authentication request
        /// </summary>
        private async Task<TinkerPopResponse> ProcessAuthenticationAsync(TinkerPopSession session, TinkerPopMessage message)
        {
            // If no authentication is configured, automatically authenticate
            if (_serverOptions.Authentication == null)
            {
                session.IsAuthenticated = true;
                return session.Serializer.CreateSuccessResponse(message.RequestId, new List<dynamic>());
            }

            var sasl = message.Args.GetValueOrDefault("sasl", "").ToString();
            
            if (string.IsNullOrEmpty(sasl))
            {
                // Return authentication challenge with proper TinkerPop format
                var response = new TinkerPopResponse
                {
                    RequestId = message.RequestId,
                    Status = new TinkerPopStatus
                    {
                        Code = TinkerPopStatusCodes.Authenticate,
                        Message = "Authentication required",
                        Attributes = new Dictionary<string, object>
                        {
                            ["sasl"] = new List<string> { "PLAIN" },
                            ["@type"] = "g:Map",
                            ["@value"] = new List<object> { "sasl", new List<string> { "PLAIN" } }
                        }
                    },
                    Result = new TinkerPopResult
                    {
                        Data = new List<object>(),
                        Meta = new Dictionary<string, object>
                        {
                            ["@type"] = "g:Map",
                            ["@value"] = new List<object>()
                        }
                    }
                };
                
                return response;
            }

            // Process SASL PLAIN authentication
            if (sasl == "PLAIN" || sasl.ToUpper() == "PLAIN")
            {
                var saslResponse = message.Args.GetValueOrDefault("saslMechanism", "").ToString();
                if (string.IsNullOrEmpty(saslResponse))
                {
                    return session.Serializer.CreateErrorResponse(
                        message.RequestId,
                        TinkerPopStatusCodes.Unauthorized,
                        "Missing SASL response");
                }

                try
                {
                    // Decode SASL PLAIN response (format: \0username\0password)
                    var decoded = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(saslResponse));
                    var parts = decoded.Split('\0');
                    
                    if (parts.Length < 2)
                    {
                        return session.Serializer.CreateErrorResponse(
                            message.RequestId,
                            TinkerPopStatusCodes.Unauthorized,
                            "Invalid SASL response format");
                    }

                    var username = parts.Length > 1 ? parts[1] : "";
                    var password = parts.Length > 2 ? parts[2] : "";

                    // Validate credentials
                    var isValid = await ValidateCredentialsAsync(username, password);
                    
                    if (isValid)
                    {
                        session.IsAuthenticated = true;
                        session.Username = username;
                        
                        // Return success response in proper TinkerPop format
                        return new TinkerPopResponse
                        {
                            RequestId = message.RequestId,
                            Status = new TinkerPopStatus
                            {
                                Code = TinkerPopStatusCodes.Success,
                                Message = "",
                                Attributes = new Dictionary<string, object>()
                            },
                            Result = new TinkerPopResult
                            {
                                Data = new List<object>(),
                                Meta = new Dictionary<string, object>
                                {
                                    ["@type"] = "g:Map",
                                    ["@value"] = new List<object>()
                                }
                            }
                        };
                    }
                    else
                    {
                        return session.Serializer.CreateErrorResponse(
                            message.RequestId,
                            TinkerPopStatusCodes.Unauthorized,
                            "Invalid credentials");
                    }
                }
                catch (Exception ex)
                {
                    return session.Serializer.CreateErrorResponse(
                        message.RequestId,
                        TinkerPopStatusCodes.Unauthorized,
                        $"Authentication error: {ex.Message}");
                }
            }

            return session.Serializer.CreateErrorResponse(
                message.RequestId,
                TinkerPopStatusCodes.Unauthorized,
                $"Unsupported SASL mechanism: {sasl}");
        }

        /// <summary>
        /// Process Gremlin evaluation request with enhanced Gremlin.Net support
        /// </summary>
        private async Task<TinkerPopResponse> ProcessEvaluationAsync(TinkerPopSession session, TinkerPopMessage message)
        {
            Console.WriteLine($"[DEBUG] ProcessEvaluationAsync called");
            Console.WriteLine($"[DEBUG] Session.IsAuthenticated: {session.IsAuthenticated}");
            
            if (!session.IsAuthenticated)
            {
                Console.WriteLine($"[DEBUG] Session not authenticated, returning unauthorized");
                return session.Serializer.CreateErrorResponse(
                    message.RequestId,
                    TinkerPopStatusCodes.Unauthorized,
                    "Authentication required");
            }

            // Enhanced argument extraction for different client types
            var gremlin = ExtractGremlinQuery(message.Args);
            Console.WriteLine($"[DEBUG] Gremlin query: '{gremlin}'");
            
            if (string.IsNullOrEmpty(gremlin))
            {
                Console.WriteLine($"[DEBUG] Missing gremlin argument");
                return session.Serializer.CreateErrorResponse(
                    message.RequestId,
                    TinkerPopStatusCodes.InvalidRequestArguments,
                    "Missing gremlin argument");
            }

            var bindings = ExtractBindings(message.Args);
            Console.WriteLine($"[DEBUG] Bindings count: {bindings.Count}");

            var language = message.Args.GetValueOrDefault("language", "gremlin-groovy").ToString();
            Console.WriteLine($"[DEBUG] Language: '{language}'");
            
            // Support multiple language variations
            if (!IsValidLanguage(language))
            {
                Console.WriteLine($"[DEBUG] Unsupported language: {language}");
                return session.Serializer.CreateErrorResponse(
                    message.RequestId,
                    TinkerPopStatusCodes.InvalidRequestArguments,
                    $"Unsupported language: {language}. Supported: gremlin-groovy, gremlin");
            }

            try
            {
                Console.WriteLine($"[DEBUG] About to execute Gremlin query via connector");
                
                // Execute the Gremlin query
                var result = await _connector.ExecuteAsync(gremlin, bindings);
                
                Console.WriteLine($"[DEBUG] Gremlin query executed successfully");
                Console.WriteLine($"[DEBUG] Result is null: {result == null}");
                
                var resultList = result?.ToList() ?? new List<dynamic>();
                Console.WriteLine($"[DEBUG] Result count: {resultList.Count}");
                
                // Create enhanced metadata for different GraphSON versions
                var meta = CreateResponseMetadata(session.GraphSONVersion);

                Console.WriteLine($"[DEBUG] Creating success response");
                var response = session.Serializer.CreateSuccessResponse(message.RequestId, resultList, meta);
                Console.WriteLine($"[DEBUG] Success response created");
                
                return response;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DEBUG] Exception in ProcessEvaluationAsync: {ex.GetType().FullName}: {ex.Message}");
                Console.WriteLine($"[DEBUG] Stack trace: {ex.StackTrace}");
                
                return session.Serializer.CreateErrorResponse(
                    message.RequestId,
                    TinkerPopStatusCodes.ScriptEvaluationError,
                    ex.Message,
                    ex);
            }
        }

        private string ExtractGremlinQuery(Dictionary<string, object> args)
        {
            // Try different argument names that various clients might use
            var possibleKeys = new[] { "gremlin", "script", "query", "traversal" };
            
            foreach (var key in possibleKeys)
            {
                if (args.ContainsKey(key))
                {
                    var value = args[key];
                    if (value != null)
                    {
                        return value.ToString();
                    }
                }
            }
            
            return "";
        }

        private Dictionary<string, object> ExtractBindings(Dictionary<string, object> args)
        {
            var bindings = new Dictionary<string, object>();
            
            // Extract from 'bindings' key
            if (args.ContainsKey("bindings") && args["bindings"] is Dictionary<string, object> explicitBindings)
            {
                foreach (var kvp in explicitBindings)
                {
                    bindings[kvp.Key] = kvp.Value;
                }
            }
            
            // Extract from 'parameters' key (alternative name)
            if (args.ContainsKey("parameters") && args["parameters"] is Dictionary<string, object> parameters)
            {
                foreach (var kvp in parameters)
                {
                    bindings[kvp.Key] = kvp.Value;
                }
            }
            
            return bindings;
        }

        private bool IsValidLanguage(string language)
        {
            if (string.IsNullOrEmpty(language)) return true;
            
            var validLanguages = new[] 
            { 
                "gremlin-groovy", 
                "gremlin", 
                "groovy",
                "gremlin-dotnet", // For Gremlin.Net
                "gremlin-java",   // For Java clients
                "gremlin-python"  // For Python clients
            };
            
            return validLanguages.Contains(language.ToLower());
        }

        private Dictionary<string, object> CreateResponseMetadata(GraphSONVersion version)
        {
            return version switch
            {
                GraphSONVersion.V1 => new Dictionary<string, object>(),
                GraphSONVersion.V2 or GraphSONVersion.V3 => new Dictionary<string, object>
                {
                    ["@type"] = "g:Map",
                    ["@value"] = new List<object>()
                },
                _ => new Dictionary<string, object>()
            };
        }

        /// <summary>
        /// Process close request
        /// </summary>
        private TinkerPopResponse ProcessClose(TinkerPopSession session, TinkerPopMessage message)
        {
            // Close the session
            RemoveSession(session.ConnectionId);
            
            return session.Serializer.CreateSuccessResponse(message.RequestId, new List<dynamic>());
        }

        /// <summary>
        /// Validate user credentials
        /// </summary>
        private async Task<bool> ValidateCredentialsAsync(string username, string password)
        {
            if (_serverOptions.Authentication == null)
                return true;

            // Basic authentication
            if (_serverOptions.Authentication.EnableBasicAuth)
            {
                return username == _serverOptions.Authentication.Username &&
                       password == _serverOptions.Authentication.Password;
            }

            // Custom authentication handler
            if (_serverOptions.Authentication.CustomAuthHandler != null)
            {
                var headers = new Dictionary<string, string>
                {
                    ["username"] = username,
                    ["password"] = password
                };
                
                return _serverOptions.Authentication.CustomAuthHandler(headers);
            }

            return false;
        }

        /// <summary>
        /// Cleanup expired sessions
        /// </summary>
        public void CleanupExpiredSessions(TimeSpan maxAge)
        {
            var expiredSessions = _sessions.Values
                .Where(s => DateTime.UtcNow - s.LastActivity > maxAge)
                .ToList();

            foreach (var session in expiredSessions)
            {
                RemoveSession(session.ConnectionId);
            }
        }

        /// <summary>
        /// Get session statistics
        /// </summary>
        public Dictionary<string, object> GetStatistics()
        {
            return new Dictionary<string, object>
            {
                ["totalSessions"] = _sessions.Count,
                ["authenticatedSessions"] = _sessions.Values.Count(s => s.IsAuthenticated),
                ["activeSessions"] = _sessions.Values.Count(s => DateTime.UtcNow - s.LastActivity < TimeSpan.FromMinutes(5))
            };
        }
    }

    /// <summary>
    /// WebSocket session information for TinkerPop protocol
    /// </summary>
    public class TinkerPopSession
    {
        /// <summary>
        /// Unique connection identifier
        /// </summary>
        public string ConnectionId { get; set; }

        /// <summary>
        /// Session creation time
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// Last activity time
        /// </summary>
        public DateTime LastActivity { get; set; }

        /// <summary>
        /// Whether the session is authenticated
        /// </summary>
        public bool IsAuthenticated { get; set; }

        /// <summary>
        /// Authenticated username
        /// </summary>
        public string Username { get; set; }

        /// <summary>
        /// GraphSON version for this session
        /// </summary>
        public GraphSONVersion GraphSONVersion { get; set; }

        /// <summary>
        /// GraphSON serializer for this session
        /// </summary>
        public ITinkerPopSerializer Serializer { get; set; }

        /// <summary>
        /// Session-specific variables
        /// </summary>
        public Dictionary<string, object> Variables { get; set; } = new Dictionary<string, object>();

        /// <summary>
        /// Session-specific configuration
        /// </summary>
        public Dictionary<string, object> Configuration { get; set; } = new Dictionary<string, object>();
    }
}

#endif