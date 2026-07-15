using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Text.Json.Nodes;
using TiHiY.StreamControlCenter.Models;

namespace TiHiY.StreamControlCenter.Services;

public sealed class DonatelloService : IAsyncDisposable
{
    private const string TokenCredentialKey = "DONATELLO_API_TOKEN";
    private const string ApiBase = "https://donatello.to/api/v1";

    private readonly AppSettingsAccessor _settings;
    private readonly SettingsService _settingsService;
    private readonly CredentialService _credentials;
    private readonly AppLogger _logger;
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(25) };
    private readonly HashSet<string> _seenIds = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, DateTime> _recentSubscriptionDonations = new(StringComparer.OrdinalIgnoreCase);
    private CancellationTokenSource? _cts;
    private Task? _pollLoop;
    private int _busy;

    public bool IsRunning => _pollLoop is not null && !_pollLoop.IsCompleted;
    public bool HasApiToken => !string.IsNullOrWhiteSpace(_credentials.LoadSecret(TokenCredentialKey));
    public string Status { get; private set; } = "НЕ ПІДКЛЮЧЕНО";
    public string ProfileNickname { get; private set; } = string.Empty;
    public string ProfilePubId { get; private set; } = string.Empty;
    public string ProfilePage { get; private set; } = string.Empty;
    public decimal ProfileTotalAmount { get; private set; }
    public int ProfileTotalCount { get; private set; }
    public int ActiveSubscriberCount { get; private set; }
    public bool HasActiveGoal { get; private set; }
    public string ActiveGoalTitle { get; private set; } = string.Empty;
    public decimal ActiveGoalCurrentAmount { get; private set; }
    public decimal ActiveGoalTargetAmount { get; private set; }
    public string ActiveGoalCurrency { get; private set; } = "UAH";
    public DateTime? RateLimitedUntil { get; private set; }
    public DateTime? LastSyncAt { get; private set; }
    public string LastError { get; private set; } = string.Empty;
    public int ConsecutiveErrors { get; private set; }
    public bool IsHealthy => IsRunning && ConsecutiveErrors < 3;

    public event EventHandler? StatusChanged;
    public event EventHandler<DonationEvent>? DonationReceived;

    public DonatelloService(AppSettingsAccessor settings, SettingsService settingsService, CredentialService credentials, AppLogger logger)
    {
        _settings = settings;
        _settingsService = settingsService;
        _credentials = credentials;
        _logger = logger;
        foreach (var id in settings.Value.DonatelloRecentEventIds.Where(x => !string.IsNullOrWhiteSpace(x)))
            _seenIds.Add(id);
    }

    public void SaveApiToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token)) throw new InvalidOperationException("Введіть API Token Donatello.");
        _credentials.SaveSecret(TokenCredentialKey, token.Trim());
    }

    public void ForgetApiToken()
    {
        _credentials.DeleteSecret(TokenCredentialKey);
        ProfileNickname = string.Empty;
        ProfilePubId = string.Empty;
        ProfilePage = string.Empty;
        ProfileTotalAmount = 0;
        ProfileTotalCount = 0;
        ActiveSubscriberCount = 0;
        HasActiveGoal = false;
        ActiveGoalTitle = string.Empty;
        ActiveGoalCurrentAmount = 0;
        ActiveGoalTargetAmount = 0;
        ActiveGoalCurrency = "UAH";
        RateLimitedUntil = null;
        LastError = string.Empty;
        ConsecutiveErrors = 0;
        SetStatus("ТОКЕН ВИДАЛЕНО");
    }

    public async Task StartAsync(CancellationToken token = default)
    {
        ValidateConfiguration();
        await StopAsync().ConfigureAwait(false);
        await TestConnectionAsync(token).ConfigureAwait(false);

        // На першому запуску фіксуємо поточний стан без показу старих подій.
        await SeedOrCatchUpAsync(token).ConfigureAwait(false);

        _cts = CancellationTokenSource.CreateLinkedTokenSource(token);
        _pollLoop = Task.Run(() => PollLoopAsync(_cts.Token), _cts.Token);
        SetStatus("ПІДКЛЮЧЕНО • ОЧІКУЮ ПОДІЇ");
    }

    public async Task StopAsync()
    {
        if (_cts is not null) _cts.Cancel();
        if (_pollLoop is not null)
        {
            try { await _pollLoop.ConfigureAwait(false); } catch { }
        }
        _pollLoop = null;
        _cts?.Dispose();
        _cts = null;
        SetStatus("НЕ ПІДКЛЮЧЕНО");
    }

    public async Task TestConnectionAsync(CancellationToken token = default)
    {
        ValidateToken();
        SetStatus("ПЕРЕВІРКА API");
        var root = await GetObjectAsync("/me", token).ConfigureAwait(false);
        ProfileNickname = ReadString(root, "nickname");
        ProfilePubId = ReadString(root, "pubId");
        ProfilePage = ReadString(root, "page");
        if (root["donates"] is JsonObject stats)
        {
            ProfileTotalAmount = ReadDecimal(stats, "totalAmount");
            ProfileTotalCount = ReadInt(stats, "totalCount");
        }
        ParseActiveGoal(root);
        LastSyncAt = DateTime.Now;
        LastError = string.Empty;
        ConsecutiveErrors = 0;
        SetStatus(string.IsNullOrWhiteSpace(ProfileNickname) ? "API ПІДКЛЮЧЕНО" : $"API ПІДКЛЮЧЕНО • {ProfileNickname}");
    }

    public async Task CheckNowAsync(CancellationToken token = default)
    {
        ValidateConfiguration();
        if (Interlocked.Exchange(ref _busy, 1) != 0) return;
        try
        {
            await RefreshOnceAsync(emitEvents: true, token).ConfigureAwait(false);
            LastError = string.Empty;
            ConsecutiveErrors = 0;
            SetStatus($"ПІДКЛЮЧЕНО • {DateTime.Now:HH:mm:ss}");
        }
        finally { Volatile.Write(ref _busy, 0); }
    }

    public async Task<int> ImportRecentAsync(CancellationToken token = default)
    {
        ValidateConfiguration();
        var donations = await GetDonationsAsync(0, 20, token).ConfigureAwait(false);
        var imported = 0;
        foreach (var item in donations.OrderBy(ParseCreatedAt))
        {
            var donation = ParseDonation(item, forceOverlay: false);
            if (donation is null) continue;
            donation.IsHistorical = true;
            donation.ShowOnOverlay = false;
            DonationReceived?.Invoke(this, donation);
            imported++;
        }
        SetStatus(imported > 0 ? $"ІМПОРТОВАНО: {imported}" : "ДОНАТИ НЕ ЗНАЙДЕНО");
        return imported;
    }

    private async Task SeedOrCatchUpAsync(CancellationToken token)
    {
        var donations = await GetDonationsAsync(0, 20, token).ConfigureAwait(false);
        var hadPersistedDonationState = _settings.Value.DonatelloRecentEventIds.Count > 0;
        foreach (var item in donations.OrderBy(ParseCreatedAt))
        {
            var id = DonationId(item);
            if (string.IsNullOrWhiteSpace(id)) continue;
            var wasSeen = _seenIds.Contains(id);
            var donation = ParseDonation(item, forceOverlay: false);
            if (donation is not null && (!wasSeen || !hadPersistedDonationState))
            {
                // Стартова синхронізація заповнює блок останніх донатів,
                // але не запускає alert та не надсилає повторні сповіщення.
                donation.IsHistorical = true;
                donation.ShowOnOverlay = false;
                DonationReceived?.Invoke(this, donation);
            }
            RememberEventId(id);
        }

        var subscribers = await GetSubscribersAsync(token).ConfigureAwait(false);
        ActiveSubscriberCount = subscribers.Count(x => ReadBool(x, "isActive"));
        var firstSubscriberState = _settings.Value.DonatelloSubscriberPayments.Count == 0;
        ProcessSubscribers(subscribers, emitEvents: !firstSubscriberState);
        SaveState();
    }

    private async Task PollLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                await CheckNowAsync(token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _logger.Error("Donatello API", ex);
                LastError = ex.GetBaseException().Message;
                ConsecutiveErrors++;
                if (RateLimitedUntil is DateTime until && until > DateTime.Now)
                {
                    var wait = Math.Max(1, (int)Math.Ceiling((until - DateTime.Now).TotalSeconds));
                    SetStatus($"ЛІМІТ API • ПОВТОР ЧЕРЕЗ {wait} С");
                }
                else
                {
                    SetStatus(ConsecutiveErrors >= 3
                        ? $"ПОМИЛКА API • {ConsecutiveErrors}"
                        : "ПІДКЛЮЧЕНО • ПОВТОР ПІСЛЯ ПОМИЛКИ");
                }
            }

            try
            {
                var normalSeconds = Math.Clamp(_settings.Value.DonatelloPollSeconds, 30, 300);
                var errorBackoff = ConsecutiveErrors switch
                {
                    <= 0 => normalSeconds,
                    1 => 30,
                    2 => 60,
                    3 => 120,
                    4 => 300,
                    _ => 600
                };
                if (RateLimitedUntil is DateTime until && until > DateTime.Now)
                    errorBackoff = Math.Max(errorBackoff, (int)Math.Ceiling((until - DateTime.Now).TotalSeconds));
                await Task.Delay(TimeSpan.FromSeconds(errorBackoff), token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task RefreshOnceAsync(bool emitEvents, CancellationToken token)
    {
        var donations = await GetDonationsAsync(0, 20, token).ConfigureAwait(false);
        foreach (var item in donations.OrderBy(ParseCreatedAt))
        {
            var id = DonationId(item);
            if (string.IsNullOrWhiteSpace(id) || _seenIds.Contains(id)) continue;
            var donation = ParseDonation(item);
            RememberEventId(id);
            if (emitEvents && donation is not null)
            {
                DonationReceived?.Invoke(this, donation);
                _logger.Info($"Donatello API: {donation.User} — {donation.DisplayAmount} ({donation.Kind})");
            }
        }

        var subscribers = await GetSubscribersAsync(token).ConfigureAwait(false);
        ActiveSubscriberCount = subscribers.Count(x => ReadBool(x, "isActive"));
        ProcessSubscribers(subscribers, emitEvents);
        LastSyncAt = DateTime.Now;
        SaveState();
    }

    private void ProcessSubscribers(IReadOnlyList<JsonObject> subscribers, bool emitEvents)
    {
        foreach (var item in subscribers)
        {
            var publicId = ReadString(item, "publicId");
            if (string.IsNullOrWhiteSpace(publicId)) publicId = ReadString(item, "mainPubOrderId");
            if (string.IsNullOrWhiteSpace(publicId)) continue;

            var payments = Math.Max(0, ReadInt(item, "successPayments"));
            var hadValue = _settings.Value.DonatelloSubscriberPayments.TryGetValue(publicId, out var oldPayments);
            _settings.Value.DonatelloSubscriberPayments[publicId] = payments;
            if (!emitEvents || !ReadBool(item, "isActive") || payments <= 0) continue;
            if (hadValue && payments <= oldPayments) continue;

            var user = FirstNonEmpty(ReadString(item, "clientName"), ReadString(item, "discordName"), ReadString(item, "twitchName"), "Підписник");
            if (_recentSubscriptionDonations.TryGetValue(user, out var recent) && DateTime.Now - recent < TimeSpan.FromMinutes(2))
                continue;

            _recentSubscriptionDonations[user] = DateTime.Now;
            var amount = ReadDecimal(item, "amount");
            var currency = FirstNonEmpty(ReadString(item, "currency"), "UAH");
            var tier = ReadString(item, "tierName");
            var donation = new DonationEvent
            {
                ExternalId = $"donatello-sub:{publicId}:{payments}",
                Source = "DONATELLO",
                Kind = "SUBSCRIPTION",
                User = user,
                Amount = amount,
                Currency = currency,
                Message = string.IsNullOrWhiteSpace(tier) ? "Платна підписка Donatello" : $"Платна підписка: {tier}",
                Accent = "#FFD329",
                ShowOnOverlay = _settings.Value.DonatelloShowOnOverlay && amount >= _settings.Value.DonatelloMinimumOverlayAmount
            };
            DonationReceived?.Invoke(this, donation);
        }
    }

    private DonationEvent? ParseDonation(JsonObject item, bool forceOverlay = false)
    {
        var id = DonationId(item);
        if (string.IsNullOrWhiteSpace(id)) return null;
        var user = FirstNonEmpty(ReadString(item, "clientName"), "Анонім");
        var actualAmount = ReadDecimal(item, "actualAmount");
        var amount = actualAmount > 0 ? actualAmount : ReadDecimal(item, "amount");
        var currency = FirstNonEmpty(ReadString(item, actualAmount > 0 ? "actualCurrency" : "currency"), ReadString(item, "currency"), "UAH");
        var isSubscription = ReadBool(item, "isSubscription");
        if (isSubscription)
        {
            if (_recentSubscriptionDonations.TryGetValue(user, out var recent) && DateTime.Now - recent < TimeSpan.FromMinutes(2))
                return null;
            _recentSubscriptionDonations[user] = DateTime.Now;
        }
        return new DonationEvent
        {
            ExternalId = $"donatello:{id}",
            Time = ParseCreatedAt(item),
            Source = "DONATELLO",
            Kind = isSubscription ? "SUBSCRIPTION" : "DONATION",
            User = user,
            Amount = amount,
            Currency = currency,
            Message = FirstNonEmpty(ReadString(item, "message"), isSubscription ? "Платна підписка Donatello" : "Підтримка через Donatello"),
            Accent = "#FFD329",
            ShowOnOverlay = forceOverlay || (_settings.Value.DonatelloShowOnOverlay && amount >= _settings.Value.DonatelloMinimumOverlayAmount)
        };
    }

    private async Task<List<JsonObject>> GetDonationsAsync(int page, int size, CancellationToken token)
    {
        var root = await GetObjectAsync($"/donates?page={Math.Max(0, page)}&size={Math.Clamp(size, 1, 100)}", token).ConfigureAwait(false);
        return (root["content"] as JsonArray)?.OfType<JsonObject>().ToList() ?? new List<JsonObject>();
    }

    private async Task<List<JsonObject>> GetSubscribersAsync(CancellationToken token)
    {
        var root = await GetObjectAsync("/subscribers?isActive=true&page=0&size=20", token).ConfigureAwait(false);
        return (root["subscribers"] as JsonArray)?.OfType<JsonObject>().ToList() ?? new List<JsonObject>();
    }

    private async Task<JsonObject> GetObjectAsync(string path, CancellationToken token)
    {
        var apiToken = _credentials.LoadSecret(TokenCredentialKey).Trim();
        using var request = new HttpRequestMessage(HttpMethod.Get, ApiBase + path);
        request.Headers.TryAddWithoutValidation("X-Token", apiToken);
        using var response = await _http.SendAsync(request, token).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(token).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.TooManyRequests)
        {
            var retrySeconds = 60;
            if (response.Headers.RetryAfter?.Delta is TimeSpan delta) retrySeconds = Math.Max(30, (int)Math.Ceiling(delta.TotalSeconds));
            else if (response.Headers.RetryAfter?.Date is DateTimeOffset retryDate) retrySeconds = Math.Max(30, (int)Math.Ceiling((retryDate - DateTimeOffset.Now).TotalSeconds));
            RateLimitedUntil = DateTime.Now.AddSeconds(retrySeconds);
            SetStatus($"ЛІМІТ API • ПОВТОР ЧЕРЕЗ {retrySeconds} С");
            throw new InvalidOperationException($"Donatello API 429: ліміт запитів, повтор через {retrySeconds} с.");
        }
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Donatello API {(int)response.StatusCode}: {Trim(body)}");
        RateLimitedUntil = null;
        return JsonNode.Parse(body) as JsonObject ?? throw new InvalidOperationException("Donatello API повернув неочікувану відповідь.");
    }


    private void ParseActiveGoal(JsonObject root)
    {
        JsonObject? goal = null;
        foreach (var key in new[] { "activeGoal", "goal", "jar", "collection", "fundraising", "activeCollection" })
        {
            if (root[key] is JsonObject obj) { goal = obj; break; }
        }
        if (goal is null && root["profile"] is JsonObject profile)
        {
            foreach (var key in new[] { "activeGoal", "goal", "jar", "collection", "fundraising", "activeCollection" })
            {
                if (profile[key] is JsonObject obj) { goal = obj; break; }
            }
        }

        if (goal is null)
        {
            HasActiveGoal = false;
            ActiveGoalTitle = string.Empty;
            ActiveGoalCurrentAmount = 0;
            ActiveGoalTargetAmount = 0;
            return;
        }

        ActiveGoalTitle = FirstNonEmpty(ReadString(goal, "title"), ReadString(goal, "name"), ReadString(goal, "description"), "Збір Donatello");
        ActiveGoalCurrentAmount = FirstPositive(
            ReadDecimal(goal, "currentAmount"), ReadDecimal(goal, "collectedAmount"),
            ReadDecimal(goal, "amount"), ReadDecimal(goal, "raised"), ReadDecimal(goal, "totalAmount"));
        ActiveGoalTargetAmount = FirstPositive(
            ReadDecimal(goal, "targetAmount"), ReadDecimal(goal, "goalAmount"),
            ReadDecimal(goal, "target"), ReadDecimal(goal, "limit"), ReadDecimal(goal, "requiredAmount"));
        ActiveGoalCurrency = FirstNonEmpty(ReadString(goal, "currency"), ReadString(goal, "currencyCode"), "UAH").ToUpperInvariant();
        HasActiveGoal = ActiveGoalTargetAmount > 0 || !string.IsNullOrWhiteSpace(ActiveGoalTitle);
    }

    private static decimal FirstPositive(params decimal[] values) => values.FirstOrDefault(x => x > 0);

    private void ValidateConfiguration()
    {
        if (!_settings.Value.DonatelloEnabled) throw new InvalidOperationException("Увімкніть інтеграцію Donatello.");
        ValidateToken();
    }

    private void ValidateToken()
    {
        if (!HasApiToken) throw new InvalidOperationException("Введіть і збережіть офіційний API Token Donatello.");
    }

    private void RememberEventId(string id)
    {
        if (!_seenIds.Add(id)) return;
        var list = _settings.Value.DonatelloRecentEventIds;
        list.Add(id);
        while (list.Count > 200) list.RemoveAt(0);
    }

    private void SaveState() => _settingsService.Save(_settings.Value);

    private static string DonationId(JsonObject item) => FirstNonEmpty(ReadString(item, "pubId"), ReadString(item, "id"));

    private static DateTime ParseCreatedAt(JsonObject item)
    {
        var raw = ReadString(item, "createdAt");
        if (long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var unix))
        {
            try { return DateTimeOffset.FromUnixTimeSeconds(unix).LocalDateTime; } catch { }
        }
        if (DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var dto)) return dto.LocalDateTime;
        if (DateTime.TryParse(raw, CultureInfo.CurrentCulture, DateTimeStyles.AssumeLocal, out var dt)) return dt;
        return DateTime.Now;
    }

    private static string ReadString(JsonObject obj, string name)
    {
        var node = obj[name];
        if (node is null) return string.Empty;
        try { return node.GetValue<string>()?.Trim() ?? string.Empty; }
        catch { return node.ToJsonString().Trim('"'); }
    }

    private static decimal ReadDecimal(JsonObject obj, string name)
    {
        var raw = ReadString(obj, name).Replace(" ", string.Empty).Replace(',', '.');
        return decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var value) ? value : 0m;
    }

    private static int ReadInt(JsonObject obj, string name)
    {
        var raw = ReadString(obj, name);
        return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : 0;
    }

    private static bool ReadBool(JsonObject obj, string name)
    {
        var raw = ReadString(obj, name);
        return bool.TryParse(raw, out var value) && value;
    }

    private static string FirstNonEmpty(params string[] values) => values.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)) ?? string.Empty;

    private static string Trim(string value)
    {
        var text = value.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return text.Length <= 400 ? text : text[..400] + "…";
    }

    private void SetStatus(string value)
    {
        Status = value;
        _logger.Info($"Donatello: {value}");
        StatusChanged?.Invoke(this, EventArgs.Empty);
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync().ConfigureAwait(false);
        _http.Dispose();
    }
}
