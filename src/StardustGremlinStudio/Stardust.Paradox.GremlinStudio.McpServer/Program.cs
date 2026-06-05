using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Stardust.Paradox.GremlinStudio.Core;
using Stardust.Paradox.GremlinStudio.Core.Storage;

// Migrate any legacy settings before building the host.
// The MCP server runs as a standalone process and may start before the GUI app has ever migrated.
using (var migrationLoggerFactory = LoggerFactory.Create(b => b.AddConsole().SetMinimumLevel(LogLevel.Warning)))
{
    LegacySettingsMigration.MigrateIfNeeded(migrationLoggerFactory.CreateLogger("McpServer"));
}

var builder = Host.CreateApplicationBuilder(args);

// Stdio transport uses stdin/stdout for MCP protocol messages.
// Console logging must be suppressed to avoid corrupting the transport.
builder.Logging.ClearProviders();
builder.Logging.AddFilter(_ => false);

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

// Register all Gremlin Studio Core services (connections, execution, schema, etc.)
builder.Services.AddGremlinStudioCore();

await builder.Build().RunAsync().ConfigureAwait(false);
