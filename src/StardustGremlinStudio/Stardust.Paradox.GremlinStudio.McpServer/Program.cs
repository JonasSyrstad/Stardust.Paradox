using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using Stardust.Paradox.GremlinStudio.Core;
using Stardust.Paradox.GremlinStudio.McpServer;
using Stardust.Paradox.GremlinStudio.Core.Storage;

// File logger writes to the GremlinStudio AppData directory (same place as connections.json).
// This is the only safe logging channel since stdin/stdout are reserved for the MCP protocol.
// Set GREMLINSTUDIO_MCP_LOG_LEVEL=Debug to log every tool call (start/finish, duration, idle time).
var minLogLevel = Enum.TryParse<LogLevel>(Environment.GetEnvironmentVariable("GREMLINSTUDIO_MCP_LOG_LEVEL"), ignoreCase: true, out var configuredLevel)
    ? configuredLevel
    : LogLevel.Information;
var fileLoggerProvider = new McpFileLoggerProvider { MinLevel = minLogLevel };

// Migrate any legacy settings before building the host.
// The MCP server runs as a standalone process and may start before the GUI app has ever migrated.
using var bootstrapLoggerFactory = LoggerFactory.Create(b => b.AddProvider(fileLoggerProvider).SetMinimumLevel(minLogLevel));
var bootstrapLogger = bootstrapLoggerFactory.CreateLogger("McpServer");

// Last-chance logging so a crash is always visible in the log rather than a silently dropped session.
AppDomain.CurrentDomain.UnhandledException += (_, e) =>
    bootstrapLogger.LogCritical(e.ExceptionObject as Exception, "Unhandled exception (terminating: {IsTerminating})", e.IsTerminating);
TaskScheduler.UnobservedTaskException += (_, e) =>
{
    bootstrapLogger.LogError(e.Exception, "Unobserved task exception");
    e.SetObserved();
};

// A failed migration must not prevent the server from starting; the tools still work on current settings.
try
{
    LegacySettingsMigration.MigrateIfNeeded(bootstrapLogger);
}
catch (Exception ex)
{
    bootstrapLogger.LogError(ex, "Legacy settings migration failed; continuing startup");
}

var toolTimeout = int.TryParse(Environment.GetEnvironmentVariable("GREMLINSTUDIO_MCP_TOOL_TIMEOUT_SECONDS"), out var timeoutSeconds) && timeoutSeconds > 0
    ? TimeSpan.FromSeconds(timeoutSeconds)
    : TimeSpan.FromMinutes(2);

var builder = Host.CreateApplicationBuilder(args);

// Stdio transport uses stdin/stdout for MCP protocol messages.
// Console logging must be suppressed to avoid corrupting the transport.
// Route all runtime logs to the rolling file instead.
builder.Logging.ClearProviders();
builder.Logging.AddProvider(fileLoggerProvider);
builder.Logging.SetMinimumLevel(minLogLevel);

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly()
    .WithRequestFilters(filters => filters.AddCallToolFilter(next => async (context, cancellationToken) =>
    {
        var logger = context.Services?.GetService<ILoggerFactory>()?.CreateLogger("McpServer.Tools");
        var toolName = context.Params?.Name ?? "<unknown>";
        var connection = context.Params?.Arguments is { } args &&
            (args.TryGetValue("connectionName", out var value) || args.TryGetValue("nameOrId", out value) || args.TryGetValue("connectionNameOrId", out value))
            ? value.ToString()
            : "-";
        var idle = ServerActivity.MarkRequest();
        logger?.LogDebug("Tool {Tool} started (connection: {Connection}, idle before request: {IdleSeconds:F1}s)", toolName, connection, idle.TotalSeconds);

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(toolTimeout);
        try
        {
            var result = await next(context, timeoutCts.Token).ConfigureAwait(false);
            logger?.LogDebug("Tool {Tool} finished in {ElapsedMs} ms (isError: {IsError})", toolName, stopwatch.ElapsedMilliseconds, result.IsError == true);
            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            logger?.LogInformation("Tool {Tool} cancelled by client after {ElapsedMs} ms", toolName, stopwatch.ElapsedMilliseconds);
            throw;
        }
        catch (OperationCanceledException)
        {
            logger?.LogWarning("Tool {Tool} timed out after {ElapsedMs} ms (connection: {Connection})", toolName, stopwatch.ElapsedMilliseconds, connection);
            return ToolError($"Tool '{toolName}' timed out after {toolTimeout.TotalSeconds:F0}s.");
        }
        catch (McpException ex)
        {
            // Expected, user-facing failures (e.g. not found); the SDK turns these into isError results.
            logger?.LogInformation("Tool {Tool} returned error in {ElapsedMs} ms: {Error}", toolName, stopwatch.ElapsedMilliseconds, ex.Message);
            throw;
        }
        catch (Exception ex)
        {
            // Unexpected failure: log full details and return a diagnosable error instead of the SDK's generic message.
            logger?.LogError(ex, "Tool {Tool} failed in {ElapsedMs} ms (connection: {Connection})", toolName, stopwatch.ElapsedMilliseconds, connection);
            return ToolError($"Tool '{toolName}' failed: {ex.GetType().Name}: {ex.Message}");
        }
    }));

// Register all Gremlin Studio Core services (connections, execution, schema, etc.)
builder.Services.AddGremlinStudioCore();

var app = builder.Build();
app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("McpServer")
    .LogInformation("Gremlin Studio MCP server {Version} started (pid {ProcessId})", ServerActivity.Version, Environment.ProcessId);

try
{
    await app.RunAsync().ConfigureAwait(false);
}
catch (Exception ex)
{
    bootstrapLogger.LogCritical(ex, "MCP server host terminated unexpectedly");
    throw;
}

static CallToolResult ToolError(string message) => new()
{
    IsError = true,
    Content = [new TextContentBlock { Text = message }]
};
