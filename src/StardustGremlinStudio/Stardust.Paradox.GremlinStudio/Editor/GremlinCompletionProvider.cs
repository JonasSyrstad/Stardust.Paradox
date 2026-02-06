using System.Windows.Media;
using ICSharpCode.AvalonEdit.CodeCompletion;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Editing;

namespace Stardust.Paradox.GremlinStudio.Editor;

/// <summary>
/// Provides code completion data for Gremlin queries.
/// </summary>
public class GremlinCompletionData : ICompletionData
{
    public GremlinCompletionData(string text, string description, GremlinCompletionKind kind)
    {
        Text = text;
        Description = description;
        Kind = kind;
    }

    public string Text { get; }
    public GremlinCompletionKind Kind { get; }
    public object Description { get; }
    public object Content => Text;
    public double Priority => (int)Kind;

    public ImageSource? Image => null;

    public void Complete(TextArea textArea, ISegment completionSegment, EventArgs insertionRequestEventArgs)
    {
        textArea.Document.Replace(completionSegment, Text);
    }
}

/// <summary>
/// Categories for Gremlin completion items.
/// </summary>
public enum GremlinCompletionKind
{
    TraversalSource = 0,
    Step = 1,
    Predicate = 2,
    Modulator = 3,
    Keyword = 4
}

/// <summary>
/// Provides Gremlin completion suggestions.
/// </summary>
public static class GremlinCompletionProvider
{
    private static readonly List<GremlinCompletionData> AllCompletions = CreateCompletions();

    /// <summary>
    /// Gets completion suggestions for the given context.
    /// </summary>
    public static IEnumerable<GremlinCompletionData> GetCompletions(string textBefore)
    {
        // Simple context detection - can be enhanced
        var lastDot = textBefore.LastIndexOf('.');
        var context = lastDot >= 0 ? textBefore[(lastDot + 1)..].Trim() : textBefore.Trim();

        if (string.IsNullOrEmpty(context))
        {
            // After a dot, show all steps
            return AllCompletions;
        }

        // Filter by prefix
        return AllCompletions
            .Where(c => c.Text.StartsWith(context, StringComparison.OrdinalIgnoreCase))
            .OrderBy(c => c.Priority)
            .ThenBy(c => c.Text);
    }

    private static List<GremlinCompletionData> CreateCompletions()
    {
        var list = new List<GremlinCompletionData>();

        // Traversal sources
        list.Add(new("g", "Graph traversal source", GremlinCompletionKind.TraversalSource));
        list.Add(new("__", "Anonymous traversal", GremlinCompletionKind.TraversalSource));

        // Vertex/Edge steps
        list.Add(new("V()", "Get vertices by id or all vertices", GremlinCompletionKind.Step));
        list.Add(new("E()", "Get edges by id or all edges", GremlinCompletionKind.Step));
        list.Add(new("addV()", "Add a new vertex with label", GremlinCompletionKind.Step));
        list.Add(new("addE()", "Add a new edge with label", GremlinCompletionKind.Step));

        // Navigation steps
        list.Add(new("out()", "Traverse outgoing edges", GremlinCompletionKind.Step));
        list.Add(new("in()", "Traverse incoming edges", GremlinCompletionKind.Step));
        list.Add(new("both()", "Traverse both directions", GremlinCompletionKind.Step));
        list.Add(new("outE()", "Get outgoing edge objects", GremlinCompletionKind.Step));
        list.Add(new("inE()", "Get incoming edge objects", GremlinCompletionKind.Step));
        list.Add(new("bothE()", "Get edges in both directions", GremlinCompletionKind.Step));
        list.Add(new("outV()", "Get outgoing vertex from edge", GremlinCompletionKind.Step));
        list.Add(new("inV()", "Get incoming vertex from edge", GremlinCompletionKind.Step));
        list.Add(new("bothV()", "Get both vertices from edge", GremlinCompletionKind.Step));
        list.Add(new("otherV()", "Get the other vertex from edge", GremlinCompletionKind.Step));

        // Filter steps
        list.Add(new("has()", "Filter by property", GremlinCompletionKind.Step));
        list.Add(new("hasLabel()", "Filter by label", GremlinCompletionKind.Step));
        list.Add(new("hasId()", "Filter by id", GremlinCompletionKind.Step));
        list.Add(new("hasNot()", "Filter by missing property", GremlinCompletionKind.Step));
        list.Add(new("where()", "Filter with traversal predicate", GremlinCompletionKind.Step));
        list.Add(new("is()", "Filter by value", GremlinCompletionKind.Step));
        list.Add(new("not()", "Negate filter", GremlinCompletionKind.Step));
        list.Add(new("and()", "Logical AND", GremlinCompletionKind.Step));
        list.Add(new("or()", "Logical OR", GremlinCompletionKind.Step));
        list.Add(new("filter()", "Custom filter predicate", GremlinCompletionKind.Step));
        list.Add(new("dedup()", "Remove duplicates", GremlinCompletionKind.Step));
        list.Add(new("limit()", "Limit results", GremlinCompletionKind.Step));
        list.Add(new("skip()", "Skip results", GremlinCompletionKind.Step));
        list.Add(new("range()", "Get range of results", GremlinCompletionKind.Step));
        list.Add(new("tail()", "Get last N results", GremlinCompletionKind.Step));

        // Map steps
        list.Add(new("map()", "Transform each element", GremlinCompletionKind.Step));
        list.Add(new("flatMap()", "Transform and flatten", GremlinCompletionKind.Step));
        list.Add(new("select()", "Select labeled steps", GremlinCompletionKind.Step));
        list.Add(new("project()", "Project to map", GremlinCompletionKind.Step));
        list.Add(new("unfold()", "Unroll collections", GremlinCompletionKind.Step));
        list.Add(new("fold()", "Fold into collection", GremlinCompletionKind.Step));
        list.Add(new("count()", "Count elements", GremlinCompletionKind.Step));
        list.Add(new("sum()", "Sum values", GremlinCompletionKind.Step));
        list.Add(new("max()", "Get maximum", GremlinCompletionKind.Step));
        list.Add(new("min()", "Get minimum", GremlinCompletionKind.Step));
        list.Add(new("mean()", "Calculate mean", GremlinCompletionKind.Step));
        list.Add(new("group()", "Group by key", GremlinCompletionKind.Step));
        list.Add(new("groupCount()", "Count by group", GremlinCompletionKind.Step));
        list.Add(new("order()", "Order results", GremlinCompletionKind.Step));
        list.Add(new("path()", "Get traversal path", GremlinCompletionKind.Step));
        list.Add(new("tree()", "Get tree structure", GremlinCompletionKind.Step));

        // Property steps
        list.Add(new("property()", "Set property", GremlinCompletionKind.Step));
        list.Add(new("properties()", "Get property objects", GremlinCompletionKind.Step));
        list.Add(new("values()", "Get property values", GremlinCompletionKind.Step));
        list.Add(new("valueMap()", "Get properties as map", GremlinCompletionKind.Step));
        list.Add(new("elementMap()", "Get element as map with id/label", GremlinCompletionKind.Step));
        list.Add(new("id()", "Get element id", GremlinCompletionKind.Step));
        list.Add(new("label()", "Get element label", GremlinCompletionKind.Step));

        // Side effect steps
        list.Add(new("sideEffect()", "Execute side effect", GremlinCompletionKind.Step));
        list.Add(new("aggregate()", "Store in side effect", GremlinCompletionKind.Step));
        list.Add(new("store()", "Lazy store", GremlinCompletionKind.Step));
        list.Add(new("cap()", "Get side effect", GremlinCompletionKind.Step));

        // Branch steps
        list.Add(new("union()", "Union of traversals", GremlinCompletionKind.Step));
        list.Add(new("coalesce()", "First non-empty traversal", GremlinCompletionKind.Step));
        list.Add(new("choose()", "If/then/else", GremlinCompletionKind.Step));
        list.Add(new("optional()", "Optional traversal", GremlinCompletionKind.Step));
        list.Add(new("repeat()", "Loop traversal", GremlinCompletionKind.Step));
        list.Add(new("until()", "Loop condition", GremlinCompletionKind.Step));
        list.Add(new("emit()", "Emit during loop", GremlinCompletionKind.Step));
        list.Add(new("times()", "Loop count", GremlinCompletionKind.Step));
        list.Add(new("loops()", "Get loop count", GremlinCompletionKind.Step));

        // Terminal steps
        list.Add(new("drop()", "Delete elements", GremlinCompletionKind.Step));
        list.Add(new("toList()", "Execute and return list", GremlinCompletionKind.Step));
        list.Add(new("next()", "Get next result", GremlinCompletionKind.Step));
        list.Add(new("iterate()", "Execute without results", GremlinCompletionKind.Step));

        // Predicates
        list.Add(new("eq()", "Equal to", GremlinCompletionKind.Predicate));
        list.Add(new("neq()", "Not equal to", GremlinCompletionKind.Predicate));
        list.Add(new("lt()", "Less than", GremlinCompletionKind.Predicate));
        list.Add(new("lte()", "Less than or equal", GremlinCompletionKind.Predicate));
        list.Add(new("gt()", "Greater than", GremlinCompletionKind.Predicate));
        list.Add(new("gte()", "Greater than or equal", GremlinCompletionKind.Predicate));
        list.Add(new("inside()", "Between (exclusive)", GremlinCompletionKind.Predicate));
        list.Add(new("outside()", "Outside range", GremlinCompletionKind.Predicate));
        list.Add(new("between()", "Between (inclusive)", GremlinCompletionKind.Predicate));
        list.Add(new("within()", "In collection", GremlinCompletionKind.Predicate));
        list.Add(new("without()", "Not in collection", GremlinCompletionKind.Predicate));
        list.Add(new("containing()", "String contains", GremlinCompletionKind.Predicate));
        list.Add(new("startingWith()", "String starts with", GremlinCompletionKind.Predicate));
        list.Add(new("endingWith()", "String ends with", GremlinCompletionKind.Predicate));

        // Modulators
        list.Add(new("as()", "Label step", GremlinCompletionKind.Modulator));
        list.Add(new("by()", "Configure previous step", GremlinCompletionKind.Modulator));
        list.Add(new("from()", "Set from vertex", GremlinCompletionKind.Modulator));
        list.Add(new("to()", "Set to vertex", GremlinCompletionKind.Modulator));
        list.Add(new("with()", "With options", GremlinCompletionKind.Modulator));

        return list;
    }
}
