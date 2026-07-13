using System.Diagnostics;
using System.Collections.Specialized;
using System.Windows.Media.Imaging;
using TiHiY.StreamControlCenter.Models;
using TiHiY.StreamControlCenter.Services;
using TiHiY.StreamControlCenter.Windows;

namespace TiHiY.StreamControlCenter;

public partial class MainWindow : Window
{
    private readonly AppServices _services = App.Services;
    private readonly DispatcherTimer _audioRefreshTimer;
    private readonly List<AudioChannel> _allQuickAudio = new();
    private int _audioPageIndex;
    private bool _loadingAudio;
    private bool _syncingVolumeFromObs;
    private bool _closing;

    public ObservableCollection<ChatMessage> MainChatMessages { get; } = new();
    public ObservableCollection<AudioChannel> QuickAudioPage { get; } = new();
    public ObservableCollection<DonationEvent> DonationPage { get; } = new();

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;

        _audioRefreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _audioRefreshTimer.Tick += async (_, _) => await RefreshQuickAudioSafeAsync();

        _services.Chat.MessageAdded += Chat_MessageAdded;
        _services.Donations.DonationAdded += Donations_DonationAdded;
        _services.Obs.ConnectionChanged += Obs_ConnectionChanged;
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
        if (App.IsRenderPreview)
        {
            Width = 1480;
            Height = 900;
            WindowStartupLocation = WindowStartupLocation.Manual;
            Left = -30000;
            Top = -30000;
            PopulatePreviewData();
            ApplyScale();
            return;
        }
        _services.Placement.Attach(this, "MainWindow");
        var layout = _services.Settings.Value;
        MainLeftColumn.Width = new GridLength(Math.Max(0.25, layout.MainLeftColumnWidth), GridUnitType.Star);
        MainRightColumn.Width = new GridLength(1, GridUnitType.Star);
        MainTopRow.Height = new GridLength(Math.Max(0.25, layout.MainTopRowHeight), GridUnitType.Star);
        MainBottomRow.Height = new GridLength(1, GridUnitType.Star);
        ApplyScale();
        RefreshMainChat();
        RefreshDonations();
        UpdateBridgeStatus();
        UpdateChannelStatus();
        UpdateObsStatus(_services.Obs.IsConnected);
        UpdateLastLog();
        _audioRefreshTimer.Start();

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

    private void PopulatePreviewData()
    {
        MainChatMessages.Clear();
        MainChatMessages.Add(new ChatMessage { Platform = "TWITCH", User = "CyberGhost", Text = "Привіт всім! Як справи?", Foreground = "#B987FF", Time = DateTime.Today.AddHours(20).AddMinutes(38).AddSeconds(12) });
        MainChatMessages.Add(new ChatMessage { Platform = "TWITCH", User = "Vitalik", Text = "Тримай стрім на висоті! 💪", Foreground = "#B987FF", Time = DateTime.Today.AddHours(20).AddMinutes(38).AddSeconds(19) });
        MainChatMessages.Add(new ChatMessage { Platform = "YOUTUBE", User = "User123", Text = "Класний стрім! Дякую за контент!", Foreground = "#FF5C67", Time = DateTime.Today.AddHours(20).AddMinutes(38).AddSeconds(23) });
        MainChatMessages.Add(new ChatMessage { Platform = "YOUTUBE", User = "Nightbot", Text = "Не забудьте підписатися та поставити лайк 👍", Foreground = "#FF5C67", Time = DateTime.Today.AddHours(20).AddMinutes(38).AddSeconds(31) });
        MainChatMessages.Add(new ChatMessage { Platform = "TWITCH", User = "gaming_bro_ua", Text = "підписався на канал 👋", Foreground = "#B987FF", Time = DateTime.Today.AddHours(20).AddMinutes(38).AddSeconds(45) });
        MainChatMessages.Add(new ChatMessage { Platform = "YOUTUBE", User = "Олена", Text = "Дякую за музику! 🎵", Foreground = "#FF5C67", Time = DateTime.Today.AddHours(20).AddMinutes(38).AddSeconds(51) });
        MainChatMessages.Add(new ChatMessage { Platform = "TWITCH", User = "StreamElements", Text = "Донат від Vitalik на суму 250 UAH ❤️", Foreground = "#B987FF", Time = DateTime.Today.AddHours(20).AddMinutes(39).AddSeconds(2) });
        MainChatMessages.Add(new ChatMessage { Platform = "YOUTUBE", User = "Макс", Text = "Коли наступний стрім?", Foreground = "#FF5C67", Time = DateTime.Today.AddHours(20).AddMinutes(39).AddSeconds(11) });

        QuickAudioPage.Clear();
        QuickAudioPage.Add(new AudioChannel { Name = "Мікрофон", Volume = 0.73, Meter = 0.82, Db = -3.2 });
        QuickAudioPage.Add(new AudioChannel { Name = "Системний звук", Volume = 0.61, Meter = 0.65, Db = -12.0 });
        QuickAudioPage.Add(new AudioChannel { Name = "Музика", Volume = 0.48, Meter = 0.48, Db = -18.1 });
        QuickAudioPage.Add(new AudioChannel { Name = "Discord", Volume = 0.66, Meter = 0.62, Db = -9.0 });

        DonationPage.Clear();
        LastDonationAmountText.Text = "250 UAH";
        LastDonationUserText.Text = "Vitalik надіслав 250 UAH";
        LastDonationMessageText.Text = "“Тримай стрім на висоті! 💪”";
        DonationGoalProgress.Value = 0.32;
        DonatelloStatusText.Text = "DONATELLO: ПІДКЛЮЧЕНО • SUPER CHAT • BITS";
        DonatelloStatusText.Foreground = (Brush)FindResource("Green");
        TwitchViewerText.Text = "14";
        YouTubeViewerText.Text = "23";
        YouTubeLikesText.Text = "37";
        TwitchLiveText.Text = "  ON";
        TwitchLiveText.Foreground = (Brush)FindResource("Green");
        YouTubeLiveText.Text = "  ON";
        YouTubeLiveText.Foreground = (Brush)FindResource("Red");
        TwitchTopStatusText.Text = "CHAT ON";
        YouTubeTopStatusText.Text = "В ЕФІРІ";
        ObsDot.Fill = (Brush)FindResource("Green");
        ObsStatusText.Text = "  ПІДКЛЮЧЕНО";
        ObsStatusText.Foreground = (Brush)FindResource("Green");
        BridgeStatusText.Text = "  ПЕРЕВІРЕНО";
        BridgeStatusText.Foreground = (Brush)FindResource("Amber");
        ChatStatusText.Text = "Twitch чат • YouTube синхронізація • 12 повідомлень";
        AudioStatusText.Text = "OBS підключено";
        AudioStatusText.Foreground = (Brush)FindResource("Green");
        SystemStateText.Text = "OBS Audio підключення активне. Усі системи готові.";
        LastLogText.Text = "[20:39:11] [INFO] Donatello: Донат 250 UAH від Vitalik";
    }

    public void RenderPreview(string outputPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? AppContext.BaseDirectory);
        UpdateLayout();
        var dpi = VisualTreeHelper.GetDpi(this);
        var width = Math.Max(1, (int)Math.Ceiling(ActualWidth * dpi.DpiScaleX));
        var height = Math.Max(1, (int)Math.Ceiling(ActualHeight * dpi.DpiScaleY));
        var bitmap = new RenderTargetBitmap(width, height, 96 * dpi.DpiScaleX, 96 * dpi.DpiScaleY, PixelFormats.Pbgra32);
        bitmap.Render(this);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(outputPath);
        encoder.Save(stream);
    }

    private async Task TryAutoConnectObsAsync()
    {
        var password = _services.Settings.Value.RememberObsPassword ? _services.Credentials.LoadPassword() : string.Empty;
        if (string.IsNullOrWhiteSpace(password))
        {
            SystemStateText.Text = "OBS не підключено. Пароль не збережено.";
            return;
        }

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
        if (_services.Obs.IsConnected)
        {
            await RefreshQuickAudioSafeAsync();
            return;
        }

        var password = _services.Settings.Value.RememberObsPassword ? _services.Credentials.LoadPassword() : string.Empty;
        if (string.IsNullOrWhiteSpace(password))
        {
            _services.Windows.Show(() => new SettingsWindow(), this);
            return;
        }

        try
        {
            await _services.Obs.ConnectAsync(_services.Settings.Value.ObsUrl, password);
            await Task.Delay(500);
            await RefreshQuickAudioSafeAsync();
        }
        catch (Exception ex)
        {
            _services.Logger.Error("Підключення OBS", ex);
            SystemStateText.Text = FriendlyObsError(ex);
            MessageBox.Show(this, FriendlyObsError(ex), "OBS Audio", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
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
            _allQuickAudio.Clear();
            QuickAudioPage.Clear();
            AudioPageText.Text = "1 / 1";
            AudioStatusText.Text = "OBS не підключено";
        }
    }));

    private void UpdateObsStatus(bool connected)
    {
        ObsDot.Fill = (Brush)FindResource(connected ? "Green" : "Red");
        ObsStatusText.Text = connected ? "  ПІДКЛЮЧЕНО" : "  ВІДКЛЮЧЕНО";
        ObsStatusText.Foreground = (Brush)FindResource(connected ? "Green" : "Red");
        SystemStateText.Text = connected
            ? "OBS Audio підключено. Керування сценами, Preview, записом і стрімом вимкнено."
            : "OBS Audio не підключено.";
    }

    private void Services_BridgeStatusChanged(object? sender, EventArgs e) => Dispatcher.BeginInvoke(new Action(UpdateBridgeStatus));
    private void UpdateBridgeStatus()
    {
        BridgeStatusText.Text = $"  {_services.BridgeStatus}";
        BridgeStatusText.Foreground = (Brush)FindResource(_services.BridgeAvailable ? "Green" : "Yellow");
    }

    private void Chat_MessageAdded(object? sender, ChatMessage message) => Dispatcher.BeginInvoke(new Action(() =>
    {
        MainChatMessages.Add(message);
        while (MainChatMessages.Count > 300) MainChatMessages.RemoveAt(0);
        UpdateChatStatusText();
        MainChatList.ScrollIntoView(message);
    }));
    private void Services_ChannelStatusChanged(object? sender, EventArgs e) => Dispatcher.BeginInvoke(new Action(UpdateChannelStatus));
    private void RefreshMainChat()
    {
        MainChatMessages.Clear();
        foreach (var message in _services.Chat.Messages.TakeLast(300)) MainChatMessages.Add(message);
        UpdateChatStatusText();
        if (MainChatMessages.Count > 0)
            Dispatcher.BeginInvoke(new Action(() => MainChatList.ScrollIntoView(MainChatMessages[^1])), DispatcherPriority.Background);
    }

    private void UpdateChatStatusText()
    {
        var tw = _services.Twitch.IsChatConnected ? "Twitch чат" : "Twitch OFF";
        var yt = _services.YouTube.IsConnected ? "YouTube синхронізація" : "YouTube OFF";
        ChatStatusText.Text = $"{tw} • {yt} • {MainChatMessages.Count} повідомлень";
    }

    private void UpdateChannelStatus()
    {
        var settings = _services.Settings.Value;
        TwitchViewerText.Text = settings.TwitchViewers.ToString();
        YouTubeViewerText.Text = settings.YouTubeViewers.ToString();
        YouTubeLikesText.Text = settings.YouTubeLikes.ToString();
        TwitchLiveText.Text = settings.TwitchLive ? "  LIVE" : "  OFF";
        TwitchLiveText.Foreground = (Brush)FindResource(settings.TwitchLive ? "Green" : "Muted");
        YouTubeLiveText.Text = settings.YouTubeLive ? "  LIVE" : "  OFF";
        YouTubeLiveText.Foreground = (Brush)FindResource(settings.YouTubeLive ? "Green" : "Muted");
        TwitchTopStatusText.Text = _services.Twitch.IsChatConnected ? "CHAT ON" : _services.Twitch.Status;
        YouTubeTopStatusText.Text = _services.YouTube.IsConnected ? _services.YouTube.Status : "OFF";
        var donatelloLabel = _services.Donatello.IsRunning
            ? (_services.Donatello.ConsecutiveErrors >= 3 ? $"ПОМИЛКА ({_services.Donatello.ConsecutiveErrors})" : "ПІДКЛЮЧЕНО")
            : _services.Donatello.Status;
        DonatelloStatusText.Text = $"DONATELLO: {donatelloLabel} • SUPER CHAT • BITS";
        DonatelloStatusText.Foreground = (Brush)FindResource(_services.Donatello.IsRunning && _services.Donatello.ConsecutiveErrors < 3 ? "Green" : _services.Donatello.ConsecutiveErrors >= 3 ? "Red" : "Muted");
        UpdateChatStatusText();
    }

    private void ChatInput_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) SendChat("Twitch + YouTube");
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
        if (_loadingAudio || !_services.Obs.IsConnected) return;
        _loadingAudio = true;
        try
        {
            var inputs = await _services.Obs.GetMixerInputsAsync();
            var selectedNames = _services.Settings.Value.SelectedAudioInputs;
            var preferred = new[] { "Гра звук", "Медіа", "Мікро", "Чат" };
            var detected = preferred.Where(name => inputs.Any(x => string.Equals(x.name, name, StringComparison.OrdinalIgnoreCase))).ToList();
            if (selectedNames.Count == 0 || (selectedNames.Count > 6 && detected.Count >= 3))
            {
                selectedNames.Clear();
                selectedNames.AddRange(detected);
                _services.Save();
            }
            if (selectedNames.Count > 0)
                inputs = inputs.Where(x => selectedNames.Contains(x.name, StringComparer.OrdinalIgnoreCase))
                    .OrderBy(x => selectedNames.FindIndex(n => string.Equals(n, x.name, StringComparison.OrdinalIgnoreCase))).ToList();
            var oldByName = _allQuickAudio.ToDictionary(x => x.Name, StringComparer.OrdinalIgnoreCase);
            var refreshed = new List<AudioChannel>();

            foreach (var input in inputs)
            {
                var channel = oldByName.TryGetValue(input.name, out var existing)
                    ? existing
                    : new AudioChannel { Name = input.name, Kind = input.kind };
                try { channel.IsMuted = await _services.Obs.GetInputMuteAsync(input.name); } catch { }
                try { channel.Volume = Math.Clamp(await _services.Obs.GetInputVolumeAsync(input.name), 0, 1); } catch { }
                refreshed.Add(channel);
            }

            _allQuickAudio.Clear();
            _allQuickAudio.AddRange(refreshed);
            _audioPageIndex = Math.Clamp(_audioPageIndex, 0, AudioPageCount - 1);
            ShowAudioPage();
            AudioStatusText.Text = _allQuickAudio.Count == 0
                ? "Оберіть канали у повному мікшері"
                : $"{_allQuickAudio.Count} вибраних каналів OBS";
        }
        catch (Exception ex)
        {
            _services.Logger.Error("Оновлення Audio Mixer OBS", ex);
            AudioStatusText.Text = "Помилка оновлення Audio Mixer OBS";
        }
        finally
        {
            _loadingAudio = false;
        }
    }

    private int AudioPageCount => Math.Max(1, (int)Math.Ceiling(_allQuickAudio.Count / 4.0));
    private void ShowAudioPage()
    {
        QuickAudioPage.Clear();
        foreach (var channel in _allQuickAudio.Skip(_audioPageIndex * 4).Take(4)) QuickAudioPage.Add(channel);
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
        var last = _services.Donations.History.LastOrDefault();
        if (last is null)
        {
            LastDonationAmountText.Text = "—";
            LastDonationUserText.Text = "Донати ще не отримано";
            LastDonationMessageText.Text = "Super Chat, Super Sticker, Bits і підписки з’являться тут автоматично.";
        }
        else
        {
            LastDonationAmountText.Text = last.DisplayAmount;
            LastDonationUserText.Text = $"{last.User} • {last.Source}";
            LastDonationMessageText.Text = last.Message;
        }
        DonationGoalProgress.Value = _services.Donations.GoalProgress;
    }

    private void TestDonation_Click(object sender, RoutedEventArgs e)
    {
        var donation = _services.Donations.AddTestDonation();
        _services.Logger.Info($"Тестовий донат: {donation.User} — {donation.DisplayAmount}");
    }

    private void OpenChat_Click(object sender, RoutedEventArgs e) => _services.Windows.Show(() => new ChatBotWindow(), this);
    private void OpenAudio_Click(object sender, RoutedEventArgs e) => _services.Windows.Show(() => new AudioMixerWindow(), this);
    private void OpenOverlay_Click(object sender, RoutedEventArgs e) => _services.Windows.Show(() => new OverlaySettingsWindow(), this);
    private void OpenMusic_Click(object sender, RoutedEventArgs e) => _services.Windows.Show(() => new MusicWindow(), this);
    private void OpenChannels_Click(object sender, RoutedEventArgs e) => _services.Windows.Show(() => new ChannelConnectionsWindow(), this);
    private void OpenNotifications_Click(object sender, RoutedEventArgs e) => _services.Windows.Show(() => new StreamNotificationsWindow(), this);
    private void OpenYouTubeSettings_Click(object sender, RoutedEventArgs e)
    {
        try { Process.Start(new ProcessStartInfo("https://studio.youtube.com/channel/UC4-t_7-LD_E15LXazQmsq_g/livestreaming/dashboard") { UseShellExecute = true }); }
        catch (Exception ex) { _services.Logger.Error("Відкриття YouTube Studio", ex); }
    }
    private void OpenDonatello_Click(object sender, RoutedEventArgs e) => _services.Windows.Show(() => new DonatelloWindow(), this);
    private void OpenSettings_Click(object sender, RoutedEventArgs e) => _services.Windows.Show(() => new SettingsWindow(), this);
    private void OpenJournal_Click(object sender, RoutedEventArgs e) => _services.Windows.Show(() => new SettingsWindow(), this);

    private void LoggerEntries_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => Dispatcher.BeginInvoke(new Action(UpdateLastLog));
    private void UpdateLastLog()
    {
        var line = _services.Logger.Entries.LastOrDefault() ?? "TiHiY StreamControl Center готовий";
        var stackIndex = line.IndexOf("   at ", StringComparison.Ordinal);
        LastLogText.Text = stackIndex > 0 ? line[..stackIndex].Trim() : line;
    }

    private void ZoomOut_Click(object sender, RoutedEventArgs e) { _services.UiScale.Decrease(); _services.Save(); }
    private void ZoomIn_Click(object sender, RoutedEventArgs e) { _services.UiScale.Increase(); _services.Save(); }
    private void ZoomAuto_Click(object sender, RoutedEventArgs e) { _services.UiScale.Reset(); _services.Save(); }
    private void UiScale_ScaleChanged(object? sender, EventArgs e) => Dispatcher.BeginInvoke(new Action(ApplyScale));
    private void Window_SizeChanged(object sender, SizeChangedEventArgs e) { if (_services.UiScale.Auto) ApplyScale(); }
    private void ApplyScale()
    {
        var appliedPercent = _services.UiScale.Apply(DesignSurface, this, 1440, 850);
        ZoomTextButton.Content = _services.UiScale.Auto ? $"АВТО {appliedPercent}%" : $"{_services.UiScale.Percent}%";
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

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (_closing) return;
        _closing = true;
        _audioRefreshTimer.Stop();
        _services.Chat.MessageAdded -= Chat_MessageAdded;
        _services.Donations.DonationAdded -= Donations_DonationAdded;
        _services.Obs.ConnectionChanged -= Obs_ConnectionChanged;
        _services.Obs.InputMuteChanged -= Obs_InputMuteChanged;
        _services.Obs.InputVolumeChanged -= Obs_InputVolumeChanged;
        _services.Obs.InputMeterChanged -= Obs_InputMeterChanged;
        _services.BridgeStatusChanged -= Services_BridgeStatusChanged;
        _services.ChannelStatusChanged -= Services_ChannelStatusChanged;
        _services.Logger.Entries.CollectionChanged -= LoggerEntries_CollectionChanged;
        _services.UiScale.ScaleChanged -= UiScale_ScaleChanged;
        var settings = _services.Settings.Value;
        settings.MainLeftColumnWidth = MainRightColumn.ActualWidth > 0 ? Math.Clamp(MainLeftColumn.ActualWidth / MainRightColumn.ActualWidth, 0.25, 6) : 2.1;
        settings.MainTopRowHeight = MainBottomRow.ActualHeight > 0 ? Math.Clamp(MainTopRow.ActualHeight / MainBottomRow.ActualHeight, 0.25, 6) : 1.85;
        _services.Windows.CloseAll();
        try { _services.Save(); } catch { }
    }
}
