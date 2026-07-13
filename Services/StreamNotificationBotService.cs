using TiHiY.StreamControlCenter.Models;

namespace TiHiY.StreamControlCenter.Services;

/// <summary>
/// Integrated local Discord stream notification bot.
/// It reuses the real Twitch/YouTube OAuth connections from StreamControl Center,
/// listens for live-state changes, suppresses duplicate notices, and sends Discord messages.
/// </summary>
public sealed class StreamNotificationBotService : IAsyncDisposable
{
    private readonly AppSettingsAccessor _settings;
    private readonly SettingsService _settingsService;
    private readonly CredentialService _credentials;
    private readonly TwitchService _twitch;
    private readonly YouTubeService _youtube;
    private readonly DiscordNotificationService _discord;
    private readonly AppLogger _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private bool _subscribed;

    public bool IsRunning { get; private set; }
    public string Status { get; private set; } = "ЗУПИНЕНО";

    public event EventHandler? StatusChanged;
    public event EventHandler<string>? Log;

    public StreamNotificationBotService(
        AppSettingsAccessor settings,
        SettingsService settingsService,
        CredentialService credentials,
        TwitchService twitch,
        YouTubeService youtube,
        DiscordNotificationService discord,
        AppLogger logger)
    {
        _settings = settings;
        _settingsService = settingsService;
        _credentials = credentials;
        _twitch = twitch;
        _youtube = youtube;
        _discord = discord;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken token = default)
    {
        await _gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            if (IsRunning)
            {
                WriteLog("Бот сповіщень уже працює.");
                return;
            }

            ValidateDiscordConfiguration();
            Subscribe();
            IsRunning = true;
            SetStatus("ПРАЦЮЄ");
            WriteLog("TiHiY Stream Notify Bot запущено всередині StreamControl Center.");

            if (_settings.Value.DiscordNotifyTwitch && !_twitch.IsAuthorized)
                WriteLog("Twitch ще не авторизовано. Відкрийте модуль «Канали». ");
            if (_settings.Value.DiscordNotifyYouTube && !_youtube.IsAuthorized)
                WriteLog("YouTube ще не авторизовано. Відкрийте модуль «Канали». ");

            await CheckNowInternalAsync(token).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task StopAsync()
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (!IsRunning) return;
            Unsubscribe();
            IsRunning = false;
            SetStatus("ЗУПИНЕНО");
            WriteLog("TiHiY Stream Notify Bot зупинено.");
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task CheckNowAsync(CancellationToken token = default)
    {
        await _gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            ValidateDiscordConfiguration();
            await CheckNowInternalAsync(token).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SendTestAsync(CancellationToken token = default)
    {
        ValidateDiscordConfiguration();
        await _discord.TestAsync(token).ConfigureAwait(false);
        WriteLog("Тестове Discord-сповіщення надіслано.");
    }

    private async Task CheckNowInternalAsync(CancellationToken token)
    {
        var settings = _settings.Value;
        var foundLive = false;

        if (settings.DiscordNotifyTwitch && _twitch.IsChatConnected && settings.TwitchLive)
        {
            foundLive = true;
            await ProcessLiveAsync(new StreamLiveInfo
            {
                Platform = "Twitch",
                IsLive = true,
                BroadcastId = settings.TwitchCurrentStreamId,
                Title = settings.TwitchStreamTitle,
                Url = $"https://www.twitch.tv/{settings.TwitchChannelName.Trim().TrimStart('#')}",
                Viewers = settings.TwitchViewers
            }, token).ConfigureAwait(false);
        }

        if (settings.DiscordNotifyYouTube && _youtube.IsConnected && settings.YouTubeLive)
        {
            foundLive = true;
            var id = settings.YouTubeActiveBroadcastId;
            await ProcessLiveAsync(new StreamLiveInfo
            {
                Platform = "YouTube",
                IsLive = true,
                BroadcastId = id,
                Title = settings.YouTubeStreamTitle,
                Url = string.IsNullOrWhiteSpace(id)
                    ? "https://www.youtube.com/@TiHiY-DED/live"
                    : $"https://www.youtube.com/watch?v={id}",
                Viewers = settings.YouTubeViewers,
                Likes = settings.YouTubeLikes,
                ThumbnailUrl = string.IsNullOrWhiteSpace(id)
                    ? string.Empty
                    : $"https://i.ytimg.com/vi/{id}/maxresdefault.jpg"
            }, token).ConfigureAwait(false);
        }

        if (!foundLive)
        {
            var missing = new List<string>();
            if (settings.DiscordNotifyTwitch && !_twitch.IsChatConnected) missing.Add("Twitch не підключено");
            if (settings.DiscordNotifyYouTube && !_youtube.IsConnected) missing.Add("YouTube не підключено");
            WriteLog(missing.Count > 0
                ? "Перевірку відкладено: " + string.Join(", ", missing) + "."
                : "Перевірку виконано: активних трансляцій поки не знайдено.");
        }
    }

    private async void Twitch_LiveStateChanged(object? sender, StreamLiveInfo info) =>
        await HandleLiveEventSafeAsync(info).ConfigureAwait(false);

    private async void YouTube_LiveStateChanged(object? sender, StreamLiveInfo info) =>
        await HandleLiveEventSafeAsync(info).ConfigureAwait(false);

    private async Task HandleLiveEventSafeAsync(StreamLiveInfo info)
    {
        if (!IsRunning || !info.IsLive) return;
        try
        {
            await ProcessLiveAsync(info, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            WriteLog($"{info.Platform}: {ex.GetBaseException().Message}");
            _logger.Error($"Discord-сповіщення {info.Platform}", ex);
        }
    }

    private async Task ProcessLiveAsync(StreamLiveInfo info, CancellationToken token)
    {
        if (!IsRunning && !_settings.Value.NotificationBotAutoStart) return;
        if (info.Platform.Equals("Twitch", StringComparison.OrdinalIgnoreCase) && !_settings.Value.DiscordNotifyTwitch) return;
        if (info.Platform.Equals("YouTube", StringComparison.OrdinalIgnoreCase) && !_settings.Value.DiscordNotifyYouTube) return;

        var alreadySent = info.Platform.Equals("Twitch", StringComparison.OrdinalIgnoreCase)
            ? !string.IsNullOrWhiteSpace(info.BroadcastId) && string.Equals(_settings.Value.TwitchLastStreamId, info.BroadcastId, StringComparison.Ordinal)
            : !string.IsNullOrWhiteSpace(info.BroadcastId) && string.Equals(_settings.Value.YouTubeLastNotifiedBroadcastId, info.BroadcastId, StringComparison.Ordinal);

        if (alreadySent)
        {
            WriteLog($"{info.Platform}: сповіщення про цю трансляцію вже надсилалося.");
            return;
        }

        await _discord.NotifyLiveAsync(info, token).ConfigureAwait(false);
        WriteLog($"{info.Platform}: сповіщення про початок трансляції надіслано.");
    }


    private void ValidateDiscordConfiguration()
    {
        if (string.IsNullOrWhiteSpace(_credentials.LoadSecret("DISCORD_BOT_TOKEN")))
            throw new InvalidOperationException("Discord Bot Token не збережено.");
        if (string.IsNullOrWhiteSpace(_settings.Value.DiscordChannelIds))
            throw new InvalidOperationException("Не вказано ID текстових каналів Discord.");
        if (!_settings.Value.DiscordNotificationsEnabled)
            throw new InvalidOperationException("Увімкніть Discord-сповіщення у модулі «СПОВІЩЕННЯ».");
    }

    private void Subscribe()
    {
        if (_subscribed) return;
        _twitch.LiveStateChanged += Twitch_LiveStateChanged;
        _youtube.LiveStateChanged += YouTube_LiveStateChanged;
        _subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!_subscribed) return;
        _twitch.LiveStateChanged -= Twitch_LiveStateChanged;
        _youtube.LiveStateChanged -= YouTube_LiveStateChanged;
        _subscribed = false;
    }

    private void SetStatus(string status)
    {
        Status = status;
        StatusChanged?.Invoke(this, EventArgs.Empty);
    }

    private void WriteLog(string message)
    {
        _logger.Info("Notify Bot: " + message);
        Log?.Invoke(this, $"[{DateTime.Now:HH:mm:ss}] {message}");
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync().ConfigureAwait(false);
        _gate.Dispose();
    }
}
