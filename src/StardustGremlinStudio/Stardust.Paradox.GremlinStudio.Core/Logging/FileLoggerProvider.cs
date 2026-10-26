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
    // When the daily file exceeds this size it is rolled to a single ".1" backup, capping each day at ~2x this size.
    private const long MaxFileBytes = 5 * 1024 * 1024;
    // Upper bound for the whole logs folder; oldest files are deleted first.
    private const long MaxTotalBytes = 25 * 1024 * 1024;

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

    /// <summary>
    /// Whether log entries are written to disk. Off by default; persisted in
    /// <see cref="AppDataPaths.FileLoggingPreferenceFilePath"/>.
    /// </summary>
    public static bool IsFileLoggingEnabled
    {
        get => _isFileLoggingEnabled ??= LoadPreference();
        set
        {
            _isFileLoggingEnabled = value;
            try
            {
                File.WriteAllText(AppDataPaths.FileLoggingPreferenceFilePath, value ? "true" : "false");
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Preference is still applied for this session.
            }
        }
    }

    private static bool? _isFileLoggingEnabled;

    private static bool LoadPreference()
    {
        try
        {
            var path = AppDataPaths.FileLoggingPreferenceFilePath;
            return File.Exists(path) && bool.TryParse(File.ReadAllText(path).Trim(), out var enabled) && enabled;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>Returns the path of the most recently written log file, or null if none exists.</summary>
    public static string? GetLatestLogFile(string? directory = null)
    {
        try
        {
            var dir = directory ?? AppDataPaths.LogsDirectory;
            return Directory.EnumerateFiles(dir, "gremlinstudio-*.log")
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .FirstOrDefault();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>Deletes all log files. Returns the number of files that could not be deleted.</summary>
    public static int DeleteAllLogFiles(string? directory = null)
    {
        var failed = 0;
        try
        {
            foreach (var file in Directory.EnumerateFiles(directory ?? AppDataPaths.LogsDirectory, "gremlinstudio-*.log").ToList())
            {
                try
                {
                    File.Delete(file);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    failed++;
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            failed++;
        }
        return failed;
    }

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
                var info = new FileInfo(path);
                if (info.Exists && info.Length >= MaxFileBytes)
                {
                    File.Move(path, Path.ChangeExtension(path, ".1.log"), overwrite: true);
                    EnforceTotalSize();
                }
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

        EnforceTotalSize();
    }

    private void EnforceTotalSize()
    {
        try
        {
            var files = new DirectoryInfo(_directory).GetFiles("gremlinstudio-*.log")
                .OrderByDescending(f => f.LastWriteTimeUtc)
                .ToList();
            long total = 0;
            foreach (var file in files)
            {
                total += file.Length;
                if (total > MaxTotalBytes)
                    file.Delete();
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

        public bool IsEnabled(LogLevel logLevel) => IsFileLoggingEnabled && logLevel != LogLevel.None && logLevel >= provider._minimumLevel;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
                return;
            provider.Write(category, logLevel, formatter(state, exception), exception);
        }
    }
}
