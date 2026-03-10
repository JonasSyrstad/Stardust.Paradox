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

        // Validate step arguments against known definitions
        CheckStepArguments(query, errors);

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

    private static void CheckStepArguments(string query, List<GremlinValidationError> errors)
    {
        // Match step invocations: .stepName(args)
        var stepPattern = new Regex(@"\.([a-zA-Z_][a-zA-Z0-9_]*)\s*\(", RegexOptions.Compiled);

        foreach (Match match in stepPattern.Matches(query))
        {
            var stepName = match.Groups[1].Value;
            var def = GremlinStepDatabase.Get(stepName);
            if (def == null)
                continue;

            var parenOpenIndex = match.Index + match.Length - 1;
            var argsEnd = FindMatchingParen(query, parenOpenIndex);
            if (argsEnd < 0)
                continue; // Unbalanced — already reported

            var argsText = query.Substring(parenOpenIndex + 1, argsEnd - parenOpenIndex - 1).Trim();

            // Parse top-level arguments (respecting nesting and strings)
            var args = ParseTopLevelArguments(argsText, parenOpenIndex + 1);
            var argCount = args.Count;

            // Skip validation for empty args if any overload accepts zero params
            if (argCount == 0 && def.Overloads.Any(o => o.Parameters.Length == 0))
                continue;

            // Check if a step with required params got zero args
            if (argCount == 0 && def.HasRequiredParameters && !def.Overloads.Any(o => o.Parameters.Length == 0))
            {
                var (line, col) = GetLineAndColumn(query, parenOpenIndex);
                errors.Add(new GremlinValidationError(
                    parenOpenIndex, argsEnd + 1, line, col,
                    $"'{stepName}()' requires parameters: {def.Overloads[0].Signature}",
                    GremlinErrorSeverity.Warning));
                continue;
            }

            // Validate argument types against the best-matching overload
            if (argCount > 0)
            {
                ValidateArgumentTypes(query, stepName, def, args, parenOpenIndex, errors);
            }
        }
    }

    private static void ValidateArgumentTypes(
        string query,
        string stepName,
        GremlinStepDefinition def,
        List<ArgumentToken> args,
        int stepOffset,
        List<GremlinValidationError> errors)
    {
        // Find the overload whose required param count is closest to the provided arg count
        var matchingOverloads = def.Overloads
            .Where(o =>
            {
                var required = o.Parameters.Count(p => !p.IsOptional);
                var total = o.Parameters.Length;
                return args.Count >= required && args.Count <= total;
            })
            .ToList();

        if (matchingOverloads.Count == 0)
        {
            // No overload matches the argument count
            var validCounts = string.Join(", ", def.Overloads
                .Select(o =>
                {
                    var req = o.Parameters.Count(p => !p.IsOptional);
                    var tot = o.Parameters.Length;
                    return req == tot ? req.ToString() : $"{req}-{tot}";
                })
                .Distinct());
            var (line, col) = GetLineAndColumn(query, stepOffset);
            errors.Add(new GremlinValidationError(
                stepOffset, stepOffset + stepName.Length + 2, line, col,
                $"'{stepName}()' expects {validCounts} argument(s), but got {args.Count}",
                GremlinErrorSeverity.Warning));
            return;
        }

        // Use the first matching overload for type validation
        var bestOverload = matchingOverloads[0];

        for (int i = 0; i < Math.Min(args.Count, bestOverload.Parameters.Length); i++)
        {
            var expectedType = bestOverload.Parameters[i].Type;
            var argValue = args[i].Text.Trim();
            var argAbsStart = args[i].StartOffset;

            var typeError = ValidateSingleArgument(argValue, expectedType);
            if (typeError != null)
            {
                var (line, col) = GetLineAndColumn(query, argAbsStart);
                errors.Add(new GremlinValidationError(
                    argAbsStart, argAbsStart + argValue.Length, line, col,
                    $"Parameter '{bestOverload.Parameters[i].Name}': {typeError}",
                    GremlinErrorSeverity.Warning));
            }
        }
    }

    private static string? ValidateSingleArgument(string argValue, GremlinParamType expectedType)
    {
        if (string.IsNullOrWhiteSpace(argValue))
            return null;

        // Skip validation for sub-traversals (contain dots or parens)
        if (argValue.Contains('.') || argValue.Contains('('))
            return null;

        return expectedType switch
        {
            GremlinParamType.String or GremlinParamType.Label or GremlinParamType.PropertyKey =>
                IsStringLiteral(argValue) ? null : $"expected a quoted string, got '{Truncate(argValue, 20)}'",

            GremlinParamType.Number =>
                IsNumericLiteral(argValue) ? null : $"expected a number, got '{Truncate(argValue, 20)}'",

            GremlinParamType.Boolean =>
                argValue is "true" or "false" ? null : $"expected true/false, got '{Truncate(argValue, 20)}'",

            _ => null // Any, Value, Traversal, Predicate, Enum — too permissive to validate statically
        };
    }

    private static bool IsStringLiteral(string value)
    {
        return (value.StartsWith('\'') && value.EndsWith('\'') && value.Length >= 2) ||
               (value.StartsWith('"') && value.EndsWith('"') && value.Length >= 2);
    }

    private static bool IsNumericLiteral(string value)
    {
        // Accept integers, decimals, and negative numbers
        return double.TryParse(value, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out _);
    }

    private static string Truncate(string value, int maxLen)
    {
        return value.Length <= maxLen ? value : value[..maxLen] + "…";
    }

    /// <summary>
    /// Finds the matching closing parenthesis, respecting nesting and strings.
    /// Returns -1 if not found.
    /// </summary>
    private static int FindMatchingParen(string text, int openIndex)
    {
        int depth = 0;
        bool inString = false;
        char stringChar = '\0';

        for (int i = openIndex; i < text.Length; i++)
        {
            var c = text[i];
            if (!inString && (c == '\'' || c == '"'))
            {
                inString = true;
                stringChar = c;
            }
            else if (inString && c == stringChar && (i == 0 || text[i - 1] != '\\'))
            {
                inString = false;
            }
            else if (!inString)
            {
                if (c == '(') depth++;
                else if (c == ')')
                {
                    depth--;
                    if (depth == 0) return i;
                }
            }
        }

        return -1;
    }

    /// <summary>
    /// Parses top-level comma-separated arguments, respecting nested parens and strings.
    /// Returns argument values with their absolute offsets in the outer query.
    /// </summary>
    private static List<ArgumentToken> ParseTopLevelArguments(string argsText, int baseOffset)
    {
        var result = new List<ArgumentToken>();
        if (string.IsNullOrWhiteSpace(argsText))
            return result;

        int depth = 0;
        bool inString = false;
        char stringChar = '\0';
        int argStart = 0;

        for (int i = 0; i < argsText.Length; i++)
        {
            var c = argsText[i];
            if (!inString && (c == '\'' || c == '"'))
            {
                inString = true;
                stringChar = c;
            }
            else if (inString && c == stringChar && (i == 0 || argsText[i - 1] != '\\'))
            {
                inString = false;
            }
            else if (!inString)
            {
                if (c == '(' || c == '[') depth++;
                else if (c == ')' || c == ']') depth--;
                else if (c == ',' && depth == 0)
                {
                    result.Add(new ArgumentToken(argsText[argStart..i].Trim(), baseOffset + argStart));
                    argStart = i + 1;
                }
            }
        }

        var last = argsText[argStart..].Trim();
        if (last.Length > 0)
        {
            result.Add(new ArgumentToken(last, baseOffset + argStart));
        }

        return result;
    }

    private record ArgumentToken(string Text, int StartOffset);

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
