using System.Diagnostics;
using System.Windows.Controls.Primitives;
using TiHiY.StreamControlCenter.Services;

namespace TiHiY.StreamControlCenter.Windows;

public partial class SettingsWindow : ModuleWindowBase
{
    private sealed class ThemePreviewItem
    {
        public required ThemeService.ThemeInfo Theme { get; init; }
        public required ImageSource Preview { get; init; }
        public required string DisplayName { get; init; }
        public required string EnglishName { get; init; }
        public string Name => Theme.Name;
        public string Description => Theme.Description;
    }

    private readonly AppServices _services = App.Services;
    private readonly ObservableCollection<ThemePreviewItem> _themeItems = new();
    private bool _loadingControls;
    private string _initialTheme = string.Empty;

    public ObservableCollection<string> VisibleLogs { get; } = new();

    public SettingsWindow()
    {
        InitializeComponent();
        DataContext = this;
        ConfigureModule(DesignSurface, 1628, 908, "Settings");

        BuildThemeCards();
        ConfigureSelectors();
        LoadSettingsToControls();

        _services.Logger.Entries.CollectionChanged += LoggerEntries_CollectionChanged;
        _services.Obs.ConnectionChanged += Obs_ConnectionChanged;
        _services.Language.LanguageChanged += Language_LanguageChanged;
        _services.Twitch.StatusChanged += Channel_StatusChanged;
        _services.YouTube.StatusChanged += Channel_StatusChanged;

        Loaded += SettingsWindow_Loaded;
        Closed += SettingsWindow_Closed;

        RefreshLogs();
        UpdateConnectionStatus();
    }

    private void BuildThemeCards()
    {
        var preferred = new[]
        {
            (Internal: "Україна", Ua: "Україна", En: "Ukraine"),
            (Internal: "Сталкер", Ua: "Сталкер", En: "Stalker"),
            (Internal: "Військова", Ua: "Військова", En: "Military"),
            (Internal: "TiHiY Default / Cyber Amber", Ua: "Темна", En: "Dark")
        };

        foreach (var item in preferred)
        {
            var theme = _services.Theme.Themes.FirstOrDefault(x =>
                string.Equals(x.Name, item.Internal, StringComparison.OrdinalIgnoreCase));
            if (theme is null) continue;
            _themeItems.Add(new ThemePreviewItem
            {
                Theme = theme,
                Preview = ThemePreviewRenderer.Render(theme, 720, 405),
                DisplayName = item.Ua,
                EnglishName = item.En
            });
        }

        ThemeList.ItemsSource = _themeItems;
        _initialTheme = _services.Theme.CurrentTheme;
        ThemeList.SelectedItem = _themeItems.FirstOrDefault(x =>
            string.Equals(x.Name, _initialTheme, StringComparison.OrdinalIgnoreCase)) ?? _themeItems.FirstOrDefault();
        UpdateThemePreview();
    }

    private void ConfigureSelectors()
    {
        LanguageCombo.ItemsSource = _services.Language.Languages;
        LanguageCombo.DisplayMemberPath = nameof(LanguageService.LanguageInfo.DisplayName);
        LanguageCombo.SelectedValuePath = nameof(LanguageService.LanguageInfo.Code);
    }

    private void LoadSettingsToControls()
    {
        _loadingControls = true;
        try
        {
            var settings = _services.Settings.Value;
            ObsUrlBox.Text = settings.ObsUrl;
            RememberPasswordCheck.IsChecked = settings.RememberObsPassword;
            AutoConnectCheck.IsChecked = settings.AutoConnectObs;
            AutoScaleCheck.IsChecked = settings.UiScaleAuto;
            ScaleText.Text = settings.UiScaleAuto ? "АВТО" : $"{settings.UiScalePercent}%";

            LanguageCombo.SelectedValue = settings.UiLanguage;
            SelectComboByTag(ScaleCombo, settings.UiScalePercent.ToString());
            SelectComboByTag(DensityCombo, settings.UiDensity);
            SelectComboByTag(UpdateFrequencyCombo, settings.UpdateCheckFrequency);
            SelectComboByTag(UpdateChannelCombo, settings.UpdateChannel);
            SelectComboByTag(AutoLockCombo, settings.SecurityAutoLockMinutes.ToString());

            AnimationsCheck.IsChecked = settings.InterfaceAnimationsEnabled;
            TooltipsCheck.IsChecked = settings.ShowTooltips;
            LockLayoutCheck.IsChecked = settings.LockLayoutAfterStartup;
            StartWithWindowsCheck.IsChecked = settings.StartWithWindows;
            MinimizeToTrayCheck.IsChecked = settings.MinimizeToTray;
            TransitionEffectsCheck.IsChecked = settings.TransitionEffectsEnabled;
            ConfirmExitCheck.IsChecked = settings.ConfirmOnExit;

            if (settings.RememberObsPassword)
                ObsPasswordBox.Password = _services.Credentials.LoadPassword();

            StreamElementsApiBox.Password = _services.Credentials.LoadSecret("StreamElementsApi");
            StreamlabsApiBox.Password = _services.Credentials.LoadSecret("StreamlabsApi");
            DiscordBotTokenBox.Password = _services.Credentials.LoadSecret("DiscordBotToken");
            SettingsPasswordBox.Password = _services.Credentials.LoadSecret("SettingsPassword");
        }
        finally
        {
            _loadingControls = false;
        }
    }

    private static void SelectComboByTag(ComboBox combo, string tag)
    {
        combo.SelectedItem = combo.Items.OfType<ComboBoxItem>()
            .FirstOrDefault(x => string.Equals(x.Tag?.ToString(), tag, StringComparison.OrdinalIgnoreCase))
            ?? combo.Items.OfType<ComboBoxItem>().FirstOrDefault();
    }

    private static string SelectedTag(ComboBox combo, string fallback) =>
        (combo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? fallback;

    private void SettingsWindow_Loaded(object sender, RoutedEventArgs e)
    {
        AttachThemeToolTips();
        ApplyLocalizedWindowText();
        UpdateConnectionStatus();
    }

    private void SettingsWindow_Closed(object? sender, EventArgs e)
    {
        _services.Logger.Entries.CollectionChanged -= LoggerEntries_CollectionChanged;
        _services.Obs.ConnectionChanged -= Obs_ConnectionChanged;
        _services.Language.LanguageChanged -= Language_LanguageChanged;
        _services.Twitch.StatusChanged -= Channel_StatusChanged;
        _services.YouTube.StatusChanged -= Channel_StatusChanged;
        Loaded -= SettingsWindow_Loaded;
        Closed -= SettingsWindow_Closed;
    }

    private void AttachThemeToolTips()
    {
        Dispatcher.BeginInvoke(new Action(() =>
        {
            for (var index = 0; index < ThemeList.Items.Count; index++)
            {
                if (ThemeList.ItemContainerGenerator.ContainerFromIndex(index) is not ListBoxItem container ||
                    ThemeList.Items[index] is not ThemePreviewItem item)
                    continue;

                container.ToolTip = BuildThemeToolTip(item);
                ToolTipService.SetInitialShowDelay(container, 180);
                ToolTipService.SetShowDuration(container, 12000);
            }
        }), DispatcherPriority.Loaded);
    }

    private static ToolTip BuildThemeToolTip(ThemePreviewItem item)
    {
        var stack = new StackPanel();
        stack.Children.Add(new Image
        {
            Source = item.Preview,
            Width = 520,
            Height = 292,
            Stretch = Stretch.Uniform
        });
        stack.Children.Add(new TextBlock
        {
            Text = $"{item.DisplayName} / {item.EnglishName}",
            FontSize = 17,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(item.Theme.Palette.Amber),
            Margin = new Thickness(0, 8, 0, 2)
        });
        stack.Children.Add(new TextBlock
        {
            Text = item.Description,
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(item.Theme.Palette.Muted),
            MaxWidth = 520
        });

        return new ToolTip
        {
            Placement = PlacementMode.Right,
            Content = new Border
            {
                Background = new SolidColorBrush(item.Theme.Palette.Panel),
                BorderBrush = new SolidColorBrush(item.Theme.Palette.Amber),
                BorderThickness = new Thickness(1.2),
                CornerRadius = new CornerRadius(7),
                Padding = new Thickness(8),
                Child = stack
            }
        };
    }

    private ThemePreviewItem? SelectedTheme() => ThemeList.SelectedItem as ThemePreviewItem;

    private void ThemeList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateThemePreview();
        if (!_loadingControls && IsLoaded && SelectedTheme() is { } item)
            StatusText.Text = $"Вибрано тему «{item.DisplayName}». Зміни набудуть чинності після «Застосувати» або «Зберегти».";
    }

    private void UpdateThemePreview()
    {
        if (SelectedTheme() is not { } item) return;
        var theme = item.Theme;
        ThemePreviewImage.Source = item.Preview;
        ThemePreviewName.Text = $"{item.DisplayName} / {item.EnglishName}";
        ThemePreviewDescription.Text = theme.Description;
        ThemePreviewPrimary.Fill = new SolidColorBrush(theme.Palette.Cyan);
        ThemePreviewAccent.Fill = new SolidColorBrush(theme.Palette.Amber);
        ThemePreviewSuccess.Fill = new SolidColorBrush(theme.Palette.Green);
        ThemePreviewBorder.Background = new SolidColorBrush(theme.Palette.Panel);
        ThemePreviewBorder.BorderBrush = new SolidColorBrush(theme.Palette.Line);
    }

    private void Language_LanguageChanged(object? sender, EventArgs e) =>
        Dispatcher.BeginInvoke(new Action(ApplyLocalizedWindowText), DispatcherPriority.Render);

    private void ApplyLocalizedWindowText()
    {
        var english = string.Equals(_services.Language.CurrentLanguage, "en-US", StringComparison.OrdinalIgnoreCase);
        Title = english ? "TiHiY StreamControl Center — Settings" : "TiHiY StreamControl Center — Налаштування";
        if (!_loadingControls && IsLoaded)
            StatusText.Text = english ? "Interface language has been applied." : "Мову інтерфейсу застосовано.";
    }

    private void SaveSettings(bool closeAfterSave)
    {
        var settings = _services.Settings.Value;
        settings.ObsUrl = NormalizeObsUrl(ObsUrlBox.Text);
        settings.RememberObsPassword = RememberPasswordCheck.IsChecked == true;
        settings.AutoConnectObs = AutoConnectCheck.IsChecked == true;
        settings.UiLanguage = LanguageCombo.SelectedValue as string ?? settings.UiLanguage;
        settings.UiScaleAuto = AutoScaleCheck.IsChecked == true;
        settings.UiScalePercent = int.TryParse(SelectedTag(ScaleCombo, "100"), out var scale) ? scale : 100;
        settings.UiDensity = SelectedTag(DensityCombo, "Standard");
        settings.InterfaceAnimationsEnabled = AnimationsCheck.IsChecked == true;
        settings.ShowTooltips = TooltipsCheck.IsChecked == true;
        settings.LockLayoutAfterStartup = LockLayoutCheck.IsChecked == true;
        settings.StartWithWindows = StartWithWindowsCheck.IsChecked == true;
        settings.MinimizeToTray = MinimizeToTrayCheck.IsChecked == true;
        settings.TransitionEffectsEnabled = TransitionEffectsCheck.IsChecked == true;
        settings.UpdateCheckFrequency = SelectedTag(UpdateFrequencyCombo, "Daily");
        settings.UpdateChannel = SelectedTag(UpdateChannelCombo, "Stable");
        settings.SecurityAutoLockMinutes = int.TryParse(SelectedTag(AutoLockCombo, "15"), out var autoLock) ? autoLock : 15;
        settings.ConfirmOnExit = ConfirmExitCheck.IsChecked == true;

        if (settings.RememberObsPassword && !string.IsNullOrWhiteSpace(ObsPasswordBox.Password))
            _services.Credentials.SavePassword(ObsPasswordBox.Password);
        else if (!settings.RememberObsPassword)
            _services.Credentials.DeletePassword();

        SaveCredential("StreamElementsApi", StreamElementsApiBox.Password);
        SaveCredential("StreamlabsApi", StreamlabsApiBox.Password);
        SaveCredential("DiscordBotToken", DiscordBotTokenBox.Password);
        SaveCredential("SettingsPassword", SettingsPasswordBox.Password);

        if (SelectedTheme() is { } selectedTheme)
            _services.Theme.Apply(selectedTheme.Name, save: false);
        _services.Language.Apply(settings.UiLanguage, save: false);
        _services.UiScale.Auto = settings.UiScaleAuto;
        _services.UiScale.Percent = settings.UiScalePercent;
        ApplyStartupShortcut(settings.StartWithWindows);
        _services.Save();

        _initialTheme = settings.UiTheme;
        StatusText.Text = "Налаштування збережено та застосовано до програми.";
        if (closeAfterSave) Close();
    }

    private void SaveCredential(string key, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            _services.Credentials.DeleteSecret(key);
        else
            _services.Credentials.SaveSecret(key, value);
    }

    private static string NormalizeObsUrl(string value) =>
        string.IsNullOrWhiteSpace(value) ? "ws://127.0.0.1:4455" : value.Trim();

    private void ApplyStartupShortcut(bool enabled)
    {
        if (!OperatingSystem.IsWindows()) return;
        var startup = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
        if (string.IsNullOrWhiteSpace(startup)) return;
        var shortcut = Path.Combine(startup, ShortcutService.ShortcutFileName);
        if (enabled)
            ShortcutService.EnsureShortcut(shortcut, _services.Logger);
        else
        {
            try { if (File.Exists(shortcut)) File.Delete(shortcut); }
            catch (Exception ex) { _services.Logger.Error("Видалення ярлика автозапуску", ex); }
        }
    }

    private void Apply_Click(object sender, RoutedEventArgs e) => SaveSettings(closeAfterSave: false);
    private void Save_Click(object sender, RoutedEventArgs e) => SaveSettings(closeAfterSave: true);
    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();

    private async void SaveAndConnect_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var password = ObsPasswordBox.Password;
            if (string.IsNullOrWhiteSpace(password) && _services.Settings.Value.RememberObsPassword)
                password = _services.Credentials.LoadPassword();

            _services.Settings.Value.ObsUrl = NormalizeObsUrl(ObsUrlBox.Text);
            _services.Settings.Value.RememberObsPassword = RememberPasswordCheck.IsChecked == true;
            _services.Settings.Value.AutoConnectObs = AutoConnectCheck.IsChecked == true;
            if (_services.Settings.Value.RememberObsPassword && !string.IsNullOrWhiteSpace(password))
                _services.Credentials.SavePassword(password);
            _services.Save();

            await _services.Obs.ConnectAsync(_services.Settings.Value.ObsUrl, password);
            StatusText.Text = "OBS WebSocket підключено.";
        }
        catch (Exception ex)
        {
            _services.Logger.Error("Збереження та підключення OBS", ex);
            MessageBox.Show(this, ex.GetBaseException().Message, "OBS WebSocket", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async void TestObsConnection_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (!_services.Obs.IsConnected)
                await _services.Obs.ConnectAsync(NormalizeObsUrl(ObsUrlBox.Text), ObsPasswordBox.Password);
            StatusText.Text = "Перевірка OBS успішна: підключення активне.";
            UpdateConnectionStatus();
        }
        catch (Exception ex)
        {
            _services.Logger.Error("Перевірка OBS", ex);
            StatusText.Text = "OBS не відповідає. Перевірте адресу, порт і пароль.";
        }
    }

    private async void Disconnect_Click(object sender, RoutedEventArgs e) => await _services.Obs.DisconnectAsync();

    private void ForgetPassword_Click(object sender, RoutedEventArgs e)
    {
        _services.Credentials.DeletePassword();
        ObsPasswordBox.Password = string.Empty;
        RememberPasswordCheck.IsChecked = false;
        _services.Settings.Value.RememberObsPassword = false;
        _services.Save();
        StatusText.Text = "Збережений пароль OBS видалено.";
    }

    private void SaveKeys_Click(object sender, RoutedEventArgs e)
    {
        SaveCredential("StreamElementsApi", StreamElementsApiBox.Password);
        SaveCredential("StreamlabsApi", StreamlabsApiBox.Password);
        SaveCredential("DiscordBotToken", DiscordBotTokenBox.Password);
        StatusText.Text = "API-ключі збережено у Windows Credential Manager.";
    }

    private void SaveSecurity_Click(object sender, RoutedEventArgs e)
    {
        SaveCredential("SettingsPassword", SettingsPasswordBox.Password);
        _services.Settings.Value.SecurityAutoLockMinutes = int.TryParse(SelectedTag(AutoLockCombo, "15"), out var value) ? value : 15;
        _services.Settings.Value.ConfirmOnExit = ConfirmExitCheck.IsChecked == true;
        _services.Save();
        StatusText.Text = "Параметри безпеки збережено.";
    }

    private void ScaleDown_Click(object sender, RoutedEventArgs e)
    {
        var value = int.TryParse(SelectedTag(ScaleCombo, "100"), out var current) ? current : 100;
        SelectComboByTag(ScaleCombo, Math.Max(80, value - 10).ToString());
        AutoScaleCheck.IsChecked = false;
        ScaleText.Text = SelectedTag(ScaleCombo, "100") + "%";
    }

    private void ScaleUp_Click(object sender, RoutedEventArgs e)
    {
        var value = int.TryParse(SelectedTag(ScaleCombo, "100"), out var current) ? current : 100;
        SelectComboByTag(ScaleCombo, Math.Min(125, value + 10).ToString());
        AutoScaleCheck.IsChecked = false;
        ScaleText.Text = SelectedTag(ScaleCombo, "100") + "%";
    }

    private void ResetDashboardLayout_Click(object sender, RoutedEventArgs e)
    {
        var settings = _services.Settings.Value;
        settings.DashboardBlockSlots.Clear();
        settings.DashboardFreeformBounds.Clear();
        settings.DashboardLayoutVersion = 0;
        settings.UkraineReferenceLayoutVersion = 0;
        _services.Save();
        StatusText.Text = "Розмітку головного вікна скинуто. Вона відновиться після повторного відкриття.";
    }

    private void Obs_ConnectionChanged(object? sender, bool connected) =>
        Dispatcher.BeginInvoke(new Action(UpdateConnectionStatus));

    private void Channel_StatusChanged(object? sender, EventArgs e) =>
        Dispatcher.BeginInvoke(new Action(UpdateConnectionStatus));

    private void UpdateConnectionStatus()
    {
        var obsConnected = _services.Obs.IsConnected;
        ObsStatusText.Text = obsConnected ? "OBS ПІДКЛЮЧЕНО" : "OBS не підключено";
        ObsStatusText.Foreground = (Brush)FindResource(obsConnected ? "Green" : "Muted");
        ObsStatusTextCard.Text = obsConnected ? "● Підключено" : "● Не підключено";
        ObsStatusTextCard.Foreground = (Brush)FindResource(obsConnected ? "Green" : "Red");

        ParseObsAddress(ObsUrlBox.Text, out var address, out var port);
        ObsAddressText.Text = address;
        ObsPortText.Text = port;

        TwitchConnectionText.Text = _services.Twitch.IsChatConnected
            ? $"Підключено: {_services.Settings.Value.TwitchChannelName}"
            : _services.Twitch.Status;
        TwitchConnectionText.Foreground = (Brush)FindResource(_services.Twitch.IsChatConnected ? "Green" : "Muted");

        YouTubeConnectionText.Text = _services.YouTube.IsConnected
            ? $"Підключено: {_services.Settings.Value.YouTubeChannelName}"
            : _services.YouTube.Status;
        YouTubeConnectionText.Foreground = (Brush)FindResource(_services.YouTube.IsConnected ? "Green" : "Muted");
    }

    private static void ParseObsAddress(string value, out string address, out string port)
    {
        if (Uri.TryCreate(NormalizeObsUrl(value), UriKind.Absolute, out var uri))
        {
            address = uri.Host;
            port = uri.Port.ToString();
            return;
        }
        address = "127.0.0.1";
        port = "4455";
    }

    private void LoggerEntries_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e) =>
        Dispatcher.BeginInvoke(new Action(RefreshLogs));

    private void RefreshLogs()
    {
        VisibleLogs.Clear();
        foreach (var line in _services.Logger.Entries.TakeLast(14)) VisibleLogs.Add(line);
    }

    private void OpenChannelsWindow_Click(object sender, RoutedEventArgs e) =>
        _services.Windows.Show(() => new ChannelConnectionsWindow(), this);

    private void OpenYouTubeSettingsWindow_Click(object sender, RoutedEventArgs e) =>
        _services.Windows.Show(() => new YouTubeStreamSettingsWindow(), this);

    private void OpenNotificationsWindow_Click(object sender, RoutedEventArgs e) =>
        _services.Windows.Show(() => new StreamNotificationsWindow(), this);

    private void OpenDonatelloWindow_Click(object sender, RoutedEventArgs e) =>
        _services.Windows.Show(() => new DonatelloWindow(), this);

    private void OpenMusicWindow_Click(object sender, RoutedEventArgs e) =>
        _services.Windows.Show(() => new MusicWindow(), this);

    private void OpenOverlayWindow_Click(object sender, RoutedEventArgs e) =>
        _services.Windows.Show(() => new OverlaySettingsWindow(), this);

    private void OpenBroadcastDashboard_Click(object sender, RoutedEventArgs e)
    {
        const string dashboardUrl = "https://studio.youtube.com/channel/UC4-t_7-LD_E15LXazQmsq_g/livestreaming/dashboard";
        try
        {
            Process.Start(new ProcessStartInfo { FileName = dashboardUrl, UseShellExecute = true });
            StatusText.Text = "Відкрито YouTube Studio Live Dashboard.";
        }
        catch (Exception ex)
        {
            _services.Logger.Error("Відкриття YouTube Studio", ex);
            StatusText.Text = "Не вдалося відкрити YouTube Studio.";
        }
    }

    private void ClearLog_Click(object sender, RoutedEventArgs e)
    {
        _services.Logger.Entries.Clear();
        RefreshLogs();
    }

    private void OpenLogs_Click(object sender, RoutedEventArgs e) => OpenFolder(_services.Logger.Folder);
    private void OpenSettingsFolder_Click(object sender, RoutedEventArgs e) => OpenFolder(_services.SettingsService.Folder);

    private void OpenFolder(string path)
    {
        try
        {
            Directory.CreateDirectory(path);
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            _services.Logger.Error("Відкриття папки", ex);
        }
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => DragTitle(sender, e);
    private void Minimize_Click(object sender, RoutedEventArgs e) => MinimizeWindow(sender, e);
    private void Maximize_Click(object sender, RoutedEventArgs e) => MaximizeWindow(sender, e);
    private void Close_Click(object sender, RoutedEventArgs e) => CloseWindow(sender, e);
}