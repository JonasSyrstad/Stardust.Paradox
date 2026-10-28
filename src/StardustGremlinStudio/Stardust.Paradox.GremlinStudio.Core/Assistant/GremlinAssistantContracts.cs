using System.Text;
using System.Text.RegularExpressions;
using Stardust.Paradox.GremlinStudio.Core.Completion;

namespace Stardust.Paradox.GremlinStudio.Core.Assistant;

/// <summary>Role of a chat message.</summary>
public enum GremlinAssistantRole
{
    User,
    Assistant
}

/// <summary>A single chat message exchanged with the assistant.</summary>
public sealed record GremlinAssistantMessage(GremlinAssistantRole Role, string Content);

/// <summary>
/// Graph context sent with assistant requests so generated queries match the user's graph.
/// Contains labels and property keys only; no vertex/edge data.
/// </summary>
public sealed record GremlinAssistantContext(
    string? ConnectionName,
    bool IsCosmosDb,
    string? CurrentQuery,
    IReadOnlyList<string> VertexLabels,
    IReadOnlyList<string> EdgeLabels,
    GremlinSchemaSample Schema);

/// <summary>A model offered by the assistant backend.</summary>
public sealed record GremlinAssistantModel(string Id, string Name)
{
    public override string ToString() => Name;
}

/// <summary>A request for the next assistant turn in a conversation.</summary>
/// <param name="SessionId">Backend session to continue, or null to start a new one.</param>
/// <param name="Prompt">The new user message.</param>
/// <param name="History">Earlier messages, replayed when the backend session cannot be resumed.</param>
/// <param name="Context">Current graph context.</param>
/// <param name="Model">Model id, or null for the backend default.</param>
public sealed record GremlinAssistantRequest(
    string? SessionId,
    string Prompt,
    IReadOnlyList<GremlinAssistantMessage> History,
    GremlinAssistantContext Context,
    string? Model);

/// <summary>The assistant reply and the backend session that produced it.</summary>
public sealed record GremlinAssistantReply(string SessionId, string Content);

/// <summary>Kind of GitHub issue the assistant can file.</summary>
public enum GremlinAssistantIssueKind
{
    Bug,
    Suggestion,
    Feedback
}

/// <summary>
/// Actions the assistant may perform in the host application. Implementations are responsible for
/// asking the user for confirmation where required (database writes, filing issues).
/// </summary>
public interface IGremlinAssistantToolHost
{
    /// <summary>Executes a Gremlin query on the active connection and returns a textual result for the model.</summary>
    Task<string> ExecuteQueryAsync(string query, CancellationToken cancellationToken);

    /// <summary>Files a GitHub issue after user confirmation and returns a textual result for the model.</summary>
    Task<string> CreateIssueAsync(GremlinAssistantIssueKind kind, string title, string body, CancellationToken cancellationToken);
}

/// <summary>Generates Gremlin queries and performs actions from natural language.</summary>
public interface IGremlinAssistantService
{
    /// <summary>Whether a GitHub token is stored for the assistant.</summary>
    Task<bool> HasPersonalAccessTokenAsync(CancellationToken cancellationToken = default);

    /// <summary>Stores the GitHub token, or removes it when empty.</summary>
    Task SetPersonalAccessTokenAsync(string? token, CancellationToken cancellationToken = default);

    /// <summary>Lists the models available to the user.</summary>
    Task<IReadOnlyList<GremlinAssistantModel>> ListModelsAsync(CancellationToken cancellationToken = default);

    /// <summary>Sends the next message of a conversation and returns the reply.</summary>
    /// <param name="progress">Optional receiver of reply text chunks as they are streamed.</param>
    Task<GremlinAssistantReply> AskAsync(
        GremlinAssistantRequest request,
        IGremlinAssistantToolHost toolHost,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>Deletes the backend session of a conversation.</summary>
    Task DeleteSessionAsync(string sessionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reviews a query for problems on the target server and optionally proposes an equivalent, more efficient query.
    /// Runs in a throw-away session without tools.
    /// </summary>
    Task<GremlinQueryReview> ReviewQueryAsync(GremlinQueryReviewRequest request, CancellationToken cancellationToken = default);

    /// <summary>Explains what a query does, step by step. Runs in a throw-away session without tools.</summary>
    Task<string> ExplainQueryAsync(GremlinQueryReviewRequest request, CancellationToken cancellationToken = default);
}

/// <summary>A request to review the query in the editor.</summary>
/// <param name="Query">The query text.</param>
/// <param name="ServerKind">The kind of Gremlin server the query targets.</param>
/// <param name="Context">Graph context (labels and property keys).</param>
/// <param name="Model">Model id, or null for the backend default.</param>
public sealed record GremlinQueryReviewRequest(
    string Query,
    Connections.GremlinConnectionKind ServerKind,
    GremlinAssistantContext Context,
    string? Model);

/// <summary>Severity of a query review finding.</summary>
public enum GremlinQueryReviewSeverity
{
    Info,
    Warning,
    Error
}

/// <summary>A single problem found when reviewing a query.</summary>
public sealed record GremlinQueryReviewIssue(GremlinQueryReviewSeverity Severity, string Message);

/// <summary>Result of an assistant query review.</summary>
/// <param name="Issues">Problems found; empty when the query looks fine.</param>
/// <param name="OptimizedQuery">An equivalent, more efficient query, or null when none is proposed with high confidence.</param>
/// <param name="OptimizationReason">Why the optimized query is better.</param>
public sealed record GremlinQueryReview(
    IReadOnlyList<GremlinQueryReviewIssue> Issues,
    string? OptimizedQuery,
    string? OptimizationReason)
{
    /// <summary>A review without findings.</summary>
    public static GremlinQueryReview Empty { get; } = new([], null, null);

    /// <summary>Whether the review has anything to show.</summary>
    public bool HasFindings => Issues.Count > 0 || OptimizedQuery is not null;
}

/// <summary>Prompt building and reply parsing for query reviews.</summary>
public static class GremlinQueryReviewPrompt
{
    /// <summary>Minimum self-reported confidence required to surface an optimization.</summary>
    public const double MinOptimizationConfidence = 0.9;

    /// <summary>Fixed instructions for the reviewer.</summary>
    public static string BuildInstructions() =>
        """
        You are a strict Gremlin (Apache TinkerPop) query reviewer embedded in Stardust Gremlin Studio.
        Review the user's query for the target server and reply with ONLY a JSON object, no markdown, in this shape:
        {"issues":[{"severity":"error|warning|info","message":"..."}],"optimizedQuery":null,"optimizationReason":null,"confidence":0.0}
        Rules:
        - Report only real problems: syntax errors, steps or features unsupported by the target server, labels or property keys that do not exist in the schema, unbounded traversals that are likely very expensive, and mistakes that make the query not do what it obviously intends.
        - Do not report style preferences. When the query is fine, return an empty issues array.
        - Keep each message to one short sentence that names the step involved.
        - Only set optimizedQuery when you are highly confident that it returns exactly the same results as the original (same elements, same order when ordered, same shape) and is clearly cheaper on the target server. Never change the intent, filters, limits or output shape. Never introduce mutations.
        - Set confidence (0.0-1.0) to how sure you are the optimized query is equivalent and better. If below 0.9, set optimizedQuery to null.
        - optimizationReason is one short sentence, or null.
        """;

    /// <summary>Builds the user message for a review.</summary>
    public static string BuildPrompt(GremlinQueryReviewRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var sb = new StringBuilder();
        sb.AppendLine("<target-server>").AppendLine(DescribeServer(request.ServerKind)).AppendLine("</target-server>");
        sb.AppendLine("<graph-context>").Append(GremlinAssistantPrompt.BuildContext(request.Context with { CurrentQuery = null })).AppendLine("</graph-context>");
        sb.AppendLine("<query>").AppendLine(request.Query.Trim()).AppendLine("</query>");
        return sb.ToString();
    }

    /// <summary>Describes the execution characteristics of a server kind.</summary>
    public static string DescribeServer(Connections.GremlinConnectionKind kind) => kind switch
    {
        Connections.GremlinConnectionKind.CosmosDb =>
            "Azure Cosmos DB Gremlin API. Unsupported: lambdas/closures, match(), sack(), subgraph(), io(), program(), " +
            "math() in older accounts, withComputer(), tail() with scope local, and most strategies (withStrategies). " +
            "Execution is breadth-first: each step is evaluated for all traversers before the next, so a late limit() does not stop earlier steps from fetching everything; " +
            "put limit()/has() filters as early as possible. Cost is measured in RUs; filtering on the partition key and on id is cheapest; " +
            "start traversals with g.V() filters on indexed properties instead of scanning. Ids are strings.",
        Connections.GremlinConnectionKind.InMemory =>
            "Stardust in-memory Gremlin engine used for testing (TinkerPop semantics, depth-first lazy evaluation). " +
            "It supports a common subset of TinkerPop steps; lambdas, match(), sack(), subgraph(), io() and program() are not supported.",
        _ =>
            "Apache TinkerPop Gremlin Server (e.g. TinkerGraph, JanusGraph, Neptune). Standard OLTP execution is lazy and depth-first: " +
            "traversers are pulled one at a time, so an early limit() short-circuits work. Lambdas may be disabled by the server.",
    };

    /// <summary>Parses a reviewer reply. Returns <see cref="GremlinQueryReview.Empty"/> for unparseable replies.</summary>
    public static GremlinQueryReview ParseReply(string? reply, string originalQuery)
    {
        if (string.IsNullOrWhiteSpace(reply))
            return GremlinQueryReview.Empty;
        var start = reply.IndexOf('{');
        var end = reply.LastIndexOf('}');
        if (start < 0 || end <= start)
            return GremlinQueryReview.Empty;

        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(reply[start..(end + 1)]);
            var root = doc.RootElement;
            var issues = new List<GremlinQueryReviewIssue>();
            if (root.TryGetProperty("issues", out var arr) && arr.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                foreach (var item in arr.EnumerateArray())
                {
                    var message = GetString(item, "message");
                    if (string.IsNullOrWhiteSpace(message))
                        continue;
                    var severity = GetString(item, "severity")?.ToLowerInvariant() switch
                    {
                        "error" => GremlinQueryReviewSeverity.Error,
                        "warning" => GremlinQueryReviewSeverity.Warning,
                        _ => GremlinQueryReviewSeverity.Info
                    };
                    issues.Add(new GremlinQueryReviewIssue(severity, message.Trim()));
                }
            }

            var optimized = GetString(root, "optimizedQuery")?.Trim();
            var confidence = root.TryGetProperty("confidence", out var c) && c.ValueKind == System.Text.Json.JsonValueKind.Number
                ? c.GetDouble()
                : 0;
            if (string.IsNullOrWhiteSpace(optimized)
                || confidence < MinOptimizationConfidence
                || !optimized.StartsWith("g.", StringComparison.Ordinal)
                || string.Equals(Normalize(optimized), Normalize(originalQuery), StringComparison.Ordinal)
                || (GremlinMutationDetector.IsMutation(optimized) && !GremlinMutationDetector.IsMutation(originalQuery)))
            {
                optimized = null;
            }

            return new GremlinQueryReview(issues, optimized, optimized is null ? null : GetString(root, "optimizationReason")?.Trim());
        }
        catch (System.Text.Json.JsonException)
        {
            return GremlinQueryReview.Empty;
        }
    }

    private static string? GetString(System.Text.Json.JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == System.Text.Json.JsonValueKind.String ? value.GetString() : null;

    private static string Normalize(string query) => Regex.Replace(query, @"\s+", string.Empty);
}

/// <summary>Prompt building and reply parsing shared by assistant backends.</summary>
public static partial class GremlinAssistantPrompt
{
    private const int MaxPropertyKeysPerLabel = 30;
    private const int MaxEdgeConnections = 100;

    /// <summary>Extracts the first Gremlin query from a fenced code block in a reply, or null when there is none.</summary>
    public static string? ExtractQuery(string? reply)
    {
        if (string.IsNullOrWhiteSpace(reply))
            return null;
        var match = CodeBlockRegex().Match(reply);
        return match.Success ? match.Groups["code"].Value.Trim() : null;
    }

    /// <summary>Fixed instructions for the assistant.</summary>
    public static string BuildInstructions()
    {
        var sb = new StringBuilder();
        sb.AppendLine("You are a Gremlin (Apache TinkerPop) query assistant embedded in Stardust Gremlin Studio.");
        sb.AppendLine("Write correct, efficient Gremlin queries that start with 'g.' using the graph schema provided with the user's messages.");
        sb.AppendLine("When you suggest a query, put it in a single fenced code block marked ```gremlin, followed by a short explanation.");
        sb.AppendLine("Only use labels and property keys from the schema unless the user explicitly names others.");
        sb.AppendLine("Prefer read-only queries; only produce mutating queries (addV, addE, property, drop) when explicitly asked.");
        sb.AppendLine("You can run queries with the execute_gremlin_query tool when the user asks for data or results. Use limit() on exploratory reads.");
        sb.AppendLine("Mutating queries require the user's confirmation, which the application asks for; if declined, do not retry.");
        sb.AppendLine("You can file a bug, suggestion or feedback for Gremlin Studio with the create_github_issue tool when the user asks for it.");
        sb.AppendLine("Write a clear title and a markdown body. The user reviews and confirms the issue before it is created.");
        return sb.ToString();
    }

    /// <summary>Describes the current graph context for the model.</summary>
    public static string BuildContext(GremlinAssistantContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var sb = new StringBuilder();
        if (context.IsCosmosDb)
            sb.AppendLine("The target is Azure Cosmos DB Gremlin API: avoid unsupported steps such as lambdas, match(), sack(), subgraph(), io(), and program().");

        sb.Append("Connection: ").AppendLine(string.IsNullOrWhiteSpace(context.ConnectionName) ? "(none)" : context.ConnectionName);

        sb.AppendLine("Vertex labels:");
        AppendLabels(sb, context.VertexLabels, context.Schema.VertexPropertyKeys);
        sb.AppendLine("Edge labels:");
        AppendLabels(sb, context.EdgeLabels, context.Schema.EdgePropertyKeys);

        if (context.Schema.EdgeConnections.Count > 0)
        {
            sb.AppendLine("Edge topology (out)-[edge]->(in):");
            foreach (var c in context.Schema.EdgeConnections.Take(MaxEdgeConnections))
                sb.Append("- (").Append(c.OutVertexLabel).Append(")-[").Append(c.EdgeLabel).Append("]->(").Append(c.InVertexLabel).AppendLine(")");
        }

        if (!string.IsNullOrWhiteSpace(context.CurrentQuery))
        {
            sb.AppendLine("Query currently in the editor:");
            sb.AppendLine("```gremlin").AppendLine(context.CurrentQuery.Trim()).AppendLine("```");
        }

        return sb.ToString();
    }

    private static void AppendLabels(StringBuilder sb, IReadOnlyList<string> labels, IReadOnlyDictionary<string, IReadOnlyCollection<string>> keys)
    {
        var all = labels.Union(keys.Keys).Distinct(StringComparer.Ordinal).ToList();
        if (all.Count == 0)
        {
            sb.AppendLine("- (unknown)");
            return;
        }

        foreach (var label in all)
        {
            sb.Append("- ").Append(label);
            if (keys.TryGetValue(label, out var props) && props.Count > 0)
                sb.Append(" { ").Append(string.Join(", ", props.Take(MaxPropertyKeysPerLabel))).Append(" }");
            sb.AppendLine();
        }
    }

    [GeneratedRegex(@"```(?:gremlin|groovy)?[ \t]*\r?\n(?<code>.*?)```", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex CodeBlockRegex();
}

/// <summary>Detects Gremlin queries that modify the graph.</summary>
public static partial class GremlinMutationDetector
{
    /// <summary>Whether the query contains a step that writes to or deletes from the graph.</summary>
    public static bool IsMutation(string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return false;
        // Ignore step names that only appear inside string literals, e.g. has('name', 'drop()').
        var withoutStrings = StringLiteralRegex().Replace(query, "''");
        return MutatingStepRegex().IsMatch(withoutStrings);
    }

    [GeneratedRegex(@"'(?:[^'\\]|\\.)*'|""(?:[^""\\]|\\.)*""", RegexOptions.Singleline)]
    private static partial Regex StringLiteralRegex();

    [GeneratedRegex(@"(?<![\w$])(?:addV|addE|drop|property|mergeV|mergeE|io)\s*\(", RegexOptions.CultureInvariant)]
    private static partial Regex MutatingStepRegex();
}
