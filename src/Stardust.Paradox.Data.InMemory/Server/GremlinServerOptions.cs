using System;
using Stardust.Paradox.Data.InMemory.Core;
using System.Collections.Generic;

namespace Stardust.Paradox.Data.InMemory.Server
{
    /// <summary>
    /// Configuration options for the Gremlin server endpoint
    /// </summary>
    public class GremlinServerOptions
    {
        /// <summary>
        /// The hostname or IP address to bind the server to. Default is "localhost"
        /// </summary>
        public string Host { get; set; } = "localhost";

        /// <summary>
        /// The port number to bind the server to. Default is 8182 (standard Gremlin port)
        /// </summary>
        public int Port { get; set; } = 8182;

        /// <summary>
        /// Enable SSL/TLS encryption. Default is false for development
        /// </summary>
        public bool EnableSsl { get; set; } = false;

        /// <summary>
        /// SSL certificate path (required if EnableSsl is true)
        /// </summary>
        public string SslCertificatePath { get; set; }

        /// <summary>
        /// SSL certificate password
        /// </summary>
        public string SslCertificatePassword { get; set; }

        /// <summary>
        /// Enable WebSocket support (.NET Core 3.1+ and .NET 6+ only). Default is true when available
        /// </summary>
        public bool EnableWebSocket { get; set; } = true;

        /// <summary>
        /// Enable HTTP endpoint support. Default is true when WebSocket is available
        /// </summary>
        public bool EnableHttp { get; set; } = true;

        /// <summary>
        /// Enable TCP endpoint support. Default is true for backward compatibility
        /// </summary>
        public bool EnableTcp { get; set; } = true;

        /// <summary>
        /// HTTP/WebSocket port (different from TCP port). Default is Port + 1
        /// </summary>
        public int? HttpPort { get; set; }

        /// <summary>
        /// Maximum number of concurrent connections. Default is 100
        /// </summary>
        public int MaxConnections { get; set; } = 100;

        /// <summary>
        /// Connection timeout in seconds. Default is 30
        /// </summary>
        public int ConnectionTimeoutSeconds { get; set; } = 30;

        /// <summary>
        /// Query timeout in seconds. Default is 60
        /// </summary>
        public int QueryTimeoutSeconds { get; set; } = 60;

        /// <summary>
        /// Enable request/response logging. Default is false
        /// </summary>
        public bool EnableLogging { get; set; } = false;

        /// <summary>
        /// Enable detailed debug logging. Default is false
        /// </summary>
        public bool EnableDebugLogging { get; set; } = false;

        /// <summary>
        /// Supported wire formats/serializers. Default includes GraphSON v1, v2, and v3
        /// </summary>
        public List<string> SupportedFormats { get; set; } = new List<string>
        {
            "application/vnd.gremlin-v1.0+json",
            "application/vnd.gremlin-v2.0+json", 
            "application/vnd.gremlin-v3.0+json",
            "application/json"
        };

        /// <summary>
        /// Database options for the underlying in-memory database
        /// </summary>
        public InMemoryDatabaseOptions DatabaseOptions { get; set; } = new InMemoryDatabaseOptions();

        /// <summary>
        /// Authentication settings (optional)
        /// </summary>
        public GremlinAuthenticationOptions Authentication { get; set; }

        /// <summary>
        /// Custom request interceptors
        /// </summary>
        public List<Func<string, Dictionary<string, object>, bool>> RequestInterceptors { get; set; } = new List<Func<string, Dictionary<string, object>, bool>>();

        /// <summary>
        /// Custom response interceptors
        /// </summary>
        public List<Func<IEnumerable<dynamic>, IEnumerable<dynamic>>> ResponseInterceptors { get; set; } = new List<Func<IEnumerable<dynamic>, IEnumerable<dynamic>>>();

        /// <summary>
        /// WebSocket-specific options
        /// </summary>
        public GremlinWebSocketOptions WebSocketOptions { get; set; } = new GremlinWebSocketOptions();

        /// <summary>
        /// Get the HTTP port, using Port + 1 if not explicitly set
        /// </summary>
        public int GetHttpPort() => HttpPort ?? (Port + 1);
    }

    /// <summary>
    /// WebSocket-specific configuration options
    /// </summary>
    public class GremlinWebSocketOptions
    {
        /// <summary>
        /// WebSocket sub-protocol. Default is "gremlin-ws"
        /// </summary>
        public string SubProtocol { get; set; } = "gremlin-ws";

        /// <summary>
        /// Maximum message size in bytes. Default is 1MB
        /// </summary>
        public int MaxMessageSize { get; set; } = 1024 * 1024;

        /// <summary>
        /// Keep-alive interval in seconds. Default is 30
        /// </summary>
        public int KeepAliveIntervalSeconds { get; set; } = 30;

        /// <summary>
        /// Enable automatic ping/pong frames. Default is true
        /// </summary>
        public bool EnablePingPong { get; set; } = true;

        /// <summary>
        /// Close timeout in seconds. Default is 5
        /// </summary>
        public int CloseTimeoutSeconds { get; set; } = 5;
    }

    /// <summary>
    /// Authentication options for the Gremlin server
    /// </summary>
    public class GremlinAuthenticationOptions
    {
        /// <summary>
        /// Enable basic authentication. Default is false
        /// </summary>
        public bool EnableBasicAuth { get; set; } = false;

        /// <summary>
        /// Username for basic authentication
        /// </summary>
        public string Username { get; set; }

        /// <summary>
        /// Password for basic authentication
        /// </summary>
        public string Password { get; set; }

        /// <summary>
        /// Enable token-based authentication. Default is false
        /// </summary>
        public bool EnableTokenAuth { get; set; } = false;

        /// <summary>
        /// Valid authentication tokens
        /// </summary>
        public HashSet<string> ValidTokens { get; set; } = new HashSet<string>();

        /// <summary>
        /// Custom authentication handler
        /// </summary>
        public Func<Dictionary<string, string>, bool> CustomAuthHandler { get; set; }
    }
}
