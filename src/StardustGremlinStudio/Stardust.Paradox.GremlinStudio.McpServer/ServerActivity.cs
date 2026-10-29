using System.Diagnostics;
using System.Reflection;

namespace Stardust.Paradox.GremlinStudio.McpServer;

/// <summary>
/// Tracks process-level activity for diagnostics (start time, last request, idle time).
/// </summary>
public static class ServerActivity
{
    private static long _lastRequestUtcTicks = DateTimeOffset.UtcNow.UtcTicks;

    public static DateTimeOffset StartedAt { get; } = GetProcessStartTime();

    private static DateTimeOffset GetProcessStartTime()
    {
        try
        {
            return new DateTimeOffset(Process.GetCurrentProcess().StartTime.ToUniversalTime(), TimeSpan.Zero);
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException)
        {
            // Process info can be unavailable in restricted environments; first-access time is a close approximation.
            return DateTimeOffset.UtcNow;
        }
    }

    public static string Version { get; } =
        typeof(ServerActivity).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0]
        ?? typeof(ServerActivity).Assembly.GetName().Version?.ToString()
        ?? "unknown";

    public static DateTimeOffset LastRequestAt => new(Interlocked.Read(ref _lastRequestUtcTicks), TimeSpan.Zero);

    /// <summary>
    /// Marks a request as received and returns how long the server had been idle before it.
    /// </summary>
    public static TimeSpan MarkRequest()
    {
        var now = DateTimeOffset.UtcNow.UtcTicks;
        var previous = Interlocked.Exchange(ref _lastRequestUtcTicks, now);
        return TimeSpan.FromTicks(now - previous);
    }
}
