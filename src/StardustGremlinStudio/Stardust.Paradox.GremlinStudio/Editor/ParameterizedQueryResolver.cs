using System.Text.Json;
using System.Text.RegularExpressions;

namespace Stardust.Paradox.GremlinStudio.Editor;

/// <summary>
/// Resolves parameterized Gremlin queries produced by the Stardust ORM logging.
/// Detects the pattern of a query line followed by a JSON parameter map and substitutes
/// placeholder tokens (__p0, __p1, …) with their actual values.
/// </summary>
public static class ParameterizedQueryResolver
{
    // Matches tokens like __p0, __p12 etc. that appear unquoted or inside quotes in the query
    private static readonly Regex ParameterTokenRegex = new(
        @"__p\d+",
        RegexOptions.Compiled);

    /// <summary>
    /// Returns true when <paramref name="text"/> looks like a parameterized query
    /// (a Gremlin query line followed by a JSON object with __p keys).
    /// </summary>
    public static bool IsParameterizedQuery(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var (query, jsonBlock) = SplitQueryAndJson(text);
        if (string.IsNullOrWhiteSpace(query) || string.IsNullOrWhiteSpace(jsonBlock))
            return false;

        return ParameterTokenRegex.IsMatch(query) && jsonBlock.TrimStart().StartsWith('{');
    }

    /// <summary>
    /// Resolves a parameterized query by replacing __pN tokens with their actual values
    /// from the accompanying JSON parameter map.
    /// </summary>
    /// <returns>The resolved Gremlin query with literal values inlined.</returns>
    /// <exception cref="ArgumentException">The input does not contain a valid parameter map.</exception>
    public static string Resolve(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return text;

        var (query, jsonBlock) = SplitQueryAndJson(text);
        if (string.IsNullOrWhiteSpace(query) || string.IsNullOrWhiteSpace(jsonBlock))
            return text.Trim();

        var parameters = ParseParameterMap(jsonBlock);
        if (parameters.Count == 0)
            return query;

        // Replace tokens longest-name-first so __p10 is matched before __p1
        var sorted = parameters
            .OrderByDescending(kv => kv.Key.Length)
            .ThenByDescending(kv => kv.Key);

        var resolved = query;
        foreach (var kv in sorted)
        {
            resolved = resolved.Replace(kv.Key, kv.Value);
        }

        return resolved;
    }

    /// <summary>
    /// Splits the input into the query line(s) and the JSON parameter block.
    /// The JSON block starts at the first '{' that appears on its own line (or after the query).
    /// </summary>
    private static (string Query, string JsonBlock) SplitQueryAndJson(string text)
    {
        // Find the first '{' that starts a JSON block (typically on a new line after the query)
        int braceIndex = -1;
        int depth = 0;
        bool inString = false;
        char stringChar = '\0';

        // Scan for the opening brace that is NOT inside a Gremlin string literal on the query line.
        // The query itself may contain braces inside string literals, so we track quotes.
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];

            if (inString)
            {
                if (c == stringChar && (i == 0 || text[i - 1] != '\\'))
                    inString = false;
                continue;
            }

            if (c == '\'' || c == '"')
            {
                // Only track quotes that appear before any unquoted newline (i.e. on the query line)
                if (braceIndex == -1)
                {
                    inString = true;
                    stringChar = c;
                }
                continue;
            }

            // After a newline, look for the JSON opening brace
            if (c == '\n' || c == '\r')
            {
                // Skip past whitespace/newlines to see if a '{' follows
                int j = i + 1;
                while (j < text.Length && (text[j] == ' ' || text[j] == '\t' || text[j] == '\r' || text[j] == '\n'))
                    j++;

                if (j < text.Length && text[j] == '{')
                {
                    braceIndex = j;
                    break;
                }
            }
        }

        if (braceIndex == -1)
            return (text.Trim(), string.Empty);

        var query = text[..braceIndex].Trim();
        var jsonBlock = text[braceIndex..].Trim();

        return (query, jsonBlock);
    }

    /// <summary>
    /// Parses the JSON parameter map and returns a dictionary mapping token names
    /// to their Gremlin-literal representations (strings are single-quoted).
    /// </summary>
    private static Dictionary<string, string> ParseParameterMap(string json)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                return result;

            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                result[prop.Name] = FormatGremlinLiteral(prop.Value);
            }
        }
        catch (JsonException)
        {
            // Malformed JSON – return empty so caller falls back to the raw text
        }

        return result;
    }

    /// <summary>
    /// Converts a JSON value to its Gremlin query literal representation.
    /// Strings are single-quoted; numbers, booleans, and nulls are bare literals.
    /// </summary>
    private static string FormatGremlinLiteral(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => $"'{EscapeGremlinString(element.GetString() ?? string.Empty)}'",
            JsonValueKind.Number => element.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Null => "null",
            _ => element.GetRawText()
        };
    }

    private static string EscapeGremlinString(string value)
    {
        return value.Replace("\\", "\\\\").Replace("'", "\\'");
    }
}
