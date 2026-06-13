using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace QobuzDownloaderX.Cli.Qobuz;

/// <summary>
/// Cross-platform reimplementation of the Qobuz API surface that the original
/// WinForms app consumed through the closed-source QopenAPI DLL. Talks to the
/// public Qobuz JSON API with <see cref="HttpClient"/>.
/// </summary>
public sealed class QobuzApiClient
{
    private const string ApiBase = "https://www.qobuz.com/api.json/0.2/";
    private const string UserAgent =
        "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 " +
        "(KHTML, like Gecko) Chrome/120.0 Safari/537.36";

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
        NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString,
    };

    private readonly HttpClient _http;

    public string AppId { get; private set; } = "";
    public string AppSecret { get; private set; } = "";
    public string UserAuthToken { get; private set; } = "";
    public string UserId { get; private set; } = "";
    public string DisplayName { get; private set; } = "";

    private IReadOnlyList<string> _candidateSecrets = Array.Empty<string>();

    public QobuzApiClient(HttpClient? http = null)
    {
        _http = http ?? new HttpClient();
        if (!_http.DefaultRequestHeaders.UserAgent.TryParseAdd(UserAgent))
            _http.DefaultRequestHeaders.Add("User-Agent", UserAgent);
    }

    /// <summary>Use already-known credentials (e.g. loaded from config).</summary>
    public void UseCredentials(string appId, string appSecret, string authToken,
        string userId = "", string displayName = "")
    {
        AppId = appId;
        AppSecret = appSecret;
        UserAuthToken = authToken;
        UserId = userId;
        DisplayName = displayName;
        _candidateSecrets = string.IsNullOrEmpty(appSecret)
            ? Array.Empty<string>()
            : new[] { appSecret };
    }

    /// <summary>Scrape app_id and the candidate secrets from the web player.</summary>
    public async Task BootstrapAppCredentialsAsync(CancellationToken ct = default)
    {
        var creds = await BundleParser.FetchAsync(_http, ct).ConfigureAwait(false);
        AppId = creds.AppId;
        _candidateSecrets = creds.Secrets;
        if (string.IsNullOrEmpty(AppSecret) && creds.Secrets.Count > 0)
            AppSecret = creds.Secrets[0];
    }

    public async Task LoginWithEmailAsync(string email, string password, CancellationToken ct = default)
    {
        var query = new Dictionary<string, string>
        {
            ["email"] = email,
            ["password"] = password,
            ["app_id"] = AppId,
        };
        var login = await GetAsync<LoginResponse>("user/login", query, ct).ConfigureAwait(false);
        ApplyLogin(login);
    }

    public async Task LoginWithTokenAsync(string token, CancellationToken ct = default)
    {
        UserAuthToken = token;
        // Validate token and pull user info.
        var login = await GetAsync<LoginResponse>("user/login",
            new Dictionary<string, string> { ["app_id"] = AppId, ["user_auth_token"] = token }, ct)
            .ConfigureAwait(false);
        ApplyLogin(login, fallbackToken: token);
    }

    private void ApplyLogin(LoginResponse login, string? fallbackToken = null)
    {
        UserAuthToken = login.UserAuthToken ?? fallbackToken
            ?? throw new QobuzApiException("Login did not return a user auth token.");
        if (login.User != null)
        {
            UserId = login.User.Id.ToString(CultureInfo.InvariantCulture);
            DisplayName = login.User.DisplayName ?? "";
        }
    }

    public Task<Album> GetAlbumAsync(string albumId, CancellationToken ct = default) =>
        GetAsync<Album>("album/get", new() { ["album_id"] = albumId, ["extra"] = "tracks" }, ct);

    public Task<Track> GetTrackAsync(string trackId, CancellationToken ct = default) =>
        GetAsync<Track>("track/get", new() { ["track_id"] = trackId }, ct);

    public Task<Playlist> GetPlaylistAsync(string playlistId, CancellationToken ct = default) =>
        GetAsync<Playlist>("playlist/get", new()
        {
            ["playlist_id"] = playlistId,
            ["extra"] = "tracks",
            ["limit"] = "500",
            ["offset"] = "0",
        }, ct);

    public Task<Artist> GetArtistAsync(string artistId, CancellationToken ct = default) =>
        GetAsync<Artist>("artist/get", new()
        {
            ["artist_id"] = artistId,
            ["extra"] = "albums",
            ["limit"] = "500",
            ["offset"] = "0",
        }, ct);

    public Task<Label> GetLabelAsync(string labelId, CancellationToken ct = default) =>
        GetAsync<Label>("label/get", new()
        {
            ["label_id"] = labelId,
            ["extra"] = "albums",
            ["limit"] = "500",
            ["offset"] = "0",
        }, ct);

    public Task<Favorites> GetFavoritesAsync(string type, CancellationToken ct = default) =>
        GetAsync<Favorites>("favorite/getUserFavorites", new()
        {
            ["type"] = type,
            ["limit"] = "500",
            ["offset"] = "0",
        }, ct);

    /// <summary>
    /// Resolve a signed stream URL for a track. Tries each candidate secret
    /// until one is accepted, then caches the working secret.
    /// </summary>
    public async Task<FileUrl> GetTrackFileUrlAsync(string trackId, string formatId, CancellationToken ct = default)
    {
        var secrets = new List<string>();
        if (!string.IsNullOrEmpty(AppSecret)) secrets.Add(AppSecret);
        foreach (var s in _candidateSecrets)
            if (!secrets.Contains(s)) secrets.Add(s);
        if (secrets.Count == 0)
            throw new QobuzApiException("No app secret available to sign the stream request.");

        Exception? last = null;
        foreach (var secret in secrets)
        {
            try
            {
                long ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                string sigSource =
                    $"trackgetFileUrlformat_id{formatId}intentstreamtrack_id{trackId}{ts}{secret}";
                string sig = Md5Hex(sigSource);

                var query = new Dictionary<string, string>
                {
                    ["request_ts"] = ts.ToString(CultureInfo.InvariantCulture),
                    ["request_sig"] = sig,
                    ["track_id"] = trackId,
                    ["format_id"] = formatId,
                    ["intent"] = "stream",
                };

                var file = await GetAsync<FileUrl>("track/getFileUrl", query, ct).ConfigureAwait(false);
                if (string.IsNullOrEmpty(file.Url))
                    throw new QobuzApiException("Stream URL was empty (track may not be streamable on this account).");

                AppSecret = secret; // cache the working secret
                return file;
            }
            catch (QobuzApiException ex)
            {
                last = ex;
            }
        }

        throw new QobuzApiException(
            "Could not obtain a stream URL with any known app secret. " +
            "The account may lack the required subscription, or the secrets are stale.",
            inner: last);
    }

    private async Task<T> GetAsync<T>(string path, Dictionary<string, string> query, CancellationToken ct)
    {
        string url = ApiBase + path + "?" + BuildQuery(query);
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        if (!string.IsNullOrEmpty(AppId)) req.Headers.Add("X-App-Id", AppId);
        if (!string.IsNullOrEmpty(UserAuthToken)) req.Headers.Add("X-User-Auth-Token", UserAuthToken);

        using var resp = await _http.SendAsync(req, ct).ConfigureAwait(false);
        string body = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

        if (!resp.IsSuccessStatusCode)
        {
            string message = TryReadMessage(body) ?? resp.ReasonPhrase ?? "Request failed";
            throw new QobuzApiException(
                $"Qobuz API error on '{path}': {(int)resp.StatusCode} {message}",
                (int)resp.StatusCode);
        }

        try
        {
            var result = JsonSerializer.Deserialize<T>(body, JsonOpts);
            if (result == null)
                throw new QobuzApiException($"Empty response from '{path}'.");
            return result;
        }
        catch (JsonException ex)
        {
            throw new QobuzApiException($"Failed to parse response from '{path}': {ex.Message}", inner: ex);
        }
    }

    private static string? TryReadMessage(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("message", out var msg))
                return msg.GetString();
        }
        catch
        {
            // not JSON
        }
        return null;
    }

    private static string BuildQuery(Dictionary<string, string> query) =>
        string.Join("&", query.Select(kv =>
            $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value)}"));

    private static string Md5Hex(string input)
    {
        byte[] hash = MD5.HashData(Encoding.UTF8.GetBytes(input));
        var sb = new StringBuilder(hash.Length * 2);
        foreach (byte b in hash) sb.Append(b.ToString("x2", CultureInfo.InvariantCulture));
        return sb.ToString();
    }
}
