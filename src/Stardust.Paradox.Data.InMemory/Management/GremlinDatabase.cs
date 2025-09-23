#if NET8_0_OR_GREATER || NETCOREAPP3_1_OR_GREATER
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Stardust.Paradox.Data.InMemory.Server;

namespace Stardust.Paradox.Data.InMemory.Management
{
    /// <summary>
    /// Simplified facade for starting and managing a Gremlin database server
    /// Provides a clean API similar to other database systems
    /// </summary>
    public static class GremlinDatabase
    {
        private static readonly Dictionary<string, GremlinServer> _runningServers = new Dictionary<string, GremlinServer>();
        private static readonly object _lockObject = new object();

        /// <summary>
        /// Start a Gremlin database server with default options
        /// </summary>
        /// <returns>The running server instance</returns>
        public static async Task<GremlinServer> StartAsync()
        {
            return await StartAsync(new GremlinServerOptions());
        }

        /// <summary>
        /// Start a Gremlin database server with custom options
        /// </summary>
        /// <param name="options">Server configuration options</param>
        /// <returns>The running server instance</returns>
        public static async Task<GremlinServer> StartAsync(GremlinServerOptions options)
        {
            if (options == null)
                throw new ArgumentNullException(nameof(options));

            var serverKey = $"{options.Host}:{options.Port}";

            lock (_lockObject)
            {
                if (_runningServers.ContainsKey(serverKey))
                {
                    throw new InvalidOperationException($"Server already running on {serverKey}");
                }
            }

            var server = new GremlinServer(options);
            
            try
            {
                await server.StartAsync();
                
                lock (_lockObject)
                {
                    _runningServers[serverKey] = server;
                }

                return server;
            }
            catch
            {
                server.Dispose();
                throw;
            }
        }

        /// <summary>
        /// Start a Gremlin database server with fluent configuration
        /// </summary>
        /// <param name="configure">Configuration action</param>
        /// <returns>The running server instance</returns>
        public static async Task<GremlinServer> StartAsync(Action<GremlinServerOptions> configure)
        {
            var options = new GremlinServerOptions();
            configure?.Invoke(options);
            return await StartAsync(options);
        }

        /// <summary>
        /// Start a simple development server on localhost:8182
        /// </summary>
        /// <returns>The running server instance</returns>
        public static async Task<GremlinServer> StartDevServerAsync()
        {
            return await StartAsync(options =>
            {
                options.Host = "localhost";
                options.Port = 8182;
                options.EnableLogging = true;
                options.EnableDebugLogging = false;
                options.DatabaseOptions.EnableDebugLogging = false;
                options.MaxConnections = 50;
            });
        }

        /// <summary>
        /// Start a test server with extensive logging and debugging
        /// </summary>
        /// <returns>The running server instance</returns>
        public static async Task<GremlinServer> StartTestServerAsync()
        {
            return await StartAsync(options =>
            {
                options.Host = "localhost";
                options.Port = 8183; // Different port for testing
                options.EnableLogging = true;
                options.EnableDebugLogging = true;
                options.DatabaseOptions.EnableDebugLogging = true;
                options.MaxConnections = 10;
                options.ConnectionTimeoutSeconds = 10;
                options.QueryTimeoutSeconds = 30;
            });
        }

        /// <summary>
        /// Start a production-ready server with optimized settings
        /// </summary>
        /// <returns>The running server instance</returns>
        public static async Task<GremlinServer> StartProductionServerAsync(string host = "0.0.0.0", int port = 8182)
        {
            return await StartAsync(options =>
            {
                options.Host = host;
                options.Port = port;
                options.EnableLogging = false;
                options.EnableDebugLogging = false;
                options.DatabaseOptions.EnableDebugLogging = false;
                options.MaxConnections = 200;
                options.ConnectionTimeoutSeconds = 60;
                options.QueryTimeoutSeconds = 120;
            });
        }

        /// <summary>
        /// Start a WebSocket-enabled development server (requires .NET Core 3.1+ or .NET 6+)
        /// </summary>
        /// <returns>The running server instance</returns>
        public static async Task<GremlinServer> StartWebSocketDevServerAsync()
        {
            return await StartAsync(options =>
            {
                options.Host = "localhost";
                options.Port = 8182;      // TCP port
                options.HttpPort = 8183;  // WebSocket/HTTP port
                options.EnableTcp = true;
                options.EnableWebSocket = true;
                options.EnableHttp = true;
                options.EnableLogging = true;
                options.EnableDebugLogging = false;
                options.DatabaseOptions.EnableDebugLogging = false;
                options.MaxConnections = 50;
            });
        }

        /// <summary>
        /// Start a WebSocket-only server (requires .NET Core 3.1+ or .NET 6+)
        /// </summary>
        /// <returns>The running server instance</returns>
        public static async Task<GremlinServer> StartWebSocketOnlyServerAsync(string host = "localhost", int port = 8182)
        {
            return await StartAsync(options =>
            {
                options.Host = host;
                options.HttpPort = port;  // Use specified port for WebSocket/HTTP
                options.EnableTcp = false;
                options.EnableWebSocket = true;
                options.EnableHttp = true;
                options.EnableLogging = true;
                options.MaxConnections = 100;
            });
        }

        /// <summary>
        /// Start a production-ready server with WebSocket support
        /// </summary>
        /// <returns>The running server instance</returns>
        public static async Task<GremlinServer> StartProductionWebSocketServerAsync(string host = "0.0.0.0", int tcpPort = 8182, int wsPort = 8183)
        {
            return await StartAsync(options =>
            {
                options.Host = host;
                options.Port = tcpPort;
                options.HttpPort = wsPort;
                options.EnableTcp = true;
                options.EnableWebSocket = true;
                options.EnableHttp = true;
                options.EnableLogging = false;
                options.EnableDebugLogging = false;
                options.DatabaseOptions.EnableDebugLogging = false;
                options.MaxConnections = 500;
                options.ConnectionTimeoutSeconds = 60;
                options.QueryTimeoutSeconds = 120;
            });
        }

        /// <summary>
        /// Stop a specific server
        /// </summary>
        /// <param name="server">The server to stop</param>
        public static async Task StopAsync(GremlinServer server)
        {
            if (server == null)
                return;

            var serverKey = $"{server.Options.Host}:{server.Options.Port}";

            try
            {
                await server.StopAsync();
            }
            finally
            {
                lock (_lockObject)
                {
                    _runningServers.Remove(serverKey);
                }

                server.Dispose();
            }
        }

        /// <summary>
        /// Stop all running servers
        /// </summary>
        public static async Task StopAllAsync()
        {
            GremlinServer[] servers;

            lock (_lockObject)
            {
                servers = new GremlinServer[_runningServers.Values.Count];
                _runningServers.Values.CopyTo(servers, 0);
                _runningServers.Clear();
            }

            var stopTasks = new List<Task>();
            foreach (var server in servers)
            {
                stopTasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        await server.StopAsync();
                    }
                    finally
                    {
                        server.Dispose();
                    }
                }));
            }

            await Task.WhenAll(stopTasks);
        }

        /// <summary>
        /// Get a running server by host and port
        /// </summary>
        /// <param name="host">Server host</param>
        /// <param name="port">Server port</param>
        /// <returns>The server instance or null if not found</returns>
        public static GremlinServer GetRunningServer(string host = "localhost", int port = 8182)
        {
            var serverKey = $"{host}:{port}";

            lock (_lockObject)
            {
                _runningServers.TryGetValue(serverKey, out var server);
                return server;
            }
        }

        /// <summary>
        /// Get all running servers
        /// </summary>
        /// <returns>Array of running server instances</returns>
        public static GremlinServer[] GetRunningServers()
        {
            lock (_lockObject)
            {
                var servers = new GremlinServer[_runningServers.Values.Count];
                _runningServers.Values.CopyTo(servers, 0);
                return servers;
            }
        }

        /// <summary>
        /// Check if a server is running on the specified host and port
        /// </summary>
        /// <param name="host">Server host</param>
        /// <param name="port">Server port</param>
        /// <returns>True if server is running</returns>
        public static bool IsRunning(string host = "localhost", int port = 8182)
        {
            var server = GetRunningServer(host, port);
            return server?.IsRunning == true;
        }

        /// <summary>
        /// Get aggregate statistics from all running servers
        /// </summary>
        /// <returns>Combined server statistics</returns>
        public static Dictionary<string, object> GetGlobalStatistics()
        {
            var stats = new Dictionary<string, object>();
            var servers = GetRunningServers();

            stats["totalServers"] = servers.Length;
            stats["totalActiveConnections"] = 0;
            stats["totalVertices"] = 0;
            stats["totalEdges"] = 0;
            stats["totalRU"] = 0.0;

            var serverDetails = new List<object>();

            foreach (var server in servers)
            {
                var serverStats = server.GetStatistics();
                serverDetails.Add(serverStats);

                if (serverStats.TryGetValue("activeConnections", out var connections))
                    stats["totalActiveConnections"] = (int)stats["totalActiveConnections"] + (int)connections;

                if (serverStats.TryGetValue("vertexCount", out var vertices))
                    stats["totalVertices"] = (int)stats["totalVertices"] + (int)vertices;

                if (serverStats.TryGetValue("edgeCount", out var edges))
                    stats["totalEdges"] = (int)stats["totalEdges"] + (int)edges;

                if (serverStats.TryGetValue("totalRU", out var ru))
                    stats["totalRU"] = (double)stats["totalRU"] + (double)ru;
            }

            stats["servers"] = serverDetails;
            return stats;
        }

        #region Quick Start Methods

        /// <summary>
        /// Quick start for console applications - starts server and waits for shutdown
        /// </summary>
        /// <param name="configure">Optional configuration action</param>
        public static async Task RunAsync(Action<GremlinServerOptions> configure = null)
        {
            var server = await StartAsync(configure ?? (opt => 
            {
                opt.EnableLogging = true;
                Console.WriteLine("Starting Gremlin server...");
                Console.WriteLine($"Server will be available at ws://{opt.Host}:{opt.Port}");
                Console.WriteLine("Press Ctrl+C to stop the server");
            }));

            try
            {
                // Set up graceful shutdown
                Console.CancelKeyPress += async (sender, e) =>
                {
                    e.Cancel = true;
                    Console.WriteLine("\nShutting down server...");
                    await StopAsync(server);
                    Environment.Exit(0);
                };

                // Keep the server running
                while (server.IsRunning)
                {
                    await Task.Delay(1000);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Server error: {ex.Message}");
                await StopAsync(server);
                throw;
            }
        }

        /// <summary>
        /// Quick start for unit tests - starts a test server with a unique port
        /// </summary>
        /// <returns>The test server instance and connection details</returns>
        public static async Task<(GremlinServer Server, string TcpConnectionString, string WebSocketConnectionString)> StartTestInstanceAsync()
        {
            var random = new Random();
            var tcpPort = random.Next(9000, 9499); // Use random port to avoid conflicts
            var wsPort = random.Next(9500, 9999);  // WebSocket port range

            var server = await StartAsync(options =>
            {
                options.Host = "localhost";
                options.Port = tcpPort;
                options.HttpPort = wsPort;
                options.EnableTcp = true;
                options.EnableWebSocket = true;
                options.EnableHttp = true;
                options.EnableLogging = false;
                options.MaxConnections = 10;
                options.ConnectionTimeoutSeconds = 5;
                options.QueryTimeoutSeconds = 10;
            });

            var tcpConnectionString = $"tcp://localhost:{tcpPort}";
            var webSocketConnectionString = server.WebSocketSupported ? $"ws://localhost:{wsPort}" : null;
            return (server, tcpConnectionString, webSocketConnectionString);
        }

        #endregion
    }
}

#endif