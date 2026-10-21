using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Stardust.Paradox.GremlinStudio.Core.Completion;
using Stardust.Paradox.GremlinStudio.Core.Connections;
using Stardust.Paradox.GremlinStudio.Core.GitHub;

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
/// Graph context sent with each assistant request so generated queries match the user's graph.
/// Contains labels and property keys only; no vertex/edge data.
/// </summary>
public sealed record GremlinAssistantContext(
    string? ConnectionName,
    bool IsCosmosDb,
    string? CurrentQuery,
    IReadOnlyList<string> VertexLabels,
    IReadOnlyList<string> EdgeLabels,
    GremlinSchemaSample Schema);

/// <summary>Generates Gremlin queries from natural language.</summary>
public interface IGremlinAssistantService
{
    /// <summary>Whether a personal access token or GitHub session is available for the assistant.</summary>
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);

    /// <summary>Whether a GitHub Models personal access token is stored.</summary>
    Task<bool> HasPersonalAccessTokenAsync(CancellationToken cancellationToken = default);

    /// <summary>Stores the GitHub Models personal access token, or removes it when empty.</summary>
    Task SetPersonalAccessTokenAsync(string? token, CancellationToken cancellationToken = default);

    /// <summary>Sends the conversation to the model and returns the assistant reply.</summary>
    /// <param name="progress">Optional receiver of reply chunks as they are streamed.</param>
    Task<string> AskAsync(
        IReadOnlyList<GremlinAssistantMessage> conversation,
        GremlinAssistantContext context,
        string model,
        CancellationToken cancellationToken = default,
        IProgress<string>? progress = null);
}

/// <summary>
/// <see cref="IGremlinAssistantService"/> backed by GitHub Models, authenticated with the signed-in GitHub account.
/// </summary>
public sealed partial class GitHubModelsGremlinAssistantService : IGremlinAssistantService
{
    private const string Endpoint = "https://models.github.ai/inference/chat/completions";
    private const int MaxPropertyKeysPerLabel = 30;
    private const int MaxEdgeConnections = 100;
    private const string PersonalAccessTokenSecretKey = "github-models-token";

    private readonly HttpClient _httpClient;
    private readonly IGitHubAccountService _gitHub;
    private readonly ISecureSecretStore _secretStore;
    private readonly ILogger<GitHubModelsGremlinAssistantService> _logger;

    public GitHubModelsGremlinAssistantService(
        HttpClient httpClient,
        IGitHubAccountService gitHub,
        ISecureSecretStore secretStore,
        ILogger<GitHubModelsGremlinAssistantService> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _gitHub = gitHub ?? throw new ArgumentNullException(nameof(gitHub));
        _secretStore = secretStore ?? throw new ArgumentNullException(nameof(secretStore));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        if (_httpClient.DefaultRequestHeaders.UserAgent.Count == 0)
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("GremlinStudio/1.0");
    }

    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default) =>
        !string.IsNullOrEmpty(await ResolveTokenAsync(cancellationToken).ConfigureAwait(false));

    public async Task<bool> HasPersonalAccessTokenAsync(CancellationToken cancellationToken = default) =>
        !string.IsNullOrEmpty(await _secretStore.GetSecretAsync(PersonalAccessTokenSecretKey, cancellationToken).ConfigureAwait(false));

    public async Task SetPersonalAccessTokenAsync(string? token, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token))
            await _secretStore.DeleteSecretAsync(PersonalAccessTokenSecretKey, cancellationToken).ConfigureAwait(false);
        else
            await _secretStore.StoreSecretAsync(PersonalAccessTokenSecretKey, token.Trim(), cancellationToken).ConfigureAwait(false);
    }

    // A personal access token (models:read) is preferred: the GitHub OAuth app token is not accepted by GitHub Models.
    private async Task<string?> ResolveTokenAsync(CancellationToken cancellationToken)
    {
        var pat = await _secretStore.GetSecretAsync(PersonalAccessTokenSecretKey, cancellationToken).ConfigureAwait(false);
        return string.IsNullOrEmpty(pat) ? _gitHub.AccessToken : pat;
    }

    public async Task<string> AskAsync(
        IReadOnlyList<GremlinAssistantMessage> conversation,
        GremlinAssistantContext context,
        string model,
        CancellationToken cancellationToken = default,
        IProgress<string>? progress = null)
    {
        ArgumentNullException.ThrowIfNull(conversation);
        ArgumentNullException.ThrowIfNull(context);
        if (string.IsNullOrWhiteSpace(model))
            throw new ArgumentException("Model is required.", nameof(model));

        var usesPersonalAccessToken = await HasPersonalAccessTokenAsync(cancellationToken).ConfigureAwait(false);
        var token = await ResolveTokenAsync(cancellationToken).ConfigureAwait(false)
                    ?? throw new InvalidOperationException("Add a GitHub personal access token or sign in with GitHub to use the Gremlin assistant.");

        var messages = new List<ChatMessage> { new("system", BuildSystemPrompt(context)) };
        messages.AddRange(conversation.Select(m =>
            new ChatMessage(m.Role == GremlinAssistantRole.User ? "user" : "assistant", m.Content)));

        var payload = System.Text.Json.JsonSerializer.Serialize(new ChatRequest(model, messages, 0.2));
        using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");

        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            _logger.LogWarning("GitHub Models request failed with {StatusCode}: {Error}", response.StatusCode, error);
            throw new InvalidOperationException(response.StatusCode switch
            {
                HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden =>
                    "GitHub Models rejected the token. Use a personal access token with the 'Models: read' permission (account menu, AI assistant section).",
                HttpStatusCode.TooManyRequests =>
                    "GitHub Models rate limit reached. Please wait a moment and try again.",
                _ => $"GitHub Models request failed ({(int)response.StatusCode})."
            });
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        ChatResponse? dto;
        try
        {
            dto = System.Text.Json.JsonSerializer.Deserialize<ChatResponse>(body);
        }
        catch (System.Text.Json.JsonException ex)
        {
            var snippet = body.Length > 300 ? body[..300] + "…" : body;
            _logger.LogWarning(ex, "GitHub Models returned a non-JSON response ({StatusCode}, {ContentType}): {Body}",
                response.StatusCode, response.Content.Headers.ContentType?.MediaType, snippet);
            // A 200 text/plain "OK" does not come from GitHub Models itself; it has been observed from TLS-inspecting
            // proxies (e.g. Zscaler) that block AI endpoints and answer with a canned response.
            if (string.Equals(body.Trim(), "OK", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(
                    "The request to models.github.ai was intercepted before reaching GitHub (it returned a plain \"OK\"). " +
                    "A corporate proxy or firewall (e.g. Zscaler) is likely blocking GitHub Models; ask your IT department to allow models.github.ai or try another network.", ex);
            if (!usesPersonalAccessToken)
                throw new InvalidOperationException(
                    "GitHub Models did not accept the GitHub sign-in token. Add a personal access token with the 'Models: read' permission in the account menu (AI assistant section).", ex);
            throw new InvalidOperationException($"GitHub Models returned an unexpected response: {snippet.Trim()}", ex);
        }

        var content = dto?.Choices?.FirstOrDefault()?.Message?.Content;
        if (string.IsNullOrWhiteSpace(content))
            throw new InvalidOperationException("The assistant returned an empty response.");
        return content.Trim();
    }

    /// <summary>
    /// Extracts the first Gremlin query from a fenced code block in an assistant reply, or null when there is none.
    /// </summary>
    public static string? ExtractQuery(string? reply)
    {
        if (string.IsNullOrWhiteSpace(reply))
            return null;
        var match = CodeBlockRegex().Match(reply);
        return match.Success ? match.Groups["code"].Value.Trim() : null;
    }

    internal static string BuildSystemPrompt(GremlinAssistantContext context)
    {
        var sb = new StringBuilder();
        sb.AppendLine("You are a Gremlin (Apache TinkerPop) query assistant embedded in Stardust Gremlin Studio.");
        sb.AppendLine("Write correct, efficient Gremlin queries that start with 'g.' using the graph schema below.");
        sb.AppendLine("Always put the query in a single fenced code block marked ```gremlin, followed by a short explanation.");
        sb.AppendLine("Only use labels and property keys from the schema unless the user explicitly names others.");
        sb.AppendLine("Prefer read-only queries; only produce mutating queries (addV, addE, property, drop) when explicitly asked.");
        if (context.IsCosmosDb)
            sb.AppendLine("The target is Azure Cosmos DB Gremlin API: avoid unsupported steps such as lambdas, match(), sack(), subgraph(), io(), and program().");

        if (!string.IsNullOrWhiteSpace(context.ConnectionName))
            sb.AppendLine().Append("Connection: ").AppendLine(context.ConnectionName);

        sb.AppendLine().AppendLine("Vertex labels:");
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
            sb.AppendLine().AppendLine("Query currently in the editor:");
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

    private sealed record ChatMessage(
        [property: JsonPropertyName("role")] string Role,
        [property: JsonPropertyName("content")] string Content);

    private sealed record ChatRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("messages")] IReadOnlyList<ChatMessage> Messages,
        [property: JsonPropertyName("temperature")] double Temperature);

    private sealed record ChatResponse([property: JsonPropertyName("choices")] IReadOnlyList<ChatChoice>? Choices);

    private sealed record ChatChoice([property: JsonPropertyName("message")] ChatMessage? Message);
}
