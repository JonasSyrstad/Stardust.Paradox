using System.Text.Json;
using Microsoft.Extensions.Logging;
using Stardust.Paradox.GremlinStudio.Core.Storage;

namespace Stardust.Paradox.GremlinStudio.Core.Assistant;

/// <summary>
/// User settings for the opt-in AI Gremlin assistant. The assistant is disabled by default.
/// </summary>
public sealed record GremlinAssistantSettings
{
    /// <summary>Whether the user has opted in to the assistant.</summary>
    public bool IsEnabled { get; init; }

    /// <summary>The GitHub Copilot model identifier used for chat.</summary>
    public string Model { get; init; } = GremlinAssistantSettingsDefaults.Model;
}

/// <summary>Default values for <see cref="GremlinAssistantSettings"/>.</summary>
public static class GremlinAssistantSettingsDefaults
{
    /// <summary>Default GitHub Copilot model identifier.</summary>
    public const string Model = "gpt-4.1";
}

/// <summary>Persists <see cref="GremlinAssistantSettings"/>.</summary>
public interface IGremlinAssistantSettingsStore
{
    /// <summary>Loads settings; returns defaults (disabled) when none are stored or the file is unreadable.</summary>
    GremlinAssistantSettings Load();

    /// <summary>Saves settings.</summary>
    void Save(GremlinAssistantSettings settings);
}

/// <summary>
/// File based <see cref="IGremlinAssistantSettingsStore"/> stored under the local app data folder.
/// </summary>
public sealed class FileGremlinAssistantSettingsStore : IGremlinAssistantSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly ILogger<FileGremlinAssistantSettingsStore> _logger;

    public FileGremlinAssistantSettingsStore(ILogger<FileGremlinAssistantSettingsStore> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public GremlinAssistantSettings Load()
    {
        try
        {
            var path = AppDataPaths.AssistantSettingsFilePath;
            if (!File.Exists(path))
                return new GremlinAssistantSettings();

            var settings = JsonSerializer.Deserialize<GremlinAssistantSettings>(File.ReadAllText(path), JsonOptions);
            if (settings is null)
                return new GremlinAssistantSettings();

            return string.IsNullOrWhiteSpace(settings.Model)
                ? settings with { Model = GremlinAssistantSettingsDefaults.Model }
                : settings;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            _logger.LogWarning(ex, "Failed to load assistant settings; using defaults");
            return new GremlinAssistantSettings();
        }
    }

    public void Save(GremlinAssistantSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        try
        {
            File.WriteAllText(AppDataPaths.AssistantSettingsFilePath, JsonSerializer.Serialize(settings, JsonOptions));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "Failed to save assistant settings");
        }
    }
}
