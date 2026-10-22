using System.Text.Json;
using Microsoft.Extensions.Logging;
using Stardust.Paradox.GremlinStudio.Core.Storage;

namespace Stardust.Paradox.GremlinStudio.Core.Assistant;

/// <summary>A saved assistant conversation.</summary>
/// <param name="Id">Local conversation id.</param>
/// <param name="Title">Display title.</param>
/// <param name="SessionId">Copilot session id used to resume the conversation, or null before the first reply.</param>
/// <param name="UpdatedAt">Last change time.</param>
/// <param name="Messages">Chat transcript.</param>
public sealed record GremlinAssistantConversation(
    string Id,
    string Title,
    string? SessionId,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<GremlinAssistantMessage> Messages);

/// <summary>Persists saved assistant conversations.</summary>
public interface IGremlinAssistantConversationStore
{
    /// <summary>Loads all conversations, most recent first.</summary>
    IReadOnlyList<GremlinAssistantConversation> Load();

    /// <summary>Saves all conversations.</summary>
    void Save(IReadOnlyList<GremlinAssistantConversation> conversations);
}

/// <summary>File based <see cref="IGremlinAssistantConversationStore"/> stored under the local app data folder.</summary>
public sealed class FileGremlinAssistantConversationStore : IGremlinAssistantConversationStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly ILogger<FileGremlinAssistantConversationStore> _logger;

    public FileGremlinAssistantConversationStore(ILogger<FileGremlinAssistantConversationStore> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public IReadOnlyList<GremlinAssistantConversation> Load()
    {
        try
        {
            var path = AppDataPaths.AssistantConversationsFilePath;
            if (!File.Exists(path))
                return [];
            var items = JsonSerializer.Deserialize<List<GremlinAssistantConversation>>(File.ReadAllText(path), JsonOptions);
            return items?.OrderByDescending(c => c.UpdatedAt).ToList() ?? [];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            _logger.LogWarning(ex, "Failed to load assistant conversations");
            return [];
        }
    }

    public void Save(IReadOnlyList<GremlinAssistantConversation> conversations)
    {
        ArgumentNullException.ThrowIfNull(conversations);
        try
        {
            File.WriteAllText(AppDataPaths.AssistantConversationsFilePath, JsonSerializer.Serialize(conversations, JsonOptions));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "Failed to save assistant conversations");
        }
    }
}
