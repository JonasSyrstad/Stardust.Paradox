namespace Stardust.Paradox.GremlinStudio.Editor;

/// <summary>
/// Central registry of all Gremlin step definitions with full parameter metadata.
/// </summary>
public static class GremlinStepDatabase
{
    private static readonly Dictionary<string, GremlinStepDefinition> _steps;

    static GremlinStepDatabase()
    {
        var list = BuildAllDefinitions();
        _steps = new Dictionary<string, GremlinStepDefinition>(list.Count, StringComparer.OrdinalIgnoreCase);
        foreach (var def in list)
        {
            _steps[def.Name] = def;
        }
    }

    /// <summary>
    /// Gets all step definitions.
    /// </summary>
    public static IReadOnlyCollection<GremlinStepDefinition> All => _steps.Values;

    /// <summary>
    /// Tries to get a step definition by name (case-insensitive).
    /// </summary>
    public static bool TryGet(string name, out GremlinStepDefinition? definition)
    {
        return _steps.TryGetValue(name, out definition);
    }

    /// <summary>
    /// Gets a step definition by name, or null if not found.
    /// </summary>
    public static GremlinStepDefinition? Get(string name)
    {
        _steps.TryGetValue(name, out var def);
        return def;
    }

    private static List<GremlinStepDefinition> BuildAllDefinitions()
    {
        var list = new List<GremlinStepDefinition>();

        // ?? Traversal Sources ??????????????????????????????????????????
        list.Add(new("g", "Graph traversal source. Start all queries with g.",
            GremlinCompletionKind.TraversalSource,
            [new([])]));

        list.Add(new("__", "Anonymous traversal. Used inside steps like where(), repeat(), coalesce().",
            GremlinCompletionKind.TraversalSource,
            [new([])]));

        // ?? Vertex / Edge Start Steps ??????????????????????????????????
        list.Add(Def("V", "Get vertices by id or all vertices.",
            GremlinCompletionKind.Step,
            Over(Desc: "Get all vertices"),
            Over([P("id", GremlinParamType.Any, desc: "Vertex id to look up")], "Get vertex by id"),
            Over([P("id1", GremlinParamType.Any), P("id2", GremlinParamType.Any, opt: true)], "Get multiple vertices by ids")));

        list.Add(Def("E", "Get edges by id or all edges.",
            GremlinCompletionKind.Step,
            Over(Desc: "Get all edges"),
            Over([P("id", GremlinParamType.Any, desc: "Edge id to look up")], "Get edge by id")));

        list.Add(Def("addV", "Add a new vertex to the graph.",
            GremlinCompletionKind.Step,
            Over(Desc: "Add vertex without label"),
            Over([P("label", GremlinParamType.Label, desc: "Vertex label")], "Add vertex with label")));

        list.Add(Def("addE", "Add a new edge to the graph. Must be followed by from()/to().",
            GremlinCompletionKind.Step,
            Over([P("label", GremlinParamType.Label, desc: "Edge label")], "Add edge with label")));

        // ?? Navigation Steps ???????????????????????????????????????????
        list.Add(Def("out", "Traverse to adjacent vertices via outgoing edges.",
            GremlinCompletionKind.Step,
            Over(Desc: "Traverse all outgoing edges"),
            Over([P("label", GremlinParamType.Label, desc: "Edge label filter")], "Traverse outgoing edges with label"),
            Over([P("label1", GremlinParamType.Label), P("label2", GremlinParamType.Label, opt: true)], "Traverse by multiple labels")));

        list.Add(Def("in", "Traverse to adjacent vertices via incoming edges.",
            GremlinCompletionKind.Step,
            Over(Desc: "Traverse all incoming edges"),
            Over([P("label", GremlinParamType.Label)], "Traverse incoming edges with label")));

        list.Add(Def("both", "Traverse to adjacent vertices in both directions.",
            GremlinCompletionKind.Step,
            Over(Desc: "Traverse all edges both directions"),
            Over([P("label", GremlinParamType.Label)], "Traverse both-direction edges with label")));

        list.Add(Def("outE", "Get outgoing edge objects.",
            GremlinCompletionKind.Step,
            Over(Desc: "Get all outgoing edges"),
            Over([P("label", GremlinParamType.Label)], "Get outgoing edges with label")));

        list.Add(Def("inE", "Get incoming edge objects.",
            GremlinCompletionKind.Step,
            Over(Desc: "Get all incoming edges"),
            Over([P("label", GremlinParamType.Label)], "Get incoming edges with label")));

        list.Add(Def("bothE", "Get edges in both directions.",
            GremlinCompletionKind.Step,
            Over(Desc: "Get all edges both directions"),
            Over([P("label", GremlinParamType.Label)], "Get edges with label in both directions")));

        list.Add(Def("outV", "Get the outgoing (source) vertex from an edge.", GremlinCompletionKind.Step, Over()));
        list.Add(Def("inV", "Get the incoming (target) vertex from an edge.", GremlinCompletionKind.Step, Over()));
        list.Add(Def("bothV", "Get both vertices from an edge.", GremlinCompletionKind.Step, Over()));
        list.Add(Def("otherV", "Get the other vertex from an edge (relative to traversal direction).", GremlinCompletionKind.Step, Over()));

        // ?? Filter Steps ???????????????????????????????????????????????
        list.Add(Def("has", "Filter elements by property existence or value.",
            GremlinCompletionKind.Step,
            Over([P("key", GremlinParamType.PropertyKey)], "Filter by property existence"),
            Over([P("key", GremlinParamType.PropertyKey), P("value", GremlinParamType.Value)], "Filter by property key and value"),
            Over([P("key", GremlinParamType.PropertyKey), P("predicate", GremlinParamType.Predicate)], "Filter by property with predicate"),
            Over([P("label", GremlinParamType.Label), P("key", GremlinParamType.PropertyKey), P("value", GremlinParamType.Value)], "Filter by label, key and value"),
            Over([P("label", GremlinParamType.Label), P("key", GremlinParamType.PropertyKey), P("predicate", GremlinParamType.Predicate)], "Filter by label, key and predicate")));

        list.Add(Def("hasLabel", "Filter elements by their label.",
            GremlinCompletionKind.Step,
            Over([P("label", GremlinParamType.Label)], "Filter by single label"),
            Over([P("label1", GremlinParamType.Label), P("label2", GremlinParamType.Label, opt: true)], "Filter by multiple labels")));

        list.Add(Def("hasId", "Filter elements by their id.",
            GremlinCompletionKind.Step,
            Over([P("id", GremlinParamType.Any)], "Filter by single id"),
            Over([P("id1", GremlinParamType.Any), P("id2", GremlinParamType.Any, opt: true)], "Filter by multiple ids")));

        list.Add(Def("hasNot", "Filter elements that do NOT have a given property.",
            GremlinCompletionKind.Step,
            Over([P("key", GremlinParamType.PropertyKey, desc: "Property key that must not exist")])));

        list.Add(Def("hasKey", "Filter properties/elements by property key.",
            GremlinCompletionKind.Step,
            Over([P("key", GremlinParamType.PropertyKey)], "Filter by single key"),
            Over([P("key1", GremlinParamType.PropertyKey), P("key2", GremlinParamType.PropertyKey, opt: true)], "Filter by multiple keys")));

        list.Add(Def("hasValue", "Filter properties by property value.",
            GremlinCompletionKind.Step,
            Over([P("value", GremlinParamType.Value)], "Filter by single value"),
            Over([P("value1", GremlinParamType.Value), P("value2", GremlinParamType.Value, opt: true)], "Filter by multiple values")));

        list.Add(Def("where", "Filter traversers by a predicate or traversal.",
            GremlinCompletionKind.Step,
            Over([P("predicate", GremlinParamType.Predicate)], "Filter with comparison predicate"),
            Over([P("traversal", GremlinParamType.Traversal)], "Filter with sub-traversal")));

        list.Add(Def("is", "Filter the current value.",
            GremlinCompletionKind.Step,
            Over([P("value", GremlinParamType.Value)], "Filter by exact value"),
            Over([P("predicate", GremlinParamType.Predicate)], "Filter by predicate")));

        list.Add(Def("not", "Negate a traversal filter.",
            GremlinCompletionKind.Step,
            Over([P("traversal", GremlinParamType.Traversal, desc: "Traversal that must return no results")])));

        list.Add(Def("and", "Logical AND — all traversals must produce a result.",
            GremlinCompletionKind.Step,
            Over([P("traversal1", GremlinParamType.Traversal), P("traversal2", GremlinParamType.Traversal, opt: true)])));

        list.Add(Def("or", "Logical OR — at least one traversal must produce a result.",
            GremlinCompletionKind.Step,
            Over([P("traversal1", GremlinParamType.Traversal), P("traversal2", GremlinParamType.Traversal, opt: true)])));

        list.Add(Def("filter", "Filter with a custom traversal predicate.",
            GremlinCompletionKind.Step,
            Over([P("traversal", GremlinParamType.Traversal)])));

        list.Add(Def("dedup", "Remove duplicate elements from the traversal.",
            GremlinCompletionKind.Step,
            Over(Desc: "Remove duplicates by element"),
            Over([P("label", GremlinParamType.Label, opt: true)], "Remove duplicates by labeled step values")));

        list.Add(Def("limit", "Limit the number of results.",
            GremlinCompletionKind.Step,
            Over([P("count", GremlinParamType.Number, desc: "Maximum number of results")])));

        list.Add(Def("skip", "Skip a number of results.",
            GremlinCompletionKind.Step,
            Over([P("count", GremlinParamType.Number, desc: "Number of results to skip")])));

        list.Add(Def("range", "Get a range of results by start and end index.",
            GremlinCompletionKind.Step,
            Over([P("start", GremlinParamType.Number, desc: "Start index (inclusive)"), P("end", GremlinParamType.Number, desc: "End index (exclusive)")])));

        list.Add(Def("tail", "Get the last N results.",
            GremlinCompletionKind.Step,
            Over(Desc: "Get the last result"),
            Over([P("count", GremlinParamType.Number)], "Get last N results")));

        list.Add(Def("coin", "Randomly filter elements with given probability.",
            GremlinCompletionKind.Step,
            Over([P("probability", GremlinParamType.Number, desc: "Probability 0.0-1.0")])));

        list.Add(Def("sample", "Randomly sample N elements.",
            GremlinCompletionKind.Step,
            Over([P("count", GremlinParamType.Number, desc: "Number of elements to sample")])));

        // ?? Map / Transform Steps ??????????????????????????????????????
        list.Add(Def("map", "Transform each element using a traversal.",
            GremlinCompletionKind.Step,
            Over([P("traversal", GremlinParamType.Traversal)])));

        list.Add(Def("flatMap", "Transform and flatten results.",
            GremlinCompletionKind.Step,
            Over([P("traversal", GremlinParamType.Traversal)])));

        list.Add(Def("select", "Select previously labeled step values.",
            GremlinCompletionKind.Step,
            Over([P("label", GremlinParamType.Label)], "Select single label"),
            Over([P("label1", GremlinParamType.Label), P("label2", GremlinParamType.Label)], "Select multiple labels")));

        list.Add(Def("project", "Project traverser into a map with named keys.",
            GremlinCompletionKind.Step,
            Over([P("key", GremlinParamType.String)], "Project single key"),
            Over([P("key1", GremlinParamType.String), P("key2", GremlinParamType.String, opt: true)], "Project multiple keys")));

        list.Add(Def("unfold", "Unroll a collection into individual elements.", GremlinCompletionKind.Step, Over()));
        list.Add(Def("fold", "Fold all elements into a single list.", GremlinCompletionKind.Step, Over()));

        list.Add(Def("count", "Count the number of traversers.", GremlinCompletionKind.Step, Over()));
        list.Add(Def("sum", "Sum the values of traversers.", GremlinCompletionKind.Step, Over()));
        list.Add(Def("max", "Get the maximum value.", GremlinCompletionKind.Step, Over()));
        list.Add(Def("min", "Get the minimum value.", GremlinCompletionKind.Step, Over()));
        list.Add(Def("mean", "Calculate the arithmetic mean.", GremlinCompletionKind.Step, Over()));

        list.Add(Def("group", "Group traversers by key. Use by() to specify key and value projections.",
            GremlinCompletionKind.Step, Over()));

        list.Add(Def("groupCount", "Count traversers per group. Use by() to specify grouping key.",
            GremlinCompletionKind.Step, Over()));

        list.Add(Def("order", "Order the traversers. Use by() to specify sort key and direction.",
            GremlinCompletionKind.Step, Over()));

        list.Add(Def("path", "Get the traversal path.",
            GremlinCompletionKind.Step, Over()));

        list.Add(Def("tree", "Get the traversal tree.",
            GremlinCompletionKind.Step, Over()));

        list.Add(Def("match", "Pattern matching across the graph.",
            GremlinCompletionKind.Step,
            Over([P("traversal1", GremlinParamType.Traversal), P("traversal2", GremlinParamType.Traversal, opt: true)], "Match traversal patterns")));

        list.Add(Def("math", "Evaluate a math expression on traverser values.",
            GremlinCompletionKind.Step,
            Over([P("expression", GremlinParamType.String, desc: "Math expression (e.g. '_ + 1')")])));

        list.Add(Def("local", "Execute a traversal on each individual element (local scope).",
            GremlinCompletionKind.Step,
            Over([P("traversal", GremlinParamType.Traversal)])));

        list.Add(Def("constant", "Return a constant value.",
            GremlinCompletionKind.Step,
            Over([P("value", GremlinParamType.Value)])));

        list.Add(Def("inject", "Inject values into the traversal stream.",
            GremlinCompletionKind.Step,
            Over([P("value1", GremlinParamType.Value), P("value2", GremlinParamType.Value, opt: true)])));

        // ?? Property Steps ?????????????????????????????????????????????
        list.Add(Def("property", "Set a property on a vertex or edge.",
            GremlinCompletionKind.Step,
            Over([P("key", GremlinParamType.PropertyKey), P("value", GremlinParamType.Value)], "Set property"),
            Over([P("cardinality", GremlinParamType.Enum, desc: "single/list/set"), P("key", GremlinParamType.PropertyKey), P("value", GremlinParamType.Value)], "Set property with cardinality")));

        list.Add(Def("properties", "Get property objects.",
            GremlinCompletionKind.Step,
            Over(Desc: "Get all properties"),
            Over([P("key", GremlinParamType.PropertyKey, opt: true)], "Get properties by key")));

        list.Add(Def("values", "Get property values.",
            GremlinCompletionKind.Step,
            Over(Desc: "Get all values"),
            Over([P("key", GremlinParamType.PropertyKey)], "Get value by key")));

        list.Add(Def("valueMap", "Get properties as a map.",
            GremlinCompletionKind.Step,
            Over(Desc: "Get all properties as map"),
            Over([P("includeTokens", GremlinParamType.Boolean, opt: true)], "Include id and label tokens"),
            Over([P("key1", GremlinParamType.PropertyKey), P("key2", GremlinParamType.PropertyKey, opt: true)], "Get specific properties")));

        list.Add(Def("elementMap", "Get element as a map including id and label.",
            GremlinCompletionKind.Step,
            Over(Desc: "Get all properties with id/label"),
            Over([P("key1", GremlinParamType.PropertyKey), P("key2", GremlinParamType.PropertyKey, opt: true)], "Get specific properties with id/label")));

        list.Add(Def("id", "Get element id.", GremlinCompletionKind.Step, Over()));
        list.Add(Def("label", "Get element label.", GremlinCompletionKind.Step, Over()));

        // ?? Side Effect Steps ??????????????????????????????????????????
        list.Add(Def("sideEffect", "Execute a side-effect traversal without affecting the main traversal.",
            GremlinCompletionKind.Step,
            Over([P("traversal", GremlinParamType.Traversal)])));

        list.Add(Def("aggregate", "Store traversers into a named side-effect (eager).",
            GremlinCompletionKind.Step,
            Over([P("sideEffectKey", GremlinParamType.String)])));

        list.Add(Def("store", "Store traversers into a named side-effect (lazy).",
            GremlinCompletionKind.Step,
            Over([P("sideEffectKey", GremlinParamType.String)])));

        list.Add(Def("cap", "Get a named side-effect value.",
            GremlinCompletionKind.Step,
            Over([P("sideEffectKey", GremlinParamType.String)])));

        list.Add(Def("subgraph", "Extract a subgraph from the traversal.",
            GremlinCompletionKind.Step,
            Over([P("sideEffectKey", GremlinParamType.String)])));

        // ?? Branch / Control Flow Steps ????????????????????????????????
        list.Add(Def("union", "Merge results from multiple traversals.",
            GremlinCompletionKind.Step,
            Over([P("traversal1", GremlinParamType.Traversal), P("traversal2", GremlinParamType.Traversal, opt: true)])));

        list.Add(Def("coalesce", "Evaluate traversals in order and return the first that produces a result.",
            GremlinCompletionKind.Step,
            Over([P("traversal1", GremlinParamType.Traversal), P("traversal2", GremlinParamType.Traversal, opt: true)])));

        list.Add(Def("choose", "If/then/else branching.",
            GremlinCompletionKind.Step,
            Over([P("predicate", GremlinParamType.Traversal)], "Boolean branch (use with option())"),
            Over([P("predicate", GremlinParamType.Traversal), P("trueTraversal", GremlinParamType.Traversal)], "If/then"),
            Over([P("predicate", GremlinParamType.Traversal), P("trueTraversal", GremlinParamType.Traversal), P("falseTraversal", GremlinParamType.Traversal)], "If/then/else")));

        list.Add(Def("optional", "Optionally traverse — return original if traversal yields nothing.",
            GremlinCompletionKind.Step,
            Over([P("traversal", GremlinParamType.Traversal)])));

        list.Add(Def("repeat", "Loop a traversal. Combine with until() and/or times().",
            GremlinCompletionKind.Step,
            Over([P("traversal", GremlinParamType.Traversal)])));

        list.Add(Def("until", "Set the termination condition for repeat().",
            GremlinCompletionKind.Step,
            Over([P("predicate", GremlinParamType.Traversal)])));

        list.Add(Def("emit", "Emit traversers during repeat().",
            GremlinCompletionKind.Step,
            Over(Desc: "Emit all traversers"),
            Over([P("predicate", GremlinParamType.Traversal)], "Emit matching traversers")));

        list.Add(Def("times", "Set the number of loops for repeat().",
            GremlinCompletionKind.Step,
            Over([P("count", GremlinParamType.Number)])));

        list.Add(Def("loops", "Get the current loop count inside repeat().",
            GremlinCompletionKind.Step, Over()));

        list.Add(Def("option", "Provide a branch option for choose().",
            GremlinCompletionKind.Step,
            Over([P("token", GremlinParamType.Value), P("traversal", GremlinParamType.Traversal)])));

        // ?? Terminal Steps ?????????????????????????????????????????????
        list.Add(Def("drop", "Delete the current element(s) from the graph.", GremlinCompletionKind.Step, Over()));
        list.Add(Def("toList", "Execute the traversal and return results as a list.", GremlinCompletionKind.Step, Over()));
        list.Add(Def("toSet", "Execute the traversal and return results as a set.", GremlinCompletionKind.Step, Over()));
        list.Add(Def("next", "Get the next result from the traversal.", GremlinCompletionKind.Step, Over()));
        list.Add(Def("iterate", "Execute the traversal without returning results.", GremlinCompletionKind.Step, Over()));
        list.Add(Def("explain", "Explain the traversal execution plan.", GremlinCompletionKind.Step, Over()));
        list.Add(Def("profile", "Profile the traversal execution.", GremlinCompletionKind.Step, Over()));

        // ?? Predicates ?????????????????????????????????????????????????
        list.Add(Def("eq", "Equal to comparison.", GremlinCompletionKind.Predicate,
            Over([P("value", GremlinParamType.Value)])));
        list.Add(Def("neq", "Not equal to comparison.", GremlinCompletionKind.Predicate,
            Over([P("value", GremlinParamType.Value)])));
        list.Add(Def("lt", "Less than comparison.", GremlinCompletionKind.Predicate,
            Over([P("value", GremlinParamType.Number)])));
        list.Add(Def("lte", "Less than or equal comparison.", GremlinCompletionKind.Predicate,
            Over([P("value", GremlinParamType.Number)])));
        list.Add(Def("gt", "Greater than comparison.", GremlinCompletionKind.Predicate,
            Over([P("value", GremlinParamType.Number)])));
        list.Add(Def("gte", "Greater than or equal comparison.", GremlinCompletionKind.Predicate,
            Over([P("value", GremlinParamType.Number)])));
        list.Add(Def("inside", "Between range (exclusive).", GremlinCompletionKind.Predicate,
            Over([P("low", GremlinParamType.Number), P("high", GremlinParamType.Number)])));
        list.Add(Def("outside", "Outside range.", GremlinCompletionKind.Predicate,
            Over([P("low", GremlinParamType.Number), P("high", GremlinParamType.Number)])));
        list.Add(Def("between", "Between range (inclusive start, exclusive end).", GremlinCompletionKind.Predicate,
            Over([P("low", GremlinParamType.Number), P("high", GremlinParamType.Number)])));
        list.Add(Def("within", "Value is within a collection of values.", GremlinCompletionKind.Predicate,
            Over([P("value1", GremlinParamType.Value), P("value2", GremlinParamType.Value, opt: true)])));
        list.Add(Def("without", "Value is not within a collection of values.", GremlinCompletionKind.Predicate,
            Over([P("value1", GremlinParamType.Value), P("value2", GremlinParamType.Value, opt: true)])));
        list.Add(Def("containing", "String contains substring.", GremlinCompletionKind.Predicate,
            Over([P("value", GremlinParamType.String)])));
        list.Add(Def("startingWith", "String starts with prefix.", GremlinCompletionKind.Predicate,
            Over([P("value", GremlinParamType.String)])));
        list.Add(Def("endingWith", "String ends with suffix.", GremlinCompletionKind.Predicate,
            Over([P("value", GremlinParamType.String)])));
        list.Add(Def("notContaining", "String does not contain substring.", GremlinCompletionKind.Predicate,
            Over([P("value", GremlinParamType.String)])));
        list.Add(Def("notStartingWith", "String does not start with prefix.", GremlinCompletionKind.Predicate,
            Over([P("value", GremlinParamType.String)])));
        list.Add(Def("notEndingWith", "String does not end with suffix.", GremlinCompletionKind.Predicate,
            Over([P("value", GremlinParamType.String)])));
        list.Add(Def("regex", "Match by regular expression.", GremlinCompletionKind.Predicate,
            Over([P("pattern", GremlinParamType.String)])));

        // ?? Modulators ?????????????????????????????????????????????????
        list.Add(Def("as", "Assign a label to the current step for later reference with select().",
            GremlinCompletionKind.Modulator,
            Over([P("label", GremlinParamType.Label)])));

        list.Add(Def("by", "Configure the preceding step (order, group, project, path, etc.).",
            GremlinCompletionKind.Modulator,
            Over(Desc: "Use default configuration"),
            Over([P("key", GremlinParamType.PropertyKey)], "By property key"),
            Over([P("traversal", GremlinParamType.Traversal)], "By traversal"),
            Over([P("key", GremlinParamType.PropertyKey), P("order", GremlinParamType.Enum, desc: "asc/desc/shuffle")], "By key with order"),
            Over([P("traversal", GremlinParamType.Traversal), P("order", GremlinParamType.Enum, desc: "asc/desc/shuffle")], "By traversal with order")));

        list.Add(Def("from", "Set the outgoing vertex for addE() or path start.",
            GremlinCompletionKind.Modulator,
            Over([P("label", GremlinParamType.Label)], "From labeled step"),
            Over([P("traversal", GremlinParamType.Traversal)], "From traversal")));

        list.Add(Def("to", "Set the incoming vertex for addE() or path end.",
            GremlinCompletionKind.Modulator,
            Over([P("label", GremlinParamType.Label)], "To labeled step"),
            Over([P("traversal", GremlinParamType.Traversal)], "To traversal")));

        list.Add(Def("with", "Pass options/configuration to a step.",
            GremlinCompletionKind.Modulator,
            Over([P("key", GremlinParamType.String)], "Set option"),
            Over([P("key", GremlinParamType.String), P("value", GremlinParamType.Value)], "Set option with value")));

        return list;
    }

    // Helper methods for concise definition building
    private static GremlinStepDefinition Def(string name, string desc, GremlinCompletionKind kind, params GremlinStepOverload[] overloads)
        => new(name, desc, kind, overloads);

    private static GremlinStepOverload Over(GremlinParam[]? Params = null, string? Desc = null)
        => new(Params ?? [], Desc);

    private static GremlinParam P(string name, GremlinParamType type, bool opt = false, string? desc = null)
        => new(name, type, opt, desc);
}
