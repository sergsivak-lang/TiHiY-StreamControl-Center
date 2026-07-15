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
        Settings.Value.DonationGoalCurrency = Donations.GoalCurrency;
        Settings.Value.ScheduledNotices = Chat.Notices.ToList();
        Settings.Value.BotCommands = Chat.Commands.ToList();
        Settings.Value.MusicPlaylistPaths = Music.Playlist.Select(x => x.FilePath).ToList();
        SettingsService.Save(Settings.Value);
    }

    private object BuildDonationSummaryPayload()
    {
        var all = Donations.History.ToList();
        var last = all.LastOrDefault(x => !x.IsHistorical);
        var recent = all.Where(x => !x.IsHistorical).TakeLast(5).Reverse().Select(x => new
        {
            id = x.StableId,
            source = x.Source,
            kind = x.Kind,
            user = x.User,
            amount = x.Amount,
            currency = x.Currency,
            message = x.Message,
            time = x.Time.ToString("O"),
            isTest = x.IsTest,
            isReplay = x.IsReplay
        }).ToList();

        return new
        {
            goalTitle = Settings.Value.DonationGoalTitle,
            goalAmount = Donations.GoalAmount,
            goalCurrency = Donations.GoalCurrency,
            currentAmount = Donations.TotalAmount,
            progressPercent = Donations.GoalProgress * 100d,
            donatelloStatus = Donatello.Status,
            lastDonation = last is null ? null : new
            {
                id = last.StableId,
                source = last.Source,
                kind = last.Kind,
                user = last.User,
                amount = last.Amount,
                currency = last.Currency,
                message = last.Message,
                time = last.Time.ToString("O"),
                isTest = last.IsTest,
                isReplay = last.IsReplay
            },
            recent
        };
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
