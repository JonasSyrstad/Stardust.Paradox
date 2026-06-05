using System.Globalization;
using Microsoft.Extensions.Logging;

namespace Stardust.Paradox.GremlinStudio.Core.Storage;

/// <summary>
/// A simple file logger for the MCP server process that writes to the GremlinStudio AppData directory.
/// Automatically rolls the log file when it exceeds <see cref="MaxFileSizeBytes"/>.
/// </summary>
public sealed class McpFileLogger : ILogger
{
    private readonly string _categoryName;
    private readonly McpFileLoggerProvider _provider;

    internal McpFileLogger(string categoryName, McpFileLoggerProvider provider)
    {
        _categoryName = categoryName;
        _provider = provider;
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => logLevel >= _provider.MinLevel;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
            return;

        var message = formatter(state, exception);
        var timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture);
        var logEntry = $"[{timestamp}] [{logLevel}] [{_categoryName}] {message}";

        if (exception is not null)
        {
            logEntry += Environment.NewLine + exception;
        }

        _provider.WriteEntry(logEntry);
    }
}

/// <summary>
/// Logger provider that writes log entries to a rolling file in the GremlinStudio AppData directory.
/// The log file is capped at <see cref="MaxFileSizeBytes"/> (default 2 MB) and rotates to a single backup.
/// </summary>
public sealed class McpFileLoggerProvider : ILoggerProvider
{
    private const string LogFileName = "mcp-server.log";
    private const string BackupLogFileName = "mcp-server.prev.log";

    /// <summary>
    /// Maximum log file size before rolling. Default: 2 MB.
    /// </summary>
    public long MaxFileSizeBytes { get; init; } = 2 * 1024 * 1024;

    /// <summary>
    /// Minimum log level to write.
    /// </summary>
    public LogLevel MinLevel { get; init; } = LogLevel.Information;

    private readonly string _logFilePath;
    private readonly string _backupFilePath;
    private readonly object _writeLock = new();

    public McpFileLoggerProvider()
    {
        var dir = AppDataPaths.EnsureAppDataDirectoryExists();
        _logFilePath = Path.Combine(dir, LogFileName);
        _backupFilePath = Path.Combine(dir, BackupLogFileName);
    }

    public ILogger CreateLogger(string categoryName) => new McpFileLogger(categoryName, this);

    internal void WriteEntry(string entry)
    {
        lock (_writeLock)
        {
            try
            {
                RollIfNeeded();
                File.AppendAllText(_logFilePath, entry + Environment.NewLine);
            }
            catch
            {
                // Logging should never crash the MCP server process.
            }
        }
    }

    private void RollIfNeeded()
    {
        try
        {
            if (!File.Exists(_logFilePath))
                return;

            var info = new FileInfo(_logFilePath);
            if (info.Length < MaxFileSizeBytes)
                return;

            // Rotate: current -> backup (overwrite previous backup)
            File.Copy(_logFilePath, _backupFilePath, overwrite: true);
            File.WriteAllText(_logFilePath, string.Empty);
        }
        catch
        {
            // Best effort; don't throw.
        }
    }

    public void Dispose()
    {
        // Nothing to dispose.
    }
}
