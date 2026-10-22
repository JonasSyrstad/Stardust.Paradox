using System.Diagnostics;
using System.Net.Http;
using System.Windows;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Stardust.Paradox.GremlinStudio.Core.GitHub;
using Stardust.Paradox.GremlinStudio.Dialogs;

namespace Stardust.Paradox.GremlinStudio.ViewModels;

/// <summary>
/// View model for the account area in the window chrome (GitHub sign-in, feedback and issue reporting).
/// </summary>
public partial class AccountViewModel : ObservableObject
{
    private const string RepositoryUrl = "https://github.com/JonasSyrstad/Stardust.Paradox";

    private readonly IGitHubAccountService _gitHub;
    private readonly ILogger<AccountViewModel> _logger;
    private CancellationTokenSource? _signInCts;

    public AccountViewModel(IGitHubAccountService gitHub, AssistantViewModel assistant, ILogger<AccountViewModel> logger)
    {
        _gitHub = gitHub;
        Assistant = assistant;
        _logger = logger;
        _gitHub.UserChanged += (_, user) => Application.Current.Dispatcher.Invoke(() => ApplyUser(user));
    }

    [ObservableProperty] private bool _isAccountMenuOpen;
    [ObservableProperty] private bool _isSignedIn;
    [ObservableProperty] private string _displayName = "Not signed in";
    [ObservableProperty] private string? _login;
    [ObservableProperty] private BitmapImage? _avatar;
    [ObservableProperty] private bool _isSigningIn;
    [ObservableProperty] private string? _deviceUserCode;
    [ObservableProperty] private string? _statusMessage;

    // Feedback form
    [ObservableProperty] private bool _isFeedbackOpen;
    [ObservableProperty] private string _feedbackKind = "Bug";
    [ObservableProperty] private string _feedbackTitle = string.Empty;
    [ObservableProperty] private string _feedbackDescription = string.Empty;
    [ObservableProperty] private bool _includeDiagnostics = true;
    [ObservableProperty] private bool _isSubmitting;

    public IReadOnlyList<string> FeedbackKinds { get; } = ["Bug", "Feature request", "Feedback"];

    public bool IsConfigured => _gitHub.IsConfigured;

    /// <summary>The opt-in AI Gremlin assistant, toggled from the account menu.</summary>
    public AssistantViewModel Assistant { get; }

    /// <summary>Diagnostics appended to reports. Contains no connection details or query content.</summary>
    public string DiagnosticsPreview =>
        $"""
        - App version: {typeof(App).Assembly.GetName().Version}
        - OS: {Environment.OSVersion}
        - .NET: {Environment.Version}
        - 64-bit process: {Environment.Is64BitProcess}
        """;

    /// <summary>Restores a stored session at startup.</summary>
    public async Task InitializeAsync()
    {
        await Assistant.InitializeAsync();
        try
        {
            await _gitHub.TryRestoreSessionAsync();
        }
        catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException or System.Text.Json.JsonException
                                       or System.IO.IOException or UnauthorizedAccessException or System.Security.Cryptography.CryptographicException)
        {
            _logger.LogWarning(ex, "Failed to restore GitHub session");
        }
    }

    [RelayCommand]
    private void ToggleAccountMenu() => IsAccountMenuOpen = !IsAccountMenuOpen;

    [RelayCommand]
    private async Task SignInAsync()
    {
        if (!_gitHub.IsConfigured)
        {
            StatusMessage = "GitHub sign-in is not configured in this build.";
            return;
        }

        _signInCts?.Cancel();
        _signInCts = new CancellationTokenSource();
        IsSigningIn = true;
        IsAccountMenuOpen = true;
        StatusMessage = null;
        try
        {
            var code = await _gitHub.StartSignInAsync(_signInCts.Token);
            DeviceUserCode = code.UserCode;
            CopyDeviceCode();
            OpenUrl(code.VerificationUri);
            StatusMessage = "Code copied. Paste it in the browser to authorize Gremlin Studio. This menu stays open until sign-in completes.";
            await _gitHub.CompleteSignInAsync(code, _signInCts.Token);
            StatusMessage = null;
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Sign-in cancelled.";
        }
        catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException or TimeoutException)
        {
            _logger.LogWarning(ex, "GitHub sign-in failed");
            StatusMessage = ex.Message;
        }
        finally
        {
            IsSigningIn = false;
            DeviceUserCode = null;
        }
    }

    [RelayCommand]
    private void CancelSignIn() => _signInCts?.Cancel();

    [RelayCommand]
    private void CopyDeviceCode()
    {
        if (string.IsNullOrEmpty(DeviceUserCode))
            return;
        try
        {
            Clipboard.SetText(DeviceUserCode);
            StatusMessage = "Code copied to clipboard.";
        }
        catch (System.Runtime.InteropServices.COMException ex)
        {
            // Clipboard can be locked by another process; the code is still visible for manual entry.
            _logger.LogWarning(ex, "Failed to copy device code");
        }
    }

    [RelayCommand]
    private async Task SignOutAsync()
    {
        await _gitHub.SignOutAsync();
        IsAccountMenuOpen = false;
    }

    [RelayCommand]
    private void OpenFeedback(string? kind)
    {
        FeedbackKind = string.IsNullOrEmpty(kind) ? "Bug" : kind;
        FeedbackTitle = string.Empty;
        FeedbackDescription = string.Empty;
        StatusMessage = null;
        IsAccountMenuOpen = false;
        IsFeedbackOpen = true;
    }

    [RelayCommand]
    private void CloseFeedback() => IsFeedbackOpen = false;

    [RelayCommand]
    private async Task SubmitFeedbackAsync()
    {
        if (!IsSignedIn)
        {
            StatusMessage = "Sign in with GitHub to send feedback.";
            return;
        }

        if (string.IsNullOrWhiteSpace(FeedbackTitle))
        {
            StatusMessage = "Please enter a title.";
            return;
        }

        var body = FeedbackDescription.Trim();
        if (IncludeDiagnostics)
            body += $"\n\n<details><summary>Diagnostics</summary>\n\n{DiagnosticsPreview}\n</details>";
        body += "\n\n_Submitted from Stardust Gremlin Studio._";

        string[] labels = FeedbackKind switch
        {
            "Bug" => ["bug", "gremlin-studio"],
            "Feature request" => ["enhancement", "gremlin-studio"],
            _ => ["feedback", "gremlin-studio"]
        };

        IsSubmitting = true;
        StatusMessage = null;
        try
        {
            var issue = await _gitHub.CreateIssueAsync(FeedbackTitle.Trim(), body, labels);
            IsFeedbackOpen = false;
            var result = ThemedMessageBox.Show(
                $"Thank you! Issue #{issue.Number} was created.\n\nOpen it in the browser?",
                "Feedback sent", MessageBoxButton.YesNo, MessageBoxImage.Information);
            if (result == MessageBoxResult.Yes)
                OpenUrl(issue.HtmlUrl);
        }
        catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException)
        {
            _logger.LogWarning(ex, "Failed to create GitHub issue");
            StatusMessage = $"Could not submit: {ex.Message}";
        }
        finally
        {
            IsSubmitting = false;
        }
    }

    [RelayCommand]
    private void OpenProfile()
    {
        if (_gitHub.CurrentUser is { } user)
            OpenUrl(user.HtmlUrl);
    }

    [RelayCommand]
    private void OpenMyIssues()
    {
        var query = Login is null ? "is:open" : $"is:open author:{Login}";
        OpenUrl($"{RepositoryUrl}/issues?q={Uri.EscapeDataString(query)}");
    }

    [RelayCommand]
    private void OpenReleaseNotes() => OpenUrl($"{RepositoryUrl}/releases");

    [RelayCommand]
    private void OpenDocumentation() => OpenUrl("https://jonassyrstad.github.io/Stardust.Paradox/");

    [RelayCommand]
    private void StarRepository() => OpenUrl(RepositoryUrl);

    private void ApplyUser(GitHubUser? user)
    {
        IsSignedIn = user is not null;
        Assistant.IsSignedIn = IsSignedIn;
        Login = user?.Login;
        DisplayName = user is null ? "Not signed in" : (string.IsNullOrWhiteSpace(user.Name) ? user.Login : user.Name);
        Avatar = user is null ? null : LoadAvatar(user.AvatarUrl);
    }

    private static BitmapImage LoadAvatar(string url)
    {
        var image = new BitmapImage();
        image.BeginInit();
        image.UriSource = new Uri(url.Contains('?') ? $"{url}&s=64" : $"{url}?s=64");
        image.DecodePixelWidth = 64;
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.EndInit();
        return image;
    }

    private void OpenUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            _logger.LogWarning(ex, "Failed to open {Url}", url);
            StatusMessage = $"Open this URL in your browser: {url}";
        }
    }
}
