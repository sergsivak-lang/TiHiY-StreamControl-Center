using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using TiHiY.StreamControlCenter.Models;

namespace TiHiY.StreamControlCenter.Services;

public sealed class TwitchService : IAsyncDisposable
{
    private const string RedirectUri = "http://localhost:17846/twitch/";
    private readonly AppSettingsAccessor _settings;
    private readonly SettingsService _settingsService;
    private readonly CredentialService _credentials;
    private readonly AppLogger _logger;
    private readonly HttpClient _http = new();
    private ClientWebSocket? _irc;
    private CancellationTokenSource? _cts;
    private Task? _ircLoop;
    private Task? _statsLoop;
    private OAuthToken _token = new();
    private bool _lastLive;
    private string _lastStreamId = string.Empty;

    public bool IsAuthorized => !string.IsNullOrWhiteSpace(_token.AccessToken);
    public bool IsChatConnected => _irc?.State == WebSocketState.Open;
    public string Status { get; private set; } = "НЕ ПІДКЛЮЧЕНО";
    public event EventHandler? StatusChanged;
    public event EventHandler<ChatMessage>? MessageReceived;
    public event EventHandler<DonationEvent>? DonationReceived;
    public event EventHandler<StreamLiveInfo>? LiveStateChanged;
    public event EventHandler<StreamLiveInfo>? StatsChanged;

    public TwitchService(AppSettingsAccessor settings, SettingsService settingsService, CredentialService credentials, AppLogger logger)
    {
        _settings = settings;
        _settingsService = settingsService;
        _credentials = credentials;
        _logger = logger;
        LoadToken();
    }

    public async Task AuthorizeAsync(string clientId, string clientSecret, CancellationToken token = default)
    {
        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
            throw new InvalidOperationException("Вкажіть Twitch Client ID і Client Secret.");
        _settings.Value.TwitchClientId = clientId.Trim();
        _credentials.SaveSecret("TWITCH_CLIENT_SECRET", clientSecret.Trim());
        var state = Guid.NewGuid().ToString("N");
        var scope = Uri.EscapeDataString("chat:read chat:edit user:read:chat user:write:chat moderator:manage:banned_users moderator:manage:chat_messages");
        var url = $"https://id.twitch.tv/oauth2/authorize?client_id={Uri.EscapeDataString(clientId.Trim())}&redirect_uri={Uri.EscapeDataString(RedirectUri)}&response_type=code&scope={scope}&state={state}";
        SetStatus("ОЧІКУВАННЯ БРАУЗЕРА");
        var result = await OAuthLoopback.AuthorizeAsync(url, RedirectUri, token);
        if (result.TryGetValue("error", out var error)) throw new InvalidOperationException($"Twitch OAuth: {error}");
        if (!result.TryGetValue("state", out var returnedState) || returnedState != state) throw new InvalidOperationException("Twitch OAuth: невірний state.");
        if (!result.TryGetValue("code", out var code) || string.IsNullOrWhiteSpace(code)) throw new InvalidOperationException("Twitch OAuth не повернув код.");
        _token = await ExchangeCodeAsync(code, clientId.Trim(), clientSecret.Trim(), token);
        SaveToken();
        _settingsService.Save(_settings.Value);
        await ConnectAsync(token);
    }

    public async Task ConnectAsync(CancellationToken token = default)
    {
        await EnsureTokenAsync(token);
        if (!_token.IsUsable) throw new InvalidOperationException("Twitch не авторизовано.");
        await DisconnectChatAsync();
        await ResolveUsersAsync(token);
        if (string.IsNullOrWhiteSpace(_settings.Value.TwitchUserLogin)) throw new InvalidOperationException("Не вдалося визначити Twitch-користувача токена.");
        _cts = CancellationTokenSource.CreateLinkedTokenSource(token);
        _irc = new ClientWebSocket();
        await _irc.ConnectAsync(new Uri("wss://irc-ws.chat.twitch.tv:443"), _cts.Token);
        await SendIrcAsync($"PASS oauth:{_token.AccessToken}", _cts.Token);
        await SendIrcAsync($"NICK {_settings.Value.TwitchUserLogin.ToLowerInvariant()}", _cts.Token);
        await SendIrcAsync("CAP REQ :twitch.tv/tags twitch.tv/commands", _cts.Token);
        await SendIrcAsync($"JOIN #{_settings.Value.TwitchChannelName.Trim().TrimStart('#').ToLowerInvariant()}", _cts.Token);
        SetStatus("ЧАТ ПІДКЛЮЧЕНО");
        _ircLoop = Task.Run(() => IrcReceiveLoopAsync(_cts.Token));
        _statsLoop = Task.Run(() => StatsLoopAsync(_cts.Token));
    }

    public async Task SendMessageAsync(string text, CancellationToken token = default)
    {
        if (!IsChatConnected) throw new InvalidOperationException("Twitch чат не підключено.");
        var safe = text.Replace("\r", " ").Replace("\n", " ").Trim();
        if (safe.Length == 0) return;
        await SendIrcAsync($"PRIVMSG #{_settings.Value.TwitchChannelName.Trim().TrimStart('#').ToLowerInvariant()} :{safe}", token);
    }

    public Task TimeoutUserAsync(string userId, int durationSeconds = 600, string reason = "Модерація чату", CancellationToken token = default) =>
        BanOrTimeoutAsync(userId, Math.Clamp(durationSeconds, 1, 1_209_600), reason, token);

    public Task BanUserAsync(string userId, string reason = "Модерація чату", CancellationToken token = default) =>
        BanOrTimeoutAsync(userId, null, reason, token);

    public async Task DeleteMessageAsync(string messageId, CancellationToken token = default)
    {
        await EnsureTokenAsync(token);
        EnsureModerationIds();
        var path = $"moderation/chat?broadcaster_id={Uri.EscapeDataString(_settings.Value.TwitchBroadcasterId)}&moderator_id={Uri.EscapeDataString(_settings.Value.TwitchUserId)}&message_id={Uri.EscapeDataString(messageId)}";
        await HelixRequestAsync(HttpMethod.Delete, path, null, token);
    }

    private async Task BanOrTimeoutAsync(string userId, int? durationSeconds, string reason, CancellationToken token)
    {
        await EnsureTokenAsync(token);
        EnsureModerationIds();
        var data = new JsonObject
        {
            ["user_id"] = userId,
            ["reason"] = string.IsNullOrWhiteSpace(reason) ? "Модерація чату" : reason
        };
        if (durationSeconds.HasValue) data["duration"] = durationSeconds.Value;
        var payload = new JsonObject { ["data"] = data };
        var path = $"moderation/bans?broadcaster_id={Uri.EscapeDataString(_settings.Value.TwitchBroadcasterId)}&moderator_id={Uri.EscapeDataString(_settings.Value.TwitchUserId)}";
        await HelixRequestAsync(HttpMethod.Post, path, payload, token);
    }

    private void EnsureModerationIds()
    {
        if (string.IsNullOrWhiteSpace(_settings.Value.TwitchBroadcasterId) || string.IsNullOrWhiteSpace(_settings.Value.TwitchUserId))
            throw new InvalidOperationException("Twitch не передав broadcaster/moderator ID. Перепідключіть канал.");
    }

    public async Task DisconnectAsync()
    {
        await DisconnectChatAsync();
        SetStatus("НЕ ПІДКЛЮЧЕНО");
    }

    public void ForgetAuthorization()
    {
        _credentials.DeleteSecret("TWITCH_TOKEN");
        _credentials.DeleteSecret("TWITCH_CLIENT_SECRET");
        _token = new OAuthToken();
        _settings.Value.TwitchUserId = string.Empty;
        _settings.Value.TwitchBroadcasterId = string.Empty;
        _settings.Value.TwitchUserLogin = string.Empty;
        _settingsService.Save(_settings.Value);
        SetStatus("АВТОРИЗАЦІЮ ВИДАЛЕНО");
    }

    private async Task ResolveUsersAsync(CancellationToken token)
    {
        var me = await HelixGetAsync("users", token);
        var meItem = me["data"]?.AsArray().FirstOrDefault()?.AsObject();
        _settings.Value.TwitchUserId = meItem?["id"]?.GetValue<string>() ?? string.Empty;
        _settings.Value.TwitchUserLogin = meItem?["login"]?.GetValue<string>() ?? string.Empty;
        var channel = _settings.Value.TwitchChannelName.Trim().TrimStart('#');
        var broadcaster = await HelixGetAsync($"users?login={Uri.EscapeDataString(channel)}", token);
        _settings.Value.TwitchBroadcasterId = broadcaster["data"]?.AsArray().FirstOrDefault()?["id"]?.GetValue<string>() ?? string.Empty;
        _settingsService.Save(_settings.Value);
    }

    private async Task StatsLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                await EnsureTokenAsync(token);
                var channel = _settings.Value.TwitchChannelName.Trim().TrimStart('#');
                var json = await HelixGetAsync($"streams?user_login={Uri.EscapeDataString(channel)}", token);
                var stream = json["data"]?.AsArray().FirstOrDefault()?.AsObject();
                var live = stream is not null;
                var id = stream?["id"]?.GetValue<string>() ?? string.Empty;
                var title = stream?["title"]?.GetValue<string>() ?? string.Empty;
                var viewers = stream?["viewer_count"]?.GetValue<int>() ?? 0;
                var thumbnail = stream?["thumbnail_url"]?.GetValue<string>() ?? string.Empty;
                thumbnail = thumbnail.Replace("{width}", "1280", StringComparison.Ordinal).Replace("{height}", "720", StringComparison.Ordinal);
                DateTimeOffset? startedAt = null;
                var startedText = stream?["started_at"]?.GetValue<string>();
                if (DateTimeOffset.TryParse(startedText, out var parsedStartedAt)) startedAt = parsedStartedAt;
                _settings.Value.TwitchLive = live;
                _settings.Value.TwitchViewers = viewers;
                _settings.Value.TwitchStreamTitle = title;
                _settings.Value.TwitchCurrentStreamId = id;
                var info = new StreamLiveInfo
                {
                    Platform = "Twitch",
                    IsLive = live,
                    BroadcastId = id,
                    Title = title,
                    Url = $"https://www.twitch.tv/{channel}",
                    Viewers = viewers,
                    ThumbnailUrl = thumbnail,
                    StartedAtUtc = startedAt
                };
                StatsChanged?.Invoke(this, info);
                if (live != _lastLive || (live && id != _lastStreamId)) LiveStateChanged?.Invoke(this, info);
                _lastLive = live;
                _lastStreamId = id;
                _settingsService.Save(_settings.Value);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex) { _logger.Error("Twitch статистика", ex); }
            try { await Task.Delay(TimeSpan.FromSeconds(15), token); } catch { break; }
        }
    }

    private async Task IrcReceiveLoopAsync(CancellationToken token)
    {
        var buffer = new byte[16384];
        var text = new StringBuilder();
        try
        {
            while (!token.IsCancellationRequested && _irc?.State == WebSocketState.Open)
            {
                WebSocketReceiveResult result;
                do
                {
                    result = await _irc.ReceiveAsync(new ArraySegment<byte>(buffer), token);
                    if (result.MessageType == WebSocketMessageType.Close) return;
                    text.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
                } while (!result.EndOfMessage);
                var payload = text.ToString();
                text.Clear();
                foreach (var line in payload.Split("\r\n", StringSplitOptions.RemoveEmptyEntries))
                {
                    if (line.StartsWith("PING", StringComparison.OrdinalIgnoreCase))
                    {
                        await SendIrcAsync(line.Replace("PING", "PONG", StringComparison.OrdinalIgnoreCase), token);
                        continue;
                    }
                    var message = ParsePrivMsg(line);
                    if (message is not null) MessageReceived?.Invoke(this, message);
                    var donation = ParseDonation(line);
                    if (donation is not null) DonationReceived?.Invoke(this, donation);
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _logger.Error("Twitch IRC", ex);
            SetStatus("ПОМИЛКА ЧАТУ");
        }
    }

    private static ChatMessage? ParsePrivMsg(string line)
    {
        var privIndex = line.IndexOf(" PRIVMSG ", StringComparison.Ordinal);
        if (privIndex < 0) return null;
        var tags = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (line.StartsWith('@'))
        {
            var space = line.IndexOf(' ');
            if (space > 1)
                foreach (var pair in line[1..space].Split(';'))
                {
                    var parts = pair.Split('=', 2);
                    tags[parts[0]] = parts.Length > 1 ? DecodeTag(parts[1]) : string.Empty;
                }
        }
        var textStart = line.IndexOf(" :", privIndex, StringComparison.Ordinal);
        if (textStart < 0) return null;
        var text = line[(textStart + 2)..];
        var user = tags.TryGetValue("display-name", out var display) && !string.IsNullOrWhiteSpace(display) ? display : "Twitch";
        var badges = tags.TryGetValue("badges", out var badgeText) ? badgeText : string.Empty;
        var role = badges.Contains("broadcaster/") ? "Owner" : badges.Contains("moderator/") ? "Moderator" : badges.Contains("vip/") ? "VIP" : badges.Contains("subscriber/") ? "Subscriber" : "Viewer";
        return new ChatMessage { Platform = "TWITCH", User = user, Text = text, Role = role, ExternalId = tags.TryGetValue("id", out var id) ? id : string.Empty, AuthorId = tags.TryGetValue("user-id", out var userId) ? userId : string.Empty, Time = DateTime.Now };
    }


    private static DonationEvent? ParseDonation(string line)
    {
        if (!line.StartsWith('@')) return null;
        var space = line.IndexOf(' ');
        if (space <= 1) return null;
        var tags = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in line[1..space].Split(';'))
        {
            var parts = pair.Split('=', 2);
            tags[parts[0]] = parts.Length > 1 ? DecodeTag(parts[1]) : string.Empty;
        }
        var user = tags.TryGetValue("display-name", out var display) && !string.IsNullOrWhiteSpace(display) ? display : "Twitch";
        if (tags.TryGetValue("bits", out var bitsText) && decimal.TryParse(bitsText, out var bits) && bits > 0)
        {
            var privIndex = line.IndexOf(" PRIVMSG ", StringComparison.Ordinal);
            var textStart = privIndex >= 0 ? line.IndexOf(" :", privIndex, StringComparison.Ordinal) : -1;
            return new DonationEvent
            {
                ExternalId = tags.TryGetValue("id", out var bitsId) ? "twitch:" + bitsId : string.Empty,
                Source = "TWITCH BITS", Kind = "DONATION", User = user, Amount = bits, Currency = "BITS",
                Message = textStart >= 0 ? line[(textStart + 2)..] : "Bits", Accent = "#A970FF"
            };
        }
        if (line.Contains(" USERNOTICE ", StringComparison.Ordinal) && tags.TryGetValue("msg-id", out var msgId) &&
            (msgId.Contains("sub", StringComparison.OrdinalIgnoreCase) || msgId.Contains("gift", StringComparison.OrdinalIgnoreCase)))
        {
            var message = tags.TryGetValue("system-msg", out var systemMessage) ? systemMessage : msgId;
            return new DonationEvent
            {
                ExternalId = tags.TryGetValue("id", out var subId) ? "twitch:" + subId : string.Empty,
                Source = "TWITCH SUB", Kind = "SUBSCRIPTION", User = user, Amount = 1, Currency = "SUB", Message = message, Accent = "#A970FF"
            };
        }
        return null;
    }

    private static string DecodeTag(string value) => value.Replace("\\s", " ").Replace("\\:", ";").Replace("\\r", "\r").Replace("\\n", "\n").Replace("\\\\", "\\");

    private async Task SendIrcAsync(string line, CancellationToken token)
    {
        if (_irc is null || _irc.State != WebSocketState.Open) throw new InvalidOperationException("Twitch IRC не підключено.");
        var bytes = Encoding.UTF8.GetBytes(line + "\r\n");
        await _irc.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, token);
    }

    private Task<JsonObject> HelixGetAsync(string path, CancellationToken token) => HelixRequestAsync(HttpMethod.Get, path, null, token);

    private async Task<JsonObject> HelixRequestAsync(HttpMethod method, string path, JsonObject? payload, CancellationToken token)
    {
        using var request = new HttpRequestMessage(method, "https://api.twitch.tv/helix/" + path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token.AccessToken);
        request.Headers.Add("Client-Id", _settings.Value.TwitchClientId);
        if (payload is not null) request.Content = new StringContent(payload.ToJsonString(), Encoding.UTF8, "application/json");
        using var response = await _http.SendAsync(request, token);
        var body = await response.Content.ReadAsStringAsync(token);
        if (!response.IsSuccessStatusCode) throw new InvalidOperationException($"Twitch API {(int)response.StatusCode}: {body}");
        return string.IsNullOrWhiteSpace(body) ? new JsonObject() : JsonNode.Parse(body)?.AsObject() ?? new JsonObject();
    }

    private async Task<OAuthToken> ExchangeCodeAsync(string code, string clientId, string clientSecret, CancellationToken token)
    {
        var form = new Dictionary<string, string>
        {
            ["client_id"] = clientId,
            ["client_secret"] = clientSecret,
            ["code"] = code,
            ["grant_type"] = "authorization_code",
            ["redirect_uri"] = RedirectUri
        };
        return await RequestTokenAsync(form, token);
    }

    private async Task EnsureTokenAsync(CancellationToken token)
    {
        if (_token.IsUsable) return;
        if (string.IsNullOrWhiteSpace(_token.RefreshToken)) return;
        var secret = _credentials.LoadSecret("TWITCH_CLIENT_SECRET");
        if (string.IsNullOrWhiteSpace(secret) || string.IsNullOrWhiteSpace(_settings.Value.TwitchClientId)) return;
        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = _token.RefreshToken,
            ["client_id"] = _settings.Value.TwitchClientId,
            ["client_secret"] = secret
        };
        _token = await RequestTokenAsync(form, token);
        SaveToken();
    }

    private async Task<OAuthToken> RequestTokenAsync(Dictionary<string, string> form, CancellationToken token)
    {
        using var response = await _http.PostAsync("https://id.twitch.tv/oauth2/token", new FormUrlEncodedContent(form), token);
        var body = await response.Content.ReadAsStringAsync(token);
        if (!response.IsSuccessStatusCode) throw new InvalidOperationException($"Twitch token {(int)response.StatusCode}: {body}");
        var json = JsonNode.Parse(body)?.AsObject() ?? new JsonObject();
        return new OAuthToken
        {
            AccessToken = json["access_token"]?.GetValue<string>() ?? string.Empty,
            RefreshToken = json["refresh_token"]?.GetValue<string>() ?? _token.RefreshToken,
            TokenType = json["token_type"]?.GetValue<string>() ?? "bearer",
            ExpiresAtUtc = DateTime.UtcNow.AddSeconds(json["expires_in"]?.GetValue<int>() ?? 3600)
        };
    }

    private void LoadToken()
    {
        try
        {
            var raw = _credentials.LoadSecret("TWITCH_TOKEN");
            if (!string.IsNullOrWhiteSpace(raw)) _token = JsonSerializer.Deserialize<OAuthToken>(raw) ?? new OAuthToken();
        }
        catch { _token = new OAuthToken(); }
    }

    private void SaveToken() => _credentials.SaveSecret("TWITCH_TOKEN", JsonSerializer.Serialize(_token));

    private async Task DisconnectChatAsync()
    {
        if (_cts is not null) _cts.Cancel();
        if (_irc is not null)
        {
            try
            {
                if (_irc.State == WebSocketState.Open)
                {
                    using var closeCts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
                    await _irc.CloseAsync(WebSocketCloseStatus.NormalClosure, "closing", closeCts.Token).ConfigureAwait(false);
                }
            }
            catch { }
            _irc.Dispose();
        }
        if (_ircLoop is not null) try { await _ircLoop.ConfigureAwait(false); } catch { }
        if (_statsLoop is not null) try { await _statsLoop.ConfigureAwait(false); } catch { }
        _irc = null;
        _ircLoop = null;
        _statsLoop = null;
        _cts?.Dispose();
        _cts = null;
    }

    private void SetStatus(string value)
    {
        Status = value;
        StatusChanged?.Invoke(this, EventArgs.Empty);
        _logger.Info($"Twitch: {value}");
    }

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync().ConfigureAwait(false);
        _http.Dispose();
    }
}
