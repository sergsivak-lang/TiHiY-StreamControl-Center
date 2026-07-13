using TiHiY.StreamControlCenter.Models;

namespace TiHiY.StreamControlCenter.Services;

public sealed class AppServices : IAsyncDisposable
{
    private int _disposeState;
    public SettingsService SettingsService { get; } = new();
    public AppSettingsAccessor Settings { get; } = new();
    public AppLogger Logger { get; } = new();
    public CredentialService Credentials { get; } = new();
    public ObsWebSocketService Obs { get; } = new();
    public MusicPlayerService Music { get; } = new();
    public DonationService Donations { get; } = new();
    public DonatelloService Donatello { get; }
    public UiScaleService UiScale { get; }
    public WindowPlacementService Placement { get; }
    public WindowManager Windows { get; }
    public ChatService Chat { get; }
    public OverlayServer Overlay { get; }
    public TwitchService Twitch { get; }
    public YouTubeService YouTube { get; }
    public DiscordNotificationService Discord { get; }
    public StreamNotificationBotService Notifications { get; }
    public bool BridgeAvailable { get; private set; }
    public string BridgeStatus { get; private set; } = "НЕ ПЕРЕВІРЕНО";
    public event EventHandler? BridgeStatusChanged;
    public event EventHandler? ChannelStatusChanged;

    public AppServices()
    {
        Settings.Value = SettingsService.Load();
        UiScale = new UiScaleService(Settings);
        Placement = new WindowPlacementService(Settings, SettingsService);
        Windows = new WindowManager(Logger);
        Chat = new ChatService(Settings, SettingsService, Logger);
        Twitch = new TwitchService(Settings, SettingsService, Credentials, Logger);
        YouTube = new YouTubeService(Settings, SettingsService, Credentials, Logger);
        Discord = new DiscordNotificationService(Settings, SettingsService, Credentials, Logger);
        Donatello = new DonatelloService(Settings, SettingsService, Credentials, Logger);
        Notifications = new StreamNotificationBotService(Settings, SettingsService, Credentials, Twitch, YouTube, Discord, Logger);
        Donations.GoalAmount = Settings.Value.DonationGoalAmount;
        Chat.SongProvider = () => Music.CurrentTrack?.Display ?? "нічого";
        Chat.MessageSender = SendChatAsync;
        Music.Restore(Settings.Value.MusicPlaylistPaths);
        Overlay = new OverlayServer(
            () => Application.Current.Dispatcher.Invoke(() => (IReadOnlyList<ChatMessage>)Chat.Messages.ToList()),
            () => Application.Current.Dispatcher.Invoke(() => (IReadOnlyList<DonationEvent>)Donations.History.ToList()),
            () => Application.Current.Dispatcher.Invoke(BuildNowPlayingPayload),
            () => new
            {
                twitchViewers = Settings.Value.TwitchViewers,
                youtubeViewers = Settings.Value.YouTubeViewers,
                youtubeLikes = Settings.Value.YouTubeLikes,
                twitchLive = Settings.Value.TwitchLive,
                youtubeLive = Settings.Value.YouTubeLive
            },
            () => Settings.Value.OverlayTheme);
        Obs.Log += (_, m) => Logger.Info(m);
        Music.PlaybackError += (_, m) => Logger.Error($"Плеєр: {m}");
        Twitch.MessageReceived += Channel_MessageReceived;
        YouTube.MessageReceived += Channel_MessageReceived;
        Twitch.DonationReceived += Channel_DonationReceived;
        YouTube.DonationReceived += Channel_DonationReceived;
        Donatello.DonationReceived += Channel_DonationReceived;
        Donatello.StatusChanged += Channel_StatusChanged;
        Twitch.StatusChanged += Channel_StatusChanged;
        YouTube.StatusChanged += Channel_StatusChanged;
        Twitch.StatsChanged += Channel_StatsChanged;
        YouTube.StatsChanged += Channel_StatsChanged;
    }

    public async Task InitializeAsync()
    {
        Chat.Start();
        try
        {
            await Overlay.StartAsync(Settings.Value.OverlayPort);
            Logger.Info($"Overlay Server: http://127.0.0.1:{Settings.Value.OverlayPort}");
        }
        catch (Exception ex) { Logger.Error("Overlay Server не запущено", ex); }

        if (Settings.Value.TwitchAutoConnect && Twitch.IsAuthorized)
            _ = SafeConnectAsync(() => Twitch.ConnectAsync(), "Twitch автопідключення");
        if (Settings.Value.YouTubeAutoConnect && YouTube.IsAuthorized)
            _ = SafeConnectAsync(() => YouTube.ConnectAsync(), "YouTube автопідключення");
        if (Settings.Value.NotificationBotAutoStart && Settings.Value.DiscordNotificationsEnabled)
            _ = SafeConnectAsync(() => Notifications.StartAsync(), "Автозапуск Discord-бота сповіщень");
        if (Settings.Value.DonatelloEnabled && Settings.Value.DonatelloAutoStart)
            _ = SafeConnectAsync(() => Donatello.StartAsync(), "Автозапуск Donatello");
    }

    private async Task SafeConnectAsync(Func<Task> action, string name)
    {
        try { await action(); }
        catch (Exception ex) { Logger.Error(name, ex); }
    }

    private void Channel_MessageReceived(object? sender, ChatMessage message) =>
        Application.Current.Dispatcher.BeginInvoke(new Action(() => Chat.AddIncoming(message)));

    private void Channel_DonationReceived(object? sender, DonationEvent donation)
    {
        Application.Current.Dispatcher.BeginInvoke(new Action(() =>
        {
            Donations.Add(donation);
            if (donation.IsHistorical) return;
            if (donation.Source.Contains("DONATELLO", StringComparison.OrdinalIgnoreCase))
            {
                var chatText = donation.Kind.Equals("SUBSCRIPTION", StringComparison.OrdinalIgnoreCase)
                    ? $"⭐ {donation.User}: платна підписка • {donation.Message}"
                    : $"💛 {donation.User}: {donation.DisplayAmount} • {donation.Message}";
                if (Settings.Value.DonatelloShowInChat)
                {
                    Chat.AddIncoming(new ChatMessage
                    {
                        Platform = "DONATELLO",
                        User = donation.User,
                        Text = $"{(donation.Kind.Equals("SUBSCRIPTION", StringComparison.OrdinalIgnoreCase) ? "Платна підписка" : donation.DisplayAmount)} • {donation.Message}",
                        Role = "Donor",
                        ExternalId = "money:" + donation.StableId,
                        Time = donation.Time
                    });
                }
                if (Settings.Value.DonatelloSendToPlatformChats)
                {
                    var target = Twitch.IsChatConnected && YouTube.IsConnected ? "Twitch + YouTube"
                        : Twitch.IsChatConnected ? "Twitch"
                        : YouTube.IsConnected ? "YouTube" : string.Empty;
                    if (!string.IsNullOrWhiteSpace(target)) _ = Chat.SendManualAsync(chatText, target);
                }
            }
        }));

        if (!donation.IsHistorical)
            _ = SafeConnectAsync(() => Discord.NotifyMonetizationAsync(donation), "Discord: донат або платна підписка");
    }

    private void Channel_StatusChanged(object? sender, EventArgs e) =>
        Application.Current.Dispatcher.BeginInvoke(new Action(() => ChannelStatusChanged?.Invoke(this, EventArgs.Empty)));

    private void Channel_StatsChanged(object? sender, StreamLiveInfo info) =>
        Application.Current.Dispatcher.BeginInvoke(new Action(() => ChannelStatusChanged?.Invoke(this, EventArgs.Empty)));


    public async Task SendChatAsync(string text, string target)
    {
        var errors = new List<string>();
        if (target.Contains("Twitch", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                await Twitch.SendMessageAsync(text);
            }
            catch (Exception ex) { errors.Add("Twitch: " + ex.Message); }
        }
        if (target.Contains("YouTube", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                await YouTube.SendMessageAsync(text);
            }
            catch (Exception ex) { errors.Add("YouTube: " + ex.Message); }
        }
        if (errors.Count > 0) throw new InvalidOperationException(string.Join(Environment.NewLine, errors));
    }

    public async Task ModerateChatUserAsync(ChatMessage message, bool permanent, int timeoutSeconds = 600)
    {
        if (string.IsNullOrWhiteSpace(message.AuthorId))
            throw new InvalidOperationException("Платформа не передала ID учасника чату.");
        if (message.Platform.Equals("TWITCH", StringComparison.OrdinalIgnoreCase))
        {
            if (permanent) await Twitch.BanUserAsync(message.AuthorId, $"Модерація TiHiY StreamControl Center: {message.User}");
            else await Twitch.TimeoutUserAsync(message.AuthorId, timeoutSeconds, $"Модерація TiHiY StreamControl Center: {message.User}");
            return;
        }
        if (message.Platform.Equals("YOUTUBE", StringComparison.OrdinalIgnoreCase))
        {
            if (permanent) await YouTube.BanUserAsync(message.AuthorId);
            else await YouTube.TimeoutUserAsync(message.AuthorId, timeoutSeconds);
            return;
        }
        throw new InvalidOperationException("Модерація доступна лише для Twitch і YouTube.");
    }

    public async Task DeleteChatMessageAsync(ChatMessage message)
    {
        if (string.IsNullOrWhiteSpace(message.ExternalId)) throw new InvalidOperationException("ID повідомлення відсутній.");
        if (message.Platform.Equals("TWITCH", StringComparison.OrdinalIgnoreCase))
            await Twitch.DeleteMessageAsync(message.ExternalId);
        else if (message.Platform.Equals("YOUTUBE", StringComparison.OrdinalIgnoreCase))
            await YouTube.DeleteMessageAsync(message.ExternalId);
        else
            throw new InvalidOperationException("Видалення доступне лише для Twitch і YouTube.");
    }

    public void SetBridgeStatus(bool available, string status)
    {
        BridgeAvailable = available;
        BridgeStatus = status;
        BridgeStatusChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Save()
    {
        Settings.Value.DonationGoalAmount = Donations.GoalAmount;
        Settings.Value.ScheduledNotices = Chat.Notices.ToList();
        Settings.Value.BotCommands = Chat.Commands.ToList();
        Settings.Value.MusicPlaylistPaths = Music.Playlist.Select(x => x.FilePath).ToList();
        SettingsService.Save(Settings.Value);
    }

    private object BuildNowPlayingPayload()
    {
        var track = Music.CurrentTrack;
        return new
        {
            active = track is not null && (Music.IsPlaying || Music.Position > TimeSpan.Zero),
            title = track?.Title ?? string.Empty,
            artist = track?.Artist ?? string.Empty,
            positionSeconds = Music.Position.TotalSeconds,
            durationSeconds = Music.Duration.TotalSeconds
        };
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposeState, 1) != 0) return;

        try { Chat.Stop(); } catch { }
        try { Save(); } catch { }
        try { Music.Dispose(); } catch { }

        try { await Notifications.DisposeAsync().ConfigureAwait(false); } catch { }
        try { await Donatello.DisposeAsync().ConfigureAwait(false); } catch { }
        try { await Twitch.DisposeAsync().ConfigureAwait(false); } catch { }
        try { await YouTube.DisposeAsync().ConfigureAwait(false); } catch { }
        try { Discord.Dispose(); } catch { }
        try { await Overlay.StopAsync().ConfigureAwait(false); } catch { }
        try { await Obs.DisconnectAsync().ConfigureAwait(false); } catch { }
    }
}
