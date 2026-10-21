using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Stardust.Paradox.GremlinStudio.Core.Connections;

namespace Stardust.Paradox.GremlinStudio.Core.GitHub;

/// <summary>
/// Configuration for the GitHub integration.
/// </summary>
public sealed record GitHubOptions
{
    /// <summary>
    /// Client ID of the GitHub OAuth App (device flow must be enabled). Client IDs are public; no secret is required.
    /// Can be overridden with the GREMLINSTUDIO_GITHUB_CLIENT_ID environment variable.
    /// </summary>
    public string ClientId { get; init; } =  "Ov23liAhrj2S0eTdSQbS";

    /// <summary>Owner of the repository that receives issues.</summary>
    public string RepositoryOwner { get; init; } = "JonasSyrstad";

    /// <summary>Name of the repository that receives issues.</summary>
    public string RepositoryName { get; init; } = "Stardust.Paradox";

    /// <summary>OAuth scopes requested during sign-in.</summary>
    public string Scopes { get; init; } = "public_repo";
}

/// <summary>A signed-in GitHub user.</summary>
public sealed record GitHubUser(string Login, string? Name, string AvatarUrl, string HtmlUrl);

/// <summary>Device code information shown to the user during sign-in.</summary>
public sealed record GitHubDeviceCode(string UserCode, string VerificationUri, string DeviceCode, int IntervalSeconds, int ExpiresInSeconds);

/// <summary>A created GitHub issue.</summary>
public sealed record GitHubIssue(int Number, string HtmlUrl, string Title);

/// <summary>
/// GitHub sign-in (OAuth device flow) and issue reporting.
/// </summary>
public interface IGitHubAccountService
{
    /// <summary>Whether a client ID is configured.</summary>
    bool IsConfigured { get; }

    /// <summary>The currently signed-in user, or null.</summary>
    GitHubUser? CurrentUser { get; }

    /// <summary>The OAuth access token of the signed-in user, or null when signed out.</summary>
    string? AccessToken { get; }

    /// <summary>Raised when the signed-in user changes.</summary>
    event EventHandler<GitHubUser?>? UserChanged;

    /// <summary>Restores a previously stored session, if any.</summary>
    Task<GitHubUser?> TryRestoreSessionAsync(CancellationToken cancellationToken = default);

    /// <summary>Starts the device flow and returns the code the user must enter.</summary>
    Task<GitHubDeviceCode> StartSignInAsync(CancellationToken cancellationToken = default);

    /// <summary>Polls until the user authorizes the device, then stores the token.</summary>
    Task<GitHubUser> CompleteSignInAsync(GitHubDeviceCode deviceCode, CancellationToken cancellationToken = default);

    /// <summary>Signs out and deletes the stored token.</summary>
    Task SignOutAsync(CancellationToken cancellationToken = default);

    /// <summary>Creates an issue in the configured repository.</summary>
    Task<GitHubIssue> CreateIssueAsync(string title, string body, IEnumerable<string> labels, CancellationToken cancellationToken = default);
}

/// <summary>
/// Default <see cref="IGitHubAccountService"/> implementation. Tokens are stored with <see cref="ISecureSecretStore"/> (DPAPI).
/// </summary>
public sealed class GitHubAccountService : IGitHubAccountService
{
    private const string TokenSecretKey = "github-oauth-token";
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

    private readonly HttpClient _httpClient;
    private readonly ISecureSecretStore _secretStore;
    private readonly GitHubOptions _options;
    private readonly ILogger<GitHubAccountService> _logger;
    private string? _token;

    public GitHubAccountService(HttpClient httpClient, ISecureSecretStore secretStore, GitHubOptions options, ILogger<GitHubAccountService> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _secretStore = secretStore ?? throw new ArgumentNullException(nameof(secretStore));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        if (_httpClient.DefaultRequestHeaders.UserAgent.Count == 0)
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("GremlinStudio/1.0");
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_options.ClientId);

    public GitHubUser? CurrentUser { get; private set; }

    public string? AccessToken => CurrentUser is null ? null : _token;

    public event EventHandler<GitHubUser?>? UserChanged;

    public async Task<GitHubUser?> TryRestoreSessionAsync(CancellationToken cancellationToken = default)
    {
        var token = await _secretStore.GetSecretAsync(TokenSecretKey, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrEmpty(token))
            return null;

        try
        {
            _token = token;
            SetUser(await GetUserAsync(cancellationToken).ConfigureAwait(false));
            return CurrentUser;
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            _logger.LogInformation("Stored GitHub token is no longer valid; signing out");
            await SignOutAsync(cancellationToken).ConfigureAwait(false);
            return null;
        }
        catch (HttpRequestException ex)
        {
            // Offline: keep the token so the session can be restored later.
            _logger.LogWarning(ex, "Could not restore GitHub session");
            _token = null;
            return null;
        }
    }

    public async Task<GitHubDeviceCode> StartSignInAsync(CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
            throw new InvalidOperationException("GitHub sign-in is not configured (missing OAuth client ID).");

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://github.com/login/device/code")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = _options.ClientId,
                ["scope"] = _options.Scopes
            })
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var dto = await response.Content.ReadFromJsonAsync<DeviceCodeResponse>(JsonOptions, cancellationToken).ConfigureAwait(false)
                  ?? throw new InvalidOperationException("Empty device code response from GitHub.");

        return new GitHubDeviceCode(dto.UserCode, dto.VerificationUri, dto.DeviceCode, Math.Max(dto.Interval, 5), dto.ExpiresIn);
    }

    public async Task<GitHubUser> CompleteSignInAsync(GitHubDeviceCode deviceCode, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(deviceCode);

        var interval = TimeSpan.FromSeconds(deviceCode.IntervalSeconds);
        var deadline = DateTimeOffset.UtcNow.AddSeconds(deviceCode.ExpiresInSeconds);

        while (DateTimeOffset.UtcNow < deadline)
        {
            await Task.Delay(interval, cancellationToken).ConfigureAwait(false);

            using var request = new HttpRequestMessage(HttpMethod.Post, "https://github.com/login/oauth/access_token")
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["client_id"] = _options.ClientId,
                    ["device_code"] = deviceCode.DeviceCode,
                    ["grant_type"] = "urn:ietf:params:oauth:grant-type:device_code"
                })
            };
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var dto = await response.Content.ReadFromJsonAsync<AccessTokenResponse>(JsonOptions, cancellationToken).ConfigureAwait(false);

            if (!string.IsNullOrEmpty(dto?.AccessToken))
            {
                _token = dto.AccessToken;
                await _secretStore.StoreSecretAsync(TokenSecretKey, _token, cancellationToken).ConfigureAwait(false);
                var user = await GetUserAsync(cancellationToken).ConfigureAwait(false);
                SetUser(user);
                return user;
            }

            switch (dto?.Error)
            {
                case "authorization_pending":
                    continue;
                case "slow_down":
                    interval += TimeSpan.FromSeconds(5);
                    continue;
                case "access_denied":
                    throw new OperationCanceledException("Sign-in was denied.");
                default:
                    throw new InvalidOperationException($"GitHub sign-in failed: {dto?.ErrorDescription ?? dto?.Error ?? "unknown error"}");
            }
        }

        throw new TimeoutException("The sign-in code expired. Please try again.");
    }

    public async Task SignOutAsync(CancellationToken cancellationToken = default)
    {
        _token = null;
        await _secretStore.DeleteSecretAsync(TokenSecretKey, cancellationToken).ConfigureAwait(false);
        SetUser(null);
    }

    public async Task<GitHubIssue> CreateIssueAsync(string title, string body, IEnumerable<string> labels, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title is required.", nameof(title));
        if (_token is null)
            throw new InvalidOperationException("Sign in with GitHub to report issues.");

        using var request = CreateApiRequest(HttpMethod.Post, $"repos/{_options.RepositoryOwner}/{_options.RepositoryName}/issues");
        request.Content = JsonContent.Create(new { title, body, labels = labels?.ToArray() ?? [] });

        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == System.Net.HttpStatusCode.UnprocessableEntity && labels?.Any() == true)
        {
            // Labels the user cannot apply are rejected; retry without them.
            _logger.LogInformation("Issue creation with labels rejected; retrying without labels");
            return await CreateIssueAsync(title, body, [], cancellationToken).ConfigureAwait(false);
        }
        response.EnsureSuccessStatusCode();

        var dto = await response.Content.ReadFromJsonAsync<IssueResponse>(JsonOptions, cancellationToken).ConfigureAwait(false)
                  ?? throw new InvalidOperationException("Empty issue response from GitHub.");
        return new GitHubIssue(dto.Number, dto.HtmlUrl, dto.Title);
    }

    private async Task<GitHubUser> GetUserAsync(CancellationToken cancellationToken)
    {
        using var request = CreateApiRequest(HttpMethod.Get, "user");
        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var dto = await response.Content.ReadFromJsonAsync<UserResponse>(JsonOptions, cancellationToken).ConfigureAwait(false)
                  ?? throw new InvalidOperationException("Empty user response from GitHub.");
        return new GitHubUser(dto.Login, dto.Name, dto.AvatarUrl, dto.HtmlUrl);
    }

    private HttpRequestMessage CreateApiRequest(HttpMethod method, string path)
    {
        var request = new HttpRequestMessage(method, $"https://api.github.com/{path}");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);
        return request;
    }

    private void SetUser(GitHubUser? user)
    {
        CurrentUser = user;
        UserChanged?.Invoke(this, user);
    }

    private sealed record DeviceCodeResponse(string DeviceCode, string UserCode, string VerificationUri, int ExpiresIn, int Interval);
    private sealed record AccessTokenResponse(string? AccessToken, string? Error, string? ErrorDescription);
    private sealed record UserResponse(string Login, string? Name, string AvatarUrl, string HtmlUrl);
    private sealed record IssueResponse(int Number, string HtmlUrl, string Title);
}
