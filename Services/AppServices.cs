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
    public Aida64SensorService SystemMonitor { get; }
    public MusicPlayerService Music { get; } = new();
    public DonationService Donations { get; }
    public DonatelloService Donatello { get; }
    public UiScaleService UiScale { get; }
    public ThemeService Theme { get; }
    public LanguageService Language { get; }
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
        Donations = new DonationService(SettingsService.Folder, Logger);
        SystemMonitor = new Aida64SensorService(Logger);
        Language = new LanguageService(Settings, SettingsService);
        Language.ApplySavedLanguage();
        Theme = new ThemeService(Settings, SettingsService);
        Theme.ApplySavedTheme();
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
        Donations.GoalCurrency = string.IsNullOrWhiteSpace(Settings.Value.DonationGoalCurrency) ? "UAH" : Settings.Value.DonationGoalCurrency.Trim().ToUpperInvariant();
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
            () => Application.Current.Dispatcher.Invoke(BuildDonationSummaryPayload),
            () => Settings.Value.OverlayTheme);
        Obs.Log += (_, m) => Logger.Info(m);
        Music.PlaybackError += (_, m) => Logger.Error($"Плеєр: {m}");
        Twitch.MessageReceived += Channel_MessageReceived;
        YouTube.MessageReceived += Channel_MessageReceived;
        Twitch.DonationReceived += Channel_DonationReceived;
        YouTube.DonationReceived += Channel_DonationReceived;
        Donatello.DonationReceived += Channel_DonationReceived;
        Donatello.StatusChanged += Channel_StatusChanged;
        Notifications.StatusChanged += Channel_StatusChanged;
        Overlay.StatusChanged += Channel_StatusChanged;
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
        if (Settings.Value.DonatelloEnabled && Donatello.HasApiToken)
            _ = SafeConnectAsync(StartAndImportDonatelloAsync, "Автозапуск і синхронізація Donatello");
    }

    private async Task StartAndImportDonatelloAsync()
    {
        await Donatello.StartAsync().ConfigureAwait(false);
        Donations.ExternalTotalAmount = Donatello.ProfileTotalAmount;
        await Donatello.ImportRecentAsync().ConfigureAwait(false);
        Logger.Info("Donatello: стартова синхронізація профілю, підписок і останніх донатів завершена.");
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
        var sent = 0;
        var wantsTwitch = target.Contains("Twitch", StringComparison.OrdinalIgnoreCase);
        var wantsYouTube = target.Contains("YouTube", StringComparison.OrdinalIgnoreCase);
        var multiTarget = wantsTwitch && wantsYouTube;

        if (wantsTwitch && Twitch.IsChatConnected)
        {
            try { await Twitch.SendMessageAsync(text); sent++; }
            catch (Exception ex) { errors.Add("Twitch: " + ex.Message); }
        }
        else if (wantsTwitch && !multiTarget)
        {
            errors.Add("Twitch: чат не підключено.");
        }

        if (wantsYouTube && YouTube.HasLiveChat)
        {
            try { await YouTube.SendMessageAsync(text); sent++; }
            catch (Exception ex) { errors.Add("YouTube: " + ex.Message); }
        }
        else if (wantsYouTube && !multiTarget)
        {
            errors.Add("YouTube: активний live chat не знайдено.");
        }

        if (sent == 0 && errors.Count == 0)
            errors.Add("Немає підключеного чату для надсилання.");
        if (errors.Count > 0)
            throw new InvalidOperationException(string.Join(Environment.NewLine, errors));
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
        if (message.Platform.Equals("TWITCH", StringComparison.OrdinalIgnoreCase)) await Twitch.DeleteMessageAsync(message.ExternalId);
        else if (message.Platform.Equals("YOUTUBE", StringComparison.OrdinalIgnoreCase)) await YouTube.DeleteMessageAsync(message.ExternalId);
        else throw new InvalidOperationException("Видалення доступне лише для Twitch і YouTube.");
    }

    public async Task<bool> CheckBridgeAsync()
    {
        BridgeAvailable = await MultiStreamBridgeService.IsAvailableAsync(Settings.Value.MultiStreamVendorName);
        BridgeStatus = BridgeAvailable ? "ДОСТУПНИЙ" : "НЕ ЗНАЙДЕНО";
        Logger.Info($"Multistream bridge: {BridgeStatus}");
        BridgeStatusChanged?.Invoke(this, EventArgs.Empty);
        return BridgeAvailable;
    }

    public async Task NotifyStreamStartedAsync(string platform, string title, string url) =>
        await Discord.NotifyStreamStartedAsync(platform, title, url);

    public void Save() => SettingsService.Save(Settings.Value);

    private object BuildNowPlayingPayload()
    {
        var t = Music.CurrentTrack;
        return new
        {
            playing = Music.IsPlaying,
            title = t?.Title ?? "",
            artist = t?.Artist ?? "",
            album = t?.Album ?? "",
            display = t?.Display ?? "",
            artwork = t?.ArtworkDataUrl ?? "",
            volume = Music.Volume,
            positionSeconds = Music.Position.TotalSeconds,
            durationSeconds = Music.Duration.TotalSeconds
        };
    }

    private object BuildDonationSummaryPayload()
    {
        var summary = Donations.Summary;
        return new
        {
            recent = Donations.History.TakeLast(12).Reverse().Select(x => new
            {
                id = x.StableId,
                source = x.Source,
                kind = x.Kind,
                user = x.User,
                amount = x.Amount,
                currency = x.Currency,
                message = x.Message,
                time = x.DisplayTime,
                accent = x.Accent,
                icon = x.EventIcon,
                historical = x.IsHistorical,
                replay = x.IsReplay,
                test = x.IsTest
            }),
            summary.TotalReceived,
            summary.RealEvents,
            summary.TestEvents,
            summary.ActiveSubscribers,
            summary.GoalAmount,
            summary.GoalCurrency,
            summary.GoalProgressPercent,
            title = Settings.Value.DonationGoalTitle
        };
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposeState, 1) != 0) return;
        Windows.CloseAll();
        try { await Notifications.StopAsync().ConfigureAwait(false); } catch { }
        try { await Discord.DisposeAsync().ConfigureAwait(false); } catch { }
        try { await Donatello.DisposeAsync().ConfigureAwait(false); } catch { }
        try { await YouTube.DisposeAsync().ConfigureAwait(false); } catch { }
        try { await Twitch.DisposeAsync().ConfigureAwait(false); } catch { }
        try { await Chat.DisposeAsync().ConfigureAwait(false); } catch { }
        try { await Music.DisposeAsync().ConfigureAwait(false); } catch { }
        try { await Overlay.DisposeAsync().ConfigureAwait(false); } catch { }
        try { await Obs.DisposeAsync().ConfigureAwait(false); } catch { }
    }
}
