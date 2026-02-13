using System.Text.Json;
using System.Text.RegularExpressions;

namespace Stardust.Paradox.GremlinStudio.Core.Variables;

public static class QueryVariableSubstitutor
{
    // ${name} or ${path.to.value}
    private static readonly Regex TokenRegex = new(@"\$\{(?<path>[a-zA-Z_][a-zA-Z0-9_\.-]*)\}", RegexOptions.Compiled);

    public static string Substitute(string query, string variablesJson)
    {
        if (string.IsNullOrWhiteSpace(query) || string.IsNullOrWhiteSpace(variablesJson))
            return query;

        using var doc = JsonDocument.Parse(variablesJson);

        return TokenRegex.Replace(query, m => ResolvePath(doc.RootElement, m.Groups["path"].Value));
    }

    private static string ResolvePath(JsonElement root, string path)
    {
        var parts = path.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var current = root;

        foreach (var part in parts)
        {
            if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(part, out var next))
                return "";

            current = next;
        }

        return current.ValueKind switch
        {
            JsonValueKind.String => current.GetString() ?? string.Empty,
            JsonValueKind.Number => current.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Null => "null",
            _ => current.GetRawText()
        };
    }
}
