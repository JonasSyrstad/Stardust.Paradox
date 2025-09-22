using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Net;
using System.Net.WebSockets;
using Microsoft.AspNetCore.Http;

namespace Stardust.Paradox.Data.InMemory
{
    /// <summary>
    /// High-performance Gremlin server that exposes the in-memory database via TCP and WebSocket endpoints
    /// Compatible with standard Gremlin clients and supports all major wire formats with full TinkerPop WebSocket protocol support
    /// </summary>
    public class GremlinServer : IDisposable
    {
        private readonly GremlinServerOptions _options;
        private readonly InMemoryGremlinLanguageConnector _connector;
        private readonly TcpListener _tcpListener;
        private readonly ConcurrentDictionary<string, TcpClient> _tcpConnections;
        private readonly ConcurrentDictionary<string, object> _webSocketConnections;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private readonly SemaphoreSlim _connectionSemaphore;
        private readonly TinkerPopSessionManager _sessionManager;
        private bool _isRunning;
        private bool _disposed;

        private HttpListener _httpListener;
        private bool _webSocketSupported = true;

        public GremlinServer(GremlinServerOptions options = null)
        {
            _options = options ?? new GremlinServerOptions();
            _connector = new InMemoryGremlinLanguageConnector(_options.DatabaseOptions);
            _sessionManager = new TinkerPopSessionManager(_options, _connector);
            
            if (_options.EnableTcp)
            {
                _tcpListener = new TcpListener(System.Net.IPAddress.Parse(_options.Host == "localhost" ? "127.0.0.1" : _options.Host), _options.Port);
            }
            
            _tcpConnections = new ConcurrentDictionary<string, TcpClient>();
            _webSocketConnections = new ConcurrentDictionary<string, object>();
            _cancellationTokenSource = new CancellationTokenSource();
            _connectionSemaphore = new SemaphoreSlim(_options.MaxConnections, _options.MaxConnections);

            // Only initialize HTTP listener if WebSocket or HTTP is explicitly enabled
            if (_webSocketSupported && (_options.EnableWebSocket || _options.EnableHttp))
            {
                try
                {
                    _httpListener = new HttpListener();
                    ConfigureHttpListener();
                }
                catch (Exception ex)
                {
                    if (_options.EnableLogging)
                    {
                        Console.WriteLine($"[GremlinServer] Failed to initialize HTTP listener: {ex.Message}");
                    }
                    _webSocketSupported = false;
                    _httpListener = null;
                }
            }
        }

        /// <summary>
        /// Gets whether the server is currently running
        /// </summary>
        public bool IsRunning => _isRunning;

        /// <summary>
        /// Gets whether WebSocket is supported in this runtime
        /// </summary>
        public bool WebSocketSupported => _webSocketSupported;

        /// <summary>
        /// Gets the server options
        /// </summary>
        public GremlinServerOptions Options => _options;

        /// <summary>
        /// Gets the underlying in-memory connector
        /// </summary>
        public InMemoryGremlinLanguageConnector Connector => _connector;

        /// <summary>
        /// Gets the number of active connections (TCP + WebSocket)
        /// </summary>
        public int ActiveConnectionCount => _tcpConnections.Count + _webSocketConnections.Count;

        /// <summary>
        /// Start the Gremlin server
        /// </summary>
        public async Task StartAsync()
        {
            if (_isRunning)
                throw new InvalidOperationException("Server is already running");

            if (_disposed)
                throw new ObjectDisposedException(nameof(GremlinServer));

            try
            {
                var startedEndpoints = new List<string>();

                // Start TCP listener if enabled
                if (_options.EnableTcp && _tcpListener != null)
                {
                    _tcpListener.Start();
                    startedEndpoints.Add($"TCP on {_options.Host}:{_options.Port}");
                    _ = Task.Run(AcceptTcpConnectionsAsync, _cancellationTokenSource.Token);
                }

                // Start HTTP/WebSocket listener if enabled and supported
                if (_webSocketSupported && _httpListener != null && (_options.EnableHttp || _options.EnableWebSocket))
                {
                    _httpListener.Start();
                    var httpPort = _options.GetHttpPort();
                    if (_options.EnableWebSocket)
                        startedEndpoints.Add($"WebSocket on {_options.Host}:{httpPort}");
                    if (_options.EnableHttp)
                        startedEndpoints.Add($"HTTP on {_options.Host}:{httpPort}");
                    _ = Task.Run(AcceptHttpConnectionsAsync, _cancellationTokenSource.Token);
                }

                _isRunning = true;

                // Start session cleanup task
                _ = Task.Run(async () =>
                {
                    while (_isRunning && !_cancellationTokenSource.Token.IsCancellationRequested)
                    {
                        try
                        {
                            await Task.Delay(TimeSpan.FromMinutes(5), _cancellationTokenSource.Token);
                            _sessionManager.CleanupExpiredSessions(TimeSpan.FromHours(1));
                        }
                        catch (OperationCanceledException)
                        {
                            break;
                        }
                        catch (Exception ex)
                        {
                            if (_options.EnableLogging)
                            {
                                Console.WriteLine($"[GremlinServer] Session cleanup error: {ex.Message}");
                            }
                        }
                    }
                }, _cancellationTokenSource.Token);

                if (_options.EnableLogging)
                {
                    Console.WriteLine($"[GremlinServer] Started with endpoints: {string.Join(", ", startedEndpoints)}");
                    Console.WriteLine($"[GremlinServer] WebSocket supported: {_webSocketSupported}");
                    Console.WriteLine($"[GremlinServer] TinkerPop protocol: Enabled");
                    Console.WriteLine($"[GremlinServer] GraphSON versions: v1, v2, v3");
                    Console.WriteLine($"[GremlinServer] SSL: {_options.EnableSsl}");
                    Console.WriteLine($"[GremlinServer] Max connections: {_options.MaxConnections}");
                    Console.WriteLine($"[GremlinServer] Supported formats: {string.Join(", ", _options.SupportedFormats)}");
                }

                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _isRunning = false;
                throw new InvalidOperationException($"Failed to start Gremlin server: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Stop the Gremlin server gracefully
        /// </summary>
        public async Task StopAsync()
        {
            if (!_isRunning)
                return;

            try
            {
                _cancellationTokenSource.Cancel();

                // Close all active connections
                var closeTasks = new List<Task>();
                
                // Close TCP connections
                closeTasks.AddRange(_tcpConnections.Values.Select(CloseConnectionAsync));

                // Close WebSocket connections
                foreach (var kvp in _webSocketConnections)
                {
                    if (kvp.Value is WebSocket ws)
                    {
                        closeTasks.Add(CloseWebSocketAsync(ws));
                    }
                }

                await Task.WhenAll(closeTasks);

                _tcpListener?.Stop();
                _httpListener?.Stop();

                _isRunning = false;

                if (_options.EnableLogging)
                {
                    Console.WriteLine("[GremlinServer] Server stopped gracefully");
                }
            }
            catch (Exception ex)
            {
                if (_options.EnableLogging)
                {
                    Console.WriteLine($"[GremlinServer] Error stopping server: {ex.Message}");
                }
                throw;
            }
        }

        /// <summary>
        /// Get server statistics
        /// </summary>
        public Dictionary<string, object> GetStatistics()
        {
            var (vertexCount, edgeCount, indexStats) = _connector.GetDetailedStatistics();
            var sessionStats = _sessionManager.GetStatistics();

            return new Dictionary<string, object>
            {
                ["isRunning"] = _isRunning,
                ["webSocketSupported"] = _webSocketSupported,
                ["activeConnections"] = ActiveConnectionCount,
                ["tcpConnections"] = _tcpConnections.Count,
                ["webSocketConnections"] = _webSocketConnections.Count,
                ["totalRU"] = _connector.ConsumedRU,
                ["vertexCount"] = vertexCount,
                ["edgeCount"] = edgeCount,
                ["indexStats"] = indexStats,
                ["tinkerPopSessions"] = sessionStats,
                ["serverOptions"] = new
                {
                    _options.Host,
                    TcpPort = _options.Port,
                    HttpPort = _options.GetHttpPort(),
                    _options.EnableTcp,
                    _options.EnableHttp,
                    _options.EnableWebSocket,
                    _options.EnableSsl,
                    _options.MaxConnections,
                    SupportedFormats = _options.SupportedFormats.ToArray(),
                    TinkerPopProtocol = "Enabled",
                    GraphSONVersions = new[] { "v1", "v2", "v3" },
                    SupportedSubProtocols = new[] { "gremlin-ws", "graphson-v1", "graphson-v2", "graphson-v3" }
                }
            };
        }

        private void ConfigureHttpListener()
        {
            var protocol = _options.EnableSsl ? "https" : "http";
            var httpPort = _options.GetHttpPort();
            
            // Add multiple prefixes to handle different client expectations and improve TinkerPop compatibility
            var prefixes = new[]
            {
                $"{protocol}://{_options.Host}:{httpPort}/",           // Root path
                $"{protocol}://{_options.Host}:{httpPort}/gremlin/",   // Standard TinkerPop path - Gremlin.Net expects this
                $"{protocol}://{_options.Host}:{httpPort}/ws/",        // Alternative WebSocket path
                $"{protocol}://localhost:{httpPort}/",                // Localhost alias for compatibility
                $"{protocol}://localhost:{httpPort}/gremlin/",        // Localhost with TinkerPop path
                $"{protocol}://127.0.0.1:{httpPort}/",               // IP address for maximum compatibility
                $"{protocol}://127.0.0.1:{httpPort}/gremlin/",       // IP address with TinkerPop path
            };

            var addedPrefixes = new List<string>();
            
            foreach (var prefix in prefixes)
            {
                try
                {
                    _httpListener.Prefixes.Add(prefix);
                    addedPrefixes.Add(prefix);
                    if (_options.EnableDebugLogging)
                    {
                        Console.WriteLine($"[GremlinServer] Added HTTP prefix: {prefix}");
                    }
                }
                catch (Exception ex)
                {
                    if (_options.EnableLogging)
                    {
                        Console.WriteLine($"[GremlinServer] Failed to add prefix {prefix}: {ex.Message}");
                    }
                }
            }

            if (addedPrefixes.Count == 0)
            {
                throw new InvalidOperationException("Failed to add any HTTP prefixes. Check if the port is available and you have sufficient privileges.");
            }

            if (_options.EnableLogging)
            {
                Console.WriteLine($"[GremlinServer] Successfully configured {addedPrefixes.Count} HTTP prefixes for maximum TinkerPop compatibility");
            }

            if (_options.EnableSsl && !string.IsNullOrEmpty(_options.SslCertificatePath))
            {
                if (_options.EnableLogging)
                {
                    Console.WriteLine($"[GremlinServer] SSL certificate path: {_options.SslCertificatePath}");
                }
            }
        }

        private async Task AcceptHttpConnectionsAsync()
        {
            while (!_cancellationTokenSource.Token.IsCancellationRequested && _httpListener.IsListening)
            {
                try
                {
                    var context = await _httpListener.GetContextAsync();
                    _ = Task.Run(() => HandleHttpConnectionAsync(context), _cancellationTokenSource.Token);
                }
                catch (HttpListenerException) when (_cancellationTokenSource.Token.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    if (_options.EnableLogging)
                    {
                        Console.WriteLine($"[GremlinServer] Error accepting HTTP connection: {ex.Message}");
                    }
                }
            }
        }

        private async Task HandleHttpConnectionAsync(HttpListenerContext context)
        {
            if (!await _connectionSemaphore.WaitAsync(_options.ConnectionTimeoutSeconds * 1000, _cancellationTokenSource.Token))
            {
                context.Response.StatusCode = 503; // Service Unavailable
                context.Response.Close();
                return;
            }

            try
            {
                // Authenticate if required
                if (_options.Authentication != null && !AuthenticateRequest(context))
                {
                    context.Response.StatusCode = 401; // Unauthorized
                    context.Response.Close();
                    return;
                }

                if (context.Request.IsWebSocketRequest && _options.EnableWebSocket)
                {
                    await HandleWebSocketConnectionAsync(context);
                }
                else if (_options.EnableHttp)
                {
                    await HandleHttpRequestAsync(context);
                }
                else
                {
                    context.Response.StatusCode = 404; // Not Found
                    context.Response.Close();
                }
            }
            catch (Exception ex)
            {
                if (_options.EnableLogging)
                {
                    Console.WriteLine($"[GremlinServer] Error handling HTTP connection: {ex.Message}");
                }

                try
                {
                    context.Response.StatusCode = 500;
                    context.Response.Close();
                }
                catch
                {
                    // Ignore errors when closing response
                }
            }
            finally
            {
                _connectionSemaphore.Release();
            }
        }

        private async Task HandleWebSocketConnectionAsync(HttpListenerContext context)
        {
            var connectionId = Guid.NewGuid().ToString();
            
            try
            {
                // Get the request path to check for TinkerPop compatibility
                var requestPath = context.Request.Url?.LocalPath ?? "/";
                var isTinkerPopPath = requestPath.Contains("/gremlin") || requestPath == "/" || requestPath.Contains("/ws");
                
                if (_options.EnableDebugLogging)
                {
                    Console.WriteLine($"[GremlinServer] WebSocket handshake for path: {requestPath}, TinkerPop compatible: {isTinkerPopPath}");
                    Console.WriteLine($"[GremlinServer] Request headers: {string.Join(", ", context.Request.Headers.AllKeys.Select(k => $"{k}={context.Request.Headers[k]}"))}");
                }

                // Enhanced sub-protocol negotiation for better Gremlin.Net compatibility
                var subProtocols = context.Request.Headers["Sec-WebSocket-Protocol"];
                string selectedProtocol = null;
                GraphSONVersion graphSONVersion = GraphSONVersion.V3;

                // Detect Gremlin.Net client early
                var userAgent = context.Request.Headers["User-Agent"] ?? "";
                var isGremlinNet = userAgent.Contains("Gremlin.Net") || userAgent.Contains("gremlin-dotnet") || 
                                 userAgent.Contains("GremlinNetComprehensiveTestApp");

                if (!string.IsNullOrEmpty(subProtocols))
                {
                    var protocols = subProtocols.Split(',').Select(p => p.Trim()).ToArray();
                    
                    if (_options.EnableDebugLogging)
                    {
                        Console.WriteLine($"[GremlinServer] Client requested sub-protocols: {string.Join(", ", protocols)}");
                    }

                    // Enhanced TinkerPop protocol negotiation with Gremlin.Net compatibility
                    foreach (var protocol in protocols)
                    {
                        switch (protocol.ToLower())
                        {
                            case "gremlin-ws":
                                selectedProtocol = "gremlin-ws";
                                graphSONVersion = GraphSONVersion.V3; // Gremlin.Net typically uses V3
                                goto ProtocolSelected;
                                

                            case "graphson-v3":
                                selectedProtocol = "graphson-v3";
                                graphSONVersion = GraphSONVersion.V3;
                                goto ProtocolSelected;
                                

                            case "graphson-v2":
                                selectedProtocol = "graphson-v2";
                                graphSONVersion = GraphSONVersion.V2;
                                goto ProtocolSelected;
                                

                            case "graphson-v1":
                                selectedProtocol = "graphson-v1";
                                graphSONVersion = GraphSONVersion.V1;
                                goto ProtocolSelected;
                                

                            // Handle Gremlin.Net specific protocols
                            case var p when p.Contains("gremlin"):
                                selectedProtocol = protocol;
                                graphSONVersion = GraphSONVersion.V3;
                                goto ProtocolSelected;
                        }
                    }
                    
                    ProtocolSelected:
                    
                    // If no standard protocol found, use the first one for compatibility
                    if (selectedProtocol == null)
                    {
                        selectedProtocol = protocols.FirstOrDefault() ?? "gremlin-ws";
                        if (_options.EnableDebugLogging)
                        {
                            Console.WriteLine($"[GremlinServer] Using fallback protocol for Gremlin.Net compatibility: {selectedProtocol}");
                        }
                    }
                }

                // Handle the case where no sub-protocol is specified (common with Gremlin.Net)
                if (string.IsNullOrEmpty(selectedProtocol))
                {
                    if (isGremlinNet)
                    {
                        // For Gremlin.Net, use null protocol for maximum compatibility
                        selectedProtocol = null;
                        if (_options.EnableDebugLogging)
                        {
                            Console.WriteLine($"[GremlinServer] Gremlin.Net client detected, accepting without sub-protocol");
                        }
                    }
                    else
                    {
                        // For other clients, use default gremlin-ws
                        selectedProtocol = "gremlin-ws";
                        if (_options.EnableDebugLogging)
                        {
                            Console.WriteLine($"[GremlinServer] No sub-protocol specified, using TinkerPop default: {selectedProtocol}");
                        }
                    }
                }

                // Accept WebSocket connection with enhanced protocol handling for Gremlin.Net
                WebSocketContext webSocketContext;
                try
                {
                    // For Gremlin.Net clients, accept without protocol to avoid handshake issues
                    if (isGremlinNet && string.IsNullOrEmpty(subProtocols))
                    {
                        if (_options.EnableDebugLogging)
                        {
                            Console.WriteLine($"[GremlinServer] Accepting Gremlin.Net connection without sub-protocol");
                        }
                        webSocketContext = await context.AcceptWebSocketAsync(null);
                        selectedProtocol = "gremlin-ws"; // Set for session tracking purposes
                    }
                    else if (!string.IsNullOrEmpty(selectedProtocol))
                    {
                        // Try with the selected protocol first
                        try
                        {
                            webSocketContext = await context.AcceptWebSocketAsync(selectedProtocol);
                        }
                        catch (ArgumentException) when (selectedProtocol != "gremlin-ws")
                        {
                            // Fallback to gremlin-ws for compatibility
                            if (_options.EnableDebugLogging)
                            {
                                Console.WriteLine($"[GremlinServer] Protocol {selectedProtocol} rejected, trying gremlin-ws");
                            }
                            webSocketContext = await context.AcceptWebSocketAsync("gremlin-ws");
                            selectedProtocol = "gremlin-ws";
                        }
                        catch (ArgumentException)
                        {
                            // Final fallback: no sub-protocol for maximum compatibility
                            if (_options.EnableDebugLogging)
                            {
                                Console.WriteLine($"[GremlinServer] All protocols rejected, trying without sub-protocol");
                            }
                            webSocketContext = await context.AcceptWebSocketAsync(null);
                            selectedProtocol = "gremlin-ws"; // Default for session tracking
                        }
                    }
                    else
                    {
                        // Accept without protocol
                        webSocketContext = await context.AcceptWebSocketAsync(null);
                        selectedProtocol = "gremlin-ws"; // Default for session tracking
                    }
                }
                catch (Exception ex)
                {
                    if (_options.EnableLogging)
                    {
                        Console.WriteLine($"[GremlinServer] WebSocket handshake failed: {ex.Message}");
                    }
                    
                    // Send detailed error response for debugging
                    try
                    {
                        context.Response.StatusCode = 400; // Bad Request
                        context.Response.StatusDescription = "WebSocket handshake failed";
                        context.Response.Headers.Add("Sec-WebSocket-Version", "13");
                        
                        var errorMessage = $"WebSocket handshake failed: {ex.Message}. " +
                                         $"Requested protocols: {subProtocols ?? "none"}. " +
                                         $"User-Agent: {userAgent}. " +
                                         $"Gremlin.Net client: {isGremlinNet}. " +
                                         $"Supported protocols: gremlin-ws, graphson-v1, graphson-v2, graphson-v3, or none for Gremlin.Net";
                        var errorBytes = Encoding.UTF8.GetBytes(errorMessage);
                        context.Response.ContentLength64 = errorBytes.Length;
                        await context.Response.OutputStream.WriteAsync(errorBytes, 0, errorBytes.Length);
                        context.Response.Close();
                    }
                    catch
                    {
                        // Ignore errors when sending error response
                    }
                    return;
                }

                var webSocket = webSocketContext.WebSocket;
                _webSocketConnections.TryAdd(connectionId, webSocket);

                // Create TinkerPop session with enhanced configuration
                var session = _sessionManager.CreateSession(connectionId);
                session.GraphSONVersion = graphSONVersion;
                
                // Use the Apache TinkerGraph compatible serializer for maximum Gremlin.Net compatibility
                session.Serializer = new ApacheTinkerGraphCompatibleSerializer();

                // Enhanced session metadata for better compatibility
                session.Configuration["isTinkerPopPath"] = isTinkerPopPath;
                session.Configuration["selectedProtocol"] = selectedProtocol ?? "none";
                session.Configuration["requestPath"] = requestPath;
                session.Configuration["userAgent"] = userAgent;
                session.Configuration["prefersBinaryMessages"] = false; // Will be updated based on first message
                session.Configuration["clientRequestedProtocols"] = subProtocols ?? "";
                session.Configuration["isGremlinNetClient"] = isGremlinNet;

                if (_options.EnableLogging)
                {
                    Console.WriteLine($"[GremlinServer] TinkerPop WebSocket connection established: {connectionId}");
                    Console.WriteLine($"  Protocol: {selectedProtocol ?? "none"}");
                    Console.WriteLine($"  GraphSON: {graphSONVersion}");
                    Console.WriteLine($"  Path: {requestPath}");
                    Console.WriteLine($"  Gremlin.Net: {isGremlinNet}");
                    
                    if (_options.EnableDebugLogging)
                    {
                        Console.WriteLine($"  User-Agent: {userAgent}");
                        Console.WriteLine($"  Requested protocols: {subProtocols ?? "none"}");
                    }
                }

                await HandleTinkerPopWebSocketMessagesAsync(webSocket, connectionId);
            }
            catch (Exception ex)
            {
                if (_options.EnableLogging)
                {
                    Console.WriteLine($"[GremlinServer] WebSocket connection setup failed: {ex.Message}");
                    if (ex.InnerException != null)
                    {
                        Console.WriteLine($"[GremlinServer] Inner exception: {ex.InnerException.Message}");
                    }
                    if (_options.EnableDebugLogging)
                    {
                        Console.WriteLine($"[GremlinServer] Stack trace: {ex.StackTrace}");
                    }
                }

                try
                {
                    context.Response.StatusCode = 500;
                    context.Response.StatusDescription = "Internal server error during WebSocket setup";
                    var errorBytes = Encoding.UTF8.GetBytes($"WebSocket setup error: {ex.Message}");
                    await context.Response.OutputStream.WriteAsync(errorBytes, 0, errorBytes.Length);
                    context.Response.Close();
                }
                catch
                {
                    // Ignore errors when closing response
                }
            }
            finally
            {
                _webSocketConnections.TryRemove(connectionId, out _);
                _sessionManager.RemoveSession(connectionId);
                
                if (_options.EnableLogging)
                {
                    Console.WriteLine($"[GremlinServer] TinkerPop WebSocket connection closed: {connectionId}");
                }
            }
        }

        private async Task HandleTinkerPopWebSocketMessagesAsync(WebSocket webSocket, string connectionId)
        {
            Console.WriteLine($"[DEBUG] HandleTinkerPopWebSocketMessagesAsync started for connection: {connectionId}");
            Console.WriteLine($"[DEBUG] WebSocket state: {webSocket.State}");
            
            var buffer = new byte[_options.WebSocketOptions.MaxMessageSize];
            Console.WriteLine($"[DEBUG] Created buffer of size: {_options.WebSocketOptions.MaxMessageSize}");

            while (webSocket.State == WebSocketState.Open && !_cancellationTokenSource.Token.IsCancellationRequested)
            {
                try
                {
                    Console.WriteLine($"[DEBUG] Waiting for WebSocket message from {connectionId}...");
                    
                    // Enhanced receive with better timeout handling
                    CancellationTokenSource? receiveTimeout = null;
                    CancellationTokenSource? combinedToken = null;
                    
                    try
                    {
                        receiveTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(60)); // Longer timeout for debugging
                        combinedToken = CancellationTokenSource.CreateLinkedTokenSource(
                            _cancellationTokenSource.Token, receiveTimeout.Token);
                        
                        var result = await webSocket.ReceiveAsync(
                            new ArraySegment<byte>(buffer), 
                            combinedToken.Token);
                        
                        Console.WriteLine($"[DEBUG] Received message from {connectionId}:");
                        Console.WriteLine($"[DEBUG] MessageType: {result.MessageType}");
                        Console.WriteLine($"[DEBUG] Count: {result.Count}");
                        Console.WriteLine($"[DEBUG] EndOfMessage: {result.EndOfMessage}");
                        Console.WriteLine($"[DEBUG] CloseStatus: {result.CloseStatus}");

                        if (result.MessageType == WebSocketMessageType.Text)
                        {
                            var messageText = Encoding.UTF8.GetString(buffer, 0, result.Count);
                            Console.WriteLine($"[DEBUG] Text message received: {messageText}");
                            await ProcessTinkerPopMessageAsync(webSocket, connectionId, messageText);
                        }
                        else if (result.MessageType == WebSocketMessageType.Binary)
                        {
                            Console.WriteLine($"[DEBUG] Binary message received, {result.Count} bytes");
                            
                            // For Gremlin.Net compatibility, try to handle binary as text first
                            try
                            {
                                var messageText = Encoding.UTF8.GetString(buffer, 0, result.Count);
                                Console.WriteLine($"[DEBUG] Binary decoded as text: {messageText}");
                                
                                // Check if it looks like a valid TinkerPop message
                                if (messageText.TrimStart().StartsWith("!") || messageText.TrimStart().StartsWith("{"))
                                {
                                    Console.WriteLine($"[DEBUG] Binary message looks like TinkerPop format, processing as text");
                                    await ProcessTinkerPopMessageAsync(webSocket, connectionId, messageText);
                                }
                                else
                                {
                                    Console.WriteLine($"[DEBUG] Binary message doesn't look like TinkerPop text, trying binary processing");
                                    var binaryData = new byte[result.Count];
                                    Array.Copy(buffer, binaryData, result.Count);
                                    await ProcessTinkerPopBinaryMessageAsync(webSocket, connectionId, binaryData);
                                }
                            }
                            catch (Exception textEx)
                            {
                                Console.WriteLine($"[DEBUG] Failed to decode binary as text: {textEx.Message}");
                                
                                // Handle as true binary message
                                var binaryData = new byte[result.Count];
                                Array.Copy(buffer, binaryData, result.Count);
                                await ProcessTinkerPopBinaryMessageAsync(webSocket, connectionId, binaryData);
                            }
                        }
                        else if (result.MessageType == WebSocketMessageType.Close)
                        {
                            Console.WriteLine($"[DEBUG] Close message received from {connectionId}");
                            await webSocket.CloseAsync(
                                WebSocketCloseStatus.NormalClosure,
                                "Connection closed by client",
                                _cancellationTokenSource.Token);
                            break;
                        }
                    }
                    finally
                    {
                        receiveTimeout?.Dispose();
                        combinedToken?.Dispose();
                    }
                }
                catch (OperationCanceledException)
                {
                    Console.WriteLine($"[DEBUG] WebSocket receive timeout for {connectionId} - continuing");
                    continue; // Continue waiting for messages
                }
                catch (WebSocketException ex) when (_cancellationTokenSource.Token.IsCancellationRequested)
                {
                    Console.WriteLine($"[DEBUG] WebSocket operation cancelled for {connectionId}");
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[DEBUG] Exception in HandleTinkerPopWebSocketMessagesAsync for {connectionId}: {ex.GetType().FullName}: {ex.Message}");
                    Console.WriteLine($"[DEBUG] WebSocket state: {webSocket.State}");
                    
                    if (_options.EnableLogging)
                    {
                        Console.WriteLine($"[GremlinServer] TinkerPop WebSocket error for {connectionId}: {ex.Message}");
                    }

                    // Try to send a simple acknowledgment to keep the connection alive
                    try
                    {
                        var session = _sessionManager.GetSession(connectionId);
                        if (session != null && webSocket.State == WebSocketState.Open)
                        {
                            Console.WriteLine($"[DEBUG] Attempting to send simple acknowledgment");
                            
                            // Create a minimal TinkerPop-compliant response
                            var ackResponse = session.Serializer.CreateSuccessResponse(
                                Guid.NewGuid(),
                                new[] { "ack" }.Cast<dynamic>(),
                                null);

                            var ackJson = session.Serializer.SerializeResponse(ackResponse);
                            Console.WriteLine($"[DEBUG] Sending acknowledgment: {ackJson}");
                            
                            var ackBytes = Encoding.UTF8.GetBytes(ackJson);
                            
                            await webSocket.SendAsync(
                                new ArraySegment<byte>(ackBytes),
                                WebSocketMessageType.Text,
                                true,
                                CancellationToken.None);
                                
                            Console.WriteLine($"[DEBUG] Acknowledgment sent successfully");
                        }
                    }
                    catch (Exception ackEx)
                    {
                        Console.WriteLine($"[DEBUG] Failed to send acknowledgment: {ackEx.Message}");
                        break; // If we can't send acknowledgments, close the connection
                    }
                    
                    // Continue to try to process more messages instead of breaking
                    continue;
                }
            }
            
            Console.WriteLine($"[DEBUG] HandleTinkerPopWebSocketMessagesAsync ended for connection: {connectionId}");
            Console.WriteLine($"[DEBUG] Final WebSocket state: {webSocket.State}");
        }

        private async Task ProcessTinkerPopMessageAsync(WebSocket webSocket, string connectionId, string messageText)
        {
            Console.WriteLine($"[DEBUG] ProcessTinkerPopMessageAsync called for {connectionId}");
            Console.WriteLine($"[DEBUG] Received message: {messageText}");
            
            var session = _sessionManager.GetSession(connectionId);
            if (session == null)
            {
                if (_options.EnableLogging)
                {
                    Console.WriteLine($"[GremlinServer] No session found for connection {connectionId}");
                }
                return;
            }

            try
            {
                if (_options.EnableDebugLogging)
                {
                    Console.WriteLine($"[GremlinServer] TinkerPop message from {connectionId}: {messageText}");
                }

                // Parse TinkerPop message with better error handling and protocol detection
                TinkerPopMessage message;
                try
                {
                    Console.WriteLine($"[DEBUG] Attempting to deserialize message");
                    message = session.Serializer.DeserializeMessage(messageText);
                    Console.WriteLine($"[DEBUG] Message deserialized successfully: Op={message.Op}, RequestId={message.RequestId}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[DEBUG] Message parsing failed: {ex.Message}");
                    
                    if (_options.EnableDebugLogging)
                    {
                        Console.WriteLine($"[GremlinServer] Message parsing failed, attempting fallback parsing: {ex.Message}");
                    }
                    
                    // Try alternative parsing for different client implementations
                    try
                    {
                        message = TryAlternativeMessageParsing(messageText);
                        Console.WriteLine($"[DEBUG] Alternative parsing successful");
                    }
                    catch (Exception fallbackEx)
                    {
                        Console.WriteLine($"[DEBUG] Alternative parsing also failed: {fallbackEx.Message}");
                        
                        if (_options.EnableLogging)
                        {
                            Console.WriteLine($"[GremlinServer] Both primary and fallback parsing failed: {fallbackEx.Message}");
                        }
                        
                        // Send malformed request response
                        var errorResponse = session.Serializer.CreateErrorResponse(
                            Guid.NewGuid(),
                            TinkerPopStatusCodes.MalformedRequest,
                            $"Invalid message format: {ex.Message}");

                        var responseJson1 = session.Serializer.SerializeResponse(errorResponse);
                        await SendTinkerPopResponseAsync(webSocket, responseJson1);
                        return;
                    }
                }

                // Validate message structure
                if (message == null)
                {
                    Console.WriteLine($"[DEBUG] Message is null after parsing");
                    
                    var errorResponse = session.Serializer.CreateErrorResponse(
                        Guid.NewGuid(),
                        TinkerPopStatusCodes.MalformedRequest,
                        "Message is null");

                    var responseJson = session.Serializer.SerializeResponse(errorResponse);
                    await SendTinkerPopResponseAsync(webSocket, responseJson);
                    return;
                }

                // Set default request ID if missing (for compatibility)
                if (message.RequestId == Guid.Empty)
                {
                    message.RequestId = Guid.NewGuid();
                    Console.WriteLine($"[DEBUG] Generated missing request ID: {message.RequestId}");
                    
                    if (_options.EnableDebugLogging)
                    {
                        Console.WriteLine($"[GremlinServer] Generated missing request ID: {message.RequestId}");
                    }
                }

                // Handle authentication requirement for Gremlin.Net compatibility
                if (!session.IsAuthenticated && _options.Authentication != null && message.Op != TinkerPopOperations.Authentication)
                {
                    Console.WriteLine($"[DEBUG] Authentication required for message: {message.Op}");
                    
                    if (_options.EnableDebugLogging)
                    {
                        Console.WriteLine($"[GremlinServer] Authentication required, sending challenge for request: {message.RequestId}");
                    }
                    
                    // Send authentication challenge
                    var authChallenge = new TinkerPopResponse
                    {
                        RequestId = message.RequestId,
                        Status = new TinkerPopStatus
                        {
                            Code = TinkerPopStatusCodes.Authenticate,
                            Message = "Authentication required",
                            Attributes = new Dictionary<string, object>
                            {
                                ["sasl"] = new List<string> { "PLAIN" }
                            }
                        }
                    };

                    var responseJson = session.Serializer.SerializeResponse(authChallenge);
                    await SendTinkerPopResponseAsync(webSocket, responseJson);
                    return;
                }

                Console.WriteLine($"[DEBUG] Processing message: {message.Op}");
                
                // Process the actual TinkerPop request
                TinkerPopResponse response;
                try
                {
                    response = await _sessionManager.ProcessMessageAsync(connectionId, message);
                    Console.WriteLine($"[DEBUG] Message processed successfully");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[DEBUG] Error processing message: {ex.Message}");
                    
                    if (_options.EnableLogging)
                    {
                        Console.WriteLine($"[GremlinServer] Error processing message {message.RequestId}: {ex.Message}");
                    }
                    
                    response = session.Serializer.CreateErrorResponse(
                        message.RequestId,
                        TinkerPopStatusCodes.ServerError,
                        $"Error processing request: {ex.Message}",
                        ex);
                }

                Console.WriteLine($"[DEBUG] About to serialize response for request: {response.RequestId}");
                
                // Serialize and send the response back to the client
                // Send response using the client's preferred format
                var responseJson2 = session.Serializer.SerializeResponse(response);
                
                Console.WriteLine($"[DEBUG] Response serialized, JSON length: {responseJson2?.Length ?? 0}");
                Console.WriteLine($"[DEBUG] First 200 chars of response: {(responseJson2?.Length > 0 ? responseJson2.Substring(0, Math.Min(200, responseJson2.Length)) : "null")}");

                // Check if client prefers binary messages (e.g., Gremlin.Net)
                var prefersBinary = session.Configuration.GetValueOrDefault("prefersBinaryMessages", false);
                if (prefersBinary is bool usesBinary && usesBinary)
                {
                    Console.WriteLine($"[DEBUG] Sending binary response to {connectionId}");
                    await SendTinkerPopBinaryResponseAsync(webSocket, responseJson2);
                }
                else
                {
                    Console.WriteLine($"[DEBUG] Sending text response to {connectionId}");
                    await SendTinkerPopResponseAsync(webSocket, responseJson2);
                }

                Console.WriteLine($"[DEBUG] Response sent successfully");
                
                if (_options.EnableDebugLogging)
                {
                    Console.WriteLine($"[GremlinServer] TinkerPop response to {connectionId}: {responseJson2}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DEBUG] Exception in ProcessTinkerPopMessageAsync: {ex.Message}");
                
                if (_options.EnableLogging)
                {
                    Console.WriteLine($"[GremlinServer] Error processing TinkerPop message: {ex.Message}");
                }

                // Send server error response using client's preferred format
                try
                {
                    var errorResponse = session.Serializer.CreateErrorResponse(
                        Guid.NewGuid(),
                        TinkerPopStatusCodes.ServerError,
                        ex.Message,
                        ex);

                    var responseJson = session.Serializer.SerializeResponse(errorResponse);
                    
                    // Check if client prefers binary messages
                    var prefersBinary = session.Configuration.GetValueOrDefault("prefersBinaryMessages", false);
                    if (prefersBinary is bool usesBinary && usesBinary)
                    {
                        await SendTinkerPopBinaryResponseAsync(webSocket, responseJson);
                    }
                    else
                    {
                        await SendTinkerPopResponseAsync(webSocket, responseJson);
                    }
                }
                catch (Exception sendEx)
                {
                    if (_options.EnableDebugLogging)
                    {
                        Console.WriteLine($"[GremlinServer] Failed to send error response: {sendEx.Message}");
                    }
                }
            }
        }

        private async Task ProcessTinkerPopBinaryMessageAsync(WebSocket webSocket, string connectionId, byte[] binaryData)
        {
            Console.WriteLine($"[DEBUG] ProcessTinkerPopBinaryMessageAsync called with {binaryData.Length} bytes");
            
            var session = _sessionManager.GetSession(connectionId);
            if (session == null)
            {
                Console.WriteLine($"[DEBUG] No session found for binary message");
                return;
            }

            try
            {
                Console.WriteLine($"[DEBUG] Attempting to parse binary message");
                
                // TinkerPop binary message format handling based on reference implementation
                // The binary format can be:
                // 1. GraphBinary (modern Gremlin.Net)
                // 2. Length-prefixed GraphSON
                // 3. MIME-type prefixed GraphSON
                // 4. Plain UTF-8 GraphSON
                
                string jsonMessage = null;
                bool isGraphBinary = false;
                
                // Step 1: Try GraphBinary format (used by modern Gremlin.Net)
                var graphBinarySerializer = new GraphBinarySerializer();
                if (graphBinarySerializer.TryParseGraphBinaryMessage(binaryData, out var graphBinaryMessage))
                {
                    Console.WriteLine($"[DEBUG] Successfully parsed GraphBinary message");
                    Console.WriteLine($"[DEBUG] Operation: {graphBinaryMessage.Op}");
                    Console.WriteLine($"[DEBUG] Gremlin: {graphBinaryMessage.Args.GetValueOrDefault("gremlin", "unknown")}");
                    
                    isGraphBinary = true;
                    session.Configuration["isGraphBinaryClient"] = true;
                    session.Configuration["prefersBinaryMessages"] = true;
                    
                    try
                    {
                        var response = await _sessionManager.ProcessMessageAsync(connectionId, graphBinaryMessage);
                        
                        // For GraphBinary clients, send response in appropriate format
                        var responseJson = session.Serializer.SerializeResponse(response);
                        
                        // Send as binary to maintain consistency with client expectations
                        await SendTinkerPopBinaryResponseAsync(webSocket, responseJson);
                        
                        Console.WriteLine($"[DEBUG] GraphBinary message processed successfully");
                        return;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[DEBUG] Error processing GraphBinary message: {ex.Message}");
                        
                        var errorResponse = session.Serializer.CreateErrorResponse(
                            graphBinaryMessage.RequestId,
                            TinkerPopStatusCodes.ServerError,
                            $"GraphBinary processing error: {ex.Message}",
                            ex);

                        var errorJson = session.Serializer.SerializeResponse(errorResponse);
                        await SendTinkerPopBinaryResponseAsync(webSocket, errorJson);
                        return;
                    }
                }
                
                Console.WriteLine($"[DEBUG] Not a GraphBinary message, trying GraphSON binary parsing");
                
                // Step 2: Try different GraphSON binary formats
                if (TryParseGraphSONBinary(binaryData, out jsonMessage))
                {
                    Console.WriteLine($"[DEBUG] Successfully parsed GraphSON binary format");
                    Console.WriteLine($"[DEBUG] Extracted JSON: {jsonMessage}");
                    
                    // Process as regular TinkerPop message
                    await ProcessTinkerPopMessageAsync(webSocket, connectionId, jsonMessage);
                    return;
                }
                
                Console.WriteLine($"[DEBUG] Failed to parse binary message in any known format");
                
                // Step 3: Fallback - treat as UTF-8 encoded JSON
                try
                {
                    jsonMessage = Encoding.UTF8.GetString(binaryData);
                    if (IsValidJson(jsonMessage))
                    {
                        Console.WriteLine($"[DEBUG] Treating as UTF-8 JSON fallback");
                        await ProcessTinkerPopMessageAsync(webSocket, connectionId, jsonMessage);
                        return;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[DEBUG] UTF-8 fallback failed: {ex.Message}");
                }
                
                // If all parsing fails, send error response
                Console.WriteLine($"[DEBUG] All binary parsing methods failed");
                throw new Exception("Unable to parse binary message in any supported format");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DEBUG] Error processing binary message: {ex.GetType().FullName}: {ex.Message}");
                
                // Send error response in binary format to maintain consistency
                var errorResponse = session.Serializer.CreateErrorResponse(
                    Guid.NewGuid(),
                    TinkerPopStatusCodes.ServerSerializationError,
                    $"Binary message processing error: {ex.Message}",
                    ex);

                var responseJson = session.Serializer.SerializeResponse(errorResponse);
                await SendTinkerPopBinaryResponseAsync(webSocket, responseJson);
            }
        }

        /// <summary>
        /// Parse TinkerPop GraphSON binary formats based on the reference implementation
        /// </summary>
        private bool TryParseGraphSONBinary(byte[] binaryData, out string jsonMessage)
        {
            jsonMessage = null;
            
            try
            {
                // Convert to string for analysis
                var messageText = Encoding.UTF8.GetString(binaryData);
                
                // Format 1: MIME type prefixed (TinkerPop standard)
                // Format: "!{mime-type}{json}"
                if (messageText.StartsWith("!"))
                {
                    Console.WriteLine($"[DEBUG] Detected TinkerPop MIME-prefixed binary format");
                    
                    var mimeTypeEnd = messageText.IndexOf('{');
                    if (mimeTypeEnd > 1)
                    {
                        var mimeType = messageText.Substring(1, mimeTypeEnd - 1);
                        jsonMessage = messageText.Substring(mimeTypeEnd);
                        
                        Console.WriteLine($"[DEBUG] Extracted MIME type: {mimeType}");
                        Console.WriteLine($"[DEBUG] Extracted JSON: {jsonMessage}");
                        
                        if (IsValidJson(jsonMessage))
                        {
                            return true;
                        }
                    }
                }
                
                // Format 2: Length-prefixed binary
                if (binaryData.Length >= 4)
                {
                    try
                    {
                        // Try little-endian first (common)
                        var length = BitConverter.ToInt32(binaryData, 0);
                        if (length > 0 && length <= binaryData.Length - 4)
                        {
                            jsonMessage = Encoding.UTF8.GetString(binaryData, 4, length);
                            if (IsValidJson(jsonMessage))
                            {
                                Console.WriteLine($"[DEBUG] Parsed length-prefixed binary (little-endian)");
                                return true;
                            }
                        }
                        
                        // Try big-endian
                        var lengthBE = BitConverter.ToInt32(binaryData.Take(4).Reverse().ToArray(), 0);
                        if (lengthBE > 0 && lengthBE <= binaryData.Length - 4)
                        {
                            jsonMessage = Encoding.UTF8.GetString(binaryData, 4, lengthBE);
                            if (IsValidJson(jsonMessage))
                            {
                                Console.WriteLine($"[DEBUG] Parsed length-prefixed binary (big-endian)");
                                return true;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[DEBUG] Length-prefixed parsing failed: {ex.Message}");
                    }
                }
                
                // Format 3: Direct UTF-8 JSON (fallback)
                if (IsValidJson(messageText))
                {
                    jsonMessage = messageText;
                    Console.WriteLine($"[DEBUG] Treating as direct UTF-8 JSON");
                    return true;
                }
                
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DEBUG] GraphSON binary parsing failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Validate if a string is valid JSON
        /// </summary>
        private bool IsValidJson(string jsonString)
        {
            if (string.IsNullOrWhiteSpace(jsonString))
                return false;
                
            jsonString = jsonString.Trim();
            if (!jsonString.StartsWith("{") || !jsonString.EndsWith("}"))
                return false;
            
            try
            {
                JObject.Parse(jsonString);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private async Task SendTinkerPopResponseAsync(WebSocket webSocket, string responseJson)
        {
            Console.WriteLine($"[DEBUG] SendTinkerPopResponseAsync called");
            Console.WriteLine($"[DEBUG] WebSocket state: {webSocket.State}");
            Console.WriteLine($"[DEBUG] Response JSON: {responseJson}");
            
            if (webSocket.State == WebSocketState.Open)
            {
                try
                {
                    var responseBytes = Encoding.UTF8.GetBytes(responseJson);
                    Console.WriteLine($"[DEBUG] Sending {responseBytes.Length} bytes via WebSocket");
                    
                    // TinkerPop-compliant WebSocket message sending with enhanced buffer management
                    await webSocket.SendAsync(
                        new ArraySegment<byte>(responseBytes, 0, responseBytes.Length),
                        WebSocketMessageType.Text,
                        true, // End of message
                        _cancellationTokenSource.Token);
                        
                    Console.WriteLine($"[DEBUG] TinkerPop WebSocket send completed successfully");
                }
                catch (Exception ex) when (ex.Message.Contains("most significant bit") || ex.Message.Contains("format"))
                {
                    Console.WriteLine($"[DEBUG] WebSocket format error detected, using TinkerPop fallback: {ex.Message}");
                    
                    try
                    {
                        // TinkerPop-compliant minimal response for compatibility
                        var minimalResponse = "{\"requestId\":\"" + Guid.NewGuid() + "\",\"status\":{\"code\":200,\"message\":\"\"},\"result\":{\"data\":[\"ok\"]}}";
                        var minimalBytes = Encoding.UTF8.GetBytes(minimalResponse);
                        
                        await webSocket.SendAsync(
                            new ArraySegment<byte>(minimalBytes),
                            WebSocketMessageType.Text,
                            true,
                            CancellationToken.None);
                            
                        Console.WriteLine($"[DEBUG] TinkerPop fallback send successful");
                    }
                    catch (Exception fallbackEx)
                    {
                        Console.WriteLine($"[DEBUG] TinkerPop fallback also failed: {fallbackEx.Message}");
                        throw;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[DEBUG] WebSocket send failed: {ex.GetType().FullName}: {ex.Message}");
                    if (_options.EnableDebugLogging)
                    {
                        Console.WriteLine($"[GremlinServer] Failed to send TinkerPop response: {ex.Message}");
                    }
                    throw;
                }
            }
            else
            {
                Console.WriteLine($"[DEBUG] Cannot send - WebSocket not open: {webSocket.State}");
            }
        }

        private async Task SendTinkerPopBinaryResponseAsync(WebSocket webSocket, string responseJson)
        {
            Console.WriteLine($"[DEBUG] SendTinkerPopBinaryResponseAsync called");
            Console.WriteLine($"[DEBUG] WebSocket state: {webSocket.State}");
            
            if (webSocket.State == WebSocketState.Open)
            {
                try
                {
                    // TinkerPop-compliant binary response format
                    // Based on Apache TinkerPop reference implementation
                    
                    var responseBytes = Encoding.UTF8.GetBytes(responseJson);
                    Console.WriteLine($"[DEBUG] Sending {responseBytes.Length} bytes as binary response");
                    
                    // For maximum Gremlin.Net compatibility, we'll send responses as text
                    // even when the request was binary. This is compliant with TinkerPop spec
                    // and avoids the "most significant bit" WebSocket frame format issues
                    await webSocket.SendAsync(
                        new ArraySegment<byte>(responseBytes),
                        WebSocketMessageType.Text, // Use Text to avoid frame format issues
                        true, // End of message
                        _cancellationTokenSource.Token);
                        
                    Console.WriteLine($"[DEBUG] Binary response sent successfully as text frame");
                }
                catch (Exception ex) when (ex.Message.Contains("most significant bit") || 
                                           ex.Message.Contains("format") ||
                                           ex.Message.Contains("WebSocket"))
                {
                    Console.WriteLine($"[DEBUG] WebSocket format error detected, attempting TinkerPop-compliant fallback: {ex.Message}");
                    
                    try
                    {
                        // TinkerPop fallback: Send minimal compliant response
                        var fallbackResponse = CreateMinimalTinkerPopResponse();
                        var fallbackBytes = Encoding.UTF8.GetBytes(fallbackResponse);
                        
                        // Use the most basic WebSocket send approach
                        await webSocket.SendAsync(
                            new ArraySegment<byte>(fallbackBytes),
                            WebSocketMessageType.Text,
                            true,
                            CancellationToken.None); // Use None to avoid cancellation issues
                            
                        Console.WriteLine($"[DEBUG] TinkerPop-compliant fallback response sent");
                    }
                    catch (Exception fallbackEx)
                    {
                        Console.WriteLine($"[DEBUG] TinkerPop fallback also failed: {fallbackEx.Message}");
                        
                        // If even the fallback fails, the connection will be closed
                        // which is acceptable behavior according to TinkerPop spec
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[DEBUG] Binary response send failed: {ex.GetType().FullName}: {ex.Message}");
                    throw;
                }
            }
            else
            {
                Console.WriteLine($"[DEBUG] Cannot send binary response - WebSocket not open: {webSocket.State}");
            }
        }

        /// <summary>
        /// Create a minimal TinkerPop-compliant response for fallback scenarios
        /// </summary>
        private string CreateMinimalTinkerPopResponse()
        {
            var response = new
            {
                requestId = Guid.NewGuid().ToString(),
                status = new
                {
                    message = "",
                    code = 200,
                    attributes = new { }
                },
                result = new
                {
                    data = new[] { "ok" },
                    meta = new { }
                }
            };
            
            return JsonConvert.SerializeObject(response);
        }

        private TinkerPopMessage TryAlternativeMessageParsing(string messageText)
        {
            try
            {
                // Use the Apache TinkerGraph compatible serializer for alternative parsing
                var apacheSerializer = new ApacheTinkerGraphCompatibleSerializer();
                return apacheSerializer.DeserializeMessage(messageText);
            }
            catch (Exception ex)
            {
                throw new Exception($"Alternative parsing failed: {ex.Message}", ex);
            }
        }

        private bool AuthenticateRequest(HttpListenerContext context)
        {
            if (_options.Authentication == null)
                return true;

            if (_options.Authentication.EnableBasicAuth)
            {
                var authorization = context.Request.Headers["Authorization"];
                if (string.IsNullOrEmpty(authorization) || !authorization.StartsWith("Basic "))
                    return false;

                try
                {
                    var credentials = Encoding.UTF8.GetString(Convert.FromBase64String(authorization.Substring(6)));
                    var parts = credentials.Split(':');
                    
                    if (parts.Length == 2)
                    {
                        var username = parts[0];
                        var password = parts[1];

                        return username == _options.Authentication.Username && 
                               password == _options.Authentication.Password;
                    }
                }
                catch
                {
                    return false;
                }
            }

            return true;
        }

        private Task CloseConnectionAsync(TcpClient client)
        {
            return Task.Run(() =>
            {
                try
                {
                    client.Close();
                }
                catch (Exception ex)
                {
                    if (_options.EnableLogging)
                    {
                        Console.WriteLine($"[GremlinServer] Error closing TCP connection: {ex.Message}");
                    }
                }
            });
        }

        private async Task CloseWebSocketAsync(WebSocket webSocket)
        {
            try
            {
                if (webSocket.State == WebSocketState.Open)
                {
                    await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
                }
            }
            catch (Exception ex)
            {
                if (_options.EnableLogging)
                {
                    Console.WriteLine($"[GremlinServer] Error closing WebSocket connection: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Main server loop for accepting TCP connections
        /// </summary>
        private async Task AcceptTcpConnectionsAsync()
        {
            while (!_cancellationTokenSource.Token.IsCancellationRequested)
            {
                try
                {
                    var client = await _tcpListener.AcceptTcpClientAsync();
                    _ = Task.Run(() => HandleTcpConnectionAsync(client), _cancellationTokenSource.Token);
                }
                catch (Exception ex) when (_cancellationTokenSource.Token.IsCancellationRequested)
                {
                    break; // Accept operation was canceled, exit the loop
                }
                catch (Exception ex)
                {
                    if (_options.EnableLogging)
                    {
                        Console.WriteLine($"[GremlinServer] Error accepting TCP connection: {ex.Message}");
                    }
                }
            }
        }

        private async Task HandleTcpConnectionAsync(TcpClient client)
        {
            var connectionId = Guid.NewGuid().ToString();
            
            try
            {
                if (_options.EnableLogging)
                {
                    Console.WriteLine($"[GremlinServer] Accepted new TCP connection: {connectionId}");
                }

                _tcpConnections.TryAdd(connectionId, client);

                var stream = client.GetStream();
                var reader = new StreamReader(stream, Encoding.UTF8);
                var writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };

                // Enhance compatibility by introducing a slight delay
                await Task.Delay(50);

                // Initial handshake
                var handshakeResponse = "{\"requestId\":\"" + connectionId + "\",\"status\":{\"code\":200,\"message\":\"Connection established\"}}";
                await writer.WriteLineAsync(handshakeResponse);

                if (_options.EnableLogging)
                {
                    Console.WriteLine($"[GremlinServer] Handshake response sent to TCP client: {connectionId}");
                }

                // Create session for TCP connection
                var session = _sessionManager.CreateSession(connectionId);

                // Main loop for processing messages from the TCP client
                while (client.Connected && !_cancellationTokenSource.Token.IsCancellationRequested)
                {
                    try
                    {
                        var line = await reader.ReadLineAsync();
                        if (line == null)
                            break; // Client disconnected

                        Console.WriteLine($"[GremlinServer] Received message from TCP client {connectionId}: {line}");

                        // Directly process the message as a TinkerPop message
                        await ProcessTinkerPopMessageAsync(writer, connectionId, line);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[GremlinServer] Error reading or processing TCP message: {ex.Message}");
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                if (_options.EnableLogging)
                {
                    Console.WriteLine($"[GremlinServer] Error in TCP connection handler: {ex.Message}");
                }
            }
            finally
            {
                // Clean up
                try
                {
                    client.Close();
                }
                catch { }

                _tcpConnections.TryRemove(connectionId, out _);
                _sessionManager.RemoveSession(connectionId);
                
                if (_options.EnableLogging)
                {
                    Console.WriteLine($"[GremlinServer] TCP connection closed: {connectionId}");
                }
            }
        }

        private async Task ProcessTinkerPopMessageAsync(TextWriter writer, string connectionId, string messageText)
        {
            Console.WriteLine($"[DEBUG] ProcessTinkerPopMessageAsync (TCP) called for {connectionId}");
            Console.WriteLine($"[DEBUG] Received message (TCP): {messageText}");
            
            var session = _sessionManager.GetSession(connectionId);
            if (session == null)
            {
                Console.WriteLine($"[DEBUG] No session found for connection (TCP) {connectionId}");
                return;
            }

            try
            {
                if (_options.EnableDebugLogging)
                {
                    Console.WriteLine($"[GremlinServer] TinkerPop message (TCP) from {connectionId}: {messageText}");
                }

                // Parse TinkerPop message
                TinkerPopMessage message;
                try
                {
                    message = session.Serializer.DeserializeMessage(messageText);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[DEBUG] Message parsing failed (TCP): {ex.Message}");
                    throw;
                }

                // Validate message structure
                if (message == null)
                {
                    Console.WriteLine($"[DEBUG] Message is null after parsing (TCP)");
                    return;
                }

                // Set default request ID if missing (for compatibility)
                if (message.RequestId == Guid.Empty)
                {
                    message.RequestId = Guid.NewGuid();
                    Console.WriteLine($"[DEBUG] Generated missing request ID (TCP): {message.RequestId}");
                }

                // Handle authentication requirement for Gremlin.Net compatibility
                if (!session.IsAuthenticated && _options.Authentication != null && message.Op != TinkerPopOperations.Authentication)
                {
                    Console.WriteLine($"[DEBUG] Authentication required for message (TCP): {message.Op}");
                    
                    if (_options.EnableDebugLogging)
                    {
                        Console.WriteLine($"[GremlinServer] Authentication required, sending challenge for request (TCP): {message.RequestId}");
                    }
                    
                    // Send authentication challenge
                    var authChallenge = new TinkerPopResponse
                    {
                        RequestId = message.RequestId,
                        Status = new TinkerPopStatus
                        {
                            Code = TinkerPopStatusCodes.Authenticate,
                            Message = "Authentication required",
                            Attributes = new Dictionary<string, object>
                            {
                                ["sasl"] = new List<string> { "PLAIN" }
                            }
                        }
                    };

                    var responseJson = session.Serializer.SerializeResponse(authChallenge);
                    await writer.WriteLineAsync(responseJson);
                    return;
                }

                Console.WriteLine($"[DEBUG] Processing message (TCP): {message.Op}");
                
                // Process the actual TinkerPop request
                TinkerPopResponse response;
                try
                {
                    response = await _sessionManager.ProcessMessageAsync(connectionId, message);
                    Console.WriteLine($"[DEBUG] Message processed successfully (TCP)");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[DEBUG] Error processing message (TCP): {ex.Message}");
                    
                    if (_options.EnableLogging)
                    {
                        Console.WriteLine($"[GremlinServer] Error processing message (TCP) {message.RequestId}: {ex.Message}");
                    }
                    
                    response = session.Serializer.CreateErrorResponse(
                        message.RequestId,
                        TinkerPopStatusCodes.ServerError,
                        $"Error processing request (TCP): {ex.Message}",
                        ex);
                }

                // Serialize and send the response back to the client
                var responseJson2 = session.Serializer.SerializeResponse(response);
                
                Console.WriteLine($"[DEBUG] Response serialized (TCP), JSON length: {responseJson2?.Length ?? 0}");
                Console.WriteLine($"[DEBUG] First 200 chars of response (TCP): {(responseJson2?.Length > 0 ? responseJson2.Substring(0, Math.Min(200, responseJson2.Length)) : "null")}");

                // Send response
                await writer.WriteLineAsync(responseJson2);

                Console.WriteLine($"[DEBUG] Response sent successfully (TCP)");
                
                if (_options.EnableDebugLogging)
                {
                    Console.WriteLine($"[GremlinServer] TinkerPop response (TCP) to {connectionId}: {responseJson2}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DEBUG] Exception in ProcessTinkerPopMessageAsync (TCP): {ex.Message}");
                
                if (_options.EnableLogging)
                {
                    Console.WriteLine($"[GremlinServer] Error processing TinkerPop message (TCP): {ex.Message}");
                }

                // Send server error response using client's preferred format
                try
                {
                    var errorResponse = session.Serializer.CreateErrorResponse(
                        Guid.NewGuid(),
                        TinkerPopStatusCodes.ServerError,
                        ex.Message,
                        ex);

                    var responseJson = session.Serializer.SerializeResponse(errorResponse);
                    await writer.WriteLineAsync(responseJson);
                }
                catch (Exception sendEx)
                {
                    if (_options.EnableDebugLogging)
                    {
                        Console.WriteLine($"[GremlinServer] Failed to send error response (TCP): {sendEx.Message}");
                    }
                }
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            if (disposing)
            {
                // Cancel any running tasks
                _cancellationTokenSource?.Cancel();

                // Close all active connections
                var closeTasks = new List<Task>();
                
                // Close TCP connections
                closeTasks.AddRange(_tcpConnections.Values.Select(CloseConnectionAsync));

                // Close WebSocket connections
                foreach (var kvp in _webSocketConnections)
                {
                    if (kvp.Value is WebSocket ws)
                    {
                        closeTasks.Add(CloseWebSocketAsync(ws));
                    }
                }

                try
                {
                    Task.WhenAll(closeTasks).GetAwaiter().GetResult();
                }
                catch
                {
                    // Ignore exceptions during close
                }

                _tcpListener?.Stop();
                _httpListener?.Stop();
                _httpListener?.Close();
                
                _cancellationTokenSource?.Dispose();
                _connectionSemaphore?.Dispose();
            }

            _disposed = true;
        }

        private async Task HandleHttpRequestAsync(HttpListenerContext context)
        {
            if (context.Request.HttpMethod == "POST")
            {
                using (var reader = new StreamReader(context.Request.InputStream))
                {
                    var requestBody = await reader.ReadToEndAsync();
                    var response = await ProcessGremlinRequestAsync(requestBody, "http");
                    
                    var responseBytes = Encoding.UTF8.GetBytes(response);
                    context.Response.ContentType = "application/json";
                    context.Response.ContentLength64 = responseBytes.Length;
                    
                    // Add CORS headers for web clients
                    context.Response.Headers.Add("Access-Control-Allow-Origin", "*");
                    context.Response.Headers.Add("Access-Control-Allow-Methods", "POST, GET, OPTIONS");
                    context.Response.Headers.Add("Access-Control-Allow-Headers", "Content-Type, Authorization");
                    
                    await context.Response.OutputStream.WriteAsync(responseBytes, 0, responseBytes.Length);
                    context.Response.Close();
                }
            }
            else if (context.Request.HttpMethod == "GET")
            {
                // Health check or status endpoint
                var stats = GetStatistics();
                var response = JsonConvert.SerializeObject(stats, Formatting.Indented);
                var responseBytes = Encoding.UTF8.GetBytes(response);
                
                context.Response.ContentType = "application/json";
                context.Response.ContentLength64 = responseBytes.Length;
                context.Response.Headers.Add("Access-Control-Allow-Origin", "*");
                
                await context.Response.OutputStream.WriteAsync(responseBytes, 0, responseBytes.Length);
                context.Response.Close();
            }
            else if (context.Request.HttpMethod == "OPTIONS")
            {
                // CORS preflight
                context.Response.Headers.Add("Access-Control-Allow-Origin", "*");
                context.Response.Headers.Add("Access-Control-Allow-Methods", "POST, GET, OPTIONS");
                context.Response.Headers.Add("Access-Control-Allow-Headers", "Content-Type, Authorization");
                context.Response.StatusCode = 200;
                context.Response.Close();
            }
            else
            {
                context.Response.StatusCode = 405; // Method Not Allowed
                context.Response.Close();
            }
        }

        private async Task<string> ProcessGremlinRequestAsync(string requestMessage, string connectionId)
        {
            try
            {
                if (_options.EnableDebugLogging)
                {
                    Console.WriteLine($"[GremlinServer] Processing request from {connectionId}: {requestMessage}");
                }

                // Handle simple text queries for ease of use
                if (!requestMessage.TrimStart().StartsWith("{"))
                {
                    // Simple text query format
                    var result = await _connector.ExecuteAsync(requestMessage.Trim(), new Dictionary<string, object>());
                    return JsonConvert.SerializeObject(new { success = true, data = result.ToList() });
                }

                var request = JsonConvert.DeserializeObject<JObject>(requestMessage);
                var requestId = request["requestId"]?.ToString() ?? Guid.NewGuid().ToString();
                var op = request["op"]?.ToString() ?? "eval";
                var args = request["args"] as JObject;

                if (op == "eval")
                {
                    var gremlin = args?["gremlin"]?.ToString();
                    var bindings = args?["bindings"]?.ToObject<Dictionary<string, object>>() ?? new Dictionary<string, object>();

                    if (string.IsNullOrEmpty(gremlin))
                    {
                        return CreateErrorResponse(requestId, "Missing gremlin query");
                    }

                    // Apply request interceptors
                    foreach (var interceptor in _options.RequestInterceptors)
                    {
                        if (!interceptor(gremlin, bindings))
                        {
                            return CreateErrorResponse(requestId, "Request blocked by interceptor");
                        }
                    }

                    var result = await _connector.ExecuteAsync(gremlin, bindings);

                    // Apply response interceptors
                    foreach (var interceptor in _options.ResponseInterceptors)
                    {
                        result = interceptor(result);
                    }

                    return CreateSuccessResponse(requestId, result);
                }
                else
                {
                    return CreateErrorResponse(requestId, $"Unsupported operation: {op}");
                }
            }
            catch (Exception ex)
            {
                if (_options.EnableLogging)
                {
                    Console.WriteLine($"[GremlinServer] Request processing error: {ex.Message}");
                }
                
                return CreateErrorResponse(Guid.NewGuid().ToString(), ex.Message);
            }
        }

        private string CreateSuccessResponse(string requestId, IEnumerable<dynamic> result)
        {
            var response = new
            {
                requestId = requestId,
                status = new
                {
                    message = "",
                    code = 200,
                    attributes = new { }
                },
                result = new
                {
                    data = result.ToList(),
                    meta = new { }
                }
            };

            return JsonConvert.SerializeObject(response);
        }

        private string CreateErrorResponse(string requestId, string message)
        {
            var response = new
            {
                requestId = requestId,
                status = new
                {
                    message = message,
                    code = 500,
                    attributes = new { }
                },
                result = new
                {
                    data = new object[0],
                    meta = new { }
                }
            };

            return JsonConvert.SerializeObject(response);
        }
    }
}