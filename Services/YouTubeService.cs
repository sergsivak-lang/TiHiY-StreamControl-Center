using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using TiHiY.StreamControlCenter.Models;

namespace TiHiY.StreamControlCenter.Services;

public sealed class YouTubeService : IAsyncDisposable
{
    private const string RedirectUri = "http://127.0.0.1:17847/";
    private const string Scope = "https://www.googleapis.com/auth/youtube.force-ssl";
    private readonly AppSettingsAccessor _settings;
    private readonly SettingsService _settingsService;
    private readonly CredentialService _credentials;
    private readonly AppLogger _logger;
    private readonly HttpClient _http = new();
    private OAuthToken _token = new();
    private CancellationTokenSource? _cts;
    private Task? _pollLoop;
    private string _liveChatId = string.Empty;
    private string _broadcastId = string.Empty;
    private string _nextPageToken = string.Empty;
    private int _pollIntervalMs = 10_000;
    private bool _lastLive;
    private string _lastBroadcastId = string.Empty;
    private DateTime _lastBroadcastLookupUtc = DateTime.MinValue;
    private DateTime _lastStatsUpdateUtc = DateTime.MinValue;
    private bool _quotaExceeded;
    private readonly HashSet<string> _seenDonationIds = new(StringComparer.Ordinal);

    public bool IsAuthorized => !string.IsNullOrWhiteSpace(_token.AccessToken);
    public bool IsConnected => _pollLoop is not null && !_pollLoop.IsCompleted;
    public bool HasLiveChat => IsConnected && !_quotaExceeded && !string.IsNullOrWhiteSpace(_liveChatId);
    public bool QuotaExceeded => _quotaExceeded;
    public string Status { get; private set; } = "НЕ ПІДКЛЮЧЕНО";
    public string ActiveBroadcastId => _broadcastId;
    public event EventHandler? StatusChanged;
    public event EventHandler<ChatMessage>? MessageReceived;
    public event EventHandler<DonationEvent>? DonationReceived;
    public event EventHandler<StreamLiveInfo>? LiveStateChanged;
    public event EventHandler<StreamLiveInfo>? StatsChanged;

    public YouTubeService(AppSettingsAccessor settings, SettingsService settingsService, CredentialService credentials, AppLogger logger)
    {
        _settings = settings;
        _settingsService = settingsService;
        _credentials = credentials;
        _logger = logger;
        LoadToken();
    }

    public async Task AuthorizeAsync(string clientId, string clientSecret, CancellationToken token = default)
    {
        if (string.IsNullOrWhiteSpace(clientId))
            throw new InvalidOperationException("Вкажіть Google OAuth Client ID типу Desktop app.");
        _settings.Value.YouTubeClientId = clientId.Trim();
        var effectiveClientSecret = clientSecret.Trim();
        if (!string.IsNullOrWhiteSpace(effectiveClientSecret))
            _credentials.SaveSecret("YOUTUBE_CLIENT_SECRET", effectiveClientSecret);
        else
            effectiveClientSecret = _credentials.LoadSecret("YOUTUBE_CLIENT_SECRET");
        if (string.IsNullOrWhiteSpace(effectiveClientSecret))
            throw new InvalidOperationException("Вкажіть Google OAuth Client Secret для цього Desktop OAuth-клієнта.");
        var state = Guid.NewGuid().ToString("N");
        var url = "https://accounts.google.com/o/oauth2/v2/auth" +
                  $"?client_id={Uri.EscapeDataString(clientId.Trim())}" +
                  $"&redirect_uri={Uri.EscapeDataString(RedirectUri)}" +
                  "&response_type=code" +
                  $"&scope={Uri.EscapeDataString(Scope)}" +
                  "&access_type=offline&prompt=consent" +
                  $"&state={state}";
        SetStatus("ОЧІКУВАННЯ БРАУЗЕРА");
        var result = await OAuthLoopback.AuthorizeAsync(url, RedirectUri, token);
        if (result.TryGetValue("error", out var error)) throw new InvalidOperationException($"Google OAuth: {error}");
        if (!result.TryGetValue("state", out var returnedState) || returnedState != state) throw new InvalidOperationException("Google OAuth: невірний state.");
        if (!result.TryGetValue("code", out var code) || string.IsNullOrWhiteSpace(code)) throw new InvalidOperationException("Google OAuth не повернув код.");
        _token = await ExchangeCodeAsync(code, clientId.Trim(), effectiveClientSecret, token);
        SaveToken();
        _settingsService.Save(_settings.Value);
        await ConnectAsync(token);
    }

    public async Task ConnectAsync(CancellationToken token = default)
    {
        await EnsureTokenAsync(token);
        if (!_token.IsUsable) throw new InvalidOperationException("YouTube не авторизовано.");
        await DisconnectAsync();
        _quotaExceeded = false;
        _lastBroadcastLookupUtc = DateTime.MinValue;
        _lastStatsUpdateUtc = DateTime.MinValue;
        _pollIntervalMs = 10_000;
        _cts = CancellationTokenSource.CreateLinkedTokenSource(token);
        _pollLoop = Task.Run(() => PollLoopAsync(_cts.Token));
        SetStatus("ПОШУК ТРАНСЛЯЦІЇ");
    }

    public async Task SendMessageAsync(string text, CancellationToken token = default)
    {
        await EnsureTokenAsync(token);
        if (_quotaExceeded)
            throw new InvalidOperationException("Добову квоту YouTube API вичерпано. Після відновлення квоти перепідключіть YouTube.");
        if (string.IsNullOrWhiteSpace(text)) return;

        await EnsureLiveChatContextAsync(token);
        if (string.IsNullOrWhiteSpace(_liveChatId))
            throw new InvalidOperationException("Активний YouTube live chat не знайдено. Переконайтеся, що ефір запущений і чат увімкнений.");

        try
        {
            await PostMessageAsync(text, token);
        }
        catch (InvalidOperationException ex) when (!_quotaExceeded && IsLiveChatContextError(ex.Message))
        {
            _liveChatId = string.Empty;
            _nextPageToken = string.Empty;
            await RefreshBroadcastContextAsync(token, true);
            if (string.IsNullOrWhiteSpace(_liveChatId)) throw;
            await PostMessageAsync(text, token);
        }
    }

    private async Task PostMessageAsync(string text, CancellationToken token)
    {
        var payload = new JsonObject
        {
            ["snippet"] = new JsonObject
            {
                ["liveChatId"] = _liveChatId,
                ["type"] = "textMessageEvent",
                ["textMessageDetails"] = new JsonObject { ["messageText"] = text.Trim() }
            }
        };
        await ApiAsync(HttpMethod.Post, "liveChat/messages?part=snippet", payload, token);
    }

    public Task TimeoutUserAsync(string channelId, int durationSeconds = 600, CancellationToken token = default) =>
        BanOrTimeoutAsync(channelId, Math.Clamp(durationSeconds, 1, 86_400), false, token);

    public Task BanUserAsync(string channelId, CancellationToken token = default) =>
        BanOrTimeoutAsync(channelId, null, true, token);

    public async Task DeleteMessageAsync(string messageId, CancellationToken token = default)
    {
        await EnsureTokenAsync(token);
        if (string.IsNullOrWhiteSpace(messageId)) throw new InvalidOperationException("ID повідомлення YouTube відсутній.");
        await ApiAsync(HttpMethod.Delete, $"liveChat/messages?id={Uri.EscapeDataString(messageId)}", null, token);
    }

    private async Task BanOrTimeoutAsync(string channelId, int? durationSeconds, bool permanent, CancellationToken token)
    {
        await EnsureTokenAsync(token);
        await EnsureLiveChatContextAsync(token);
        if (string.IsNullOrWhiteSpace(_liveChatId)) throw new InvalidOperationException("Активний YouTube live chat не знайдено.");
        if (string.IsNullOrWhiteSpace(channelId)) throw new InvalidOperationException("YouTube не передав ID каналу учасника.");
        var snippet = new JsonObject
        {
            ["liveChatId"] = _liveChatId,
            ["type"] = permanent ? "permanent" : "temporary",
            ["bannedUserDetails"] = new JsonObject { ["channelId"] = channelId }
        };
        if (!permanent && durationSeconds.HasValue) snippet["banDurationSeconds"] = durationSeconds.Value.ToString();
        await ApiAsync(HttpMethod.Post, "liveChat/bans?part=snippet", new JsonObject { ["snippet"] = snippet }, token);
    }

    public async Task<IReadOnlyList<YouTubeBroadcastSettings>> GetBroadcastsAsync(CancellationToken token = default)
    {
        await EnsureTokenAsync(token);
        var json = await ApiAsync(HttpMethod.Get, "liveBroadcasts?part=id,snippet,status&mine=true&broadcastType=all&maxResults=50", null, token);
        var result = new List<YouTubeBroadcastSettings>();
        if (json["items"] is JsonArray items)
        {
            foreach (var item in items.OfType<JsonObject>())
            {
                var snippet = item["snippet"] as JsonObject ?? new JsonObject();
                var status = item["status"] as JsonObject ?? new JsonObject();
                var dateText = snippet["scheduledStartTime"]?.GetValue<string>();
                DateTime.TryParse(dateText, out var scheduled);
                result.Add(new YouTubeBroadcastSettings
                {
                    Id = item["id"]?.GetValue<string>() ?? string.Empty,
                    Title = snippet["title"]?.GetValue<string>() ?? string.Empty,
                    Description = snippet["description"]?.GetValue<string>() ?? string.Empty,
                    PrivacyStatus = status["privacyStatus"]?.GetValue<string>() ?? "private",
                    ScheduledStartTime = scheduled == default ? DateTime.Now : scheduled.ToLocalTime(),
                    LifeCycleStatus = status["lifeCycleStatus"]?.GetValue<string>() ?? string.Empty,
                    LiveChatId = snippet["liveChatId"]?.GetValue<string>() ?? string.Empty
                });
            }
        }
        return result.OrderByDescending(x => x.LifeCycleStatus == "live").ThenBy(x => x.ScheduledStartTime).ToList();
    }

    public async Task UpdateBroadcastAsync(YouTubeBroadcastSettings broadcast, CancellationToken token = default)
    {
        if (string.IsNullOrWhiteSpace(broadcast.Id)) throw new InvalidOperationException("Оберіть трансляцію YouTube.");
        await EnsureTokenAsync(token);
        var payload = new JsonObject
        {
            ["id"] = broadcast.Id,
            ["snippet"] = new JsonObject
            {
                ["title"] = broadcast.Title,
                ["description"] = broadcast.Description,
                ["scheduledStartTime"] = broadcast.ScheduledStartTime.ToUniversalTime().ToString("O")
            },
            ["status"] = new JsonObject
            {
                ["privacyStatus"] = broadcast.PrivacyStatus,
                ["selfDeclaredMadeForKids"] = false
            }
        };
        await ApiAsync(HttpMethod.Put, "liveBroadcasts?part=snippet,status", payload, token);
    }

    public async Task DisconnectAsync()
    {
        if (_cts is not null) _cts.Cancel();
        if (_pollLoop is not null) try { await _pollLoop.ConfigureAwait(false); } catch { }
        _pollLoop = null;
        _cts?.Dispose();
        _cts = null;
        _liveChatId = string.Empty;
        _broadcastId = string.Empty;
        _nextPageToken = string.Empty;
        _quotaExceeded = false;
        SetStatus("НЕ ПІДКЛЮЧЕНО");
    }

    public void ForgetAuthorization()
    {
        _credentials.DeleteSecret("YOUTUBE_TOKEN");
        _credentials.DeleteSecret("YOUTUBE_CLIENT_SECRET");
        _token = new OAuthToken();
        _settings.Value.YouTubeActiveBroadcastId = string.Empty;
        _settingsService.Save(_settings.Value);
        SetStatus("АВТОРИЗАЦІЮ ВИДАЛЕНО");
    }

    private async Task PollLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            if (_quotaExceeded)
            {
                SetStatus("КВОТУ ВИЧЕРПАНО");
                try { await Task.Delay(TimeSpan.FromMinutes(15), token); }
                catch { break; }
                _quotaExceeded = false;
                _lastBroadcastLookupUtc = DateTime.MinValue;
            }

            try
            {
                await EnsureTokenAsync(token);
                var now = DateTime.UtcNow;
                var refreshInterval = string.IsNullOrWhiteSpace(_broadcastId) ? TimeSpan.FromSeconds(30) : TimeSpan.FromSeconds(60);
                if (now - _lastBroadcastLookupUtc >= refreshInterval)
                    await RefreshBroadcastContextAsync(token, true);

                var live = !string.IsNullOrWhiteSpace(_broadcastId);
                var info = BuildLiveInfo(live);

                if (live && !string.IsNullOrWhiteSpace(_liveChatId))
                {
                    if (now - _lastStatsUpdateUtc >= TimeSpan.FromSeconds(30))
                    {
                        await UpdateStatsAsync(info, token);
                        _lastStatsUpdateUtc = now;
                        StatsChanged?.Invoke(this, info);
                    }
                    await ReadChatAsync(token);
                    SetStatus("ЧАТ ПІДКЛЮЧЕНО");
                }
                else if (live)
                {
                    SetStatus("ЕФІР Є • ЧАТ НЕДОСТУПНИЙ");
                }
                else
                {
                    _settings.Value.YouTubeViewers = 0;
                    _settings.Value.YouTubeLikes = 0;
                    SetStatus("ЕФІР НЕ ЗНАЙДЕНО");
                }

                if (live != _lastLive || (live && _broadcastId != _lastBroadcastId))
                    LiveStateChanged?.Invoke(this, info);
                _lastLive = live;
                _lastBroadcastId = _broadcastId;
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _logger.Error("YouTube live", ex);
                if (!_quotaExceeded) SetStatus("ПОМИЛКА API");
            }

            var delay = string.IsNullOrWhiteSpace(_liveChatId) ? 15_000 : Math.Clamp(_pollIntervalMs, 10_000, 30_000);
            try { await Task.Delay(delay, token); } catch { break; }
        }
    }

    private async Task EnsureLiveChatContextAsync(CancellationToken token)
    {
        if (!string.IsNullOrWhiteSpace(_liveChatId)) return;
        await RefreshBroadcastContextAsync(token, true);
    }

    private async Task RefreshBroadcastContextAsync(CancellationToken token, bool save)
    {
        var mine = await ApiAsync(HttpMethod.Get, "liveBroadcasts?part=id,snippet,status&mine=true&broadcastType=all&maxResults=50", null, token);
        var item = mine["items"] is JsonArray broadcasts
            ? broadcasts.OfType<JsonObject>().FirstOrDefault(IsLiveBroadcast)
            : null;
        var id = item?["id"]?.GetValue<string>() ?? string.Empty;
        var snippet = item?["snippet"] as JsonObject;
        var title = snippet?["title"]?.GetValue<string>() ?? string.Empty;
        var chatId = snippet?["liveChatId"]?.GetValue<string>() ?? string.Empty;

        if (!string.Equals(id, _broadcastId, StringComparison.Ordinal) ||
            !string.Equals(chatId, _liveChatId, StringComparison.Ordinal))
        {
            _broadcastId = id;
            _liveChatId = chatId;
            _nextPageToken = string.Empty;
        }

        _lastBroadcastLookupUtc = DateTime.UtcNow;
        _settings.Value.YouTubeLive = !string.IsNullOrWhiteSpace(id);
        _settings.Value.YouTubeActiveBroadcastId = id;
        _settings.Value.YouTubeStreamTitle = title;
        if (save) _settingsService.Save(_settings.Value);
    }

    private StreamLiveInfo BuildLiveInfo(bool live) => new()
    {
        Platform = "YouTube",
        IsLive = live,
        BroadcastId = _broadcastId,
        Title = _settings.Value.YouTubeStreamTitle,
        Url = string.IsNullOrWhiteSpace(_broadcastId) ? "https://www.youtube.com/@TiHiY-DED/live" : $"https://www.youtube.com/watch?v={_broadcastId}",
        ThumbnailUrl = string.IsNullOrWhiteSpace(_broadcastId) ? string.Empty : $"https://i.ytimg.com/vi/{_broadcastId}/maxresdefault.jpg"
    };

    private static bool IsLiveBroadcast(JsonObject broadcast)
    {
        var status = broadcast["status"] as JsonObject;
        var lifeCycleStatus = status?["lifeCycleStatus"]?.GetValue<string>() ?? string.Empty;
        return lifeCycleStatus.Equals("live", StringComparison.OrdinalIgnoreCase);
    }

    private async Task UpdateStatsAsync(StreamLiveInfo info, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(_broadcastId)) return;
        var video = await ApiAsync(HttpMethod.Get, $"videos?part=liveStreamingDetails,statistics&id={Uri.EscapeDataString(_broadcastId)}", null, token);
        var item = video["items"]?.AsArray().FirstOrDefault()?.AsObject();
        var live = item?["liveStreamingDetails"] as JsonObject;
        var stats = item?["statistics"] as JsonObject;
        int.TryParse(live?["concurrentViewers"]?.GetValue<string>(), out var viewers);
        int.TryParse(stats?["likeCount"]?.GetValue<string>(), out var likes);
        _settings.Value.YouTubeViewers = viewers;
        _settings.Value.YouTubeLikes = likes;
        info.Viewers = viewers;
        info.Likes = likes;
        _settingsService.Save(_settings.Value);
    }

    private async Task ReadChatAsync(CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(_liveChatId)) return;
        var path = $"liveChat/messages?part=id,snippet,authorDetails&liveChatId={Uri.EscapeDataString(_liveChatId)}&maxResults=200";
        if (!string.IsNullOrWhiteSpace(_nextPageToken)) path += $"&pageToken={Uri.EscapeDataString(_nextPageToken)}";
        var json = await ApiAsync(HttpMethod.Get, path, null, token);
        _nextPageToken = json["nextPageToken"]?.GetValue<string>() ?? _nextPageToken;
        _pollIntervalMs = Math.Clamp(json["pollingIntervalMillis"]?.GetValue<int>() ?? 10_000, 10_000, 30_000);
        if (json["items"] is not JsonArray items) return;
        foreach (var item in items.OfType<JsonObject>())
        {
            var snippet = item["snippet"] as JsonObject ?? new JsonObject();
            var author = item["authorDetails"] as JsonObject ?? new JsonObject();
            var type = snippet["type"]?.GetValue<string>() ?? string.Empty;
            var isPaid = type.Contains("superChat", StringComparison.OrdinalIgnoreCase) || type.Contains("superSticker", StringComparison.OrdinalIgnoreCase);
            var isMembership = type.Contains("newSponsor", StringComparison.OrdinalIgnoreCase)
                || type.Contains("memberMilestone", StringComparison.OrdinalIgnoreCase)
                || type.Contains("membershipGifting", StringComparison.OrdinalIgnoreCase)
                || type.Contains("giftMembershipReceived", StringComparison.OrdinalIgnoreCase);
            var text = snippet["displayMessage"]?.GetValue<string>() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(text) && isMembership) text = "Нова платна підписка YouTube";
            if (string.IsNullOrWhiteSpace(text) && isPaid) text = "Платне повідомлення YouTube";
            if (string.IsNullOrWhiteSpace(text)) continue;
            var role = author["isChatOwner"]?.GetValue<bool>() == true ? "Owner" :
                       author["isChatModerator"]?.GetValue<bool>() == true ? "Moderator" :
                       author["isChatSponsor"]?.GetValue<bool>() == true ? "Subscriber" : "Viewer";
            if (isPaid || isMembership) role = "Donor";
            var messageId = item["id"]?.GetValue<string>() ?? string.Empty;
            if (isPaid && !string.IsNullOrWhiteSpace(messageId) && _seenDonationIds.Add(messageId))
            {
                var details = (snippet["superChatDetails"] as JsonObject) ?? (snippet["superStickerDetails"] as JsonObject) ?? new JsonObject();
                var micros = details["amountMicros"]?.GetValue<string>() ?? "0";
                decimal.TryParse(micros, out var amountMicros);
                var currency = details["currency"]?.GetValue<string>() ?? string.Empty;
                var comment = details["userComment"]?.GetValue<string>() ?? text;
                DonationReceived?.Invoke(this, new DonationEvent
                {
                    ExternalId = "youtube:" + messageId,
                    Source = type.Contains("Sticker", StringComparison.OrdinalIgnoreCase) ? "YOUTUBE SUPER STICKER" : "YOUTUBE SUPER CHAT",
                    Kind = "DONATION",
                    User = author["displayName"]?.GetValue<string>() ?? "YouTube",
                    Amount = amountMicros / 1_000_000m,
                    Currency = currency,
                    Message = comment,
                    Accent = "#FF4B4B"
                });
            }
            else if (isMembership && !string.IsNullOrWhiteSpace(messageId) && _seenDonationIds.Add(messageId))
            {
                var sponsor = snippet["newSponsorDetails"] as JsonObject;
                var milestone = snippet["memberMilestoneChatDetails"] as JsonObject;
                var level = sponsor?["memberLevelName"]?.GetValue<string>()
                    ?? milestone?["memberLevelName"]?.GetValue<string>()
                    ?? "YouTube Member";
                DonationReceived?.Invoke(this, new DonationEvent
                {
                    ExternalId = "youtube:" + messageId,
                    Source = "YOUTUBE MEMBER",
                    Kind = "SUBSCRIPTION",
                    User = author["displayName"]?.GetValue<string>() ?? "YouTube",
                    Amount = 1,
                    Currency = "MEMBER",
                    Message = level + (string.IsNullOrWhiteSpace(text) ? string.Empty : " • " + text),
                    Accent = "#FF4B4B"
                });
            }
            MessageReceived?.Invoke(this, new ChatMessage
            {
                Platform = "YOUTUBE",
                ExternalId = item["id"]?.GetValue<string>() ?? string.Empty,
                User = author["displayName"]?.GetValue<string>() ?? "YouTube",
                AuthorId = author["channelId"]?.GetValue<string>() ?? string.Empty,
                Text = text,
                Role = role,
                Time = DateTime.Now
            });
        }
    }

    private async Task<JsonObject> ApiAsync(HttpMethod method, string path, JsonObject? payload, CancellationToken token)
    {
        if (!_token.IsUsable) throw new InvalidOperationException("YouTube не авторизовано. Відкрийте «Канали» та виконайте OAuth-авторизацію.");
        using var request = new HttpRequestMessage(method, "https://www.googleapis.com/youtube/v3/" + path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token.AccessToken);
        if (payload is not null) request.Content = new StringContent(payload.ToJsonString(), Encoding.UTF8, "application/json");
        using var response = await _http.SendAsync(request, token);
        var body = await response.Content.ReadAsStringAsync(token);
        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            throw new InvalidOperationException("YouTube OAuth-токен відсутній або прострочений. Повторно авторизуйте YouTube у модулі «Канали».");
        if (!response.IsSuccessStatusCode)
        {
            var reason = ExtractApiReason(body);
            if (reason.Equals("quotaExceeded", StringComparison.OrdinalIgnoreCase) ||
                body.Contains("exceeded your", StringComparison.OrdinalIgnoreCase) && body.Contains("quota", StringComparison.OrdinalIgnoreCase))
            {
                _quotaExceeded = true;
                SetStatus("КВОТУ ВИЧЕРПАНО");
                throw new InvalidOperationException("Добову квоту YouTube API вичерпано. Програма призупинила автоматичні запити на 15 хвилин.");
            }
            throw new InvalidOperationException(FriendlyApiError((int)response.StatusCode, reason, body));
        }
        return string.IsNullOrWhiteSpace(body) ? new JsonObject() : JsonNode.Parse(body)?.AsObject() ?? new JsonObject();
    }

    private static string ExtractApiReason(string body)
    {
        try
        {
            var json = JsonNode.Parse(body)?.AsObject();
            return json?["error"]?["errors"]?.AsArray().FirstOrDefault()?["reason"]?.GetValue<string>() ?? string.Empty;
        }
        catch { return string.Empty; }
    }

    private static string FriendlyApiError(int statusCode, string reason, string raw)
    {
        if (reason.Contains("liveChatEnded", StringComparison.OrdinalIgnoreCase)) return "YouTube live chat уже завершено.";
        if (reason.Contains("liveChatDisabled", StringComparison.OrdinalIgnoreCase)) return "Чат для цієї трансляції YouTube вимкнено.";
        if (reason.Contains("liveChatNotFound", StringComparison.OrdinalIgnoreCase)) return "Активний YouTube live chat не знайдено.";
        if (reason.Contains("forbidden", StringComparison.OrdinalIgnoreCase)) return "YouTube заборонив операцію. Перевірте права OAuth і власника трансляції.";
        var compact = raw.Length > 500 ? raw[..500] + "…" : raw;
        return $"YouTube API {statusCode}: {compact}";
    }

    private static bool IsLiveChatContextError(string message) =>
        message.Contains("live chat", StringComparison.OrdinalIgnoreCase) ||
        message.Contains("liveChat", StringComparison.OrdinalIgnoreCase);

    private async Task<OAuthToken> ExchangeCodeAsync(string code, string clientId, string clientSecret, CancellationToken token)
    {
        var form = new Dictionary<string, string>
        {
            ["client_id"] = clientId,
            ["code"] = code,
            ["grant_type"] = "authorization_code",
            ["redirect_uri"] = RedirectUri
        };
        if (!string.IsNullOrWhiteSpace(clientSecret)) form["client_secret"] = clientSecret;
        return await RequestTokenAsync(form, token);
    }

    private async Task EnsureTokenAsync(CancellationToken token)
    {
        if (_token.IsUsable) return;
        if (string.IsNullOrWhiteSpace(_token.RefreshToken) || string.IsNullOrWhiteSpace(_settings.Value.YouTubeClientId))
            throw new InvalidOperationException("YouTube не авторизовано. Відкрийте «Канали» → YouTube → «Авторизувати».");
        var secret = _credentials.LoadSecret("YOUTUBE_CLIENT_SECRET");
        var form = new Dictionary<string, string>
        {
            ["client_id"] = _settings.Value.YouTubeClientId,
            ["refresh_token"] = _token.RefreshToken,
            ["grant_type"] = "refresh_token"
        };
        if (!string.IsNullOrWhiteSpace(secret)) form["client_secret"] = secret;
        _token = await RequestTokenAsync(form, token);
        SaveToken();
    }

    private async Task<OAuthToken> RequestTokenAsync(Dictionary<string, string> form, CancellationToken token)
    {
        using var response = await _http.PostAsync("https://oauth2.googleapis.com/token", new FormUrlEncodedContent(form), token);
        var body = await response.Content.ReadAsStringAsync(token);
        if (!response.IsSuccessStatusCode) throw new InvalidOperationException($"Google token {(int)response.StatusCode}: {body}");
        var json = JsonNode.Parse(body)?.AsObject() ?? new JsonObject();
        return new OAuthToken
        {
            AccessToken = json["access_token"]?.GetValue<string>() ?? string.Empty,
            RefreshToken = json["refresh_token"]?.GetValue<string>() ?? _token.RefreshToken,
            TokenType = json["token_type"]?.GetValue<string>() ?? "Bearer",
            ExpiresAtUtc = DateTime.UtcNow.AddSeconds(json["expires_in"]?.GetValue<int>() ?? 3600)
        };
    }

    private void LoadToken()
    {
        try
        {
            var raw = _credentials.LoadSecret("YOUTUBE_TOKEN");
            if (!string.IsNullOrWhiteSpace(raw)) _token = JsonSerializer.Deserialize<OAuthToken>(raw) ?? new OAuthToken();
        }
        catch { _token = new OAuthToken(); }
    }

    private void SaveToken() => _credentials.SaveSecret("YOUTUBE_TOKEN", JsonSerializer.Serialize(_token));

    private void SetStatus(string value)
    {
        if (Status == value) return;
        Status = value;
        StatusChanged?.Invoke(this, EventArgs.Empty);
        _logger.Info($"YouTube: {value}");
    }

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync().ConfigureAwait(false);
        _http.Dispose();
    }
}
