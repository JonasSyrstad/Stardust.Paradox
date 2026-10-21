using System.Text;
using Microsoft.Extensions.Logging;
using Stardust.Paradox.GremlinStudio.Core.Storage;

namespace Stardust.Paradox.GremlinStudio.Core.Logging;

/// <summary>
/// Minimal rolling file logger. Writes one file per day to <see cref="AppDataPaths.LogsDirectory"/>
/// (gremlinstudio-yyyyMMdd.log) and deletes files older than the retention period.
/// </summary>
public sealed class FileLoggerProvider : ILoggerProvider
{
    private readonly object _sync = new();
    private readonly string _directory;
    private readonly LogLevel _minimumLevel;
    private readonly int _retentionDays;

    /// <summary>Creates a file logger provider.</summary>
    /// <param name="minimumLevel">Lowest level that is written.</param>
    /// <param name="retentionDays">Number of daily log files to keep.</param>
    /// <param name="directory">Log directory; defaults to the app data logs folder.</param>
    public FileLoggerProvider(LogLevel minimumLevel = LogLevel.Information, int retentionDays = 7, string? directory = null)
    {
        _directory = directory ?? AppDataPaths.LogsDirectory;
        _minimumLevel = minimumLevel;
        _retentionDays = Math.Max(1, retentionDays);
        Directory.CreateDirectory(_directory);
        DeleteOldFiles();
    }

    /// <summary>The directory log files are written to.</summary>
    public string LogDirectory => _directory;

    public ILogger CreateLogger(string categoryName) => new FileLogger(this, categoryName);

    public void Dispose()
    {
    }

    private void Write(string category, LogLevel level, string message, Exception? exception)
    {
        var sb = new StringBuilder()
            .Append(DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss.fff zzz"))
            .Append(" [").Append(level).Append("] ")
            .Append(category).Append(": ")
            .AppendLine(message);
        if (exception is not null)
            sb.AppendLine(exception.ToString());

        var path = Path.Combine(_directory, $"gremlinstudio-{DateTime.Now:yyyyMMdd}.log");
        lock (_sync)
        {
            try
            {
                File.AppendAllText(path, sb.ToString());
            }
            catch (IOException)
            {
                // Logging must never break the app.
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }

    private void DeleteOldFiles()
    {
        try
        {
            var cutoff = DateTime.Now.Date.AddDays(-_retentionDays);
            foreach (var file in Directory.EnumerateFiles(_directory, "gremlinstudio-*.log"))
            {
                if (File.GetLastWriteTime(file) < cutoff)
                    File.Delete(file);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Best effort cleanup.
        }
    }

    private sealed class FileLogger(FileLoggerProvider provider, string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None && logLevel >= provider._minimumLevel;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
                return;
            provider.Write(category, logLevel, formatter(state, exception), exception);
        }
    }
}
