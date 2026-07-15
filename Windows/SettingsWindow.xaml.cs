using System.Diagnostics;
using TiHiY.StreamControlCenter.Services;

namespace TiHiY.StreamControlCenter.Windows;

public partial class SettingsWindow : ModuleWindowBase
{
    private sealed record ThemePreviewItem(ThemeService.ThemeInfo Theme, ImageSource Preview)
    {
        public string Name => Theme.Name;
        public string Description => Theme.Description;
    }

    private readonly AppServices _services = App.Services;
    private readonly ObservableCollection<ThemePreviewItem> _themeItems = new();
    private ComboBox? _languageCombo;
    public ObservableCollection<string> VisibleLogs { get; } = new();

    public SettingsWindow()
    {
        InitializeComponent();
        DataContext = this;
        ConfigureModule(DesignSurface, 1260, 780, "Settings");
        ObsUrlBox.Text = _services.Settings.Value.ObsUrl;
        RememberPasswordCheck.IsChecked = _services.Settings.Value.RememberObsPassword;
        AutoConnectCheck.IsChecked = _services.Settings.Value.AutoConnectObs;
        AutoScaleCheck.IsChecked = _services.UiScale.Auto;
        ScaleText.Text = $"{_services.UiScale.Percent}%";

        ThemeCombo.ItemsSource = _themeItems;
        ThemeCombo.DisplayMemberPath = nameof(ThemePreviewItem.Name);
        ThemeCombo.SelectedValuePath = nameof(ThemePreviewItem.Name);
        foreach (var theme in _services.Theme.Themes)
            _themeItems.Add(new ThemePreviewItem(theme, ThemePreviewRenderer.Render(theme)));
        ThemeCombo.SelectedValue = _services.Theme.CurrentTheme;
        ThemeCombo.DropDownOpened += ThemeCombo_DropDownOpened;
        UpdateThemePreview();

        if (_services.Settings.Value.RememberObsPassword) ObsPasswordBox.Password = _services.Credentials.LoadPassword();
        _services.Logger.Entries.CollectionChanged += LoggerEntries_CollectionChanged;
        _services.Obs.ConnectionChanged += Obs_ConnectionChanged;
        _services.Language.LanguageChanged += Language_LanguageChanged;
        Loaded += SettingsWindow_Loaded;
        Closed += SettingsWindow_Closed;
        RefreshLogs();
        UpdateObsStatus(_services.Obs.IsConnected);
    }

    private void SettingsWindow_Loaded(object sender, RoutedEventArgs e)
    {
        InstallLanguageSelector();
        ApplyLocalizedWindowText();
    }

    private void SettingsWindow_Closed(object? sender, EventArgs e)
    {
        _services.Logger.Entries.CollectionChanged -= LoggerEntries_CollectionChanged;
        _services.Obs.ConnectionChanged -= Obs_ConnectionChanged;
        _services.Language.LanguageChanged -= Language_LanguageChanged;
        ThemeCombo.DropDownOpened -= ThemeCombo_DropDownOpened;
        Loaded -= SettingsWindow_Loaded;
        Closed -= SettingsWindow_Closed;
    }

    private void InstallLanguageSelector()
    {
        if (_languageCombo is not null) return;

        var languageTitle = FindVisualDescendants<TextBlock>(DesignSurface)
            .FirstOrDefault(x => x.Text.Contains("Мова програми", StringComparison.OrdinalIgnoreCase) ||
                                 x.Text.Contains("Application language", StringComparison.OrdinalIgnoreCase));
        if (languageTitle?.Parent is not StackPanel host) return;

        _languageCombo = new ComboBox
        {
            MinWidth = 250,
            MinHeight = 36,
            Margin = new Thickness(0, 10, 0, 0),
            ItemsSource = _services.Language.Languages,
            DisplayMemberPath = nameof(LanguageService.LanguageInfo.DisplayName),
            SelectedValuePath = nameof(LanguageService.LanguageInfo.Code),
            SelectedValue = _services.Language.CurrentLanguage,
            ToolTip = "Українська / English"
        };
        _languageCombo.SelectionChanged += LanguageCombo_SelectionChanged;
        host.Children.Add(_languageCombo);
    }

    private void LanguageCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded || _languageCombo?.SelectedValue is not string languageCode) return;
        _services.Language.Apply(languageCode, save: true);
    }

    private void Language_LanguageChanged(object? sender, EventArgs e) =>
        Dispatcher.BeginInvoke(new Action(ApplyLocalizedWindowText), DispatcherPriority.Render);

    private void ApplyLocalizedWindowText()
    {
        var english = string.Equals(_services.Language.CurrentLanguage, "en-US", StringComparison.OrdinalIgnoreCase);
        Title = english ? "TiHiY StreamControl Center — Settings" : "TiHiY StreamControl Center — Налаштування";
        StatusText.Text = english ? "Language applied to the application resources." : "Мову застосовано до ресурсів програми.";

        if (_languageCombo is not null && !Equals(_languageCombo.SelectedValue, _services.Language.CurrentLanguage))
            _languageCombo.SelectedValue = _services.Language.CurrentLanguage;
    }

    private static IEnumerable<T> FindVisualDescendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            if (child is T match) yield return match;
            foreach (var descendant in FindVisualDescendants<T>(child)) yield return descendant;
        }
    }

    private void ThemeCombo_DropDownOpened(object? sender, EventArgs e) =>
        Dispatcher.BeginInvoke(new Action(AttachThemeToolTips), DispatcherPriority.Loaded);

    private void AttachThemeToolTips()
    {
        for (var index = 0; index < ThemeCombo.Items.Count; index++)
        {
            if (ThemeCombo.ItemContainerGenerator.ContainerFromIndex(index) is not ComboBoxItem container ||
                ThemeCombo.Items[index] is not ThemePreviewItem item)
                continue;
            container.ToolTip = BuildThemeToolTip(item);
            ToolTipService.SetInitialShowDelay(container, 180);
            ToolTipService.SetBetweenShowDelay(container, 40);
            ToolTipService.SetShowDuration(container, 12000);
        }
    }

    private ToolTip BuildThemeToolTip(ThemePreviewItem item)
    {
        var image = new Image
        {
            Source = item.Preview,
            Width = 520,
            Height = 292,
            Stretch = Stretch.UniformToFill
        };
        var name = new TextBlock
        {
            Text = item.Name,
            FontSize = 17,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(item.Theme.Palette.Amber),
            Margin = new Thickness(0, 8, 0, 2)
        };
        var description = new TextBlock
        {
            Text = item.Description,
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(item.Theme.Palette.Muted),
            MaxWidth = 520
        };
        var stack = new StackPanel();
        stack.Children.Add(image);
        stack.Children.Add(name);
        stack.Children.Add(description);

        return new ToolTip
        {
            Placement = System.Windows.Controls.Primitives.PlacementMode.Right,
            StaysOpen = false,
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

    private ThemePreviewItem SelectedThemePreview() =>
        ThemeCombo.SelectedItem as ThemePreviewItem ??
        _themeItems.FirstOrDefault(x => string.Equals(x.Name, _services.Theme.CurrentTheme, StringComparison.OrdinalIgnoreCase)) ??
        _themeItems[0];

    private void LoggerEntries_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e) => Dispatcher.BeginInvoke(new Action(RefreshLogs));
    private void Obs_ConnectionChanged(object? sender, bool connected) => Dispatcher.BeginInvoke(new Action(() => UpdateObsStatus(connected)));

    private void RefreshLogs()
    {
        VisibleLogs.Clear();
        foreach (var line in _services.Logger.Entries.TakeLast(14)) VisibleLogs.Add(line);
    }

    private void UpdateObsStatus(bool connected)
    {
        ObsStatusText.Text = connected ? "OBS ПІДКЛЮЧЕНО" : "OBS не підключено";
        ObsStatusText.Foreground = (Brush)FindResource(connected ? "Green" : "Muted");
    }

    private async void SaveAndConnect_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var password = ObsPasswordBox.Password;
            if (string.IsNullOrWhiteSpace(password) && _services.Settings.Value.RememberObsPassword)
                password = _services.Credentials.LoadPassword();

            _services.Settings.Value.ObsUrl = string.IsNullOrWhiteSpace(ObsUrlBox.Text) ? "ws://127.0.0.1:4455" : ObsUrlBox.Text.Trim();
            _services.Settings.Value.RememberObsPassword = RememberPasswordCheck.IsChecked == true;
            _services.Settings.Value.AutoConnectObs = AutoConnectCheck.IsChecked == true;

            if (_services.Settings.Value.RememberObsPassword && !string.IsNullOrWhiteSpace(password))
                _services.Credentials.SavePassword(password);
            else if (!_services.Settings.Value.RememberObsPassword)
                _services.Credentials.DeletePassword();

            _services.Save();
            await _services.Obs.ConnectAsync(_services.Settings.Value.ObsUrl, password);
            StatusText.Text = string.IsNullOrWhiteSpace(password)
                ? "OBS підключено без пароля."
                : (_services.Settings.Value.RememberObsPassword
                    ? "Пароль зашифровано та збережено в Windows Credential Manager."
                    : "OBS підключено. Пароль не збережено.");
        }
        catch (Exception ex)
        {
            _services.Logger.Error("Збереження та підключення OBS", ex);
            MessageBox.Show(this, ex.Message, "OBS WebSocket", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async void Disconnect_Click(object sender, RoutedEventArgs e) => await _services.Obs.DisconnectAsync();

    private void ForgetPassword_Click(object sender, RoutedEventArgs e)
    {
        _services.Credentials.DeletePassword();
        ObsPasswordBox.Password = string.Empty;
        _services.Settings.Value.RememberObsPassword = false;
        RememberPasswordCheck.IsChecked = false;
        _services.Save();
        StatusText.Text = "Збережений пароль OBS видалено.";
    }

    private void AutoScaleCheck_Changed(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;
        _services.UiScale.Auto = AutoScaleCheck.IsChecked == true;
        ScaleText.Text = _services.UiScale.Auto ? "АВТО" : $"{_services.UiScale.Percent}%";
        _services.Save();
    }

    private void ScaleDown_Click(object sender, RoutedEventArgs e)
    {
        _services.UiScale.Decrease();
        AutoScaleCheck.IsChecked = false;
        ScaleText.Text = $"{_services.UiScale.Percent}%";
        _services.Save();
    }

    private void ScaleUp_Click(object sender, RoutedEventArgs e)
    {
        _services.UiScale.Increase();
        AutoScaleCheck.IsChecked = false;
        ScaleText.Text = $"{_services.UiScale.Percent}%";
        _services.Save();
    }

    private void ThemeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateThemePreview();
        if (!IsLoaded) return;
        var item = SelectedThemePreview();
        StatusText.Text = $"Вибрано попередній перегляд «{item.Name}». Натисніть «ЗАСТОСУВАТИ ТЕМУ».";
    }

    private void ApplyTheme_Click(object sender, RoutedEventArgs e)
    {
        var item = SelectedThemePreview();
        _services.Theme.Apply(item.Name, save: true);
        StatusText.Text = $"Тему «{item.Name}» застосовано до головного вікна, відкритих модулів і overlay.";
    }

    private void RestoreDefaultTheme_Click(object sender, RoutedEventArgs e)
    {
        const string defaultTheme = "TiHiY Default / Cyber Amber";
        ThemeCombo.SelectedValue = defaultTheme;
        _services.Theme.Apply(defaultTheme, save: true);
        UpdateThemePreview();
        StatusText.Text = "Відновлено стандартну тему TiHiY Default / Cyber Amber.";
    }

    private void UpdateThemePreview()
    {
        if (_themeItems.Count == 0) return;
        var item = SelectedThemePreview();
        var theme = item.Theme;
        ThemePreviewName.Text = theme.Name;
        ThemePreviewDescription.Text = theme.Description;
        ThemePreviewPrimary.Fill = new SolidColorBrush(theme.Palette.Cyan);
        ThemePreviewAccent.Fill = new SolidColorBrush(theme.Palette.Amber);
        ThemePreviewSuccess.Fill = new SolidColorBrush(theme.Palette.Green);
        ThemePreviewBorder.Background = new SolidColorBrush(theme.Palette.Panel);
        ThemePreviewBorder.BorderBrush = new SolidColorBrush(theme.Palette.Line);
        ThemePreviewName.Foreground = new SolidColorBrush(theme.Palette.Amber);
        ThemePreviewDescription.Foreground = new SolidColorBrush(theme.Palette.Muted);
        ThemePreviewImage.Source = item.Preview;
    }

    private void ResetDashboardLayout_Click(object sender, RoutedEventArgs e)
    {
        _services.Settings.Value.DashboardBlockSlots.Clear();
        _services.Settings.Value.DashboardLayoutVersion = 0;
        _services.Save();
        StatusText.Text = "Стандартний макет буде відновлено після повторного відкриття головного вікна.";
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
            _services.Logger.Error("Відкриття YouTube Studio Live Dashboard", ex);
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
