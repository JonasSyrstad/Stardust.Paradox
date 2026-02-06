using System.Text.RegularExpressions;

namespace Stardust.Paradox.GremlinStudio.Editor;

/// <summary>
/// Represents a validation error in a Gremlin query.
/// </summary>
public record GremlinValidationError(
    int StartOffset,
    int EndOffset,
    int Line,
    int Column,
    string Message,
    GremlinErrorSeverity Severity);

/// <summary>
/// Severity of validation errors.
/// </summary>
public enum GremlinErrorSeverity
{
    Error,
    Warning,
    Info
}

/// <summary>
/// Validates Gremlin queries for syntax and semantic errors.
/// </summary>
public static class GremlinValidator
{
    private static readonly HashSet<string> ValidSteps = new(StringComparer.OrdinalIgnoreCase)
    {
        // Vertex/Edge
        "V", "E", "addV", "addE", "drop",
        // Navigation
        "out", "in", "both", "outE", "inE", "bothE", "outV", "inV", "bothV", "otherV",
        // Filter
        "has", "hasNot", "hasLabel", "hasId", "hasKey", "hasValue", "is", "where", "not", "and", "or",
        "filter", "dedup", "limit", "skip", "range", "tail", "coin", "sample",
        // Map
        "map", "flatMap", "select", "project", "unfold", "fold", "count", "sum", "max", "min", "mean",
        "group", "groupCount", "order", "path", "tree", "match", "math", "local", "optional",
        "union", "coalesce", "choose", "repeat", "until", "emit", "times", "loops",
        // Property
        "property", "properties", "values", "valueMap", "elementMap", "id", "label", "constant", "inject",
        // Side effect
        "sideEffect", "cap", "store", "aggregate", "subgraph",
        // Terminal
        "toList", "toSet", "next", "iterate", "explain", "profile",
        // Modulators
        "as", "by", "from", "to", "with", "option"
    };

    private static readonly HashSet<string> ValidPredicates = new(StringComparer.OrdinalIgnoreCase)
    {
        "eq", "neq", "lt", "lte", "gt", "gte", "inside", "outside", "between",
        "within", "without", "containing", "startingWith", "endingWith",
        "notContaining", "notStartingWith", "notEndingWith", "regex"
    };

    /// <summary>
    /// Validates a Gremlin query and returns any errors.
    /// </summary>
    public static List<GremlinValidationError> Validate(string query)
    {
        var errors = new List<GremlinValidationError>();

        if (string.IsNullOrWhiteSpace(query))
        {
            return errors;
        }

        // Check for basic structure
        var trimmed = query.Trim();

        // Should start with 'g.'
        if (!trimmed.StartsWith("g.", StringComparison.OrdinalIgnoreCase) &&
            !trimmed.StartsWith("g\n", StringComparison.OrdinalIgnoreCase) &&
            !trimmed.StartsWith("g\r", StringComparison.OrdinalIgnoreCase))
        {
            if (!trimmed.Equals("g", StringComparison.OrdinalIgnoreCase))
            {
                errors.Add(new GremlinValidationError(
                    0, Math.Min(2, query.Length), 1, 1,
                    "Gremlin query should start with 'g.'",
                    GremlinErrorSeverity.Error));
            }
        }

        // Check parentheses balance
        var parenBalance = CheckParenthesesBalance(query, errors);

        // Check for unclosed strings
        CheckUnclosedStrings(query, errors);

        // Check for unknown steps
        CheckUnknownSteps(query, errors);

        // Check for common mistakes
        CheckCommonMistakes(query, errors);

        return errors;
    }

    private static int CheckParenthesesBalance(string query, List<GremlinValidationError> errors)
    {
        int balance = 0;
        int lastOpenParen = -1;
        bool inString = false;
        char stringChar = '\0';

        for (int i = 0; i < query.Length; i++)
        {
            var c = query[i];

            // Handle strings
            if (!inString && (c == '"' || c == '\''))
            {
                inString = true;
                stringChar = c;
            }
            else if (inString && c == stringChar && (i == 0 || query[i - 1] != '\\'))
            {
                inString = false;
            }

            if (!inString)
            {
                if (c == '(')
                {
                    balance++;
                    lastOpenParen = i;
                }
                else if (c == ')')
                {
                    balance--;
                    if (balance < 0)
                    {
                        var (line, col) = GetLineAndColumn(query, i);
                        errors.Add(new GremlinValidationError(
                            i, i + 1, line, col,
                            "Unexpected closing parenthesis",
                            GremlinErrorSeverity.Error));
                        balance = 0;
                    }
                }
            }
        }

        if (balance > 0)
        {
            var (line, col) = GetLineAndColumn(query, lastOpenParen);
            errors.Add(new GremlinValidationError(
                lastOpenParen, lastOpenParen + 1, line, col,
                $"Unclosed parenthesis ({balance} remaining)",
                GremlinErrorSeverity.Error));
        }

        return balance;
    }

    private static void CheckUnclosedStrings(string query, List<GremlinValidationError> errors)
    {
        bool inSingleQuote = false;
        bool inDoubleQuote = false;
        int stringStart = -1;

        for (int i = 0; i < query.Length; i++)
        {
            var c = query[i];

            if (c == '\'' && !inDoubleQuote && (i == 0 || query[i - 1] != '\\'))
            {
                if (!inSingleQuote)
                {
                    inSingleQuote = true;
                    stringStart = i;
                }
                else
                {
                    inSingleQuote = false;
                }
            }
            else if (c == '"' && !inSingleQuote && (i == 0 || query[i - 1] != '\\'))
            {
                if (!inDoubleQuote)
                {
                    inDoubleQuote = true;
                    stringStart = i;
                }
                else
                {
                    inDoubleQuote = false;
                }
            }
        }

        if (inSingleQuote || inDoubleQuote)
        {
            var (line, col) = GetLineAndColumn(query, stringStart);
            errors.Add(new GremlinValidationError(
                stringStart, query.Length, line, col,
                "Unclosed string literal",
                GremlinErrorSeverity.Error));
        }
    }

    private static void CheckUnknownSteps(string query, List<GremlinValidationError> errors)
    {
        // Match step patterns like .stepName( or .stepName()
        var stepPattern = new Regex(@"\.([a-zA-Z_][a-zA-Z0-9_]*)\s*\(", RegexOptions.Compiled);
        var matches = stepPattern.Matches(query);

        foreach (Match match in matches)
        {
            var stepName = match.Groups[1].Value;
            if (!ValidSteps.Contains(stepName) && !ValidPredicates.Contains(stepName))
            {
                var (line, col) = GetLineAndColumn(query, match.Groups[1].Index);
                errors.Add(new GremlinValidationError(
                    match.Groups[1].Index,
                    match.Groups[1].Index + stepName.Length,
                    line, col,
                    $"Unknown step or predicate: '{stepName}'",
                    GremlinErrorSeverity.Warning));
            }
        }
    }

    private static void CheckCommonMistakes(string query, List<GremlinValidationError> errors)
    {
        // Check for .V without parentheses
        var vWithoutParens = new Regex(@"\.V(?!\s*\()", RegexOptions.Compiled);
        foreach (Match match in vWithoutParens.Matches(query))
        {
            var (line, col) = GetLineAndColumn(query, match.Index);
            errors.Add(new GremlinValidationError(
                match.Index, match.Index + match.Length, line, col,
                "V() requires parentheses",
                GremlinErrorSeverity.Error));
        }

        // Check for .E without parentheses
        var eWithoutParens = new Regex(@"\.E(?!\s*\()", RegexOptions.Compiled);
        foreach (Match match in eWithoutParens.Matches(query))
        {
            var (line, col) = GetLineAndColumn(query, match.Index);
            errors.Add(new GremlinValidationError(
                match.Index, match.Index + match.Length, line, col,
                "E() requires parentheses",
                GremlinErrorSeverity.Error));
        }

        // Check for double dots
        var doubleDots = query.IndexOf("..", StringComparison.Ordinal);
        if (doubleDots >= 0)
        {
            var (line, col) = GetLineAndColumn(query, doubleDots);
            errors.Add(new GremlinValidationError(
                doubleDots, doubleDots + 2, line, col,
                "Double dots found - possible typo",
                GremlinErrorSeverity.Error));
        }

        // Check for trailing dot
        var trimmed = query.TrimEnd();
        if (trimmed.EndsWith('.'))
        {
            var dotIndex = query.LastIndexOf('.');
            var (line, col) = GetLineAndColumn(query, dotIndex);
            errors.Add(new GremlinValidationError(
                dotIndex, dotIndex + 1, line, col,
                "Query ends with a dot - incomplete step",
                GremlinErrorSeverity.Warning));
        }
    }

    private static (int line, int column) GetLineAndColumn(string text, int offset)
    {
        int line = 1;
        int column = 1;

        for (int i = 0; i < offset && i < text.Length; i++)
        {
            if (text[i] == '\n')
            {
                line++;
                column = 1;
            }
            else
            {
                column++;
            }
        }

        return (line, column);
    }
}
