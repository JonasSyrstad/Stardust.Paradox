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
    // Session (JSON with refresh token) is stored under its own key so older builds, which read
    // LegacyTokenSecretKey as a raw token, never see or delete it.
    private const string SessionSecretKey = "github-oauth-session";
    private const string LegacyTokenSecretKey = "github-oauth-token";
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };
    // Refresh slightly before expiry to avoid using a token that expires mid-request.
    private static readonly TimeSpan ExpirySkew = TimeSpan.FromMinutes(5);

    private readonly HttpClient _httpClient;
    private readonly ISecureSecretStore _secretStore;
    private readonly GitHubOptions _options;
    private readonly ILogger<GitHubAccountService> _logger;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private StoredSession? _session;
    private string? _token => _session?.AccessToken;

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
        var stored = await _secretStore.GetSecretAsync(SessionSecretKey, cancellationToken).ConfigureAwait(false)
                     ?? await _secretStore.GetSecretAsync(LegacyTokenSecretKey, cancellationToken).ConfigureAwait(false);
        var session = StoredSession.Parse(stored);
        if (session is null)
            return null;

        _session = session;

        // Show the cached profile immediately so the user stays signed in even when GitHub is unreachable at startup.
        if (session.User is not null)
            SetUser(session.User);

        try
        {
            if (!await EnsureValidTokenAsync(cancellationToken).ConfigureAwait(false))
                return null;

            var user = await GetUserAsync(cancellationToken).ConfigureAwait(false);
            await SaveSessionAsync(_session! with { User = user }, cancellationToken).ConfigureAwait(false);
            SetUser(user);
            return CurrentUser;
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            _logger.LogInformation("Stored GitHub credentials are no longer valid; signing out");
            await SignOutAsync(cancellationToken).ConfigureAwait(false);
            return null;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            // Transient (offline, timeout, GitHub outage): keep the session and cached profile; validate again on next use.
            _logger.LogWarning(ex, "Could not validate GitHub session; keeping cached session");
            return CurrentUser;
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
                _session = StoredSession.FromTokenResponse(dto, previous: null);
                var user = await GetUserAsync(cancellationToken).ConfigureAwait(false);
                await SaveSessionAsync(_session with { User = user }, cancellationToken).ConfigureAwait(false);
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
        _session = null;
        await _secretStore.DeleteSecretAsync(SessionSecretKey, cancellationToken).ConfigureAwait(false);
        await _secretStore.DeleteSecretAsync(LegacyTokenSecretKey, cancellationToken).ConfigureAwait(false);
        SetUser(null);
    }

    public async Task<GitHubIssue> CreateIssueAsync(string title, string body, IEnumerable<string> labels, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title is required.", nameof(title));
        if (_session is null || !await EnsureValidTokenAsync(cancellationToken).ConfigureAwait(false))
            throw new InvalidOperationException("Sign in with GitHub to report issues.");

        using var response = await SendApiWithRefreshAsync(() =>
        {
            var request = CreateApiRequest(HttpMethod.Post, $"repos/{_options.RepositoryOwner}/{_options.RepositoryName}/issues");
            request.Content = JsonContent.Create(new { title, body, labels = labels?.ToArray() ?? [] });
            return request;
        }, cancellationToken).ConfigureAwait(false);
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
        using var response = await SendApiWithRefreshAsync(() => CreateApiRequest(HttpMethod.Get, "user"), cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var dto = await response.Content.ReadFromJsonAsync<UserResponse>(JsonOptions, cancellationToken).ConfigureAwait(false)
                  ?? throw new InvalidOperationException("Empty user response from GitHub.");
        return new GitHubUser(dto.Login, dto.Name, dto.AvatarUrl, dto.HtmlUrl);
    }

    /// <summary>
    /// Sends an API request; on 401 refreshes the access token (when a refresh token exists) and retries once.
    /// </summary>
    private async Task<HttpResponseMessage> SendApiWithRefreshAsync(Func<HttpRequestMessage> createRequest, CancellationToken cancellationToken)
    {
        using (var request = createRequest())
        {
            var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (response.StatusCode != System.Net.HttpStatusCode.Unauthorized || _session?.RefreshToken is null)
                return response;
            response.Dispose();
        }

        if (!await RefreshAsync(force: true, cancellationToken).ConfigureAwait(false))
            throw new HttpRequestException("GitHub session expired.", null, System.Net.HttpStatusCode.Unauthorized);

        using var retry = createRequest();
        return await _httpClient.SendAsync(retry, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Ensures the access token is usable, refreshing it when it is (about to be) expired. Returns false if the session ended.
    /// </summary>
    private async Task<bool> EnsureValidTokenAsync(CancellationToken cancellationToken)
    {
        if (_session is null)
            return false;
        if (!_session.IsAccessTokenExpiring(ExpirySkew))
            return true;
        return await RefreshAsync(force: false, cancellationToken).ConfigureAwait(false);
    }

    private async Task<bool> RefreshAsync(bool force, CancellationToken cancellationToken)
    {
        await _refreshLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var session = _session;
            if (session is null)
                return false;
            // Another caller may already have refreshed while we waited.
            if (!force && !session.IsAccessTokenExpiring(ExpirySkew))
                return true;

            if (string.IsNullOrEmpty(session.RefreshToken) || session.IsRefreshTokenExpired)
            {
                _logger.LogInformation("GitHub session cannot be refreshed; signing out");
                await SignOutAsync(cancellationToken).ConfigureAwait(false);
                return false;
            }

            using var request = new HttpRequestMessage(HttpMethod.Post, "https://github.com/login/oauth/access_token")
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["client_id"] = _options.ClientId,
                    ["grant_type"] = "refresh_token",
                    ["refresh_token"] = session.RefreshToken
                })
            };
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var dto = await response.Content.ReadFromJsonAsync<AccessTokenResponse>(JsonOptions, cancellationToken).ConfigureAwait(false);

            if (string.IsNullOrEmpty(dto?.AccessToken))
            {
                // bad_refresh_token / expired: the grant is gone, a new sign-in is required.
                _logger.LogInformation("GitHub token refresh rejected ({Error}); signing out", dto?.Error);
                await SignOutAsync(cancellationToken).ConfigureAwait(false);
                return false;
            }

            await SaveSessionAsync(StoredSession.FromTokenResponse(dto, session), cancellationToken).ConfigureAwait(false);
            _logger.LogDebug("GitHub access token refreshed");
            return true;
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    private async Task SaveSessionAsync(StoredSession session, CancellationToken cancellationToken)
    {
        _session = session;
        await _secretStore.StoreSecretAsync(SessionSecretKey, JsonSerializer.Serialize(session, JsonOptions), cancellationToken).ConfigureAwait(false);
        await _secretStore.DeleteSecretAsync(LegacyTokenSecretKey, cancellationToken).ConfigureAwait(false);
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
    private sealed record AccessTokenResponse(
        string? AccessToken,
        string? Error,
        string? ErrorDescription,
        string? RefreshToken = null,
        int? ExpiresIn = null,
        int? RefreshTokenExpiresIn = null);

    /// <summary>
    /// Persisted (DPAPI-encrypted) session. Expiry fields are null for non-expiring OAuth App tokens.
    /// </summary>
    private sealed record StoredSession(
        string AccessToken,
        string? RefreshToken,
        DateTimeOffset? AccessTokenExpiresAt,
        DateTimeOffset? RefreshTokenExpiresAt,
        GitHubUser? User)
    {
        public bool IsAccessTokenExpiring(TimeSpan skew) =>
            AccessTokenExpiresAt is { } expires && DateTimeOffset.UtcNow + skew >= expires;

        public bool IsRefreshTokenExpired =>
            RefreshTokenExpiresAt is { } expires && DateTimeOffset.UtcNow >= expires;

        public static StoredSession FromTokenResponse(AccessTokenResponse dto, StoredSession? previous)
        {
            var now = DateTimeOffset.UtcNow;
            return new StoredSession(
                dto.AccessToken!,
                // GitHub rotates refresh tokens; fall back to the previous one only if none was returned.
                dto.RefreshToken ?? previous?.RefreshToken,
                dto.ExpiresIn is > 0 ? now.AddSeconds(dto.ExpiresIn.Value) : null,
                dto.RefreshTokenExpiresIn is > 0 ? now.AddSeconds(dto.RefreshTokenExpiresIn.Value) : previous?.RefreshTokenExpiresAt,
                previous?.User);
        }

        public static StoredSession? Parse(string? stored)
        {
            if (string.IsNullOrWhiteSpace(stored))
                return null;

            if (stored.TrimStart().StartsWith('{'))
            {
                try
                {
                    var session = JsonSerializer.Deserialize<StoredSession>(stored, JsonOptions);
                    return string.IsNullOrEmpty(session?.AccessToken) ? null : session;
                }
                catch (JsonException)
                {
                    return null;
                }
            }

            // Sessions saved by earlier versions contain only the raw access token.
            return new StoredSession(stored.Trim(), null, null, null, null);
        }
    }
    private sealed record UserResponse(string Login, string? Name, string AvatarUrl, string HtmlUrl);
    private sealed record IssueResponse(int Number, string HtmlUrl, string Title);
}
