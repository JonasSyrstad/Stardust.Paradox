using System.ComponentModel;
using System.Text;
using GitHub.Copilot;
using GitHub.Copilot.Rpc;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Stardust.Paradox.GremlinStudio.Core.Connections;

namespace Stardust.Paradox.GremlinStudio.Core.Assistant;

/// <summary>
/// <see cref="IGremlinAssistantService"/> backed by the GitHub Copilot SDK, which runs the Copilot CLI runtime
/// and uses the user's Copilot subscription (CLI login, or an optional personal access token).
/// </summary>
public sealed partial class CopilotGremlinAssistantService : IGremlinAssistantService, IAsyncDisposable
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

	public async Task<bool> HasPersonalAccessTokenAsync(CancellationToken cancellationToken = default) =>
		!string.IsNullOrEmpty(await _secretStore.GetSecretAsync(PersonalAccessTokenSecretKey, cancellationToken).ConfigureAwait(false));

	public async Task SetPersonalAccessTokenAsync(string? token, CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrWhiteSpace(token))
			await _secretStore.DeleteSecretAsync(PersonalAccessTokenSecretKey, cancellationToken).ConfigureAwait(false);
		else
			await _secretStore.StoreSecretAsync(PersonalAccessTokenSecretKey, token.Trim(), cancellationToken).ConfigureAwait(false);
	}

	public async Task<IReadOnlyList<GremlinAssistantModel>> ListModelsAsync(CancellationToken cancellationToken = default)
	{
		var client = await GetClientAsync(cancellationToken).ConfigureAwait(false);
		var models = await client.ListModelsAsync(cancellationToken).ConfigureAwait(false);
		return models
			.Where(m => m.Policy is null || !string.Equals(m.Policy.State, "disabled", StringComparison.OrdinalIgnoreCase))
			.Select(m => new GremlinAssistantModel(m.Id, string.IsNullOrWhiteSpace(m.Name) ? m.Id : m.Name))
			.ToList();
	}

	public async Task DeleteSessionAsync(string sessionId, CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
		var client = await GetClientAsync(cancellationToken).ConfigureAwait(false);
		await client.DeleteSessionAsync(sessionId, cancellationToken).ConfigureAwait(false);
	}

	public async Task<GremlinAssistantReply> AskAsync(
		GremlinAssistantRequest request,
		IGremlinAssistantToolHost toolHost,
		IProgress<string>? progress = null,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(request);
		ArgumentNullException.ThrowIfNull(toolHost);
		ArgumentException.ThrowIfNullOrWhiteSpace(request.Prompt);

		var client = await GetClientAsync(cancellationToken).ConfigureAwait(false);
		var tools = CreateTools(toolHost, cancellationToken);
		// GitHub Models ids ("openai/...") from older settings are not Copilot ids; fall back to the runtime default.
		var model = !string.IsNullOrWhiteSpace(request.Model) && !request.Model.Contains('/') ? request.Model : null;

		CopilotSession? session = null;
		var resumed = false;
		if (!string.IsNullOrWhiteSpace(request.SessionId))
		{
			try
			{
				var resume = new ResumeSessionConfig();
				Configure(resume, tools, model);
				session = await client.ResumeSessionAsync(request.SessionId, resume, cancellationToken).ConfigureAwait(false);
				resumed = true;
			}
			catch (Exception ex) when (ex is not OperationCanceledException)
			{
				_logger.LogInformation(ex, "Could not resume Copilot session {SessionId}; starting a new one", request.SessionId);
			}
		}

		if (session is null)
		{
			var config = new SessionConfig();
			Configure(config, tools, model);
			session = await client.CreateSessionAsync(config, cancellationToken).ConfigureAwait(false);
		}

		await using (session.ConfigureAwait(false))
		{
			var reply = new StringBuilder();
			var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
			using var subscription = session.On<SessionEvent>(evt =>
			{
				switch (evt)
				{
					case AssistantMessageDeltaEvent delta when !string.IsNullOrEmpty(delta.Data.DeltaContent):
						progress?.Report(delta.Data.DeltaContent);
						break;
					case AssistantMessageEvent msg when !string.IsNullOrEmpty(msg.Data.Content):
						lock (reply)
						{
							if (reply.Length > 0)
							{
								reply.AppendLine().AppendLine();
								progress?.Report(Environment.NewLine + Environment.NewLine);
							}
							reply.Append(msg.Data.Content);
						}
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

			var prompt = BuildPrompt(request, includeHistory: !resumed);
			await session.SendAsync(new MessageOptions { Prompt = prompt }, cancellationToken).ConfigureAwait(false);
			await done.Task.ConfigureAwait(false);

			string content;
			lock (reply)
				content = reply.ToString().Trim();
			if (string.IsNullOrWhiteSpace(content))
				throw new InvalidOperationException("The assistant returned an empty response.");
			return new GremlinAssistantReply(session.SessionId, content);
		}
	}

	public async Task<GremlinQueryReview> ReviewQueryAsync(GremlinQueryReviewRequest request, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(request);
		if (string.IsNullOrWhiteSpace(request.Query))
			return GremlinQueryReview.Empty;

		var client = await GetClientAsync(cancellationToken).ConfigureAwait(false);
		var model = !string.IsNullOrWhiteSpace(request.Model) && !request.Model.Contains('/') ? request.Model : null;
		var config = new SessionConfig
		{
			SystemMessage = new SystemMessageConfig { Mode = SystemMessageMode.Replace, Content = GremlinQueryReviewPrompt.BuildInstructions() },
			Tools = [],
			AvailableTools = [],
			Streaming = false,
#pragma warning disable GHCP001 // PermissionDecision is an evaluation API
			OnPermissionRequest = (_, _) => Task.FromResult(PermissionDecision.Reject("Query review does not use tools."))
#pragma warning restore GHCP001
		};
		if (model is not null)
			config.Model = model;

		var session = await client.CreateSessionAsync(config, cancellationToken).ConfigureAwait(false);
		var sessionId = session.SessionId;
		try
		{
			await using (session.ConfigureAwait(false))
			{
				var reply = new StringBuilder();
				var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
				using var subscription = session.On<SessionEvent>(evt =>
				{
					switch (evt)
					{
						case AssistantMessageEvent msg when !string.IsNullOrEmpty(msg.Data.Content):
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
				await session.SendAsync(new MessageOptions { Prompt = GremlinQueryReviewPrompt.BuildPrompt(request) }, cancellationToken).ConfigureAwait(false);
				await done.Task.ConfigureAwait(false);

				string content;
				lock (reply)
					content = reply.ToString();
				return GremlinQueryReviewPrompt.ParseReply(content, request.Query);
			}
		}
		finally
		{
			// Review sessions are throw-away; do not let them pile up in the user's Copilot session list.
			try
			{
				await client.DeleteSessionAsync(sessionId, CancellationToken.None).ConfigureAwait(false);
			}
			catch (Exception ex) when (ex is not OutOfMemoryException)
			{
				_logger.LogDebug(ex, "Failed to delete query review session {SessionId}", sessionId);
			}
		}
	}

	private static void Configure(SessionConfigBase config, List<AIFunction> tools, string? model)
	{
		config.SystemMessage = new SystemMessageConfig
		{
			Mode = SystemMessageMode.Replace,
			Content = GremlinAssistantPrompt.BuildInstructions()
		};
		// Only the application's own tools; no shell, file or other agent tools on the user's machine.
		config.Tools = tools.Cast<AIFunctionDeclaration>().ToList();
		config.AvailableTools = tools.Select(t => t.Name).ToList();
		config.Streaming = true;
#pragma warning disable GHCP001 // PermissionDecision is an evaluation API
		config.OnPermissionRequest = (_, _) => Task.FromResult(PermissionDecision.Reject("Gremlin Studio assistant only allows its own tools."));
#pragma warning restore GHCP001
		if (model is not null)
			config.Model = model;
	}

	// Confirmation for writes and issues is performed by the tool host, so the runtime permission prompt is skipped.
	private static List<AIFunction> CreateTools(IGremlinAssistantToolHost host, CancellationToken cancellationToken) =>
	[
		CopilotTool.DefineTool(
			([Description("The Gremlin query to run, starting with 'g.'")] string query) =>
				host.ExecuteQueryAsync(query, cancellationToken),
			new CopilotToolOptions { SkipPermission = true },
			new AIFunctionFactoryOptions
			{
				Name = "execute_gremlin_query",
				Description = "Runs a Gremlin query against the user's active connection and returns the results as JSON. Mutating queries are confirmed by the user before they run."
			}),
		CopilotTool.DefineTool(
			([Description("Kind of issue: Bug, Suggestion or Feedback")] GremlinAssistantIssueKind kind,
			 [Description("Short, descriptive issue title")] string title,
			 [Description("Issue body in markdown")] string body) =>
				host.CreateIssueAsync(kind, title, body, cancellationToken),
			new CopilotToolOptions { SkipPermission = true },
			new AIFunctionFactoryOptions
			{
				Name = "create_github_issue",
				Description = "Files a bug report, suggestion or feedback for Gremlin Studio on GitHub. The user reviews and confirms it before it is created."
			})
	];

	private static string BuildPrompt(GremlinAssistantRequest request, bool includeHistory)
	{
		var sb = new StringBuilder();
		sb.AppendLine("<graph-context>").Append(GremlinAssistantPrompt.BuildContext(request.Context)).AppendLine("</graph-context>");
		if (includeHistory && request.History.Count > 0)
		{
			sb.AppendLine("<conversation-so-far>");
			foreach (var m in request.History)
				sb.Append(m.Role == GremlinAssistantRole.User ? "User: " : "Assistant: ").AppendLine(m.Content);
			sb.AppendLine("</conversation-so-far>");
		}
		sb.AppendLine().Append(request.Prompt);
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
