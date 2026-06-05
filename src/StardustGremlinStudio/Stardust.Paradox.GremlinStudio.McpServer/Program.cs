using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Stardust.Paradox.GremlinStudio.Core;
using Stardust.Paradox.GremlinStudio.Core.Storage;

// File logger writes to the GremlinStudio AppData directory (same place as connections.json).
// This is the only safe logging channel since stdin/stdout are reserved for the MCP protocol.
var fileLoggerProvider = new McpFileLoggerProvider { MinLevel = LogLevel.Information };

// Migrate any legacy settings before building the host.
// The MCP server runs as a standalone process and may start before the GUI app has ever migrated.
using (var migrationLoggerFactory = LoggerFactory.Create(b => b.AddProvider(fileLoggerProvider).SetMinimumLevel(LogLevel.Warning)))
{
    LegacySettingsMigration.MigrateIfNeeded(migrationLoggerFactory.CreateLogger("McpServer"));
}

var builder = Host.CreateApplicationBuilder(args);

// Stdio transport uses stdin/stdout for MCP protocol messages.
// Console logging must be suppressed to avoid corrupting the transport.
// Route all runtime logs to the rolling file instead.
builder.Logging.ClearProviders();
builder.Logging.AddProvider(fileLoggerProvider);
builder.Logging.SetMinimumLevel(LogLevel.Information);

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

// Register all Gremlin Studio Core services (connections, execution, schema, etc.)
builder.Services.AddGremlinStudioCore();

await builder.Build().RunAsync().ConfigureAwait(false);
