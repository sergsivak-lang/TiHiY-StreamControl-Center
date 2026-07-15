using System.Diagnostics;
using TiHiY.StreamControlCenter.Services;

namespace TiHiY.StreamControlCenter.Windows;

public partial class SettingsWindow : ModuleWindowBase
{
    private readonly AppServices _services = App.Services;
    public ObservableCollection<string> VisibleLogs { get; } = new();

    public SettingsWindow()
    {
        InitializeComponent();
        DataContext = this;
        ConfigureModule(DesignSurface, 1140, 730, "Settings");
        ObsUrlBox.Text = _services.Settings.Value.ObsUrl;
        RememberPasswordCheck.IsChecked = _services.Settings.Value.RememberObsPassword;
        AutoConnectCheck.IsChecked = _services.Settings.Value.AutoConnectObs;
        AutoScaleCheck.IsChecked = _services.UiScale.Auto;
        ScaleText.Text = $"{_services.UiScale.Percent}%";
        foreach (var theme in _services.Theme.Themes) ThemeCombo.Items.Add(theme.Name);
        ThemeCombo.SelectedItem = _services.Theme.CurrentTheme;
        UpdateThemePreview();
        if (_services.Settings.Value.RememberObsPassword) ObsPasswordBox.Password = _services.Credentials.LoadPassword();
        _services.Logger.Entries.CollectionChanged += LoggerEntries_CollectionChanged;
        _services.Obs.ConnectionChanged += Obs_ConnectionChanged;
        Closed += (_, _) =>
        {
            _services.Logger.Entries.CollectionChanged -= LoggerEntries_CollectionChanged;
            _services.Obs.ConnectionChanged -= Obs_ConnectionChanged;
        };
        RefreshLogs();
        UpdateObsStatus(_services.Obs.IsConnected);
    }

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
    private void ScaleDown_Click(object sender, RoutedEventArgs e) { _services.UiScale.Decrease(); AutoScaleCheck.IsChecked = false; ScaleText.Text = $"{_services.UiScale.Percent}%"; _services.Save(); }
    private void ScaleUp_Click(object sender, RoutedEventArgs e) { _services.UiScale.Increase(); AutoScaleCheck.IsChecked = false; ScaleText.Text = $"{_services.UiScale.Percent}%"; _services.Save(); }

    private void ThemeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateThemePreview();
        if (!IsLoaded || ThemeCombo.SelectedItem is not string themeName) return;
        StatusText.Text = $"Вибрано попередній перегляд «{themeName}». Натисніть «ЗАСТОСУВАТИ ТЕМУ».";
    }

    private void ApplyTheme_Click(object sender, RoutedEventArgs e)
    {
        var themeName = ThemeCombo.SelectedItem?.ToString() ?? "TiHiY Default / Cyber Amber";
        _services.Theme.Apply(themeName, save: true);
        StatusText.Text = $"Тему «{themeName}» застосовано до головного вікна, відкритих модулів і overlay.";
    }

    private void RestoreDefaultTheme_Click(object sender, RoutedEventArgs e)
    {
        const string defaultTheme = "TiHiY Default / Cyber Amber";
        ThemeCombo.SelectedItem = defaultTheme;
        _services.Theme.Apply(defaultTheme, save: true);
        UpdateThemePreview();
        StatusText.Text = "Відновлено стандартну тему TiHiY Default / Cyber Amber.";
    }

    private void UpdateThemePreview()
    {
        var selected = ThemeCombo.SelectedItem?.ToString() ?? _services.Theme.CurrentTheme;
        var theme = _services.Theme.Themes.FirstOrDefault(x => x.Name == selected) ?? _services.Theme.Themes[0];
        ThemePreviewName.Text = theme.Name;
        ThemePreviewDescription.Text = theme.Description;
        ThemePreviewPrimary.Fill = new SolidColorBrush(theme.Palette.Cyan);
        ThemePreviewAccent.Fill = new SolidColorBrush(theme.Palette.Amber);
        ThemePreviewSuccess.Fill = new SolidColorBrush(theme.Palette.Green);
        ThemePreviewBorder.Background = new SolidColorBrush(theme.Palette.Panel);
        ThemePreviewBorder.BorderBrush = new SolidColorBrush(theme.Palette.Line);
        ThemePreviewName.Foreground = new SolidColorBrush(theme.Palette.Cyan);
        ThemePreviewDescription.Foreground = new SolidColorBrush(theme.Palette.Muted);

        var previewUri = _services.Theme.GetPreviewUri(theme.Name);
        if (previewUri is null)
        {
            ThemePreviewImage.Source = null;
            return;
        }

        try
        {
            var preview = new BitmapImage();
            preview.BeginInit();
            preview.UriSource = previewUri;
            preview.CacheOption = BitmapCacheOption.OnLoad;
            preview.EndInit();
            preview.Freeze();
            ThemePreviewImage.Source = preview;
        }
        catch (Exception ex)
        {
            ThemePreviewImage.Source = null;
            _services.Logger.Info($"Попередній перегляд теми «{theme.Name}» недоступний: {ex.GetBaseException().Message}");
        }
    }

    private void ResetDashboardLayout_Click(object sender, RoutedEventArgs e)
    {
        _services.Settings.Value.DashboardBlockSlots.Clear();
        _services.Settings.Value.DashboardLayoutVersion = 0;
        _services.Save();
        StatusText.Text = "Стандартний макет буде відновлено після повторного відкриття головного вікна.";
    }

    private void ClearLog_Click(object sender, RoutedEventArgs e) { _services.Logger.Entries.Clear(); RefreshLogs(); }
    private void OpenLogs_Click(object sender, RoutedEventArgs e) => OpenFolder(_services.Logger.Folder);
    private void OpenSettingsFolder_Click(object sender, RoutedEventArgs e) => OpenFolder(_services.SettingsService.Folder);
    private void OpenFolder(string path)
    {
        try { Directory.CreateDirectory(path); Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); }
        catch (Exception ex) { _services.Logger.Error("Відкриття папки", ex); }
    }
    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => DragTitle(sender, e);
    private void Minimize_Click(object sender, RoutedEventArgs e) => MinimizeWindow(sender, e);
    private void Maximize_Click(object sender, RoutedEventArgs e) => MaximizeWindow(sender, e);
    private void Close_Click(object sender, RoutedEventArgs e) => CloseWindow(sender, e);
}
