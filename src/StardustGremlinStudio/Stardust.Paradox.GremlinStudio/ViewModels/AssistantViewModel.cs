using System.Collections.ObjectModel;
using System.Net.Http;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Stardust.Paradox.GremlinStudio.Core.Assistant;

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
        Query = Role == GremlinAssistantRole.Assistant && !IsStreaming ? GitHubModelsGremlinAssistantService.ExtractQuery(Content) : null;
}

/// <summary>
/// View model for the opt-in AI Gremlin assistant side panel (GitHub Copilot).
/// </summary>
public partial class AssistantViewModel : ObservableObject
{
    private readonly IGremlinAssistantService _assistant;
    private readonly IGremlinAssistantSettingsStore _settingsStore;
    private readonly ILogger<AssistantViewModel> _logger;
    private GremlinAssistantSettings _settings;
    private Func<GremlinAssistantContext>? _contextProvider;
    private Action<string>? _insertQuery;
    private CancellationTokenSource? _requestCts;
    private bool _isRestoring;

    public AssistantViewModel(
        IGremlinAssistantService assistant,
        IGremlinAssistantSettingsStore settingsStore,
        ILogger<AssistantViewModel> logger)
    {
        _assistant = assistant;
        _settingsStore = settingsStore;
        _logger = logger;
        _settings = settingsStore.Load();
        _isRestoring = true;
        IsEnabled = _settings.IsEnabled;
        _isRestoring = false;
    }

    public ObservableCollection<AssistantChatMessageViewModel> Messages { get; } = [];

    /// <summary>Whether the user has opted in. Persisted.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPanelVisible))]
    private bool _isEnabled;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPanelVisible))]
    private bool _isPanelOpen;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendCommand))]
    private string _prompt = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendCommand))]
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
    }

    [RelayCommand(CanExecute = nameof(HasPersonalAccessToken))]
    private async Task ClearPersonalAccessTokenAsync()
    {
        await _assistant.SetPersonalAccessTokenAsync(null);
        HasPersonalAccessToken = false;
    }

    public bool IsPanelVisible => IsEnabled && IsPanelOpen;

    public string Model => _settings.Model;

    /// <summary>Connects the assistant to the main window: graph context source and query insertion.</summary>
    public void Attach(Func<GremlinAssistantContext> contextProvider, Action<string> insertQuery)
    {
        _contextProvider = contextProvider ?? throw new ArgumentNullException(nameof(contextProvider));
        _insertQuery = insertQuery ?? throw new ArgumentNullException(nameof(insertQuery));
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

    [RelayCommand]
    private void TogglePanel()
    {
        if (IsEnabled)
            IsPanelOpen = !IsPanelOpen;
    }

    [RelayCommand]
    private void ClosePanel() => IsPanelOpen = false;

    [RelayCommand]
    private void Clear()
    {
        _requestCts?.Cancel();
        Messages.Clear();
        ErrorMessage = null;
    }

    private bool CanSend() => !IsBusy && !string.IsNullOrWhiteSpace(Prompt);

    [RelayCommand(CanExecute = nameof(CanSend))]
    private async Task SendAsync()
    {
        if (!await _assistant.IsAvailableAsync())
        {
            ErrorMessage = "Sign in with the GitHub Copilot CLI or add a GitHub token (account menu) to use the Gremlin assistant.";
            return;
        }

        var text = Prompt.Trim();
        Prompt = string.Empty;
        ErrorMessage = null;
        Messages.Add(new AssistantChatMessageViewModel(GremlinAssistantRole.User, text));

        _requestCts?.Cancel();
        _requestCts = new CancellationTokenSource(TimeSpan.FromSeconds(120));
        IsBusy = true;
        AssistantChatMessageViewModel? pending = null;
        try
        {
            var context = _contextProvider?.Invoke()
                          ?? new GremlinAssistantContext(null, false, null, [], [], Core.Completion.GremlinSchemaSample.Empty);
            var conversation = Messages.Select(m => new GremlinAssistantMessage(m.Role, m.Content)).ToList();
            pending = new AssistantChatMessageViewModel(GremlinAssistantRole.Assistant, string.Empty, isStreaming: true);
            Messages.Add(pending);
            var target = pending;
            var progress = new Progress<string>(chunk => target.Content += chunk);
            var reply = await _assistant.AskAsync(conversation, context, _settings.Model, _requestCts.Token, progress);
            pending.Content = reply;
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
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void Cancel() => _requestCts?.Cancel();

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
}
