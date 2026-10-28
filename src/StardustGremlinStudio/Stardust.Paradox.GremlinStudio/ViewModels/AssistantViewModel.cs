using System.Collections.ObjectModel;
using System.Net.Http;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Stardust.Paradox.GremlinStudio.Core.Assistant;
using Stardust.Paradox.GremlinStudio.Core.Execution;
using Stardust.Paradox.GremlinStudio.Core.GitHub;
using Stardust.Paradox.GremlinStudio.Dialogs;

namespace Stardust.Paradox.GremlinStudio.ViewModels;

/// <summary>A chat message shown in the assistant panel.</summary>
public sealed partial class AssistantChatMessageViewModel : ObservableObject
{
	public AssistantChatMessageViewModel(GremlinAssistantRole role, string content, bool isStreaming = false)
	{
		Role = role;
		_content = content;
		_isStreaming = isStreaming;
		UpdateQuery();
	}

	public GremlinAssistantRole Role { get; }

	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(IsWaitingForFirstToken))]
	private string _content;

	/// <summary>Whether the reply is still being received.</summary>
	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(IsWaitingForFirstToken))]
	private bool _isStreaming;

	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(HasQuery))]
	private string? _query;

	public bool IsUser => Role == GremlinAssistantRole.User;
	public bool HasQuery => !IsStreaming && !string.IsNullOrEmpty(Query);
	public bool IsWaitingForFirstToken => IsStreaming && string.IsNullOrEmpty(Content);

	partial void OnIsStreamingChanged(bool value)
	{
		UpdateQuery();
		OnPropertyChanged(nameof(HasQuery));
	}

	private void UpdateQuery() =>
		Query = Role == GremlinAssistantRole.Assistant && !IsStreaming ? GremlinAssistantPrompt.ExtractQuery(Content) : null;
}

/// <summary>A saved conversation shown in the conversation selector.</summary>
public sealed partial class AssistantConversationViewModel : ObservableObject
{
	public AssistantConversationViewModel(GremlinAssistantConversation conversation)
	{
		Id = conversation.Id;
		_title = conversation.Title;
		SessionId = conversation.SessionId;
		UpdatedAt = conversation.UpdatedAt;
		Messages = conversation.Messages.ToList();
	}

	public string Id { get; }

	[ObservableProperty] private string _title;

	public string? SessionId { get; set; }

	public DateTimeOffset UpdatedAt { get; set; }

	public List<GremlinAssistantMessage> Messages { get; set; }

	public GremlinAssistantConversation ToRecord() => new(Id, Title, SessionId, UpdatedAt, Messages.ToList());

	public override string ToString() => Title;
}

/// <summary>
/// View model for the opt-in AI Gremlin assistant side panel (GitHub Copilot).
/// </summary>
public partial class AssistantViewModel : ObservableObject, IGremlinAssistantToolHost
{
	private const string NewConversationTitle = "New conversation";
	private const int MaxToolResultLength = 20_000;

	private readonly IGremlinAssistantService _assistant;
	private readonly IGremlinAssistantSettingsStore _settingsStore;
	private readonly IGremlinAssistantConversationStore _conversationStore;
	private readonly IGitHubAccountService _gitHub;
	private readonly ILogger<AssistantViewModel> _logger;
	private GremlinAssistantSettings _settings;
	private Func<GremlinAssistantContext>? _contextProvider;
	private Action<string>? _insertQuery;
	private Func<string, CancellationToken, Task<QueryExecutionResult>>? _executeQuery;
	private CancellationTokenSource? _requestCts;
	private bool _isRestoring;
	private bool _modelsLoaded;

	public AssistantViewModel(
		IGremlinAssistantService assistant,
		IGremlinAssistantSettingsStore settingsStore,
		IGremlinAssistantConversationStore conversationStore,
		IGitHubAccountService gitHub,
		ILogger<AssistantViewModel> logger)
	{
		_assistant = assistant;
		_settingsStore = settingsStore;
		_conversationStore = conversationStore;
		_gitHub = gitHub;
		_logger = logger;
		_settings = settingsStore.Load();
		_isRestoring = true;
		IsEnabled = _settings.IsEnabled;
		foreach (var c in conversationStore.Load())
			Conversations.Add(new AssistantConversationViewModel(c));
		SelectedModel = _settings.Model;
		if (!string.IsNullOrWhiteSpace(_settings.Model))
			Models.Add(new GremlinAssistantModel(_settings.Model, _settings.Model));
		SelectedConversation = Conversations.FirstOrDefault();
		_isRestoring = false;
	}

	public ObservableCollection<AssistantChatMessageViewModel> Messages { get; } = [];

	/// <summary>Saved conversations, most recent first.</summary>
	public ObservableCollection<AssistantConversationViewModel> Conversations { get; } = [];

	/// <summary>Models available from GitHub Copilot.</summary>
	public ObservableCollection<GremlinAssistantModel> Models { get; } = [];

	/// <summary>The active conversation, or null for a new, unsaved one.</summary>
	[ObservableProperty]
	[NotifyCanExecuteChangedFor(nameof(DeleteConversationCommand))]
	private AssistantConversationViewModel? _selectedConversation;

	/// <summary>The selected model id. Persisted.</summary>
	[ObservableProperty] private string? _selectedModel;

	[ObservableProperty] private bool _isLoadingModels;

	/// <summary>Short description of what the assistant is currently doing (e.g. running a query).</summary>
	[ObservableProperty] private string? _activity;

	/// <summary>Whether the user has opted in. Persisted.</summary>
	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(IsPanelVisible))]
	[NotifyPropertyChangedFor(nameof(IsAvailable))]
	private bool _isEnabled;

	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(IsPanelVisible))]
	private bool _isPanelOpen;

	/// <summary>Whether a GitHub user is signed in. The assistant is only available to signed-in users.</summary>
	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(IsPanelVisible))]
	[NotifyPropertyChangedFor(nameof(IsAvailable))]
	[NotifyCanExecuteChangedFor(nameof(SendCommand))]
	private bool _isSignedIn;

	/// <summary>Whether the assistant is enabled and usable (signed in).</summary>
	public bool IsAvailable => IsEnabled && IsSignedIn;

	[ObservableProperty]
	[NotifyCanExecuteChangedFor(nameof(SendCommand))]
	private string _prompt = string.Empty;

	[ObservableProperty]
	[NotifyCanExecuteChangedFor(nameof(SendCommand))]
	[NotifyCanExecuteChangedFor(nameof(NewConversationCommand))]
	[NotifyCanExecuteChangedFor(nameof(DeleteConversationCommand))]
	private bool _isBusy;

	[ObservableProperty] private string? _errorMessage;

	/// <summary>Personal access token input (never read back from storage).</summary>
	[ObservableProperty]
	[NotifyCanExecuteChangedFor(nameof(SavePersonalAccessTokenCommand))]
	private string _personalAccessToken = string.Empty;

	[ObservableProperty]
	[NotifyCanExecuteChangedFor(nameof(ClearPersonalAccessTokenCommand))]
	private bool _hasPersonalAccessToken;

	/// <summary>Loads whether a personal access token is stored.</summary>
	public async Task InitializeAsync()
	{
		try
		{
			HasPersonalAccessToken = await _assistant.HasPersonalAccessTokenAsync();
		}
		catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException or System.Security.Cryptography.CryptographicException)
		{
			_logger.LogWarning(ex, "Failed to load GitHub Copilot token state");
		}
	}

	private bool CanSavePersonalAccessToken() => !string.IsNullOrWhiteSpace(PersonalAccessToken);

	[RelayCommand(CanExecute = nameof(CanSavePersonalAccessToken))]
	private async Task SavePersonalAccessTokenAsync()
	{
		await _assistant.SetPersonalAccessTokenAsync(PersonalAccessToken);
		PersonalAccessToken = string.Empty;
		HasPersonalAccessToken = true;
		ErrorMessage = null;
		_modelsLoaded = false;
	}

	[RelayCommand(CanExecute = nameof(HasPersonalAccessToken))]
	private async Task ClearPersonalAccessTokenAsync()
	{
		await _assistant.SetPersonalAccessTokenAsync(null);
		HasPersonalAccessToken = false;
		_modelsLoaded = false;
	}

	public bool IsPanelVisible => IsAvailable && IsPanelOpen;

	/// <summary>Connects the assistant to the main window: graph context source, query insertion and execution.</summary>
	public void Attach(
		Func<GremlinAssistantContext> contextProvider,
		Action<string> insertQuery,
		Func<string, CancellationToken, Task<QueryExecutionResult>> executeQuery)
	{
		_contextProvider = contextProvider ?? throw new ArgumentNullException(nameof(contextProvider));
		_insertQuery = insertQuery ?? throw new ArgumentNullException(nameof(insertQuery));
		_executeQuery = executeQuery ?? throw new ArgumentNullException(nameof(executeQuery));
	}

	partial void OnIsEnabledChanged(bool value)
	{
		if (_isRestoring)
			return;
		_settings = _settings with { IsEnabled = value };
		_settingsStore.Save(_settings);
		IsPanelOpen = value;
		if (!value)
			_requestCts?.Cancel();
	}

	partial void OnIsPanelOpenChanged(bool value)
	{
		if (value && !_modelsLoaded)
			_ = LoadModelsAsync();
	}

	partial void OnSelectedModelChanged(string? value)
	{
		if (_isRestoring || string.IsNullOrWhiteSpace(value) || value == _settings.Model)
			return;
		_settings = _settings with { Model = value };
		_settingsStore.Save(_settings);
	}

	partial void OnSelectedConversationChanged(AssistantConversationViewModel? value)
	{
		_requestCts?.Cancel();
		Messages.Clear();
		ErrorMessage = null;
		if (value is null)
			return;
		foreach (var m in value.Messages)
			Messages.Add(new AssistantChatMessageViewModel(m.Role, m.Content));
	}

	[RelayCommand]
	private async Task LoadModelsAsync()
	{
		if (IsLoadingModels)
			return;
		IsLoadingModels = true;
		try
		{
			var models = await _assistant.ListModelsAsync();
			var selected = SelectedModel;
			_isRestoring = true;
			Models.Clear();
			foreach (var m in models)
				Models.Add(m);
			if (!string.IsNullOrWhiteSpace(selected) && Models.All(m => m.Id != selected))
				Models.Insert(0, new GremlinAssistantModel(selected, selected));
			SelectedModel = selected;
			_isRestoring = false;
			_modelsLoaded = true;
		}
		catch (Exception ex) when (ex is InvalidOperationException or HttpRequestException or System.IO.IOException)
		{
			_logger.LogWarning(ex, "Failed to list GitHub Copilot models");
			ErrorMessage = $"Could not load models: {ex.Message}";
		}
		finally
		{
			_isRestoring = false;
			IsLoadingModels = false;
		}
	}

	[RelayCommand]
	private void TogglePanel()
	{
		if (IsAvailable)
			IsPanelOpen = !IsPanelOpen;
	}

	[RelayCommand]
	private void ClosePanel() => IsPanelOpen = false;

	private bool CanChangeConversation() => !IsBusy;

	[RelayCommand(CanExecute = nameof(CanChangeConversation))]
	private void NewConversation() => SelectedConversation = null;

	private bool CanDeleteConversation() => !IsBusy && SelectedConversation is not null;

	[RelayCommand(CanExecute = nameof(CanDeleteConversation))]
	private async Task DeleteConversationAsync()
	{
		if (SelectedConversation is not { } conversation)
			return;
		if (ThemedMessageBox.Show($"Delete the conversation \"{conversation.Title}\"?", "Delete conversation",
				MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
			return;

		Conversations.Remove(conversation);
		SaveConversations();
		SelectedConversation = Conversations.FirstOrDefault();

		if (conversation.SessionId is { } sessionId)
		{
			try
			{
				await _assistant.DeleteSessionAsync(sessionId);
			}
			catch (Exception ex) when (ex is InvalidOperationException or HttpRequestException or System.IO.IOException)
			{
				_logger.LogInformation(ex, "Failed to delete Copilot session {SessionId}", sessionId);
			}
		}
	}

	private bool CanSend() => IsSignedIn && !IsBusy && !string.IsNullOrWhiteSpace(Prompt);

	partial void OnIsSignedInChanged(bool value)
	{
		if (!value)
			_requestCts?.Cancel();
	}

	[RelayCommand(CanExecute = nameof(CanSend))]
	private async Task SendAsync()
	{
		var text = Prompt.Trim();
		Prompt = string.Empty;
		ErrorMessage = null;

		var conversation = SelectedConversation;
		if (conversation is null)
		{
			conversation = new AssistantConversationViewModel(new GremlinAssistantConversation(
				Guid.NewGuid().ToString("N"), CreateTitle(text), null, DateTimeOffset.Now, []));
			_isRestoring = true;
			Conversations.Insert(0, conversation);
			_selectedConversation = conversation;
			OnPropertyChanged(nameof(SelectedConversation));
			DeleteConversationCommand.NotifyCanExecuteChanged();
			_isRestoring = false;
		}

		var history = conversation.Messages.ToList();
		Messages.Add(new AssistantChatMessageViewModel(GremlinAssistantRole.User, text));
		conversation.Messages.Add(new GremlinAssistantMessage(GremlinAssistantRole.User, text));

		_requestCts?.Cancel();
		_requestCts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
		IsBusy = true;
		AssistantChatMessageViewModel? pending = null;
		try
		{
			var context = _contextProvider?.Invoke()
						  ?? new GremlinAssistantContext(null, false, null, [], [], Core.Completion.GremlinSchemaSample.Empty);
			pending = new AssistantChatMessageViewModel(GremlinAssistantRole.Assistant, string.Empty, isStreaming: true);
			Messages.Add(pending);
			var target = pending;
			var progress = new Progress<string>(chunk => target.Content += chunk);
			var request = new GremlinAssistantRequest(conversation.SessionId, text, history, context, SelectedModel);
			var reply = await _assistant.AskAsync(request, this, progress, _requestCts.Token);
			conversation.SessionId = reply.SessionId;
			pending.Content = reply.Content;
			pending.IsStreaming = false;
			pending = null;
		}
		catch (OperationCanceledException)
		{
			ErrorMessage = "Request cancelled or timed out.";
		}
		catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException or System.Text.Json.JsonException)
		{
			_logger.LogWarning(ex, "Gremlin assistant request failed");
			ErrorMessage = ex.Message;
		}
		finally
		{
			if (pending is not null)
			{
				// Keep partial streamed text; drop the bubble if nothing arrived.
				if (string.IsNullOrEmpty(pending.Content))
					Messages.Remove(pending);
				else
					pending.IsStreaming = false;
			}

			var last = Messages.LastOrDefault();
			if (last is { Role: GremlinAssistantRole.Assistant })
				conversation.Messages.Add(new GremlinAssistantMessage(GremlinAssistantRole.Assistant, last.Content));
			conversation.UpdatedAt = DateTimeOffset.Now;
			SaveConversations();
			Activity = null;
			IsBusy = false;
		}
	}

	[RelayCommand]
	private void Cancel() => _requestCts?.Cancel();

	/// <summary>
	/// Reviews an editor query for the given server kind. Returns null when the assistant is not enabled and signed in.
	/// </summary>
	public async Task<GremlinQueryReview?> ReviewQueryAsync(string query, Core.Connections.GremlinConnectionKind serverKind, CancellationToken cancellationToken)
	{
		if (!IsAvailable || string.IsNullOrWhiteSpace(query))
			return null;
		var context = _contextProvider?.Invoke()
					  ?? new GremlinAssistantContext(null, false, null, [], [], Core.Completion.GremlinSchemaSample.Empty);
		using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		timeout.CancelAfter(TimeSpan.FromSeconds(60));
		return await _assistant.ReviewQueryAsync(new GremlinQueryReviewRequest(query, serverKind, context, SelectedModel), timeout.Token);
	}

	/// <summary>
	/// Explains a query with Copilot. Returns null when the assistant is not enabled and signed in.
	/// </summary>
	public async Task<string?> ExplainQueryAsync(string query, Core.Connections.GremlinConnectionKind serverKind, CancellationToken cancellationToken)
	{
		if (!IsAvailable || string.IsNullOrWhiteSpace(query))
			return null;
		var context = _contextProvider?.Invoke()
					  ?? new GremlinAssistantContext(null, false, null, [], [], Core.Completion.GremlinSchemaSample.Empty);
		using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		timeout.CancelAfter(TimeSpan.FromSeconds(60));
		var text = await _assistant.ExplainQueryAsync(new GremlinQueryReviewRequest(query, serverKind, context, SelectedModel), timeout.Token);
		return string.IsNullOrWhiteSpace(text) ? null : text;
	}

	[RelayCommand]
	private void InsertQuery(AssistantChatMessageViewModel? message)
	{
		if (message?.Query is { } query)
			_insertQuery?.Invoke(query);
	}

	[RelayCommand]
	private void CopyQuery(AssistantChatMessageViewModel? message)
	{
		if (message?.Query is { } query)
			Clipboard.SetText(query);
	}

	async Task<string> IGremlinAssistantToolHost.ExecuteQueryAsync(string query, CancellationToken cancellationToken)
	{
		if (string.IsNullOrWhiteSpace(query))
			return "Error: the query is empty.";
		if (_executeQuery is null)
			return "Error: query execution is not available.";

		query = query.Trim();
		if (GremlinMutationDetector.IsMutation(query))
		{
			var confirmed = await OnUiAsync(() => ThemedMessageBox.Show(
				$"The assistant wants to run a query that modifies the database:\n\n{query}\n\nRun this query?",
				"Confirm database write", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes);
			if (!confirmed)
				return "The user declined to run this mutating query. Do not retry it.";
		}

		await OnUiAsync(() => Activity = "Running query…");
		try
		{
			var result = await _executeQuery(query, cancellationToken);
			if (!result.IsSuccess)
				return $"Query failed: {result.ErrorMessage}";
			var json = result.ResultJson ?? "[]";
			if (json.Length > MaxToolResultLength)
				json = json[..MaxToolResultLength] + "\n…(truncated)";
			return $"{result.ResultCount} result(s) in {result.Duration.TotalMilliseconds:F0} ms:\n{json}";
		}
		finally
		{
			await OnUiAsync(() => Activity = null);
		}
	}

	async Task<string> IGremlinAssistantToolHost.CreateIssueAsync(GremlinAssistantIssueKind kind, string title, string body, CancellationToken cancellationToken)
	{
		if (string.IsNullOrWhiteSpace(title))
			return "Error: the issue title is empty.";
		if (_gitHub.CurrentUser is null)
			return "Error: the user is not signed in with GitHub.";

		title = title.Trim();
		body = body?.Trim() ?? string.Empty;
		var confirmed = await OnUiAsync(() => ThemedMessageBox.Show(
			$"The assistant wants to file a {kind.ToString().ToLowerInvariant()} on GitHub:\n\nTitle: {title}\n\n{body}\n\nSend it?",
			"Confirm GitHub issue", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes);
		if (!confirmed)
			return "The user declined to send this issue. Do not retry unless asked.";

		string[] labels = kind switch
		{
			GremlinAssistantIssueKind.Bug => ["bug", "gremlin-studio"],
			GremlinAssistantIssueKind.Suggestion => ["enhancement", "gremlin-studio"],
			_ => ["feedback", "gremlin-studio"]
		};

		await OnUiAsync(() => Activity = "Filing issue…");
		try
		{
			var issue = await _gitHub.CreateIssueAsync(title, $"{body}\n\n---\n_Filed via the Gremlin Studio assistant._", labels, cancellationToken);
			return $"Created issue #{issue.Number}: {issue.HtmlUrl}";
		}
		catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException)
		{
			_logger.LogWarning(ex, "Assistant failed to create GitHub issue");
			return $"Failed to create the issue: {ex.Message}";
		}
		finally
		{
			await OnUiAsync(() => Activity = null);
		}
	}

	private void SaveConversations() =>
		_conversationStore.Save(Conversations.Select(c => c.ToRecord()).ToList());

	private static string CreateTitle(string prompt)
	{
		var line = prompt.Split('\n', 2)[0].Trim();
		if (line.Length == 0)
			return NewConversationTitle;
		return line.Length <= 60 ? line : line[..57] + "…";
	}

	private static Task<T> OnUiAsync<T>(Func<T> func) =>
		Application.Current.Dispatcher.InvokeAsync(func).Task;

	private static Task OnUiAsync(Action action) =>
		Application.Current.Dispatcher.InvokeAsync(action).Task;
}
