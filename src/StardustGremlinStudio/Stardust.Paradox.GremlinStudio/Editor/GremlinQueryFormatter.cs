using System.Text;

namespace Stardust.Paradox.GremlinStudio.Editor;

/// <summary>
/// Formats Gremlin queries with proper indentation and line breaks.
/// </summary>
public static class GremlinQueryFormatter
{
    /// <summary>
    /// Formats a Gremlin query with each step on its own line and proper indentation.
    /// </summary>
    public static string Format(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return query;

        // Normalize whitespace: collapse newlines, tabs, and multiple spaces
        var normalized = NormalizeWhitespace(query.Trim());

        var sb = new StringBuilder();
        int depth = 0;
        int i = 0;
        bool inString = false;
        char stringChar = '\0';
        bool isFirstStep = true;

        while (i < normalized.Length)
        {
            char c = normalized[i];

            // Handle string literals
            if (inString)
            {
                sb.Append(c);
                if (c == stringChar && (i == 0 || normalized[i - 1] != '\\'))
                {
                    inString = false;
                }
                i++;
                continue;
            }

            if (c == '\'' || c == '"')
            {
                inString = true;
                stringChar = c;
                sb.Append(c);
                i++;
                continue;
            }

            // Detect dot-chained step boundaries (e.g., ".V(", ".has(")
            if (c == '.' && i + 1 < normalized.Length && char.IsLetter(normalized[i + 1]))
            {
                // Check if this is the first dot (after "g")
                if (isFirstStep)
                {
                    isFirstStep = false;
                    sb.Append('.');
                    i++;
                    continue;
                }

                sb.AppendLine();
                sb.Append(new string(' ', (depth + 1) * 2));
                sb.Append('.');
                i++;
                continue;
            }

            if (c == '(')
            {
                depth++;
                sb.Append(c);
                i++;
                continue;
            }

            if (c == ')')
            {
                depth = Math.Max(0, depth - 1);
                sb.Append(c);
                i++;
                continue;
            }

            sb.Append(c);
            i++;
        }

        return sb.ToString();
    }

    private static string NormalizeWhitespace(string input)
    {
        var sb = new StringBuilder(input.Length);
        bool lastWasSpace = false;

        foreach (char c in input)
        {
            if (char.IsWhiteSpace(c))
            {
                if (!lastWasSpace)
                {
                    sb.Append(' ');
                    lastWasSpace = true;
                }
            }
            else
            {
                sb.Append(c);
                lastWasSpace = false;
            }
        }

        return sb.ToString();
    }
}
