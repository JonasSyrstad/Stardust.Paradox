using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Gremlin.Net.Driver;
using Gremlin.Net.Structure.IO.GraphSON;
using Newtonsoft.Json;
using Stardust.Paradox.Data.InMemory.Server;

namespace Stardust.Paradox.Data.InMemory.Demo
{
    /// <summary>
    /// Wire protocol options for connecting to the Gremlin server
    /// </summary>
    public enum WireProtocol
    {
        Direct,    // Direct in-processor connector
        TCP,       // TCP connection
        WebSocket, // WebSocket connection (requires .NET Core 3.1+)
        HTTP       // HTTP REST API
    }

    /// <summary>
    /// Client abstraction that can connect using different wire protocols
    /// </summary>
    public interface IGremlinQueryClient : IDisposable
    {
        Task<IEnumerable<dynamic>> ExecuteAsync(string query);
        string Protocol { get; }
        bool IsConnected { get; }
    }

    /// <summary>
    /// Direct in-process client using the connector directly
    /// </summary>
    public class DirectGremlinClient : IGremlinQueryClient
    {
        private readonly InMemoryGremlinLanguageConnector _connector;

        public DirectGremlinClient(InMemoryGremlinLanguageConnector connector)
        {
            _connector = connector;
        }

        public string Protocol => "Direct In-Process";
        public bool IsConnected => true;

        public async Task<IEnumerable<dynamic>> ExecuteAsync(string query)
        {
            return await _connector.ExecuteAsync(query, new Dictionary<string, object>());
        }

        public void Dispose()
        {
            // Nothing to dispose for direct client
        }
    }

    /// <summary>
    /// TCP client using the custom TCP protocol
    /// </summary>
    public class TcpGremlinClient : IGremlinQueryClient
    {
        private readonly GremlinTcpClient _tcpClient;
        private bool _disposed;

        public TcpGremlinClient(string host, int port)
        {
            _tcpClient = new GremlinTcpClient(host, port);
        }

        public string Protocol => "TCP";
        public bool IsConnected => _tcpClient.IsConnected;

        public async Task ConnectAsync()
        {
            await _tcpClient.ConnectAsync();
        }

        public async Task<IEnumerable<dynamic>> ExecuteAsync(string query)
        {
            var response = await _tcpClient.ExecuteQueryAsync(query);
            var responseObj = JsonConvert.DeserializeObject<dynamic>(response);
            
            if (responseObj?.success == true)
            {
                return responseObj.data ?? new List<dynamic>();
            }
            
            throw new InvalidOperationException($"Query failed: {response}");
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                _tcpClient?.Dispose();
            }
        }
    }

    /// <summary>
    /// WebSocket client using the Gremlin WebSocket protocol
    /// </summary>
    public class WebSocketGremlinClient : IGremlinQueryClient
    {
        private readonly GremlinWebSocketClient _wsClient;
        private bool _disposed;

        public WebSocketGremlinClient(string host, int port)
        {
#if NETCOREAPP3_1_OR_GREATER || NET6_0_OR_GREATER || NET8_0_OR_GREATER
            _wsClient = new GremlinWebSocketClient(host, port);
#else
            throw new NotSupportedException("WebSocket client is only available on .NET Core 3.1+ and .NET 6+");
#endif
        }

        public string Protocol => "WebSocket";
        public bool IsConnected => 
#if NETCOREAPP3_1_OR_GREATER || NET6_0_OR_GREATER || NET8_0_OR_GREATER
            _wsClient?.IsConnected == true;
#else
            false;
#endif

        public async Task ConnectAsync()
        {
#if NETCOREAPP3_1_OR_GREATER || NET6_0_OR_GREATER || NET8_0_OR_GREATER
            await _wsClient.ConnectAsync();
#else
            throw new NotSupportedException("WebSocket client is only available on .NET Core 3.1+ and .NET 6+");
#endif
        }

        public async Task<IEnumerable<dynamic>> ExecuteAsync(string query)
        {
#if NETCOREAPP3_1_OR_GREATER || NET6_0_OR_GREATER || NET8_0_OR_GREATER
            try
            {
                var response = await _wsClient.ExecuteSimpleQueryAsync(query);
                
                if (response?.success == true)
                {
                    return response.data ?? new List<dynamic>();
                }
                
                throw new InvalidOperationException($"Query failed: {JsonConvert.SerializeObject(response)}");
            }
            catch (Exception ex) when (!(ex is InvalidOperationException))
            {
                throw new InvalidOperationException($"WebSocket query execution failed: {ex.Message}", ex);
            }
#else
            throw new NotSupportedException("WebSocket client is only available on .NET Core 3.1+ and .NET 6+");
#endif
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
#if NETCOREAPP3_1_OR_GREATER || NET6_0_OR_GREATER || NET8_0_OR_GREATER
                try
                {
                    _wsClient?.Dispose();
                }
                catch (Exception)
                {
                    // Ignore disposal exceptions
                }
#endif
            }
        }
    }

    /// <summary>
    /// Standard Gremlin.Net client using WebSocket
    /// </summary>
    public class GremlinNetClient : IGremlinQueryClient
    {
        private readonly GremlinClient _gremlinClient;
        private bool _disposed;

        public GremlinNetClient(string host, int port)
        {
            try
            {
                Console.WriteLine($"[DEBUG] Creating Gremlin.Net client for {host}:{port}");
                
                // Create Gremlin server connection with improved TinkerPop compatibility
                Console.WriteLine($"[DEBUG] Creating GremlinServer instance");
                var server = new Gremlin.Net.Driver.GremlinServer(host, port, false);
                Console.WriteLine($"[DEBUG] GremlinServer created successfully");
                
                // More conservative connection settings for better reliability
                Console.WriteLine($"[DEBUG] Creating ConnectionPoolSettings");
                var connectionPoolSettings = new ConnectionPoolSettings
                {
                    MaxInProcessPerConnection = 1,  // Reduced for testing
                    PoolSize = 1,                   // Single connection for testing
                    ReconnectionAttempts = 1,       // Reduce retry attempts
                    ReconnectionBaseDelay = TimeSpan.FromSeconds(2)
                };
                Console.WriteLine($"[DEBUG] ConnectionPoolSettings created");
                
                // Use GraphSON v3 for best compatibility
                Console.WriteLine($"[DEBUG] Creating GraphSON reader and writer");
                var reader = new GraphSON3Reader();
                var writer = new GraphSON3Writer();
                var mimeType = "application/vnd.gremlin-v3.0+json";
                Console.WriteLine($"[DEBUG] GraphSON components created, MIME type: {mimeType}");
                
                // Simplified WebSocket configuration
                Console.WriteLine($"[DEBUG] Creating WebSocket configuration");
                var webSocketConfiguration = new Action<System.Net.WebSockets.ClientWebSocketOptions>(options =>
                {
                    Console.WriteLine($"[DEBUG] Configuring WebSocket options");
                    options.AddSubProtocol("gremlin-ws");
                    options.KeepAliveInterval = TimeSpan.FromSeconds(60); // Longer keepalive
                    
                    // Add timeout settings
                    options.SetRequestHeader("User-Agent", "Stardust.Paradox.GremlinNet/1.0");
                    Console.WriteLine($"[DEBUG] WebSocket options configured");
                });
                Console.WriteLine($"[DEBUG] WebSocket configuration created");
                
                Console.WriteLine($"[DEBUG] Creating GremlinClient with conservative settings");
                _gremlinClient = new GremlinClient(
                    server, 
                    reader, 
                    writer, 
                    mimeType, 
                    connectionPoolSettings,
                    webSocketConfiguration);
                    
                Console.WriteLine($"[DEBUG] GremlinClient created successfully");
                Console.WriteLine($"[DEBUG] GremlinClient is null: {_gremlinClient == null}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DEBUG] GremlinClient creation failed: {ex.GetType().FullName}: {ex.Message}");
                Console.WriteLine($"[DEBUG] Stack trace: {ex.StackTrace}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"[DEBUG] Inner exception: {ex.InnerException.GetType().FullName}: {ex.InnerException.Message}");
                }
                throw new InvalidOperationException($"Failed to create Gremlin.Net client for {host}:{port}: {ex.Message}", ex);
            }
        }

        public string Protocol => "Gremlin.Net WebSocket";
        public bool IsConnected => !_disposed;

        public async Task<IEnumerable<dynamic>> ExecuteAsync(string query)
        {
            Console.WriteLine($"[DEBUG] GremlinNetClient.ExecuteAsync called with query: '{query}'");
            
            try
            {
                Console.WriteLine($"[DEBUG] Creating CancellationTokenSource with 30s timeout");
                using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(30));
                
                Console.WriteLine($"[DEBUG] Starting Task.Run for query execution");
                var task = Task.Run(async () => 
                {
                    try
                    {
                        Console.WriteLine($"[DEBUG] Inside Task.Run - about to call _gremlinClient.SubmitAsync");
                        Console.WriteLine($"[DEBUG] _gremlinClient is null: {_gremlinClient == null}");
                        Console.WriteLine($"[DEBUG] _disposed: {_disposed}");
                        
                        // Use the correct Gremlin.Net API method
                        var result = await _gremlinClient.SubmitAsync<dynamic>(query);
                        Console.WriteLine($"[DEBUG] _gremlinClient.SubmitAsync completed successfully");
                        Console.WriteLine($"[DEBUG] Result is null: {result == null}");
                        
                        if (result != null)
                        {
                            var resultList = result.ToList();
                            Console.WriteLine($"[DEBUG] Result converted to list with {resultList.Count} items");
                            return resultList;
                        }
                        else
                        {
                            Console.WriteLine($"[DEBUG] Result was null, returning empty list");
                            return new List<dynamic>();
                        }
                    }
                    catch (Exception ex)
                    {
                        // Log the specific exception for debugging
                        Console.WriteLine($"[DEBUG] Exception in Task.Run: {ex.GetType().FullName}");
                        Console.WriteLine($"[DEBUG] Exception message: {ex.Message}");
                        Console.WriteLine($"[DEBUG] Exception stack trace: {ex.StackTrace}");
                        if (ex.InnerException != null)
                        {
                            Console.WriteLine($"[DEBUG] Inner exception: {ex.InnerException.GetType().FullName}: {ex.InnerException.Message}");
                        }
                        throw;
                    }
                }, cts.Token);
                
                Console.WriteLine($"[DEBUG] Task.Run created, now awaiting result");
                var finalResult = await task;
                Console.WriteLine($"[DEBUG] Task completed successfully, returning result");
                return finalResult;
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine($"[DEBUG] Query operation was cancelled (timeout)");
                throw new TimeoutException("Gremlin.Net query timed out after 30 seconds");
            }
            catch (Exception ex) when (ex.GetType().Name.Contains("ResponseException"))
            {
                Console.WriteLine($"[DEBUG] TinkerPop ResponseException caught: {ex.Message}");
                throw new InvalidOperationException($"Gremlin.Net query failed: {ex.Message}", ex);
            }
            catch (System.Net.WebSockets.WebSocketException wsEx)
            {
                Console.WriteLine($"[DEBUG] WebSocketException caught: {wsEx.Message}, Code: {wsEx.WebSocketErrorCode}");
                throw new InvalidOperationException($"WebSocket connection error: {wsEx.Message} (Code: {wsEx.WebSocketErrorCode})", wsEx);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DEBUG] General exception caught: {ex.GetType().FullName}: {ex.Message}");
                Console.WriteLine($"[DEBUG] Exception stack trace: {ex.StackTrace}");
                throw new InvalidOperationException($"Gremlin.Net query execution failed: {ex.Message}", ex);
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                try
                {
                    _gremlinClient?.Dispose();
                }
                catch (Exception)
                {
                    // Ignore disposal exceptions
                }
            }
        }
    }

    /// <summary>
    /// HTTP client using REST API
    /// </summary>
    public class HttpGremlinClient : IGremlinQueryClient
    {
        private readonly System.Net.Http.HttpClient _httpClient;
        private readonly string _baseUrl;
        private bool _disposed;

        public HttpGremlinClient(string host, int port)
        {
            _httpClient = new System.Net.Http.HttpClient();
            _baseUrl = $"http://{host}:{port}";
        }

        public string Protocol => "HTTP REST";
        public bool IsConnected => !_disposed;

        public async Task<IEnumerable<dynamic>> ExecuteAsync(string query)
        {
            var request = new
            {
                requestId = Guid.NewGuid().ToString(),
                op = "eval",
                args = new
                {
                    gremlin = query,
                    bindings = new { }
                }
            };

            var json = JsonConvert.SerializeObject(request);
            var content = new System.Net.Http.StringContent(json, System.Text.Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(_baseUrl, content);
            var responseText = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var responseObj = JsonConvert.DeserializeObject<dynamic>(responseText);
                if (responseObj?.result?.data != null)
                {
                    return responseObj.result.data;
                }
                return new List<dynamic>();
            }

            throw new InvalidOperationException($"HTTP request failed: {response.StatusCode} - {responseText}");
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                _httpClient?.Dispose();
            }
        }
    }

    /// <summary>
    /// Factory for creating Gremlin clients based on wire protocol
    /// </summary>
    public static class GremlinClientFactory
    {
        public static async Task<IGremlinQueryClient> CreateClientAsync(WireProtocol protocol, InMemoryGremlinLanguageConnector directConnector = null, string host = "localhost", int tcpPort = 8182, int wsPort = 8183)
        {
            try
            {
                switch (protocol)
                {
                    case WireProtocol.Direct:
                        if (directConnector == null)
                            throw new ArgumentNullException(nameof(directConnector), "Direct connector is required for Direct protocol");
                        return new DirectGremlinClient(directConnector);

                    case WireProtocol.TCP:
                        var tcpClient = new TcpGremlinClient(host, tcpPort);
                        await tcpClient.ConnectAsync();
                        if (!tcpClient.IsConnected)
                            throw new InvalidOperationException($"Failed to connect to TCP server at {host}:{tcpPort}");
                        return tcpClient;

                    case WireProtocol.WebSocket:
                        var wsClient = new WebSocketGremlinClient(host, wsPort);
                        await wsClient.ConnectAsync();
                        if (!wsClient.IsConnected)
                            throw new InvalidOperationException($"Failed to connect to WebSocket server at {host}:{wsPort}");
                        return wsClient;

                    case WireProtocol.HTTP:
                        var httpClient = new HttpGremlinClient(host, wsPort); // HTTP uses same port as WebSocket
                        // Test the HTTP connection with a simple query
                        try
                        {
                            await httpClient.ExecuteAsync("g.V().limit(1).count()");
                        }
                        catch (Exception ex)
                        {
                            httpClient.Dispose();
                            throw new InvalidOperationException($"HTTP client connection test failed: {ex.Message}", ex);
                        }
                        return httpClient;

                    default:
                        throw new ArgumentException($"Unsupported protocol: {protocol}");
                }
            }
            catch (Exception ex) when (!(ex is ArgumentException || ex is ArgumentNullException))
            {
                throw new InvalidOperationException($"Failed to create client for {protocol} protocol: {ex.Message}", ex);
            }
        }

        public static IGremlinQueryClient CreateGremlinNetClientAsync(string host = "localhost", int port = 8183)
        {
            try
            {
                // Create Gremlin.Net client that works with our TinkerPop WebSocket implementation
                var client = new GremlinNetClient(host, port);
                
                return client;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to create Gremlin.Net client: {ex.Message}", ex);
            }
        }
    }
}