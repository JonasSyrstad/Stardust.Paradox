using System.Text;

namespace Stardust.Paradox.GremlinStudio.Editor;

/// <summary>
/// Represents a single parsed step from a Gremlin query.
/// </summary>
public record ParsedGremlinStep(
    string Name,
    string RawText,
    string Arguments,
    GremlinStepDefinition? Definition);

/// <summary>
/// Generates detailed, human-readable explanations of Gremlin queries
/// by parsing them into individual steps and describing each one using
/// the <see cref="GremlinStepDatabase"/>.
/// </summary>
public static class GremlinQueryExplainer
{
    /// <summary>
    /// Generates a detailed explanation of a Gremlin query.
    /// </summary>
    public static string Explain(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return "No query to explain.";

        var normalized = NormalizeWhitespace(query.Trim());
        var steps = ParseSteps(normalized);

        if (steps.Count == 0)
            return "Could not parse any steps from the query.";

        var sb = new StringBuilder();

        // Header
        sb.AppendLine("================================================================");
        sb.AppendLine("  GREMLIN QUERY EXPLANATION");
        sb.AppendLine("================================================================");
        sb.AppendLine();

        // Original query
        sb.AppendLine("  Query:");
        sb.AppendLine($"  {normalized}");
        sb.AppendLine();

        // Summary
        sb.AppendLine("----------------------------------------------------------------");
        sb.AppendLine("  SUMMARY");
        sb.AppendLine("----------------------------------------------------------------");
        sb.AppendLine();
        sb.AppendLine(GenerateSummary(steps));
        sb.AppendLine();

        // Step-by-step breakdown
        sb.AppendLine("----------------------------------------------------------------");
        sb.AppendLine("  STEP-BY-STEP BREAKDOWN");
        sb.AppendLine("----------------------------------------------------------------");
        sb.AppendLine();

        for (int i = 0; i < steps.Count; i++)
        {
            var step = steps[i];
            AppendStepExplanation(sb, step, i + 1, steps);
        }

        // Traversal flow analysis
        sb.AppendLine("----------------------------------------------------------------");
        sb.AppendLine("  TRAVERSAL FLOW");
        sb.AppendLine("----------------------------------------------------------------");
        sb.AppendLine();
        AppendFlowAnalysis(sb, steps);

        // Performance tips
        var tips = GeneratePerformanceTips(steps);
        if (tips.Count > 0)
        {
            sb.AppendLine("----------------------------------------------------------------");
            sb.AppendLine("  TIPS & OBSERVATIONS");
            sb.AppendLine("----------------------------------------------------------------");
            sb.AppendLine();
            foreach (var tip in tips)
            {
                sb.AppendLine($"  [TIP] {tip}");
            }
            sb.AppendLine();
        }

        sb.AppendLine("================================================================");

        return sb.ToString();
    }

    /// <summary>
    /// Parses a Gremlin query string into a list of individual steps.
    /// </summary>
    internal static List<ParsedGremlinStep> ParseSteps(string query)
    {
        var steps = new List<ParsedGremlinStep>();
        int i = 0;
        bool inString = false;
        char stringChar = '\0';

        while (i < query.Length)
        {
            // Skip whitespace
            while (i < query.Length && char.IsWhiteSpace(query[i]))
                i++;

            if (i >= query.Length)
                break;

            // At top level, expect either a step name or a dot followed by a step name
            if (query[i] == '.')
            {
                i++; // skip dot
                continue;
            }

            // Read step name
            int nameStart = i;
            while (i < query.Length && (char.IsLetterOrDigit(query[i]) || query[i] == '_'))
                i++;

            if (i == nameStart)
            {
                i++; // skip unrecognized character
                continue;
            }

            var name = query[nameStart..i];

            // Skip whitespace
            while (i < query.Length && char.IsWhiteSpace(query[i]))
                i++;

            // Read arguments if there's an opening paren
            string arguments = string.Empty;
            string rawText = name;

            if (i < query.Length && query[i] == '(')
            {
                int parenStart = i;
                int depth = 0;
                inString = false;
                stringChar = '\0';

                while (i < query.Length)
                {
                    char c = query[i];

                    if (inString)
                    {
                        if (c == stringChar && (i == 0 || query[i - 1] != '\\'))
                            inString = false;
                        i++;
                        continue;
                    }

                    if (c == '\'' || c == '"')
                    {
                        inString = true;
                        stringChar = c;
                        i++;
                        continue;
                    }

                    if (c == '(') depth++;
                    if (c == ')') depth--;

                    i++;

                    if (depth == 0)
                        break;
                }

                rawText = query[nameStart..i];
                // Extract inner arguments (strip outer parens)
                if (parenStart + 1 < i - 1)
                    arguments = query[(parenStart + 1)..(i - 1)].Trim();
            }

            GremlinStepDatabase.TryGet(name, out var definition);
            steps.Add(new ParsedGremlinStep(name, rawText.Trim(), arguments, definition));
        }

        return steps;
    }

    private static string GenerateSummary(List<ParsedGremlinStep> steps)
    {
        var sb = new StringBuilder();
        var categories = CategorizeSteps(steps);

        sb.Append($"  This query has {steps.Count} step(s)");

        var parts = new List<string>();
        if (categories.StartSteps > 0) parts.Add($"{categories.StartSteps} start");
        if (categories.FilterSteps > 0) parts.Add($"{categories.FilterSteps} filter");
        if (categories.NavigationSteps > 0) parts.Add($"{categories.NavigationSteps} navigation");
        if (categories.MapSteps > 0) parts.Add($"{categories.MapSteps} transform");
        if (categories.MutationSteps > 0) parts.Add($"{categories.MutationSteps} mutation");
        if (categories.ModulatorSteps > 0) parts.Add($"{categories.ModulatorSteps} modulator");

        if (parts.Count > 0)
        {
            sb.Append($" ({string.Join(", ", parts)})");
        }

        sb.AppendLine(".");

        // Detect overall query intent
        var intent = DetectQueryIntent(steps);
        if (!string.IsNullOrEmpty(intent))
        {
            sb.AppendLine($"  Intent: {intent}");
        }

        return sb.ToString();
    }

    private static void AppendStepExplanation(StringBuilder sb, ParsedGremlinStep step, int number, List<ParsedGremlinStep> allSteps)
    {
        var category = CategorizeStep(step.Name);
        sb.AppendLine($"  Step {number}: {step.RawText}");
        sb.AppendLine($"  |-- Category: {category}");

        if (step.Definition != null)
        {
            sb.AppendLine($"  |-- Description: {step.Definition.Description}");

            // Try to match the best overload based on argument count
            var bestOverload = MatchOverload(step);
            if (bestOverload?.Description != null)
            {
                sb.AppendLine($"  |-- Usage: {bestOverload.Description}");
            }

            if (!string.IsNullOrEmpty(step.Arguments))
            {
                sb.AppendLine($"  |-- Arguments: {step.Arguments}");
                AppendArgumentDetails(sb, step);
            }
        }
        else
        {
            sb.AppendLine($"  |-- Description: Unknown step '{step.Name}'");
            if (!string.IsNullOrEmpty(step.Arguments))
            {
                sb.AppendLine($"  |-- Arguments: {step.Arguments}");
            }
        }

        // Contextual explanation
        var contextHint = GetContextualHint(step, number, allSteps);
        if (!string.IsNullOrEmpty(contextHint))
        {
            sb.AppendLine($"  +-- Note: {contextHint}");
        }
        else
        {
            sb.AppendLine($"  +--");
        }

        sb.AppendLine();
    }

    private static void AppendArgumentDetails(StringBuilder sb, ParsedGremlinStep step)
    {
        if (step.Definition == null || string.IsNullOrEmpty(step.Arguments))
            return;

        var bestOverload = MatchOverload(step);
        if (bestOverload == null)
            return;

        // Split top-level arguments (respecting strings and nested parens)
        var args = SplitArguments(step.Arguments);
        for (int i = 0; i < args.Count && i < bestOverload.Parameters.Length; i++)
        {
            var param = bestOverload.Parameters[i];
            var argValue = args[i].Trim();
            var desc = param.Description ?? param.TypeName;
            sb.AppendLine($"  |    {param.Name} = {argValue} ({desc})");
        }
    }

    private static void AppendFlowAnalysis(StringBuilder sb, List<ParsedGremlinStep> steps)
    {
        var flowParts = new List<string>();

        foreach (var step in steps)
        {
            var desc = GetFlowDescription(step);
            if (!string.IsNullOrEmpty(desc))
                flowParts.Add(desc);
        }

        if (flowParts.Count == 0)
        {
            sb.AppendLine("  Unable to determine traversal flow.");
            sb.AppendLine();
            return;
        }

        for (int i = 0; i < flowParts.Count; i++)
        {
            sb.Append($"  {flowParts[i]}");
            if (i < flowParts.Count - 1)
            {
                sb.AppendLine();
                sb.AppendLine("    |");
            }
        }

        sb.AppendLine();
        sb.AppendLine();
    }

    private static List<string> GeneratePerformanceTips(List<ParsedGremlinStep> steps)
    {
        var tips = new List<string>();

        bool hasHasLabel = steps.Any(s => s.Name == "hasLabel");
        bool hasHas = steps.Any(s => s.Name == "has");
        bool hasLimit = steps.Any(s => s.Name is "limit" or "range" or "tail");
        bool startWithV = steps.Any(s => s.Name == "V" && string.IsNullOrEmpty(s.Arguments));
        bool startWithE = steps.Any(s => s.Name == "E" && string.IsNullOrEmpty(s.Arguments));
        bool hasDrop = steps.Any(s => s.Name == "drop");
        bool hasRepeat = steps.Any(s => s.Name == "repeat");
        bool hasUntil = steps.Any(s => s.Name == "until");
        bool hasTimes = steps.Any(s => s.Name == "times");
        bool hasDedup = steps.Any(s => s.Name == "dedup");
        bool hasCount = steps.Any(s => s.Name == "count");

        // Full scan warning
        if (startWithV && !hasHasLabel && !hasHas)
        {
            tips.Add("g.V() without filters scans ALL vertices. Add hasLabel() or has() early to reduce scope.");
        }

        if (startWithE && !hasHasLabel && !hasHas)
        {
            tips.Add("g.E() without filters scans ALL edges. Add hasLabel() or has() early to reduce scope.");
        }

        // No limit warning
        if ((startWithV || startWithE) && !hasLimit && !hasCount)
        {
            tips.Add("Consider adding .limit(N) to prevent returning very large result sets.");
        }

        // Drop without filter warning
        if (hasDrop && startWithV && !hasHas && !hasHasLabel)
        {
            tips.Add("[!] g.V().drop() will DELETE ALL vertices! Make sure this is intentional.");
        }

        if (hasDrop && startWithE && !hasHas && !hasHasLabel)
        {
            tips.Add("[!] g.E().drop() will DELETE ALL edges! Make sure this is intentional.");
        }

        // Repeat without termination
        if (hasRepeat && !hasUntil && !hasTimes)
        {
            tips.Add("repeat() without until() or times() may cause infinite loops.");
        }

        // Dedup after large traversal
        if (hasDedup && !hasLimit)
        {
            tips.Add("dedup() on large result sets can be expensive. Consider filtering or limiting first.");
        }

        // Filter ordering
        var firstNavIndex = steps.FindIndex(s => IsNavigationStep(s.Name));
        var firstFilterIndex = steps.FindIndex(s => IsFilterStep(s.Name));
        if (firstNavIndex >= 0 && firstFilterIndex > firstNavIndex &&
            !IsStartStep(steps.ElementAtOrDefault(firstFilterIndex - 1)?.Name ?? ""))
        {
            tips.Add("Filtering after navigation may be less efficient. Consider filtering before traversing edges when possible.");
        }

        return tips;
    }

    private static GremlinStepOverload? MatchOverload(ParsedGremlinStep step)
    {
        if (step.Definition == null)
            return null;

        if (string.IsNullOrEmpty(step.Arguments))
        {
            // Return the parameterless overload or first one
            return step.Definition.Overloads.FirstOrDefault(o => o.Parameters.Length == 0)
                ?? step.Definition.Overloads.FirstOrDefault();
        }

        var argCount = SplitArguments(step.Arguments).Count;

        // Try to match by argument count (prefer exact match, then closest with optionals)
        var exact = step.Definition.Overloads.FirstOrDefault(o =>
            o.Parameters.Length == argCount ||
            (o.Parameters.Count(p => !p.IsOptional) <= argCount && o.Parameters.Length >= argCount));

        return exact ?? step.Definition.Overloads.LastOrDefault();
    }

    /// <summary>
    /// Splits comma-separated arguments at the top level, respecting nested parentheses and strings.
    /// </summary>
    internal static List<string> SplitArguments(string args)
    {
        var result = new List<string>();
        int depth = 0;
        bool inString = false;
        char stringChar = '\0';
        int start = 0;

        for (int i = 0; i < args.Length; i++)
        {
            char c = args[i];

            if (inString)
            {
                if (c == stringChar && (i == 0 || args[i - 1] != '\\'))
                    inString = false;
                continue;
            }

            if (c == '\'' || c == '"')
            {
                inString = true;
                stringChar = c;
                continue;
            }

            if (c == '(' || c == '[') depth++;
            if (c == ')' || c == ']') depth--;

            if (c == ',' && depth == 0)
            {
                result.Add(args[start..i]);
                start = i + 1;
            }
        }

        if (start < args.Length)
            result.Add(args[start..]);

        return result;
    }

    private static string GetFlowDescription(ParsedGremlinStep step)
    {
        return step.Name switch
        {
            "g" => "Start graph traversal",
            "V" when string.IsNullOrEmpty(step.Arguments) => "Select ALL vertices",
            "V" => $"Select vertex(es) with id: {step.Arguments}",
            "E" when string.IsNullOrEmpty(step.Arguments) => "Select ALL edges",
            "E" => $"Select edge(s) with id: {step.Arguments}",
            "addV" => $"Create new vertex{(string.IsNullOrEmpty(step.Arguments) ? "" : $" with label {step.Arguments}")}",
            "addE" => $"Create new edge with label {step.Arguments}",
            "out" => $"Traverse outgoing edges{(string.IsNullOrEmpty(step.Arguments) ? "" : $" labeled {step.Arguments}")} -> adjacent vertices",
            "in" => $"Traverse incoming edges{(string.IsNullOrEmpty(step.Arguments) ? "" : $" labeled {step.Arguments}")} -> source vertices",
            "both" => $"Traverse edges in both directions{(string.IsNullOrEmpty(step.Arguments) ? "" : $" labeled {step.Arguments}")}",
            "outE" => $"Get outgoing edge objects{(string.IsNullOrEmpty(step.Arguments) ? "" : $" labeled {step.Arguments}")}",
            "inE" => $"Get incoming edge objects{(string.IsNullOrEmpty(step.Arguments) ? "" : $" labeled {step.Arguments}")}",
            "bothE" => $"Get edge objects in both directions{(string.IsNullOrEmpty(step.Arguments) ? "" : $" labeled {step.Arguments}")}",
            "outV" => "Get source vertex of edge",
            "inV" => "Get target vertex of edge",
            "bothV" => "Get both vertices of edge",
            "otherV" => "Get the other vertex of edge",
            "has" => $"Filter: keep elements where {step.Arguments}",
            "hasLabel" => $"Filter: keep elements with label {step.Arguments}",
            "hasId" => $"Filter: keep elements with id {step.Arguments}",
            "hasNot" => $"Filter: keep elements WITHOUT property {step.Arguments}",
            "where" => $"Filter: apply condition {step.Arguments}",
            "is" => $"Filter: current value matches {step.Arguments}",
            "not" => $"Filter: negate {step.Arguments}",
            "and" => "Filter: ALL conditions must match",
            "or" => "Filter: ANY condition must match",
            "dedup" => "Remove duplicate elements",
            "limit" => $"Take first {step.Arguments} result(s)",
            "skip" => $"Skip first {step.Arguments} result(s)",
            "range" => $"Take results in range {step.Arguments}",
            "tail" => $"Take last {(string.IsNullOrEmpty(step.Arguments) ? "1" : step.Arguments)} result(s)",
            "count" => "Count the elements",
            "sum" => "Sum the values",
            "max" => "Get maximum value",
            "min" => "Get minimum value",
            "mean" => "Calculate average",
            "values" => $"Extract property value(s){(string.IsNullOrEmpty(step.Arguments) ? "" : $": {step.Arguments}")}",
            "valueMap" => "Get properties as key-value map",
            "elementMap" => "Get element as map (id + label + properties)",
            "properties" => $"Get property objects{(string.IsNullOrEmpty(step.Arguments) ? "" : $": {step.Arguments}")}",
            "id" => "Get element id",
            "label" => "Get element label",
            "property" => $"Set property {step.Arguments}",
            "select" => $"Select labeled step value(s): {step.Arguments}",
            "project" => $"Project into map with keys: {step.Arguments}",
            "by" => $"Configure preceding step: {(string.IsNullOrEmpty(step.Arguments) ? "default" : step.Arguments)}",
            "as" => $"Label current step as '{step.Arguments}'",
            "from" => $"Set source: {step.Arguments}",
            "to" => $"Set target: {step.Arguments}",
            "fold" => "Collect all elements into a list",
            "unfold" => "Unroll collection into individual elements",
            "group" => "Group elements",
            "groupCount" => "Count elements per group",
            "order" => "Order elements",
            "path" => "Get the traversal path",
            "tree" => "Get the traversal tree",
            "repeat" => $"Loop: {step.Arguments}",
            "until" => $"Loop termination: {step.Arguments}",
            "emit" => "Emit elements during loop",
            "times" => $"Repeat {step.Arguments} time(s)",
            "union" => "Merge results from multiple traversals",
            "coalesce" => "Use first traversal that produces results",
            "choose" => "Conditional branching (if/then/else)",
            "optional" => "Optional traversal (fallback to current if empty)",
            "drop" => "DELETE current element(s)",
            "constant" => $"Return constant: {step.Arguments}",
            "inject" => $"Inject value(s): {step.Arguments}",
            "sample" => $"Random sample of {step.Arguments} element(s)",
            "coin" => $"Random filter with probability {step.Arguments}",
            _ => $"{step.Name}({step.Arguments})"
        };
    }

    private static string GetContextualHint(ParsedGremlinStep step, int number, List<ParsedGremlinStep> allSteps)
    {
        // Previous step context
        var prev = number >= 2 ? allSteps[number - 2] : null;

        return step.Name switch
        {
            "by" when prev?.Name == "order" => "Specifies the sort key and optional direction (asc/desc) for order().",
            "by" when prev?.Name == "group" => "First by() sets the grouping key, second by() sets the value aggregation.",
            "by" when prev?.Name == "groupCount" => "Specifies what to group by for counting.",
            "by" when prev?.Name == "project" => "Populates one of the projected keys with a traversal value.",
            "by" when prev?.Name == "path" => "Transforms each element in the path.",
            "from" when prev?.Name == "addE" || allSteps.Any(s => s.Name == "addE") => "Specifies the source vertex for the new edge.",
            "to" when prev?.Name == "addE" || allSteps.Any(s => s.Name == "addE") => "Specifies the target vertex for the new edge.",
            "until" when allSteps.Any(s => s.Name == "repeat") => "Stops the repeat() loop when this condition is met.",
            "emit" when allSteps.Any(s => s.Name == "repeat") => "Outputs traversers at each iteration of repeat(), not just the final one.",
            "times" when allSteps.Any(s => s.Name == "repeat") => "Limits the repeat() loop to a fixed number of iterations.",
            "as" => $"Labels this position as '{step.Arguments}' for later use with select(), where(), or match().",
            "drop" => "[!] This is a DESTRUCTIVE operation -- it permanently removes the matched elements.",
            "property" when allSteps.Any(s => s.Name == "addV") => "Sets a property on the newly created vertex.",
            "property" when allSteps.Any(s => s.Name == "addE") => "Sets a property on the newly created edge.",
            "valueMap" when step.Arguments.Contains("true") => "Including id and label tokens in the result map.",
            _ => string.Empty
        };
    }

    private static string DetectQueryIntent(List<ParsedGremlinStep> steps)
    {
        var names = steps.Select(s => s.Name).ToList();

        if (names.Contains("addV"))
            return "Create new vertex(es) in the graph.";
        if (names.Contains("addE"))
            return "Create new edge(s) in the graph.";
        if (names.Contains("drop") && names.Contains("V"))
            return "Delete vertex(es) from the graph.";
        if (names.Contains("drop") && names.Contains("E"))
            return "Delete edge(s) from the graph.";
        if (names.Contains("property") && !names.Contains("addV") && !names.Contains("addE"))
            return "Update properties on existing elements.";
        if (names.Contains("count"))
            return "Count graph elements.";
        if (names.Contains("groupCount"))
            return "Aggregate elements into groups with counts.";
        if (names.Contains("group"))
            return "Group elements by criteria.";
        if (names.Contains("path") || names.Contains("tree"))
            return "Explore traversal paths/tree structure.";
        if (names.Contains("repeat"))
            return "Recursive/iterative graph traversal.";
        if ((names.Contains("out") || names.Contains("in") || names.Contains("both")) &&
            (names.Contains("values") || names.Contains("valueMap") || names.Contains("elementMap")))
            return "Navigate relationships and retrieve property data.";
        if (names.Contains("out") || names.Contains("in") || names.Contains("both"))
            return "Navigate graph relationships.";
        if (names.Contains("V") && (names.Contains("values") || names.Contains("valueMap") || names.Contains("elementMap")))
            return "Query vertex properties.";
        if (names.Contains("V") || names.Contains("E"))
            return "Query graph elements.";

        return "General graph traversal.";
    }

    private static (int StartSteps, int FilterSteps, int NavigationSteps, int MapSteps, int MutationSteps, int ModulatorSteps) CategorizeSteps(List<ParsedGremlinStep> steps)
    {
        int start = 0, filter = 0, nav = 0, map = 0, mutation = 0, modulator = 0;
        foreach (var s in steps)
        {
            var cat = CategorizeStep(s.Name);
            switch (cat)
            {
                case "Start": start++; break;
                case "Filter": filter++; break;
                case "Navigation": nav++; break;
                case "Transform": map++; break;
                case "Mutation": mutation++; break;
                case "Modulator": modulator++; break;
            }
        }
        return (start, filter, nav, map, mutation, modulator);
    }

    private static string CategorizeStep(string name) => name switch
    {
        "g" or "__" => "Source",
        "V" or "E" => "Start",
        "addV" or "addE" or "drop" or "property" => "Mutation",
        "out" or "in" or "both" or "outE" or "inE" or "bothE" or "outV" or "inV" or "bothV" or "otherV" => "Navigation",
        "has" or "hasLabel" or "hasId" or "hasNot" or "hasKey" or "hasValue" or "where" or "is" or "not" or "and" or "or"
            or "filter" or "dedup" or "limit" or "skip" or "range" or "tail" or "coin" or "sample" => "Filter",
        "values" or "valueMap" or "elementMap" or "properties" or "id" or "label" or "count" or "sum" or "max" or "min"
            or "mean" or "map" or "flatMap" or "select" or "project" or "unfold" or "fold" or "group" or "groupCount"
            or "order" or "path" or "tree" or "match" or "math" or "local" or "constant" or "inject" => "Transform",
        "repeat" or "until" or "emit" or "times" or "loops" or "union" or "coalesce" or "choose" or "optional" or "option" => "Control Flow",
        "as" or "by" or "from" or "to" or "with" => "Modulator",
        "toList" or "toSet" or "next" or "iterate" or "explain" or "profile" => "Terminal",
        "sideEffect" or "aggregate" or "store" or "cap" or "subgraph" => "Side Effect",
        _ => "Unknown"
    };

    private static bool IsNavigationStep(string name) => name is "out" or "in" or "both" or "outE" or "inE" or "bothE";
    private static bool IsFilterStep(string name) => name is "has" or "hasLabel" or "hasId" or "hasNot" or "where" or "is" or "not";
    private static bool IsStartStep(string name) => name is "V" or "E";

    private static string NormalizeWhitespace(string input)
    {
        var sb = new StringBuilder(input.Length);
        bool lastWasSpace = false;
        bool inString = false;
        char stringChar = '\0';

        foreach (char c in input)
        {
            if (inString)
            {
                sb.Append(c);
                if (c == stringChar)
                    inString = false;
                lastWasSpace = false;
                continue;
            }

            if (c == '\'' || c == '"')
            {
                inString = true;
                stringChar = c;
                sb.Append(c);
                lastWasSpace = false;
                continue;
            }

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
