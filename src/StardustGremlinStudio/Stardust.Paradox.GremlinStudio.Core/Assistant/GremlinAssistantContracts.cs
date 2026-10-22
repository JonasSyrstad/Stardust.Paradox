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
