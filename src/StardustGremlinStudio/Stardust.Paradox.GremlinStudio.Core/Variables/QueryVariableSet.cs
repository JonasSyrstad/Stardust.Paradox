namespace Stardust.Paradox.GremlinStudio.Core.Variables;

/// <summary>
/// A named collection of variables with optional per-connection overrides.
/// Global variables apply to all connections; connection-scoped variables
/// override or extend them for a specific connection.
/// </summary>
public sealed record QueryVariableSet(
    string Id,
    string Name,
    string Json,
    DateTimeOffset CreatedAt,
    DateTimeOffset LastModifiedAt,
    Dictionary<string, string>? ConnectionVariables = null)
{
    /// <summary>
    /// Gets the merged JSON for a specific connection, layering connection-scoped
    /// values over the global variables. Returns the global JSON when no
    /// connection-specific overrides exist.
    /// </summary>
    public string GetMergedJson(string? connectionId)
    {
        if (string.IsNullOrWhiteSpace(connectionId)
            || ConnectionVariables is null
            || !ConnectionVariables.TryGetValue(connectionId, out var connectionJson)
            || string.IsNullOrWhiteSpace(connectionJson))
        {
            return Json;
        }

        return MergeJson(Json, connectionJson);
    }

    private static string MergeJson(string baseJson, string overrideJson)
    {
        try
        {
            using var baseDoc = System.Text.Json.JsonDocument.Parse(baseJson);
            using var overrideDoc = System.Text.Json.JsonDocument.Parse(overrideJson);

            var merged = new Dictionary<string, System.Text.Json.JsonElement>();

            foreach (var prop in baseDoc.RootElement.EnumerateObject())
            {
                merged[prop.Name] = prop.Value.Clone();
            }

            foreach (var prop in overrideDoc.RootElement.EnumerateObject())
            {
                merged[prop.Name] = prop.Value.Clone();
            }

            return System.Text.Json.JsonSerializer.Serialize(merged);
        }
        catch
        {
            // Fallback to base if merge fails
            return baseJson;
        }
    }
}
