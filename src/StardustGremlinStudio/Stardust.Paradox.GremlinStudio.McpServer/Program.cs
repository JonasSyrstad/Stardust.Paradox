using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Stardust.Paradox.GremlinStudio.Core;

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
