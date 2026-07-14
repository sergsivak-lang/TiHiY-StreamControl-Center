using System.Diagnostics;
using System.Collections.Specialized;
using TiHiY.StreamControlCenter.Models;
using TiHiY.StreamControlCenter.Services;
using TiHiY.StreamControlCenter.Windows;

namespace TiHiY.StreamControlCenter;

public partial class MainWindow : Window
{
    private readonly AppServices _services = App.Services;
    private readonly DispatcherTimer _audioRefreshTimer;
    private readonly DispatcherTimer _systemRefreshTimer;
    private readonly Stopwatch _uptime = Stopwatch.StartNew();
    private bool _systemRefreshBusy;
    private readonly List<AudioChannel> _allQuickAudio = new();
    private int _audioPageIndex;
    private bool _loadingAudio;
    private bool _syncingVolumeFromObs;
    private bool _closing;
    private bool _obsActionInProgress;
    private readonly List<IDisposable> _responsiveBlocks = new();
    private bool _layoutEditMode;
    private Point _blockDragStart;
    private ContentControl? _blockDragSource;
    private static readonly string[] DashboardBlockNames =
    {
        "ChatBlockPanel", "NotificationsBlockPanel", "DonationsBlockPanel", "MixerBlockPanel",
        "SystemStatusBlockPanel", "SystemMonitorPanel", "ModulesBlockPanel"
    };

    public ObservableCollection<ChatMessage> MainChatMessages { get; } = new();
    public ObservableCollection<AudioChannel> QuickAudioPage { get; } = new();
    public ObservableCollection<DonationEvent> RecentDonations { get; } = new();
    public ObservableCollection<DonationEvent> DonationPage { get; } = new();
    public ObservableCollection<string> RecentLogLines { get; } = new();

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;
        InitializeMainChatContextMenu();

        _audioRefreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _audioRefreshTimer.Tick += async (_, _) => await RefreshQuickAudioSafeAsync();
        _systemRefreshTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(Math.Clamp(_services.Settings.Value.SystemMonitorRefreshMilliseconds, 500, 5000)) };
        _systemRefreshTimer.Tick += async (_, _) => await RefreshSystemHealthAsync();

        _services.Chat.MessageAdded += Chat_MessageAdded;
        _services.Donations.DonationAdded += Donations_DonationAdded;
        _services.Obs.ConnectionChanged += Obs_ConnectionChanged;
        _services.Obs.ProgramSceneChanged += Obs_ProgramSceneChanged;
        _services.Obs.InputMuteChanged += Obs_InputMuteChanged;
        _services.Obs.InputVolumeChanged += Obs_InputVolumeChanged;
        _services.Obs.InputMeterChanged += Obs_InputMeterChanged;
        _services.BridgeStatusChanged += Services_BridgeStatusChanged;
        _services.ChannelStatusChanged += Services_ChannelStatusChanged;
        _services.Logger.Entries.CollectionChanged += LoggerEntries_CollectionChanged;
        _services.UiScale.ScaleChanged += UiScale_ScaleChanged;
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        _services.Placement.Attach(this, "MainWindow");
        var layout = _services.Settings.Value;
        if (layout.DashboardLayoutVersion < 13)
        {
            layout.MainLeftColumnWidth = 1.02;
            layout.MainBottomLeftColumnWidth = 1.02;
            layout.MainTopRowHeight = 1.46;
            layout.FooterHeight = 152;
            layout.FooterSystemColumnWeight = 0.31;
            layout.FooterEventsColumnWeight = 0.42;
            layout.FooterMonitorColumnWeight = 0.27;
            layout.DashboardLayoutVersion = 13;
        }
        if (layout.DashboardLayoutVersion < 15)
        {
            // v1.0.3.3: швидкий мікшер показує тільки канали, які користувач
            // вручну додав у вікні «Повний аудіомікшер». Існуючий вибір зберігаємо.
            layout.DashboardLayoutVersion = 15;
            _services.Save();
        }
        if (layout.DashboardLayoutVersion < 16)
        {
            // v1.0.3.4 introduces true responsive internals. Reset only the panel
            // proportions once; manually selected OBS channels remain untouched.
            layout.MainLeftColumnWidth = 1.02;
            layout.MainBottomLeftColumnWidth = 1.02;
            layout.MainTopRowHeight = 1.46;
            layout.FooterHeight = 152;
            layout.FooterSystemColumnWeight = 0.31;
            layout.FooterEventsColumnWeight = 0.42;
            layout.FooterMonitorColumnWeight = 0.27;
            layout.DashboardLayoutVersion = 16;
            _services.Save();
        }
        if (layout.DashboardLayoutVersion < 17)
        {
            // v1.0.4.1: invisible resize zones, compact module buttons and the
            // lower-row order System Status → PC Monitor → Modules.
            layout.MainLeftColumnWidth = 1.02;
            layout.MainBottomLeftColumnWidth = 1.02;
            layout.MainTopRowHeight = 1.28;
            layout.FooterHeight = 180;
            layout.FooterSystemColumnWeight = 0.22;
            layout.FooterEventsColumnWeight = 0.38;
            layout.FooterMonitorColumnWeight = 0.40;
            layout.DashboardLayoutVersion = 17;
            _services.Save();
        }
        if (layout.DashboardLayoutVersion < 18)
        {
            // v1.0.4.1: вертикальний швидкий мікшер отримує більше висоти за замовчуванням.
            layout.MainTopRowHeight = 1.12;
            layout.DashboardLayoutVersion = 18;
            _services.Save();
        }
        RestoreDashboardLayout(layout);
        RestoreDashboardBlockSlots();
        ApplyScale();
        Dispatcher.BeginInvoke(new Action(InitializeResponsiveBlocks), DispatcherPriority.Loaded);
        RefreshMainChat();
        RefreshDonations();
        UpdateBridgeStatus();
        UpdateChannelStatus();
        UpdateObsStatus(_services.Obs.IsConnected);
        ClearQuickAudio("OBS не підключено");
        UpdateLastLog();
        await RefreshSystemHealthAsync();
        _audioRefreshTimer.Start();
        _systemRefreshTimer.Start();

        if (_services.Settings.Value.LocalChatOverlayAutoStart && !_services.Windows.IsOpen<LocalChatOverlayWindow>())
            _services.Windows.Show(() => new LocalChatOverlayWindow());

        if (_services.Obs.IsConnected)
        {
            await RefreshQuickAudioSafeAsync();
            return;
        }

        if (_services.Settings.Value.AutoConnectObs)
            await TryAutoConnectObsAsync();
    }

    private async Task TryAutoConnectObsAsync()
    {
        var password = _services.Settings.Value.RememberObsPassword
            ? _services.Credentials.LoadPassword()
            : string.Empty;

        try
        {
            SystemStateText.Text = "Автоматичне підключення OBS Audio…";
            await _services.Obs.ConnectAsync(_services.Settings.Value.ObsUrl, password);
            await Task.Delay(500);
            await RefreshQuickAudioSafeAsync();
        }
        catch (Exception ex)
        {
            _services.Logger.Error("Автоматичне підключення OBS", ex);
            SystemStateText.Text = FriendlyObsError(ex);
            UpdateObsStatus(false);
        }
    }

    private async void ConnectObs_Click(object sender, RoutedEventArgs e)
    {
        if (_obsActionInProgress) return;
        _obsActionInProgress = true;
        ConnectObsButton.IsEnabled = false;

        try
        {
            if (_services.Obs.IsConnected)
            {
                ConnectObsButtonText.Text = "  ВІДКЛЮЧЕННЯ…";
                SystemStateText.Text = "Відключення від OBS WebSocket…";
                await _services.Obs.DisconnectAsync();
                return;
            }

            ConnectObsButtonText.Text = "  ПІДКЛЮЧЕННЯ…";
            SystemStateText.Text = "Підключення до OBS WebSocket…";
            var password = _services.Settings.Value.RememberObsPassword
                ? _services.Credentials.LoadPassword()
                : string.Empty;

            await _services.Obs.ConnectAsync(_services.Settings.Value.ObsUrl, password);
            await Task.Delay(500);
            await RefreshQuickAudioSafeAsync();
        }
        catch (Exception ex)
        {
            _services.Logger.Error("Підключення OBS", ex);
            SystemStateText.Text = FriendlyObsError(ex);
            UpdateObsStatus(_services.Obs.IsConnected);
            MessageBox.Show(this, FriendlyObsError(ex), "OBS Audio", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            _obsActionInProgress = false;
            ConnectObsButton.IsEnabled = true;
            UpdateObsStatus(_services.Obs.IsConnected);
        }
    }

    private Brush GetBrushResource(object key)
    {
        var value = TryFindResource(key);
        return value switch
        {
            Brush brush => brush,
            Color color => new SolidColorBrush(color),
            _ => Brushes.Transparent
        };
    }

    private static string FriendlyObsError(Exception ex)
    {
        var message = ex.GetBaseException().Message;
        if (message.Contains("actively refused", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("відмов", StringComparison.OrdinalIgnoreCase))
            return "OBS WebSocket недоступний на 127.0.0.1:4455. Запустіть OBS і ввімкніть WebSocket.";
        if (message.Contains("password", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("authentication", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("авториза", StringComparison.OrdinalIgnoreCase))
            return "OBS відхилив пароль WebSocket. Перевірте пароль у налаштуваннях.";
        return $"OBS Audio не підключено: {message}";
    }

    private void Obs_ConnectionChanged(object? sender, bool connected) => Dispatcher.BeginInvoke(new Action(async () =>
    {
        UpdateObsStatus(connected);
        if (connected)
        {
            await Task.Delay(350);
            await RefreshQuickAudioSafeAsync();
        }
        else
        {
            ClearQuickAudio("OBS не підключено");
        }
    }));

    private void Obs_ProgramSceneChanged(object? sender, string sceneName) => Dispatcher.BeginInvoke(new Action(async () =>
    {
        if (!_services.Obs.IsConnected) return;
        _audioPageIndex = 0;
        await RefreshQuickAudioSafeAsync();
    }));

    private void UpdateObsStatus(bool connected)
    {
        var statusBrush = GetBrushResource(connected ? "Green" : "Red");
        ObsDot.Fill = statusBrush;
        ObsStatusText.Text = connected ? "  ПІДКЛЮЧЕНО" : "  ВІДКЛЮЧЕНО";
        ObsStatusText.Foreground = statusBrush;
        ConnectObsButtonText.Text = connected ? "  ВІДКЛЮЧИТИ OBS" : "  ПІДКЛЮЧИТИ OBS";
        ConnectObsButton.BorderBrush = GetBrushResource(connected ? "Amber" : "Cyan2");
        SystemStateText.Text = connected
            ? "OBS Audio підключено й активне. Керування мікшером, сценами, Preview, записом і трансляцією доступне у модулях."
            : "OBS Audio не підключено. Запустіть OBS і натисніть «Підключити OBS».";
        UpdateSystemStatusPanel();
    }

    private void Services_BridgeStatusChanged(object? sender, EventArgs e) => Dispatcher.BeginInvoke(new Action(UpdateBridgeStatus));
    private void UpdateBridgeStatus()
    {
        var settings = _services.Settings.Value;
        var liveCount = (settings.TwitchLive ? 1 : 0) + (settings.YouTubeLive ? 1 : 0);
        var checkedCount = (_services.Twitch.IsChatConnected ? 1 : 0) + (_services.YouTube.IsConnected ? 1 : 0);
        var label = liveCount > 0
            ? $"{liveCount} / 2 В ЕФІРІ"
            : checkedCount == 2 ? "0 / 2 OFF"
            : checkedCount == 1 ? "1 / 2 ПЕРЕВІРЕНО"
            : "НЕ ПЕРЕВІРЕНО";
        var brushName = liveCount == 2 ? "Green" : liveCount == 1 || checkedCount > 0 ? "Yellow" : "Muted";
        var brush = GetBrushResource(brushName);
        BridgeStatusText.Text = "  " + label;
        BridgeStatusText.Foreground = brush;
        BridgeDot.Fill = brush;
    }

    private void Chat_MessageAdded(object? sender, ChatMessage message) => Dispatcher.BeginInvoke(new Action(() =>
    {
        MainChatMessages.Add(message);
        while (MainChatMessages.Count > 300) MainChatMessages.RemoveAt(0);
        UpdateChatStatusText();
        UpdateChatEmptyState();
        UpdateNotificationFromMessage(message);
        MainChatList.ScrollIntoView(message);
    }));
    private void UpdateNotificationFromMessage(ChatMessage message)
    {
        var text = message.Text ?? string.Empty;
        var isNotification = message.IsHighlighted ||
            text.Contains("підпис", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("follow", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("member", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("донат", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("super chat", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("bits", StringComparison.OrdinalIgnoreCase);
        if (!isNotification) return;
        LastNotificationText.Text = $"{message.User}: {text}";
        LastNotificationTimeText.Text = message.DisplayTime;
    }

    private void Services_ChannelStatusChanged(object? sender, EventArgs e) => Dispatcher.BeginInvoke(new Action(() => { UpdateChannelStatus(); RefreshDonations(); }));
    private void RefreshMainChat()
    {
        MainChatMessages.Clear();
        foreach (var message in _services.Chat.Messages.TakeLast(300)) MainChatMessages.Add(message);
        UpdateChatStatusText();
        UpdateChatEmptyState();
        if (MainChatMessages.Count > 0)
            Dispatcher.BeginInvoke(new Action(() => MainChatList.ScrollIntoView(MainChatMessages[^1])), DispatcherPriority.Background);
    }

    private void UpdateChatStatusText()
    {
        var twitch = _services.Twitch.IsChatConnected ? "Twitch чат підключено" : $"Twitch: {_services.Twitch.Status}";
        var youtube = _services.YouTube.HasLiveChat ? "YouTube чат підключено" : $"YouTube: {_services.YouTube.Status}";
        ChatStatusText.Text = $"{twitch} • {youtube} • {MainChatMessages.Count} повідомлень";
    }

    private void UpdateChatEmptyState()
    {
        if (MainChatMessages.Count > 0)
        {
            ChatEmptyStateText.Visibility = Visibility.Collapsed;
            return;
        }

        ChatEmptyStateText.Visibility = Visibility.Visible;
        ChatEmptyStateText.Text = _services.Twitch.IsChatConnected || _services.YouTube.HasLiveChat
            ? "Чати підключено. Очікування нових повідомлень…"
            : "Підключіть Twitch або YouTube у модулі «Канали».";
    }

    private void UpdateChannelStatus()
    {
        var settings = _services.Settings.Value;
        TwitchViewerText.Text = settings.TwitchViewers.ToString("N0");
        YouTubeViewerText.Text = settings.YouTubeViewers.ToString("N0");
        YouTubeLikesText.Text = settings.YouTubeLikes.ToString("N0");
        TwitchLiveText.Text = settings.TwitchLive ? "LIVE" : "OFF";
        TwitchLiveText.Foreground = GetBrushResource(settings.TwitchLive ? "Green" : "Muted");
        TwitchLiveDot.Fill = GetBrushResource(settings.TwitchLive ? "Green" : "Muted");
        YouTubeLiveText.Text = settings.YouTubeLive ? "LIVE" : "OFF";
        YouTubeLiveText.Foreground = GetBrushResource(settings.YouTubeLive ? "Green" : "Muted");
        YouTubeLiveDot.Fill = GetBrushResource(settings.YouTubeLive ? "Green" : "Muted");
        TwitchTopStatusText.Text = _services.Twitch.IsChatConnected ? (settings.TwitchLive ? "LIVE" : "CHAT ON") : _services.Twitch.Status;
        TwitchTopStatusText.Foreground = GetBrushResource(_services.Twitch.IsChatConnected ? (settings.TwitchLive ? "Green" : "Purple") : "Muted");
        YouTubeTopStatusText.Text = _services.YouTube.IsConnected ? (settings.YouTubeLive ? "В ЕФІРІ" : _services.YouTube.Status) : _services.YouTube.Status;
        YouTubeTopStatusText.Foreground = GetBrushResource(_services.YouTube.IsConnected ? (settings.YouTubeLive ? "Green" : "Red") : "Muted");
        var donatelloLabel = _services.Donatello.IsRunning
            ? (_services.Donatello.ConsecutiveErrors >= 3 ? $"ПОМИЛКА ({_services.Donatello.ConsecutiveErrors})" : "ПІДКЛЮЧЕНО")
            : _services.Donatello.Status;
        var youtubeMoney = _services.YouTube.HasLiveChat ? "YT: ON" : "YT: OFF";
        var twitchMoney = _services.Twitch.IsChatConnected ? "TW: ON" : "TW: OFF";
        DonatelloStatusText.Text = $"DONATELLO: {donatelloLabel} • {youtubeMoney} • {twitchMoney}";
        DonatelloStatusText.Foreground = GetBrushResource(_services.Donatello.IsRunning && _services.Donatello.ConsecutiveErrors < 3 ? "Green" : _services.Donatello.ConsecutiveErrors >= 3 ? "Red" : "Muted");
        SendTwitchButton.IsEnabled = _services.Twitch.IsChatConnected;
        SendYouTubeButton.IsEnabled = _services.YouTube.HasLiveChat;
        SendBothButton.IsEnabled = _services.Twitch.IsChatConnected || _services.YouTube.HasLiveChat;
        ChatInput.IsEnabled = SendBothButton.IsEnabled;
        UpdateChatStatusText();
        UpdateChatEmptyState();
        UpdateBridgeStatus();
        UpdateSystemStatusPanel();
    }

    private void ChatInput_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) return;
        e.Handled = true;
        SendChat("Twitch + YouTube");
    }
    private void SendTwitch_Click(object sender, RoutedEventArgs e) => SendChat("Twitch");
    private void SendYouTube_Click(object sender, RoutedEventArgs e) => SendChat("YouTube");
    private void SendBoth_Click(object sender, RoutedEventArgs e) => SendChat("Twitch + YouTube");
    private void SendChat(string target)
    {
        var text = ChatInput.Text.Trim();
        if (string.IsNullOrWhiteSpace(text)) return;
        _services.Chat.SendManual(text, target);
        ChatInput.Clear();
        ChatInput.Focus();
    }


    private void InitializeMainChatContextMenu()
    {
        var menu = new ContextMenu();

        var mute = new MenuItem { Header = "Мут на 10 хвилин" };
        mute.Click += MuteChatUser_Click;
        menu.Items.Add(mute);

        var ban = new MenuItem { Header = "Забанити користувача" };
        ban.Click += BanChatUser_Click;
        menu.Items.Add(ban);

        menu.Items.Add(new Separator());

        var delete = new MenuItem { Header = "Видалити повідомлення" };
        delete.Click += DeleteChatMessage_Click;
        menu.Items.Add(delete);

        menu.Opened += (_, _) =>
        {
            var message = MainChatList.SelectedItem as ChatMessage;
            foreach (var item in menu.Items.OfType<MenuItem>())
            {
                item.Tag = message;
                item.IsEnabled = message is not null;
            }
        };

        MainChatList.ContextMenu = menu;
    }

    private void MainChatList_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        var item = ItemsControl.ContainerFromElement(
            MainChatList,
            e.OriginalSource as DependencyObject) as ListBoxItem;

        if (item is not null)
        {
            item.IsSelected = true;
            item.Focus();
        }
        else
        {
            MainChatList.SelectedItem = null;
        }
    }

    private async void MuteChatUser_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { Tag: ChatMessage message }) return;
        await RunModerationAsync(message, "мут", () => _services.ModerateChatUserAsync(message, false, 600));
    }

    private async void BanChatUser_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { Tag: ChatMessage message }) return;
        if (MessageBox.Show(this, $"Забанити {message.User} на {message.Platform}?", "Модерація чату", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        await RunModerationAsync(message, "бан", () => _services.ModerateChatUserAsync(message, true));
    }

    private async void DeleteChatMessage_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { Tag: ChatMessage message }) return;
        await RunModerationAsync(message, "видалення повідомлення", async () =>
        {
            await _services.DeleteChatMessageAsync(message);
            _services.Chat.Messages.Remove(message);
            MainChatMessages.Remove(message);
        });
    }

    private async Task RunModerationAsync(ChatMessage message, string action, Func<Task> operation)
    {
        try
        {
            await operation();
            _services.Logger.Info($"Чат {message.Platform}: {action} — {message.User}");
            SystemStateText.Text = $"{message.User}: {action} виконано ({message.Platform}).";
        }
        catch (Exception ex)
        {
            _services.Logger.Error($"Модерація {message.Platform}", ex);
            MessageBox.Show(this, ex.GetBaseException().Message + "\n\nДля Twitch після оновлення потрібна повторна OAuth-авторизація з правами модерації.", "Модерація чату", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async Task RefreshQuickAudioSafeAsync()
    {
        if (_loadingAudio) return;
        if (!_services.Obs.IsConnected)
        {
            ClearQuickAudio("OBS не підключено");
            return;
        }

        _loadingAudio = true;
        try
        {
            var selectedNames = _services.Settings.Value.SelectedAudioInputs
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (selectedNames.Count == 0)
            {
                ClearQuickAudio("Канали для головного мікшера не вибрані");
                AudioEmptyStateText.Text = "Відкрийте «Повний аудіомікшер» і натисніть «Додати на головну» біля потрібних каналів OBS.";
                return;
            }

            var inputs = (await _services.Obs.GetPrimaryMixerInputsAsync(selectedNames)).ToList();

            var oldByName = _allQuickAudio.ToDictionary(x => x.Name, StringComparer.OrdinalIgnoreCase);
            var refreshed = new List<AudioChannel>();
            foreach (var input in inputs)
            {
                var channel = oldByName.TryGetValue(input.name, out var existing)
                    ? existing
                    : new AudioChannel { Name = input.name, Kind = input.kind };
                channel.Kind = input.kind;
                try { channel.IsMuted = await _services.Obs.GetInputMuteAsync(input.name); } catch { }
                try { channel.Volume = Math.Clamp(await _services.Obs.GetInputVolumeAsync(input.name), 0, 1); } catch { }
                refreshed.Add(channel);
            }

            _allQuickAudio.Clear();
            _allQuickAudio.AddRange(refreshed);
            _audioPageIndex = Math.Clamp(_audioPageIndex, 0, AudioPageCount - 1);
            ShowAudioPage();
            AudioStatusText.Text = _allQuickAudio.Count == 0
                ? "Вибрані канали не знайдені в поточному OBS"
                : $"{_allQuickAudio.Count} каналів вибрано для головного мікшера";
            AudioEmptyStateText.Visibility = _allQuickAudio.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            AudioEmptyStateText.Text = "Вибрані канали не знайдені. Відкрийте «Повний аудіомікшер» і оновіть вибір.";
        }
        catch (Exception ex)
        {
            _services.Logger.Error("Оновлення Audio Mixer OBS", ex);
            ClearQuickAudio("Помилка отримання аудіоджерел OBS");
        }
        finally
        {
            _loadingAudio = false;
        }
    }

    private void ClearQuickAudio(string message)
    {
        _allQuickAudio.Clear();
        QuickAudioPage.Clear();
        _audioPageIndex = 0;
        AudioPageText.Text = "0 / 0";
        AudioStatusText.Text = message;
        AudioEmptyStateText.Text = message == "OBS не підключено"
            ? "Підключіть OBS WebSocket, щоб завантажити реальні канали мікшера."
            : message;
        AudioEmptyStateText.Visibility = Visibility.Visible;
    }

    private int AudioPageCount => Math.Max(1, (int)Math.Ceiling(_allQuickAudio.Count / 6.0));
    private void ShowAudioPage()
    {
        QuickAudioPage.Clear();
        foreach (var channel in _allQuickAudio.Skip(_audioPageIndex * 6).Take(6)) QuickAudioPage.Add(channel);
        AudioPageText.Text = $"{_audioPageIndex + 1} / {AudioPageCount}";
    }

    private void Obs_InputMeterChanged(object? sender, (string inputName, double meter, double db) data) => Dispatcher.BeginInvoke(new Action(() =>
    {
        var channel = _allQuickAudio.FirstOrDefault(x => string.Equals(x.Name, data.inputName, StringComparison.OrdinalIgnoreCase));
        if (channel is not null)
        {
            channel.Meter = data.meter;
            channel.Db = data.db;
        }
    }));

    private void Obs_InputVolumeChanged(object? sender, (string inputName, double volume) data) => Dispatcher.BeginInvoke(new Action(() =>
    {
        var channel = _allQuickAudio.FirstOrDefault(x => string.Equals(x.Name, data.inputName, StringComparison.OrdinalIgnoreCase));
        if (channel is null) return;
        _syncingVolumeFromObs = true;
        try { channel.Volume = Math.Clamp(data.volume, 0, 1); }
        finally { _syncingVolumeFromObs = false; }
    }));

    private void Obs_InputMuteChanged(object? sender, (string inputName, bool muted) data) => Dispatcher.BeginInvoke(new Action(() =>
    {
        var channel = _allQuickAudio.FirstOrDefault(x => string.Equals(x.Name, data.inputName, StringComparison.OrdinalIgnoreCase));
        if (channel is not null) channel.IsMuted = data.muted;
    }));

    private async void QuickVolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_loadingAudio || _syncingVolumeFromObs || !IsLoaded || sender is not Slider { Tag: AudioChannel channel }) return;
        if (!_services.Obs.IsConnected)
        {
            SystemStateText.Text = "OBS не підключено. Зміна гучності недоступна.";
            return;
        }
        try
        {
            await _services.Obs.SetInputVolumeAsync(channel.Name, e.NewValue);
            channel.Volume = e.NewValue;
        }
        catch (Exception ex)
        {
            _services.Logger.Error($"Гучність {channel.Name}", ex);
        }
    }

    private async void QuickMute_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: AudioChannel channel }) return;
        if (!_services.Obs.IsConnected)
        {
            SystemStateText.Text = "OBS не підключено. MUTE недоступний.";
            return;
        }
        try
        {
            await _services.Obs.SetInputMuteAsync(channel.Name, !channel.IsMuted);
            channel.IsMuted = !channel.IsMuted;
        }
        catch (Exception ex)
        {
            _services.Logger.Error($"MUTE {channel.Name}", ex);
            MessageBox.Show(this, ex.GetBaseException().Message, "OBS Audio", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void PreviousAudioPage_Click(object sender, RoutedEventArgs e)
    {
        if (_audioPageIndex <= 0) return;
        _audioPageIndex--;
        ShowAudioPage();
    }

    private void NextAudioPage_Click(object sender, RoutedEventArgs e)
    {
        if (_audioPageIndex + 1 >= AudioPageCount) return;
        _audioPageIndex++;
        ShowAudioPage();
    }

    private void Donations_DonationAdded(object? sender, DonationEvent donation) => Dispatcher.BeginInvoke(new Action(RefreshDonations));
    private void RefreshDonations()
    {
        DonationPage.Clear();
        foreach (var donation in _services.Donations.History.TakeLast(4).Reverse()) DonationPage.Add(donation);

        RecentDonations.Clear();
        foreach (var donation in _services.Donations.History
                     .Where(x => x.Amount > 0 && !x.IsTest && !x.IsReplay && !x.Source.Equals("TEST", StringComparison.OrdinalIgnoreCase))
                     .TakeLast(3).Reverse())
            RecentDonations.Add(donation);
        RecentDonationsEmptyText.Visibility = RecentDonations.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        DonationEmptyStateText.Visibility = DonationPage.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        var newest = _services.Donations.History.LastOrDefault();
        LastNotificationText.Text = newest is null ? "Подій ще немає" : $"{newest.KindLabel}: {newest.User} — {newest.EventSummary}";
        LastNotificationTimeText.Text = newest?.DisplayTime ?? "--:--:--";
    }

    private void TestDonation_Click(object sender, RoutedEventArgs e)
    {
        var donation = _services.Donations.AddTestDonation();
        SystemStateText.Text = "TEST-подію створено. Вона не змінює реальну ціль збору й не надсилається в Discord.";
        _services.Logger.Info($"Тестовий донат: {donation.User} — {donation.DisplayAmount} (не враховується у цілі)");
    }

    private void OpenChat_Click(object sender, RoutedEventArgs e) => _services.Windows.Show(() => new ChatBotWindow(), this);
    private void OpenAudio_Click(object sender, RoutedEventArgs e) => _services.Windows.Show(() => new AudioMixerWindow(), this);
    private void OpenOverlay_Click(object sender, RoutedEventArgs e) => _services.Windows.Show(() => new OverlaySettingsWindow(), this);
    private void OpenMusic_Click(object sender, RoutedEventArgs e) => _services.Windows.Show(() => new MusicWindow(), this);
    private void OpenChannels_Click(object sender, RoutedEventArgs e) => _services.Windows.Show(() => new ChannelConnectionsWindow(), this);
    private void OpenNotifications_Click(object sender, RoutedEventArgs e) => _services.Windows.Show(() => new StreamNotificationsWindow(), this);
    private void OpenYouTubeSettings_Click(object sender, RoutedEventArgs e) =>
        _services.Windows.Show(() => new YouTubeStreamSettingsWindow(), this);

    private void OpenYouTubeStudioDashboard_Click(object sender, RoutedEventArgs e)
    {
        const string dashboardUrl = "https://studio.youtube.com/channel/UC4-t_7-LD_E15LXazQmsq_g/livestreaming/dashboard";
        try
        {
            Process.Start(new ProcessStartInfo { FileName = dashboardUrl, UseShellExecute = true });
            _services.Logger.Info("Відкрито YouTube Studio Live Dashboard");
        }
        catch (Exception ex)
        {
            SystemStateText.Text = "Не вдалося відкрити YouTube Studio у браузері.";
            _services.Logger.Error("Відкриття YouTube Studio Live Dashboard", ex);
        }
    }
    private void OpenDonatello_Click(object sender, RoutedEventArgs e) => _services.Windows.Show(() => new DonatelloWindow(), this);
    private void OpenSettings_Click(object sender, RoutedEventArgs e) => _services.Windows.Show(() => new SettingsWindow(), this);
    private void OpenJournal_Click(object sender, RoutedEventArgs e) => _services.Windows.Show(() => new JournalWindow(), this);

    private void LoggerEntries_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => Dispatcher.BeginInvoke(new Action(UpdateLastLog));
    private void UpdateLastLog()
    {
        RecentLogLines.Clear();
        foreach (var entry in _services.Logger.Entries.TakeLast(4).Reverse())
        {
            var stackIndex = entry.IndexOf("   at ", StringComparison.Ordinal);
            RecentLogLines.Add(stackIndex > 0 ? entry[..stackIndex].Trim() : entry);
        }
        LastLogText.Text = RecentLogLines.FirstOrDefault() ?? "TiHiY StreamControl Center готовий";
    }

    private async Task RefreshSystemHealthAsync()
    {
        if (_systemRefreshBusy) return;
        _systemRefreshBusy = true;
        try
        {
            var snapshot = _services.SystemMonitor.ReadSnapshot(_services.Settings.Value.Aida64MonitoringEnabled);
            UpdateHardwareMonitor(snapshot);
            UptimeText.Text = $"UP {_uptime.Elapsed:hh\\:mm\\:ss}";

            if (_services.Obs.IsConnected)
            {
                try
                {
                    var stats = await _services.Obs.GetStatsAsync();
                    var fps = stats["activeFps"]?.GetValue<double>() ?? 0;
                    ObsFpsText.Text = $"OBS {fps:0.0} FPS";
                }
                catch { ObsFpsText.Text = "OBS — FPS"; }
            }
            else
            {
                ObsFpsText.Text = "OBS — FPS";
            }

            UpdateSystemStatusPanel();
        }
        finally
        {
            _systemRefreshBusy = false;
        }
    }

    private void UpdateHardwareMonitor(SystemMonitorSnapshot snapshot)
    {
        var sourceBrush = GetBrushResource(snapshot.AidaAvailable ? "Green" : "Yellow");
        AidaStatusDot.Fill = sourceBrush;
        AidaStatusText.Foreground = sourceBrush;
        AidaStatusText.Text = snapshot.AidaAvailable ? "AIDA64 LIVE" : "WINDOWS";
        AidaStatusText.ToolTip = snapshot.StatusText;

        SystemClockText.Text = snapshot.Timestamp.ToString("HH:mm:ss");
        CpuClockMonitorText.Text = FormatClockMhz(snapshot.CpuClockMhz);
        MemoryClockMonitorText.Text = FormatClockMhz(snapshot.MemoryClockMhz);
        CpuLoadMonitorText.Text = FormatPercent(snapshot.CpuUsagePercent);
        GpuLoadMonitorText.Text = FormatPercent(snapshot.GpuUsagePercent);
        CpuTemperatureMonitorText.Text = FormatTemperature(snapshot.CpuTemperatureC);
        GpuTemperatureMonitorText.Text = FormatTemperature(snapshot.GpuTemperatureC);
        RamLoadMonitorText.Text = $"{FormatPercent(snapshot.RamUsagePercent)}  •  {FormatMemory(snapshot.RamUsedGb, snapshot.RamTotalGb)}";
        VramLoadMonitorText.Text = $"{FormatPercent(snapshot.VramUsagePercent)}  •  {FormatMemory(snapshot.VramUsedGb, snapshot.VramTotalGb)}";
        NetworkText.Text = $"NET ↓ {FormatRate(snapshot.DownloadMbps)}  ↑ {FormatRate(snapshot.UploadMbps)}";

        CpuLoadMonitorText.Foreground = LoadBrush(snapshot.CpuUsagePercent);
        GpuLoadMonitorText.Foreground = LoadBrush(snapshot.GpuUsagePercent);
        RamLoadMonitorText.Foreground = LoadBrush(snapshot.RamUsagePercent);
        VramLoadMonitorText.Foreground = LoadBrush(snapshot.VramUsagePercent);
        CpuTemperatureMonitorText.Foreground = TemperatureBrush(snapshot.CpuTemperatureC);
        GpuTemperatureMonitorText.Foreground = TemperatureBrush(snapshot.GpuTemperatureC);

        var cpuPower = snapshot.CpuPowerW.HasValue ? $"CPU Power: {snapshot.CpuPowerW:0.0} W" : "CPU Power: —";
        var gpuPower = snapshot.GpuPowerW.HasValue ? $"GPU Power: {snapshot.GpuPowerW:0.0} W" : "GPU Power: —";
        SystemMonitorPanel.ToolTip = $"{snapshot.StatusText}\n{cpuPower}\n{gpuPower}\n{NetworkText.Text}\n{ObsFpsText.Text}\nUP {_uptime.Elapsed:hh\\:mm\\:ss}";
    }

    private Brush LoadBrush(double? value) => GetBrushResource(value switch
    {
        >= 90 => "Red",
        >= 70 => "Yellow",
        >= 0 => "Green",
        _ => "Muted"
    });

    private Brush TemperatureBrush(double? value) => GetBrushResource(value switch
    {
        >= 90 => "Red",
        >= 75 => "Amber2",
        >= 0 => "Green",
        _ => "Muted"
    });

    private static string FormatPercent(double? value) => value.HasValue ? $"{Math.Clamp(value.Value, 0, 100):0}%" : "— %";
    private static string FormatTemperature(double? value) => value.HasValue ? $"{value.Value:0} °C" : "— °C";
    private static string FormatClockMhz(double? mhz) => mhz.HasValue ? $"{mhz.Value:0} МГц" : "— МГц";
    private static string FormatMemory(double? used, double? total) => used.HasValue && total.HasValue ? $"{used.Value:0.0}/{total.Value:0.0} GB" : "— / — GB";
    private static string FormatRate(double? value) => value.HasValue ? $"{value.Value:0.0} Mb/s" : "—";

    private void UpdateSystemStatusPanel()
    {
        SystemObsText.Text = _services.Obs.IsConnected ? "OBS WebSocket: підключено" : "OBS WebSocket: не підключено";
        SystemObsText.Foreground = GetBrushResource(_services.Obs.IsConnected ? "Green" : "Red");
        SystemTwitchText.Text = _services.Twitch.IsChatConnected ? "Twitch: чат підключено" : $"Twitch: {_services.Twitch.Status}";
        SystemTwitchText.Foreground = GetBrushResource(_services.Twitch.IsChatConnected ? "Green" : "Yellow");
        SystemYouTubeText.Text = _services.YouTube.IsConnected ? $"YouTube: {_services.YouTube.Status}" : $"YouTube: {_services.YouTube.Status}";
        SystemYouTubeText.Foreground = GetBrushResource(_services.YouTube.IsConnected ? "Green" : "Yellow");
        SystemOverlayText.Text = _services.Overlay.IsRunning
            ? $"Overlay Server: 127.0.0.1:{_services.Settings.Value.OverlayPort}"
            : "Overlay Server: не запущено";
        SystemOverlayText.Foreground = GetBrushResource(_services.Overlay.IsRunning ? "Green" : "Red");

        SystemDonatelloText.Text = _services.Donatello.IsHealthy
            ? "Donatello: підключено"
            : $"Donatello: {_services.Donatello.Status}";
        SystemDonatelloText.Foreground = GetBrushResource(_services.Donatello.IsHealthy ? "Green" : _services.Donatello.ConsecutiveErrors >= 3 ? "Red" : "Yellow");
        SystemDiscordText.Text = _services.Notifications.IsRunning
            ? "Discord Bot: працює"
            : $"Discord Bot: {_services.Notifications.Status.ToLowerInvariant()}";
        SystemDiscordText.Foreground = GetBrushResource(_services.Notifications.IsRunning ? "Green" : "Yellow");

        var active = new[]
        {
            _services.Obs.IsConnected,
            _services.Twitch.IsChatConnected,
            _services.YouTube.IsConnected,
            _services.Overlay.IsRunning,
            _services.Donatello.IsHealthy,
            _services.Notifications.IsRunning
        }.Count(x => x);
        AllSystemsStatusText.Text = $"  Активно систем: {active} / 6";
        AllSystemsStatusText.Foreground = GetBrushResource(active == 6 ? "Green" : active > 0 ? "Yellow" : "Red");
    }

    private void RestoreDashboardLayout(AppSettings layout)
    {
        TopLeftColumn.Width = new GridLength(Math.Max(0.35, layout.MainLeftColumnWidth), GridUnitType.Star);
        TopRightColumn.Width = new GridLength(1, GridUnitType.Star);
        BottomLeftColumn.Width = new GridLength(Math.Max(0.35, layout.MainBottomLeftColumnWidth), GridUnitType.Star);
        BottomRightColumn.Width = new GridLength(1, GridUnitType.Star);
        MainTopRow.Height = new GridLength(Math.Max(0.25, layout.MainTopRowHeight), GridUnitType.Star);
        MainBottomRow.Height = new GridLength(1, GridUnitType.Star);
        FooterRow.Height = new GridLength(Math.Clamp(layout.FooterHeight, 92, 380), GridUnitType.Pixel);

        var footerSum = layout.FooterSystemColumnWeight + layout.FooterEventsColumnWeight + layout.FooterMonitorColumnWeight;
        if (footerSum <= 0.01) footerSum = 1;
        FooterSystemColumn.Width = new GridLength(Math.Max(0.08, layout.FooterSystemColumnWeight / footerSum), GridUnitType.Star);
        FooterEventsColumn.Width = new GridLength(Math.Max(0.08, layout.FooterEventsColumnWeight / footerSum), GridUnitType.Star);
        FooterMonitorColumn.Width = new GridLength(Math.Max(0.08, layout.FooterMonitorColumnWeight / footerSum), GridUnitType.Star);
    }

    private void SaveDashboardLayout(bool saveToDisk = true)
    {
        var settings = _services.Settings.Value;
        settings.MainLeftColumnWidth = TopRightColumn.ActualWidth > 0
            ? Math.Clamp(TopLeftColumn.ActualWidth / TopRightColumn.ActualWidth, 0.35, 3.5)
            : 1.02;
        settings.MainBottomLeftColumnWidth = BottomRightColumn.ActualWidth > 0
            ? Math.Clamp(BottomLeftColumn.ActualWidth / BottomRightColumn.ActualWidth, 0.35, 3.5)
            : 1.02;
        settings.MainTopRowHeight = MainBottomRow.ActualHeight > 0
            ? Math.Clamp(MainTopRow.ActualHeight / MainBottomRow.ActualHeight, 0.25, 6)
            : 1.28;
        settings.FooterHeight = Math.Clamp(FooterRow.ActualHeight, 92, 380);

        var footerTotal = FooterSystemColumn.ActualWidth + FooterEventsColumn.ActualWidth + FooterMonitorColumn.ActualWidth;
        if (footerTotal > 1)
        {
            settings.FooterSystemColumnWeight = FooterSystemColumn.ActualWidth / footerTotal;
            settings.FooterEventsColumnWeight = FooterEventsColumn.ActualWidth / footerTotal;
            settings.FooterMonitorColumnWeight = FooterMonitorColumn.ActualWidth / footerTotal;
        }
        settings.DashboardLayoutVersion = 17;
        if (saveToDisk)
        {
            try { _services.Save(); } catch { }
        }
    }

    private void LayoutSplitter_DragCompleted(object sender, DragCompletedEventArgs e)
    {
        Dispatcher.BeginInvoke(new Action(() => SaveDashboardLayout()), DispatcherPriority.Background);
    }

    private void LayoutSplitter_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        var settings = _services.Settings.Value;
        settings.MainLeftColumnWidth = 1.02;
        settings.MainBottomLeftColumnWidth = 1.02;
        settings.MainTopRowHeight = 1.28;
        settings.FooterHeight = 180;
        settings.FooterSystemColumnWeight = 0.22;
        settings.FooterEventsColumnWeight = 0.38;
        settings.FooterMonitorColumnWeight = 0.40;
        RestoreDashboardLayout(settings);
        SaveDashboardLayout();
    }

    private void ToggleLayoutEdit_Click(object sender, RoutedEventArgs e)
    {
        _layoutEditMode = !_layoutEditMode;
        LayoutEditButton.Content = _layoutEditMode ? "МАКЕТ: РЕДАГУВАННЯ" : "МАКЕТ: ЗАКРІПЛЕНО";
        LayoutEditButton.SetResourceReference(Control.BackgroundProperty, _layoutEditMode ? "AmberButtonGradient" : "ButtonGradient");
        LayoutEditButton.SetResourceReference(Control.BorderBrushProperty, _layoutEditMode ? "Amber" : "Line");
        foreach (var block in DashboardBlocks())
        {
            block.Cursor = _layoutEditMode ? Cursors.SizeAll : Cursors.Arrow;
            block.ToolTip = _layoutEditMode
                ? "Затисніть ЛКМ у верхній частині блока та перетягніть на інший блок"
                : null;
        }
        SystemStateText.Text = _layoutEditMode
            ? "Редагування макета: перетягуйте блоки за верхню панель"
            : "Макет закріплено";
    }

    private IEnumerable<ContentControl> DashboardBlocks()
    {
        yield return ChatBlockPanel;
        yield return NotificationsBlockPanel;
        yield return DonationsBlockPanel;
        yield return MixerBlockPanel;
        yield return SystemStatusBlockPanel;
        yield return SystemMonitorPanel;
        yield return ModulesBlockPanel;
    }

    private void DashboardBlock_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (!_layoutEditMode || sender is not ContentControl block || IsInteractiveWindowDragSource(e.OriginalSource)) return;
        var point = e.GetPosition(block);
        if (point.Y > Math.Min(64, block.ActualHeight * 0.30)) return;
        _blockDragStart = e.GetPosition(this);
        _blockDragSource = block;
        block.CaptureMouse();
        e.Handled = true;
    }

    private void DashboardBlock_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (!_layoutEditMode || e.LeftButton != MouseButtonState.Pressed || _blockDragSource is null) return;
        var current = e.GetPosition(this);
        if (Math.Abs(current.X - _blockDragStart.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(current.Y - _blockDragStart.Y) < SystemParameters.MinimumVerticalDragDistance) return;
        var source = _blockDragSource;
        _blockDragSource = null;
        source.ReleaseMouseCapture();
        DragDrop.DoDragDrop(source, source.Name, DragDropEffects.Move);
    }

    private void DashboardBlock_DragOver(object sender, DragEventArgs e)
    {
        if (!_layoutEditMode || sender is not ContentControl target || !e.Data.GetDataPresent(DataFormats.StringFormat))
        {
            e.Effects = DragDropEffects.None;
            return;
        }
        var sourceName = e.Data.GetData(DataFormats.StringFormat) as string;
        e.Effects = string.Equals(sourceName, target.Name, StringComparison.Ordinal) ? DragDropEffects.None : DragDropEffects.Move;
        e.Handled = true;
    }

    private void DashboardBlock_Drop(object sender, DragEventArgs e)
    {
        if (!_layoutEditMode || sender is not ContentControl target) return;
        var sourceName = e.Data.GetData(DataFormats.StringFormat) as string;
        var source = DashboardBlocks().FirstOrDefault(x => x.Name == sourceName);
        if (source is null || ReferenceEquals(source, target)) return;
        SwapDashboardBlocks(source, target);
        SaveDashboardBlockSlots();
        SystemStateText.Text = $"Блоки «{source.Name}» і «{target.Name}» поміняно місцями";
        e.Handled = true;
    }

    private sealed record DashboardSlot(Grid Parent, int Row, int Column, int RowSpan, int ColumnSpan, Thickness Margin);

    private static DashboardSlot CaptureSlot(ContentControl block)
    {
        if (block.Parent is not Grid parent) throw new InvalidOperationException("Блок не знаходиться у Grid.");
        return new DashboardSlot(parent, Grid.GetRow(block), Grid.GetColumn(block), Grid.GetRowSpan(block), Grid.GetColumnSpan(block), block.Margin);
    }

    private static void PlaceInSlot(ContentControl block, DashboardSlot slot)
    {
        slot.Parent.Children.Add(block);
        Grid.SetRow(block, slot.Row);
        Grid.SetColumn(block, slot.Column);
        Grid.SetRowSpan(block, slot.RowSpan);
        Grid.SetColumnSpan(block, slot.ColumnSpan);
        block.Margin = slot.Margin;
    }

    private static void SwapDashboardBlocks(ContentControl first, ContentControl second)
    {
        var firstSlot = CaptureSlot(first);
        var secondSlot = CaptureSlot(second);
        firstSlot.Parent.Children.Remove(first);
        secondSlot.Parent.Children.Remove(second);
        PlaceInSlot(first, secondSlot);
        PlaceInSlot(second, firstSlot);
    }

    private static string SlotKey(ContentControl block)
    {
        var slot = CaptureSlot(block);
        return $"{slot.Parent.Name}:{slot.Row}:{slot.Column}:{slot.RowSpan}:{slot.ColumnSpan}";
    }

    private void SaveDashboardBlockSlots()
    {
        var map = _services.Settings.Value.DashboardBlockSlots;
        map.Clear();
        foreach (var block in DashboardBlocks()) map[block.Name] = SlotKey(block);
        _services.Save();
    }

    private void RestoreDashboardBlockSlots()
    {
        var desired = _services.Settings.Value.DashboardBlockSlots;
        if (desired.Count == 0) return;
        for (var pass = 0; pass < DashboardBlockNames.Length * 2; pass++)
        {
            var changed = false;
            foreach (var block in DashboardBlocks().ToList())
            {
                if (!desired.TryGetValue(block.Name, out var targetSlot) || SlotKey(block) == targetSlot) continue;
                var occupant = DashboardBlocks().FirstOrDefault(x => SlotKey(x) == targetSlot);
                if (occupant is null) continue;
                SwapDashboardBlocks(block, occupant);
                changed = true;
            }
            if (!changed) break;
        }
    }

    private void ZoomOut_Click(object sender, RoutedEventArgs e) { _services.UiScale.Decrease(); _services.Save(); }
    private void ZoomIn_Click(object sender, RoutedEventArgs e) { _services.UiScale.Increase(); _services.Save(); }
    private void ZoomAuto_Click(object sender, RoutedEventArgs e) { _services.UiScale.Reset(); _services.Save(); }
    private void UiScale_ScaleChanged(object? sender, EventArgs e) => Dispatcher.BeginInvoke(new Action(ApplyScale));
    private void Window_SizeChanged(object sender, SizeChangedEventArgs e) => ApplyScale();
    private void ApplyScale()
    {
        var appliedPercent = _services.UiScale.Apply(DesignSurface, this, 1672, 941);
        ZoomTextButton.Content = _services.UiScale.Auto ? $"АВТО {appliedPercent}%" : $"{appliedPercent}%";
    }

    private void InitializeResponsiveBlocks()
    {
        if (_responsiveBlocks.Count > 0) return;

        // Each resizable dashboard panel scales its own internal controls from the
        // default layout baseline. Fonts, icons, buttons, sliders and pixel GridLength
        // values therefore follow the block while GridSplitter changes its size.
        _responsiveBlocks.Add(ResponsiveBlockService.Attach(ChatBlockPanel, 0.35, 1.20));
        _responsiveBlocks.Add(ResponsiveBlockService.Attach(NotificationsBlockPanel, 0.34, 1.22));
        _responsiveBlocks.Add(ResponsiveBlockService.Attach(DonationsBlockPanel, 0.32, 1.20));
        _responsiveBlocks.Add(ResponsiveBlockService.Attach(MixerBlockPanel, 0.34, 1.22));
        _responsiveBlocks.Add(ResponsiveBlockService.Attach(SystemStatusBlockPanel, 0.42, 1.28));
        _responsiveBlocks.Add(ResponsiveBlockService.Attach(ModulesBlockPanel, 0.38, 1.24));
        _responsiveBlocks.Add(ResponsiveBlockService.Attach(SystemMonitorPanel, 0.40, 1.28));
    }

    private void Window_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_layoutEditMode) return;
        if (e.LeftButton != MouseButtonState.Pressed || IsInteractiveWindowDragSource(e.OriginalSource)) return;

        if (e.ClickCount == 2)
        {
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
            e.Handled = true;
            return;
        }

        try
        {
            DragMove();
            e.Handled = true;
        }
        catch (InvalidOperationException)
        {
            // Кнопку миші могли відпустити між PreviewMouseDown і DragMove.
        }
    }

    private static bool IsInteractiveWindowDragSource(object? source)
    {
        for (var current = source as DependencyObject; current is not null; current = GetInputParent(current))
        {
            if (current is ButtonBase or TextBoxBase or PasswordBox or Slider or Thumb or ScrollBar or GridSplitter or Selector or MenuItem)
                return true;
            if (current is ListBoxItem or ComboBoxItem)
                return true;
        }
        return false;
    }

    private static DependencyObject? GetInputParent(DependencyObject current)
    {
        if (current is FrameworkContentElement contentElement)
            return contentElement.Parent;
        if (current is FrameworkElement element && element.Parent is not null)
            return element.Parent;
        try { return VisualTreeHelper.GetParent(current); }
        catch { return null; }
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed) return;
        if (e.ClickCount == 2) Maximize_Click(sender, e);
        else DragMove();
    }
    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void Maximize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    internal void ApplyCiDemoState()
    {
        MainChatMessages.Clear();
        var demo = new[]
        {
            new ChatMessage { Time = DateTime.Today.AddHours(20).AddMinutes(38).AddSeconds(12), Platform = "TWITCH", User = "CyberGhost", Text = "Привіт всім! Як стрім?", Foreground = "#B58AFF" },
            new ChatMessage { Time = DateTime.Today.AddHours(20).AddMinutes(38).AddSeconds(19), Platform = "TWITCH", User = "Vitalik", Text = "Тримай стрім на висоті! 💪", Foreground = "#6BE5FF" },
            new ChatMessage { Time = DateTime.Today.AddHours(20).AddMinutes(38).AddSeconds(23), Platform = "YOUTUBE", User = "User123", Text = "Класний стрім! Дякую за контент!", Foreground = "#FF7474" },
            new ChatMessage { Time = DateTime.Today.AddHours(20).AddMinutes(38).AddSeconds(31), Platform = "YOUTUBE", User = "Nightbot", Text = "Не забувайте підписатись та поставити лайк 👍", Foreground = "#71E7FF" },
            new ChatMessage { Time = DateTime.Today.AddHours(20).AddMinutes(38).AddSeconds(45), Platform = "TWITCH", User = "gaming_bro_ua", Text = "підписався на канал! 🎉", Foreground = "#4CF095" },
            new ChatMessage { Time = DateTime.Today.AddHours(20).AddMinutes(38).AddSeconds(51), Platform = "YOUTUBE", User = "Олена", Text = "Дякую за музику! 🎵", Foreground = "#FF71C8" },
            new ChatMessage { Time = DateTime.Today.AddHours(20).AddMinutes(39).AddSeconds(2), Platform = "DONATELLO", User = "StreamElements", Text = "Донат від Vitalik на суму 250 UAH ❤️", Foreground = "#FFD66B" },
            new ChatMessage { Time = DateTime.Today.AddHours(20).AddMinutes(39).AddSeconds(11), Platform = "YOUTUBE", User = "Макс", Text = "Коли наступний стрім?", Foreground = "#70A5FF" }
        };
        foreach (var item in demo) MainChatMessages.Add(item);

        QuickAudioPage.Clear();
        QuickAudioPage.Add(new AudioChannel { Name = "Мікрофон", Volume = 0.72, Meter = 0.78, Db = -3.2 });
        QuickAudioPage.Add(new AudioChannel { Name = "Системний звук", Volume = 0.61, Meter = 0.58, Db = -12.0 });
        QuickAudioPage.Add(new AudioChannel { Name = "Музика", Volume = 0.46, Meter = 0.42, Db = -18.1 });
        QuickAudioPage.Add(new AudioChannel { Name = "OBS Audio", Volume = 0.66, Meter = 0.68, Db = -6.5 });
        QuickAudioPage.Add(new AudioChannel { Name = "Браузер", Volume = 0.52, Meter = 0.48, Db = -15.4 });
        QuickAudioPage.Add(new AudioChannel { Name = "Discord", Volume = 0.57, Meter = 0.53, Db = -9.0 });

        ChatStatusText.Text = "Twitch чат • YouTube синхронізація • 12 повідомлень";
        TwitchViewerText.Text = "14";
        TwitchLiveText.Text = "ON";
        TwitchLiveText.Foreground = GetBrushResource("Green");
        TwitchLiveDot.Fill = GetBrushResource("Green");
        YouTubeViewerText.Text = "23";
        YouTubeLikesText.Text = "7";
        YouTubeLiveText.Text = "ON";
        YouTubeLiveText.Foreground = GetBrushResource("Green");
        YouTubeLiveDot.Fill = GetBrushResource("Green");
        TwitchTopStatusText.Text = "CHAT ON";
        YouTubeTopStatusText.Text = "В ЕФІРІ";
        ObsDot.Fill = GetBrushResource("Green");
        ObsStatusText.Text = "  ПІДКЛЮЧЕНО";
        ObsStatusText.Foreground = GetBrushResource("Green");
        BridgeStatusText.Text = "  ПЕРЕВІРЕНО";
        BridgeStatusText.Foreground = GetBrushResource("Amber");
        AudioStatusText.Text = "6 вибраних каналів OBS";
        AudioPageText.Text = "1 / 1";
        DonatelloStatusText.Text = "DONATELLO: ПІДКЛЮЧЕНО • SUPER CHAT • BITS";
        DonatelloStatusText.Foreground = GetBrushResource("Green");
        RecentDonations.Clear();
        DonationPage.Clear();
        var demoDonations = new[]
        {
            new DonationEvent { Time = DateTime.Today.AddHours(20).AddMinutes(38).AddSeconds(45), Source = "DONATELLO", User = "Vitalik", Amount = 250m, Currency = "UAH", Message = "Тримай стрім на висоті! 💪", ExternalId = "ci-demo-1" },
            new DonationEvent { Time = DateTime.Today.AddHours(20).AddMinutes(31).AddSeconds(12), Source = "DONATELLO", User = "Олена", Amount = 150m, Currency = "UAH", Message = "Дякую за український контент!", ExternalId = "ci-demo-2" },
            new DonationEvent { Time = DateTime.Today.AddHours(20).AddMinutes(22).AddSeconds(7), Source = "DONATELLO", User = "CyberGhost", Amount = 100m, Currency = "UAH", Message = "На розвиток каналу", ExternalId = "ci-demo-3" }
        };
        foreach (var donation in demoDonations)
        {
            RecentDonations.Add(donation);
            DonationPage.Add(donation);
        }
        RecentDonationsEmptyText.Visibility = Visibility.Collapsed;
        DonationEmptyStateText.Visibility = Visibility.Collapsed;
        LastNotificationText.Text = "Новий підписник: gaming_bro_ua (Twitch)";
        LastNotificationTimeText.Text = "20:38:51";
        ConnectObsButtonText.Text = "  ВІДКЛЮЧИТИ OBS";
        AllSystemsStatusText.Text = "  Усі системи активні";
        SystemStateText.Text = "OBS Audio підключено. Керування мікшером, чатами, донатами та overlay активне.";
        LastLogText.Text = "[20:39:11] [INFO] Donatello: Донат 250 UAH від Vitalik";
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        foreach (var controller in _responsiveBlocks) controller.Dispose();
        _responsiveBlocks.Clear();
        if (_closing) return;
        _closing = true;
        _audioRefreshTimer.Stop();
        _systemRefreshTimer.Stop();
        _services.Chat.MessageAdded -= Chat_MessageAdded;
        _services.Donations.DonationAdded -= Donations_DonationAdded;
        _services.Obs.ConnectionChanged -= Obs_ConnectionChanged;
        _services.Obs.ProgramSceneChanged -= Obs_ProgramSceneChanged;
        _services.Obs.InputMuteChanged -= Obs_InputMuteChanged;
        _services.Obs.InputVolumeChanged -= Obs_InputVolumeChanged;
        _services.Obs.InputMeterChanged -= Obs_InputMeterChanged;
        _services.BridgeStatusChanged -= Services_BridgeStatusChanged;
        _services.ChannelStatusChanged -= Services_ChannelStatusChanged;
        _services.Logger.Entries.CollectionChanged -= LoggerEntries_CollectionChanged;
        _services.UiScale.ScaleChanged -= UiScale_ScaleChanged;
        SaveDashboardLayout(false);
        _services.Windows.CloseAll();
        try { _services.Save(); } catch { }
    }
}
