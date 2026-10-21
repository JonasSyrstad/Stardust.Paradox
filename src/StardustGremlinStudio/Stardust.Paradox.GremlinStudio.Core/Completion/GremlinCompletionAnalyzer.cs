namespace Stardust.Paradox.GremlinStudio.Core.Completion;

/// <summary>
/// Schema-aware completion candidates for the argument at the caret.
/// </summary>
/// <param name="StepName">The step whose argument list contains the caret.</param>
/// <param name="ArgumentIndex">Zero-based index of the argument being typed.</param>
/// <param name="InputState">Traverser state flowing into the step (for <c>by</c>, the state of the modulated step).</param>
/// <param name="HasOpenQuote">True when the caret is inside an unterminated string literal.</param>
/// <param name="Prefix">The partially typed value inside the string literal.</param>
/// <param name="PropertyKeys">Property keys valid at the caret.</param>
/// <param name="VertexLabels">Vertex labels valid at the caret.</param>
/// <param name="EdgeLabels">Edge labels valid at the caret.</param>
/// <param name="StepLabels">Step labels declared earlier with <c>as()</c> that are valid at the caret.</param>
public sealed record GremlinArgumentCompletion(
    string StepName,
    int ArgumentIndex,
    GremlinElementState InputState,
    bool HasOpenQuote,
    string Prefix,
    IReadOnlyList<string> PropertyKeys,
    IReadOnlyList<string> VertexLabels,
    IReadOnlyList<string> EdgeLabels,
    IReadOnlyList<string> StepLabels)
{
    /// <summary>True when there is at least one candidate.</summary>
    public bool HasCandidates => PropertyKeys.Count > 0 || VertexLabels.Count > 0 || EdgeLabels.Count > 0 || StepLabels.Count > 0;
}

/// <summary>
/// Parses the Gremlin text before the caret, tracks the traverser state (vertex/edge, labels and
/// direction) step by step and determines which property keys or labels are valid for the argument
/// being typed.
/// </summary>
public static class GremlinCompletionAnalyzer
{
    private static readonly HashSet<string> Modulators = new(StringComparer.Ordinal)
    {
        "by", "as", "from", "to", "option", "times", "until", "emit", "with", "read", "write"
    };

    private static readonly HashSet<string> SameStateSteps = new(StringComparer.Ordinal)
    {
        "has", "hasNot", "hasId", "hasKey", "hasValue", "where", "filter", "not", "and", "or", "is",
        "dedup", "limit", "range", "tail", "skip", "sample", "coin", "order", "simplePath", "cyclicPath",
        "timeLimit", "sideEffect", "aggregate", "store", "property", "barrier", "identity", "drop",
        "subgraph", "inject", "profile", "explain", "iterate", "next", "toList"
    };

    private static readonly HashSet<string> CardinalityTokens = new(StringComparer.Ordinal)
    {
        "single", "list", "set", "Cardinality", "VertexProperty"
    };

    private static readonly GremlinElementState Source = new(GremlinElementKind.Unknown);

    // Element id/label are T tokens, not properties; only filter steps accept them as string keys (e.g. Cosmos DB has('id', ...)).
    private static readonly string[] ElementTokenKeys = ["id", "label"];

    /// <summary>
    /// Analyzes <paramref name="textBefore"/> and returns completion candidates for a label or
    /// property-key argument at the caret, or <c>null</c> when the caret is not in such an argument.
    /// </summary>
    public static GremlinArgumentCompletion? Analyze(string textBefore, GremlinCompletionSchema schema)
    {
        ArgumentNullException.ThrowIfNull(textBefore);
        ArgumentNullException.ThrowIfNull(schema);

        var parser = new Parser(textBefore, schema);
        if (!parser.Run(out var call, out var hasOpenQuote, out var prefix)) return null;
        return BuildCompletion(call!, hasOpenQuote, prefix, schema, parser.StepLabels);
    }

    /// <summary>
    /// Returns the traverser state at the end of <paramref name="traversal"/>.
    /// </summary>
    public static GremlinElementState ResolveState(string traversal, GremlinCompletionSchema schema)
    {
        ArgumentNullException.ThrowIfNull(traversal);
        ArgumentNullException.ThrowIfNull(schema);
        var parser = new Parser(traversal, schema);
        parser.Run(out _, out _, out _);
        return parser.TopLevelState;
    }

    private static GremlinArgumentCompletion? BuildCompletion(
        CallFrame call,
        bool hasOpenQuote,
        string prefix,
        GremlinCompletionSchema schema,
        IReadOnlyCollection<string> declaredStepLabels)
    {
        var step = call.Name;
        var index = call.ArgIndex;
        var input = step == "by" ? call.ModulatedInput : call.Input;
        IReadOnlyList<string> keys = [], vertices = [], edges = [], stepLabels = [];

        switch (step)
        {
            case "has":
                if (index == 0)
                {
                    keys = WithElementTokens(schema.GetPropertyKeys(input), input);
                }
                else if (index == 1 && call.ArgLiteral(0) is { } first && IsLabelOf(first, input, schema, out var labelled))
                {
                    // has(label, key, value): the first argument narrows the label.
                    keys = WithElementTokens(schema.GetPropertyKeys(labelled), labelled);
                }
                else
                {
                    // has(key, value) / has(key, predicate): the value is user input.
                    return null;
                }
                break;
            case "hasNot":
                if (index != 0) return null;
                keys = WithElementTokens(schema.GetPropertyKeys(input), input);
                break;
            case "select" or "dedup":
                stepLabels = Sorted(declaredStepLabels);
                break;
            case "where" or "from" or "to":
                if (index != 0) return null;
                stepLabels = Sorted(declaredStepLabels);
                break;
            case "hasKey":
                keys = schema.GetPropertyKeys(input.Kind == GremlinElementKind.Property ? input.Owner ?? GremlinElementState.Unknown : input);
                break;
            case "values" or "properties" or "valueMap" or "elementMap" or "propertyMap":
                keys = schema.GetPropertyKeys(input);
                break;
            case "property":
                var keyIndex = call.ArgIdentifier(0) is { } id && CardinalityTokens.Contains(id) ? 1 : 0;
                if (index != keyIndex) return null;
                keys = schema.GetPropertyKeys(input);
                break;
            case "by":
                if (index != 0) return null;
                keys = schema.GetPropertyKeys(input);
                break;
            case "hasLabel":
                (vertices, edges) = LabelsFor(input, schema);
                break;
            case "out" or "outE":
                edges = EdgeLabelsFrom(input, GremlinDirection.Out, schema);
                break;
            case "in" or "inE":
                edges = EdgeLabelsFrom(input, GremlinDirection.In, schema);
                break;
            case "both" or "bothE":
                edges = EdgeLabelsFrom(input, GremlinDirection.Both, schema);
                break;
            case "addV":
                if (index != 0) return null;
                vertices = Sorted(schema.VertexLabels.Keys);
                break;
            case "addE":
                if (index != 0) return null;
                edges = Sorted(schema.EdgeLabels.Keys);
                break;
            default:
                return null;
        }

        return new GremlinArgumentCompletion(step, index, input, hasOpenQuote, prefix, keys, vertices, edges, stepLabels);
    }

    private static IReadOnlyList<string> WithElementTokens(IReadOnlyList<string> keys, GremlinElementState input) =>
        input.Kind is GremlinElementKind.Vertex or GremlinElementKind.Edge or GremlinElementKind.Unknown
            ? Sorted(keys.Concat(ElementTokenKeys))
            : keys;

    private static bool IsLabelOf(string value, GremlinElementState input, GremlinCompletionSchema schema, out GremlinElementState labelled)
    {
        var labels = new HashSet<string>(StringComparer.Ordinal) { value };
        labelled = input;
        var isVertex = schema.VertexLabels.ContainsKey(value);
        var isEdge = schema.EdgeLabels.ContainsKey(value);
        switch (input.Kind)
        {
            case GremlinElementKind.Vertex when isVertex:
                labelled = new GremlinElementState(GremlinElementKind.Vertex, labels);
                return true;
            case GremlinElementKind.Edge when isEdge:
                labelled = new GremlinElementState(GremlinElementKind.Edge, labels);
                return true;
            case GremlinElementKind.Unknown when isVertex ^ isEdge:
                labelled = new GremlinElementState(isVertex ? GremlinElementKind.Vertex : GremlinElementKind.Edge, labels);
                return true;
            default:
                return false;
        }
    }

    private static (IReadOnlyList<string> Vertices, IReadOnlyList<string> Edges) LabelsFor(GremlinElementState input, GremlinCompletionSchema schema) =>
        input.Kind switch
        {
            GremlinElementKind.Vertex => (Sorted(input.Labels ?? (IEnumerable<string>)schema.VertexLabels.Keys), []),
            GremlinElementKind.Edge => ([], Sorted(input.Labels ?? (IEnumerable<string>)schema.EdgeLabels.Keys)),
            GremlinElementKind.Unknown => (Sorted(schema.VertexLabels.Keys), Sorted(schema.EdgeLabels.Keys)),
            _ => ([], [])
        };

    private static IReadOnlyList<string> EdgeLabelsFrom(GremlinElementState input, GremlinDirection direction, GremlinCompletionSchema schema)
    {
        if (input.Kind is GremlinElementKind.Edge or GremlinElementKind.Value or GremlinElementKind.Property) return [];
        var incident = input.Kind == GremlinElementKind.Vertex ? schema.GetIncidentEdgeLabels(input.Labels, direction) : null;
        return Sorted(incident ?? (IEnumerable<string>)schema.EdgeLabels.Keys);
    }

    private static List<string> Sorted(IEnumerable<string> values) =>
        values.Distinct(StringComparer.Ordinal).OrderBy(v => v, StringComparer.OrdinalIgnoreCase).ToList();

    private static HashSet<string>? ToSet(IEnumerable<string>? values) =>
        values == null ? null : new HashSet<string>(values, StringComparer.Ordinal);

    private static GremlinElementState Merge(IReadOnlyList<GremlinElementState> states)
    {
        if (states.Count == 0) return GremlinElementState.Unknown;
        var kind = states[0].Kind;
        if (states.Any(s => s.Kind != kind)) return GremlinElementState.Unknown;
        if (kind is not (GremlinElementKind.Vertex or GremlinElementKind.Edge)) return states[0];
        if (states.Any(s => s.Labels == null)) return new GremlinElementState(kind);
        return new GremlinElementState(kind, ToSet(states.SelectMany(s => s.Labels!)));
    }

    private sealed class ArgInfo
    {
        public int TokenCount;
        public string? FirstString;
        public string? FirstIdentifier;
        public bool FirstIsString;
        public bool FirstIsIdentifier;
    }

    private sealed class ChainFrame(GremlinElementState start)
    {
        public GremlinElementState State = start;
        public GremlinElementState LastInput = start;
        public string? PendingIdentifier;
        public bool AfterDot;
        public bool HasSteps;
        public GremlinElementState? ChooseInput;
        public List<GremlinElementState>? OptionStates;
    }

    private sealed class CallFrame
    {
        public required string Name;
        public required GremlinElementState Input;
        public required GremlinElementState ModulatedInput;
        public required ChainFrame Owner;
        public int ArgIndex;
        public readonly List<ArgInfo> Args = [new ArgInfo()];
        public readonly List<GremlinElementState> InnerStates = [];
        public ChainFrame? ArgChain;

        public ArgInfo CurrentArg => Args[^1];

        public string? ArgLiteral(int index) =>
            index < Args.Count && Args[index].FirstIsString && Args[index].TokenCount == 1 ? Args[index].FirstString : null;

        public string? ArgIdentifier(int index) =>
            index < Args.Count && Args[index].FirstIsIdentifier ? Args[index].FirstIdentifier : null;

        public IEnumerable<string> StringArgs() =>
            Args.Where(a => a.FirstIsString && a.TokenCount == 1).Select(a => a.FirstString!);
    }

    private sealed class Parser(string text, GremlinCompletionSchema schema)
    {
        private readonly Stack<CallFrame> _calls = new();
        private readonly Dictionary<string, GremlinElementState> _stepLabels = new(StringComparer.Ordinal);
        private ChainFrame _top = new(Source);

        public GremlinElementState TopLevelState => _top.State;

        public IReadOnlyCollection<string> StepLabels => _stepLabels.Keys;

        private ChainFrame CurrentChain => _calls.Count == 0 ? _top : _calls.Peek().ArgChain!;

        public bool Run(out CallFrame? call, out bool hasOpenQuote, out string prefix)
        {
            call = null;
            hasOpenQuote = false;
            prefix = string.Empty;
            var i = 0;
            var trailingIdentifier = string.Empty;

            while (i < text.Length)
            {
                var c = text[i];
                trailingIdentifier = string.Empty;

                if (char.IsWhiteSpace(c)) { i++; continue; }

                if (c == '/' && i + 1 < text.Length && text[i + 1] == '/')
                {
                    while (i < text.Length && text[i] != '\n') i++;
                    continue;
                }

                if (c == '/' && i + 1 < text.Length && text[i + 1] == '*')
                {
                    var end = text.IndexOf("*/", i + 2, StringComparison.Ordinal);
                    if (end < 0) return false;
                    i = end + 2;
                    continue;
                }

                if (c is '\'' or '"')
                {
                    i++;
                    var sb = new System.Text.StringBuilder();
                    var closed = false;
                    while (i < text.Length)
                    {
                        if (text[i] == '\\' && i + 1 < text.Length) { sb.Append(text[i + 1]); i += 2; continue; }
                        if (text[i] == c) { closed = true; i++; break; }
                        sb.Append(text[i++]);
                    }

                    if (!closed)
                    {
                        // Caret is inside this string literal.
                        if (_calls.Count == 0 || _calls.Peek().CurrentArg.TokenCount != 0) return false;
                        call = _calls.Peek();
                        hasOpenQuote = true;
                        prefix = sb.ToString();
                        return true;
                    }

                    OnToken(isString: true, value: sb.ToString());
                    continue;
                }

                if (char.IsLetter(c) || c == '_')
                {
                    var start = i;
                    while (i < text.Length && (char.IsLetterOrDigit(text[i]) || text[i] == '_')) i++;
                    var identifier = text[start..i];
                    OnIdentifier(identifier);
                    trailingIdentifier = identifier;
                    continue;
                }

                switch (c)
                {
                    case '.':
                        CurrentChain.AfterDot = true;
                        if (CurrentChain.PendingIdentifier is { } pending)
                        {
                            // Identifier followed by '.' (g., __., T., Cardinality.)
                            ApplyStartIdentifier(pending);
                            CurrentChain.PendingIdentifier = null;
                        }
                        break;
                    case '(':
                        OpenCall();
                        break;
                    case ',':
                        if (_calls.Count > 0) NextArgument();
                        break;
                    case ')':
                        CloseCall();
                        break;
                    case ';':
                        _calls.Clear();
                        _stepLabels.Clear();
                        _top = new ChainFrame(Source);
                        break;
                    default:
                        OnToken(isString: false, value: null);
                        break;
                }
                i++;
            }

            if (_calls.Count == 0) return false;
            call = _calls.Peek();
            // Only an empty argument position (no quote yet) is a completion target.
            if (call.CurrentArg.TokenCount != 0 || trailingIdentifier.Length != 0) return false;
            return true;
        }

        private void OnIdentifier(string identifier)
        {
            var chain = CurrentChain;
            if (_calls.Count == 0 && chain.HasSteps && !chain.AfterDot)
            {
                // A new statement on the top level.
                _stepLabels.Clear();
                _top = new ChainFrame(Source);
                chain = _top;
            }

            if (_calls.Count > 0 && chain.PendingIdentifier == null && !chain.AfterDot && !chain.HasSteps)
            {
                var arg = _calls.Peek().CurrentArg;
                if (arg.TokenCount == 0)
                {
                    arg.FirstIsIdentifier = true;
                    arg.FirstIdentifier = identifier;
                }
                arg.TokenCount++;
            }

            chain.PendingIdentifier = identifier;
            chain.AfterDot = false;
        }

        private void ApplyStartIdentifier(string identifier)
        {
            var chain = CurrentChain;
            if (identifier == "g") chain.State = Source;
            chain.HasSteps = true;
        }

        private void OnToken(bool isString, string? value)
        {
            var chain = CurrentChain;
            chain.PendingIdentifier = null;
            chain.AfterDot = false;
            if (_calls.Count == 0) return;
            var arg = _calls.Peek().CurrentArg;
            if (arg.TokenCount == 0 && isString)
            {
                arg.FirstIsString = true;
                arg.FirstString = value;
            }
            arg.TokenCount++;
        }

        private void OpenCall()
        {
            var chain = CurrentChain;
            var name = chain.PendingIdentifier ?? "(";
            chain.PendingIdentifier = null;
            chain.AfterDot = false;

            var frame = new CallFrame
            {
                Name = name,
                Input = chain.State,
                ModulatedInput = chain.LastInput,
                Owner = chain
            };
            frame.ArgChain = new ChainFrame(ArgumentStart(frame));
            _calls.Push(frame);
        }

        private static GremlinElementState ArgumentStart(CallFrame frame) => frame.Name switch
        {
            "by" => frame.ModulatedInput,
            "option" => frame.Owner.ChooseInput ?? frame.Input,
            _ => frame.Input
        };

        private void NextArgument()
        {
            var frame = _calls.Peek();
            FinishArgChain(frame);
            frame.ArgIndex++;
            frame.Args.Add(new ArgInfo());
            frame.ArgChain = new ChainFrame(ArgumentStart(frame));
        }

        private static void FinishArgChain(CallFrame frame)
        {
            if (frame.ArgChain is { HasSteps: true } argChain)
            {
                frame.InnerStates.Add(argChain.State);
            }
        }

        private void CloseCall()
        {
            if (_calls.Count == 0) return;
            var frame = _calls.Pop();
            FinishArgChain(frame);

            var chain = frame.Owner;
            chain.HasSteps = true;
            chain.AfterDot = false;
            if (frame.Name == "(") return;

            // Inside an argument the step counts as a token of that argument.
            if (_calls.Count > 0 && ReferenceEquals(chain, _calls.Peek().ArgChain))
            {
                var arg = _calls.Peek().CurrentArg;
                if (arg.TokenCount == 0) arg.TokenCount = 1;
                else if (!arg.FirstIsIdentifier) arg.TokenCount++;
                arg.FirstIsString = false;
            }

            var input = chain.State;
            if (frame.Name == "option" && chain.OptionStates != null)
            {
                if (frame.InnerStates.Count > 0) chain.OptionStates.Add(frame.InnerStates[^1]);
                chain.State = Merge(chain.OptionStates);
                return;
            }

            chain.State = Apply(frame, input);
            if (Modulators.Contains(frame.Name)) return;
            chain.LastInput = input;
            if (frame.Name == "choose" && frame.InnerStates.Count <= 1)
            {
                // choose(selector).option(...) form: options branch from the choose input.
                chain.ChooseInput = input;
                chain.OptionStates = [];
            }
            else
            {
                chain.ChooseInput = null;
                chain.OptionStates = null;
            }
        }

        private GremlinElementState Apply(CallFrame frame, GremlinElementState s)
        {
            var labels = frame.StringArgs().ToList();
            switch (frame.Name)
            {
                case "V": return GremlinElementState.AnyVertex;
                case "E": return GremlinElementState.AnyEdge;
                case "addV":
                    return labels.Count > 0
                        ? new GremlinElementState(GremlinElementKind.Vertex, ToSet(labels.Take(1)))
                        : GremlinElementState.AnyVertex;
                case "addE":
                    return labels.Count > 0
                        ? new GremlinElementState(GremlinElementKind.Edge, ToSet(labels.Take(1)))
                        : GremlinElementState.AnyEdge;
                case "hasLabel":
                    return labels.Count == 0 ? s : NarrowLabels(s, labels);
                case "has":
                    return frame.Args.Count == 3 && frame.ArgLiteral(0) is { } label ? NarrowLabels(s, [label]) : s;
                case "out": return Adjacent(s, labels, GremlinDirection.Out);
                case "in": return Adjacent(s, labels, GremlinDirection.In);
                case "both": return Adjacent(s, labels, GremlinDirection.Both);
                case "outE": return Incident(s, labels, GremlinDirection.Out, GremlinDirection.In);
                case "inE": return Incident(s, labels, GremlinDirection.In, GremlinDirection.Out);
                case "bothE": return Incident(s, labels, GremlinDirection.Both, GremlinDirection.Both);
                case "outV": return Endpoint(s, GremlinDirection.Out);
                case "inV": return Endpoint(s, GremlinDirection.In);
                case "bothV": return Endpoint(s, GremlinDirection.Both);
                case "otherV": return Endpoint(s, s.OtherEnd);
                case "properties": return new GremlinElementState(GremlinElementKind.Property, Owner: s);
                case "element":
                    return s.Kind == GremlinElementKind.Property && s.Owner != null ? s.Owner : GremlinElementState.Unknown;
                case "fold": return new GremlinElementState(GremlinElementKind.Value, Owner: s);
                case "unfold":
                    return s.Kind == GremlinElementKind.Value && s.Owner != null ? s.Owner : GremlinElementState.Unknown;
                case "as":
                    foreach (var name in labels) _stepLabels[name] = s;
                    return s;
                case "select":
                    if (labels.Count == 1 && _stepLabels.TryGetValue(labels[0], out var selected)) return selected;
                    // select('a','b') yields a map; an undeclared single key may come from a side effect.
                    return labels.Count > 1 ? GremlinElementState.Value : GremlinElementState.Unknown;
                case "optional":
                    return Merge([s, .. frame.InnerStates]);
                case "choose":
                    return Merge(frame.InnerStates.Count > 1 ? frame.InnerStates.Skip(1).ToList() : [s]);
                case "repeat":
                    // Any number of iterations: labels are not predictable, only the element kind.
                    if (frame.InnerStates.Count == 0) return s;
                    var loop = frame.InnerStates[^1];
                    return loop.Kind == s.Kind && s.Kind is GremlinElementKind.Vertex or GremlinElementKind.Edge
                        ? new GremlinElementState(s.Kind)
                        : GremlinElementState.Unknown;
                case "local" or "map" or "flatMap" or "union" or "coalesce":
                    return frame.InnerStates.Count == 0 ? s : Merge(frame.InnerStates);
            }

            if (SameStateSteps.Contains(frame.Name) || Modulators.Contains(frame.Name)) return s;
            return frame.Name switch
            {
                "values" or "valueMap" or "elementMap" or "propertyMap" or "id" or "label" or "key" or "value"
                    or "count" or "sum" or "min" or "max" or "mean" or "group" or "groupCount" or "project"
                    or "path" or "tree" or "constant" or "math" or "loops" or "index" => GremlinElementState.Value,
                _ => GremlinElementState.Unknown
            };
        }

        private GremlinElementState NarrowLabels(GremlinElementState s, IReadOnlyList<string> labels)
        {
            var kind = s.Kind;
            if (kind is not (GremlinElementKind.Vertex or GremlinElementKind.Edge))
            {
                var allVertex = labels.All(schema.VertexLabels.ContainsKey);
                var allEdge = labels.All(schema.EdgeLabels.ContainsKey);
                if (allVertex == allEdge) return s;
                kind = allVertex ? GremlinElementKind.Vertex : GremlinElementKind.Edge;
            }

            var narrowed = s.Labels == null ? ToSet(labels)! : ToSet(s.Labels.Intersect(labels))!;
            return s with { Kind = kind, Labels = narrowed };
        }

        private GremlinElementState Adjacent(GremlinElementState s, IReadOnlyList<string> edgeLabels, GremlinDirection direction)
        {
            if (s.Kind is not (GremlinElementKind.Vertex or GremlinElementKind.Unknown)) return GremlinElementState.Unknown;
            if (s.Labels == null && edgeLabels.Count == 0) return GremlinElementState.AnyVertex;
            var labels = schema.GetAdjacentVertexLabels(s.Labels, edgeLabels, direction);
            return new GremlinElementState(GremlinElementKind.Vertex, labels);
        }

        private GremlinElementState Incident(GremlinElementState s, IReadOnlyList<string> edgeLabels, GremlinDirection direction, GremlinDirection otherEnd)
        {
            if (s.Kind is not (GremlinElementKind.Vertex or GremlinElementKind.Unknown)) return GremlinElementState.Unknown;
            IReadOnlySet<string>? labels = edgeLabels.Count > 0
                ? ToSet(edgeLabels)
                : schema.GetIncidentEdgeLabels(s.Labels, direction);
            return new GremlinElementState(GremlinElementKind.Edge, labels, OtherEnd: otherEnd);
        }

        private GremlinElementState Endpoint(GremlinElementState s, GremlinDirection end)
        {
            if (s.Kind is not (GremlinElementKind.Edge or GremlinElementKind.Unknown)) return GremlinElementState.Unknown;
            return new GremlinElementState(GremlinElementKind.Vertex, schema.GetEdgeEndpointLabels(s.Labels, end));
        }
    }
}
