using System.Text;
using GitHub.Copilot;
using GitHub.Copilot.Rpc;
using Microsoft.Extensions.Logging;
using Stardust.Paradox.GremlinStudio.Core.Connections;

namespace Stardust.Paradox.GremlinStudio.Core.Assistant;

/// <summary>
/// <see cref="IGremlinAssistantService"/> backed by the GitHub Copilot SDK, which runs the Copilot CLI runtime
/// and uses the user's Copilot subscription (CLI login, or an optional personal access token).
/// </summary>
public sealed class CopilotGremlinAssistantService : IGremlinAssistantService, IAsyncDisposable
{
    private const string PersonalAccessTokenSecretKey = "github-copilot-token";

    private readonly ISecureSecretStore _secretStore;
    private readonly ILogger<CopilotGremlinAssistantService> _logger;
    private readonly SemaphoreSlim _clientLock = new(1, 1);
    private CopilotClient? _client;
    private string? _clientToken;

    public CopilotGremlinAssistantService(ISecureSecretStore secretStore, ILogger<CopilotGremlinAssistantService> logger)
    {
        _secretStore = secretStore ?? throw new ArgumentNullException(nameof(secretStore));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    // The Copilot runtime authenticates with the signed-in Copilot CLI user when no token is stored.
    public Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);

    public async Task<bool> HasPersonalAccessTokenAsync(CancellationToken cancellationToken = default) =>
        !string.IsNullOrEmpty(await _secretStore.GetSecretAsync(PersonalAccessTokenSecretKey, cancellationToken).ConfigureAwait(false));

    public async Task SetPersonalAccessTokenAsync(string? token, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token))
            await _secretStore.DeleteSecretAsync(PersonalAccessTokenSecretKey, cancellationToken).ConfigureAwait(false);
        else
            await _secretStore.StoreSecretAsync(PersonalAccessTokenSecretKey, token.Trim(), cancellationToken).ConfigureAwait(false);
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
        if (conversation.Count == 0)
            throw new ArgumentException("Conversation is empty.", nameof(conversation));

        var client = await GetClientAsync(cancellationToken).ConfigureAwait(false);

        var config = new SessionConfig
        {
            SystemMessage = new SystemMessageConfig
            {
                Mode = SystemMessageMode.Replace,
                Content = GitHubModelsGremlinAssistantService.BuildSystemPrompt(context)
            },
            // Pure chat: the assistant must not run shell, file or other agent tools on the user's machine.
            AvailableTools = [],
            Streaming = true,
#pragma warning disable GHCP001
            OnPermissionRequest = (_, _) => Task.FromResult(PermissionDecision.Reject("Gremlin Studio assistant does not allow tool use."))
#pragma warning restore GHCP001 // PermissionDecision is an evaluation API
        };
        // GitHub Models ids ("openai/...") from older settings are not Copilot ids; fall back to the runtime default.
        if (!string.IsNullOrWhiteSpace(model) && !model.Contains('/'))
            config.Model = model;

        await using var session = await client.CreateSessionAsync(config, cancellationToken).ConfigureAwait(false);

        var reply = new StringBuilder();
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var subscription = session.On<SessionEvent>(evt =>
        {
            switch (evt)
            {
                case AssistantMessageDeltaEvent delta when !string.IsNullOrEmpty(delta.Data.DeltaContent):
                    progress?.Report(delta.Data.DeltaContent);
                    break;
                case AssistantMessageEvent msg:
                    lock (reply)
                        reply.Append(msg.Data.Content);
                    break;
                case SessionErrorEvent err:
                    done.TrySetException(new InvalidOperationException($"GitHub Copilot error: {err.Data.Message}"));
                    break;
                case SessionIdleEvent:
                    done.TrySetResult();
                    break;
            }
        });
        await using var registration = cancellationToken.Register(() => done.TrySetCanceled(cancellationToken));

        await session.SendAsync(new MessageOptions { Prompt = BuildPrompt(conversation) }, cancellationToken).ConfigureAwait(false);
        await done.Task.ConfigureAwait(false);

        string content;
        lock (reply)
            content = reply.ToString().Trim();
        if (string.IsNullOrWhiteSpace(content))
            throw new InvalidOperationException("The assistant returned an empty response.");
        return content;
    }

    // Each request uses a fresh session, so earlier turns are replayed as a transcript.
    private static string BuildPrompt(IReadOnlyList<GremlinAssistantMessage> conversation)
    {
        if (conversation.Count == 1)
            return conversation[0].Content;

        var sb = new StringBuilder("Conversation so far:").AppendLine();
        foreach (var m in conversation.Take(conversation.Count - 1))
            sb.AppendLine().Append(m.Role == GremlinAssistantRole.User ? "User: " : "Assistant: ").AppendLine(m.Content);
        sb.AppendLine().Append("Current request: ").Append(conversation[^1].Content);
        return sb.ToString();
    }

    private async Task<CopilotClient> GetClientAsync(CancellationToken cancellationToken)
    {
        var token = await _secretStore.GetSecretAsync(PersonalAccessTokenSecretKey, cancellationToken).ConfigureAwait(false);
        await _clientLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_client is not null && string.Equals(_clientToken, token, StringComparison.Ordinal))
                return _client;

            if (_client is not null)
                await _client.DisposeAsync().ConfigureAwait(false);

            var options = new CopilotClientOptions { Logger = _logger };
            if (!string.IsNullOrEmpty(token))
                options.GitHubToken = token;

            var client = new CopilotClient(options);
            try
            {
                await client.StartAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                await client.DisposeAsync().ConfigureAwait(false);
                _logger.LogWarning(ex, "Failed to start the GitHub Copilot runtime");
                throw new InvalidOperationException(
                    "Could not start GitHub Copilot. Sign in with the Copilot CLI ('copilot' then '/login') or add a GitHub token with Copilot access in the account menu.", ex);
            }

            _client = client;
            _clientToken = token;
            return client;
        }
        finally
        {
            _clientLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_client is not null)
            await _client.DisposeAsync().ConfigureAwait(false);
        _clientLock.Dispose();
    }
}
