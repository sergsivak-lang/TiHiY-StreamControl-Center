using System.Diagnostics;
using TiHiY.StreamControlCenter.Services;

namespace TiHiY.StreamControlCenter.Windows;

public partial class OverlaySettingsWindow : ModuleWindowBase
{
    private readonly AppServices _services = App.Services;

    public OverlaySettingsWindow()
    {
        InitializeComponent();
        ConfigureModule(DesignSurface, 1160, 730, "OverlaySettings");
        LoadSettings();
        UpdateUrls();
    }

    private void LoadSettings()
    {
        PortBox.Text = _services.Settings.Value.OverlayPort.ToString();
        SetComboByContent(ThemeCombo, _services.Settings.Value.OverlayTheme);
        HighlightWordsBox.Text = _services.Settings.Value.HighlightWords;
        OwnerColorBox.Text = _services.Settings.Value.OwnerColor;
        ModeratorColorBox.Text = _services.Settings.Value.ModeratorColor;
        SubscriberColorBox.Text = _services.Settings.Value.SubscriberColor;
        VipColorBox.Text = _services.Settings.Value.VipColor;
        ViewerColorBox.Text = _services.Settings.Value.ViewerColor;
        BotColorBox.Text = _services.Settings.Value.BotColor;
        HighlightTextColorBox.Text = _services.Settings.Value.HighlightTextColor;
        HighlightBackgroundBox.Text = _services.Settings.Value.HighlightBackgroundColor;
        TwitchViewersBox.Text = _services.Settings.Value.TwitchViewers.ToString();
        YouTubeViewersBox.Text = _services.Settings.Value.YouTubeViewers.ToString();
        YouTubeLikesBox.Text = _services.Settings.Value.YouTubeLikes.ToString();
        LocalOverlayAutoStartCheck.IsChecked = _services.Settings.Value.LocalChatOverlayAutoStart;
        LocalOverlayClickThroughCheck.IsChecked = _services.Settings.Value.LocalChatOverlayClickThrough;
        LocalOverlayOpacitySlider.Value = _services.Settings.Value.LocalChatOverlayBackgroundOpacity;
        UpdateLocalOverlayOpacityText();
    }

    private void SaveToSettings()
    {
        _services.Settings.Value.OverlayPort = int.TryParse(PortBox.Text, out var port) ? Math.Clamp(port, 1025, 65535) : 17845;
        _services.Settings.Value.OverlayTheme = ComboText(ThemeCombo);
        _services.Settings.Value.HighlightWords = HighlightWordsBox.Text.Trim();
        _services.Settings.Value.OwnerColor = NormalizeColor(OwnerColorBox.Text, "#FFD329");
        _services.Settings.Value.ModeratorColor = NormalizeColor(ModeratorColorBox.Text, "#45B6FF");
        _services.Settings.Value.SubscriberColor = NormalizeColor(SubscriberColorBox.Text, "#22D878");
        _services.Settings.Value.VipColor = NormalizeColor(VipColorBox.Text, "#C77DFF");
        _services.Settings.Value.ViewerColor = NormalizeColor(ViewerColorBox.Text, "#EDF7FF");
        _services.Settings.Value.BotColor = NormalizeColor(BotColorBox.Text, "#95A4AE");
        _services.Settings.Value.HighlightTextColor = NormalizeColor(HighlightTextColorBox.Text, "#07131E");
        _services.Settings.Value.HighlightBackgroundColor = NormalizeColor(HighlightBackgroundBox.Text, "#FFD329");
        _services.Settings.Value.TwitchViewers = ParseInt(TwitchViewersBox.Text);
        _services.Settings.Value.YouTubeViewers = ParseInt(YouTubeViewersBox.Text);
        _services.Settings.Value.YouTubeLikes = ParseInt(YouTubeLikesBox.Text);
        _services.Settings.Value.LocalChatOverlayAutoStart = LocalOverlayAutoStartCheck.IsChecked == true;
        _services.Settings.Value.LocalChatOverlayClickThrough = LocalOverlayClickThroughCheck.IsChecked == true;
        _services.Settings.Value.LocalChatOverlayBackgroundOpacity = Math.Clamp(LocalOverlayOpacitySlider.Value, 0, 0.7);
        _services.Save();
    }

    private void UpdateUrls()
    {
        var port = _services.Overlay.IsRunning ? _services.Overlay.Port : _services.Settings.Value.OverlayPort;
        var theme = Uri.EscapeDataString(ComboText(ThemeCombo));
        ChatUrlBox.Text = $"http://127.0.0.1:{port}/overlay/chat?theme={theme}";
        AlertsUrlBox.Text = $"http://127.0.0.1:{port}/overlay/alerts?theme={theme}";
        NowPlayingUrlBox.Text = $"http://127.0.0.1:{port}/overlay/now-playing?theme={theme}";
    }

    private void ThemeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e) { if (IsLoaded) UpdateUrls(); }
    private async void RestartServer_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            SaveToSettings();
            await _services.Overlay.RestartAsync(_services.Settings.Value.OverlayPort);
            UpdateUrls();
            StatusText.Text = $"Overlay Server перезапущено на порту {_services.Settings.Value.OverlayPort}";
            _services.Logger.Info(StatusText.Text);
        }
        catch (Exception ex) { ShowError("Overlay Server", ex); }
    }
    private void Save_Click(object sender, RoutedEventArgs e) { SaveToSettings(); UpdateUrls(); StatusText.Text = "Оформлення чату та overlay збережено."; }
    private void ApplyStats_Click(object sender, RoutedEventArgs e) { SaveToSettings(); StatusText.Text = "Лічильники overlay оновлено."; }
    private void TestMention_Click(object sender, RoutedEventArgs e) => _services.Chat.AddIncoming("TWITCH", "TestViewer", "TiHiY-DED, тест кольору звернення", "Viewer");
    private void OpenChat_Click(object sender, RoutedEventArgs e) => OpenUrl(ChatUrlBox.Text);
    private void OpenNowPlaying_Click(object sender, RoutedEventArgs e) => OpenUrl(NowPlayingUrlBox.Text);
    private void OpenAlerts_Click(object sender, RoutedEventArgs e) => OpenUrl(AlertsUrlBox.Text);
    private void CopyChat_Click(object sender, RoutedEventArgs e) => Copy(ChatUrlBox.Text);
    private void CopyNowPlaying_Click(object sender, RoutedEventArgs e) => Copy(NowPlayingUrlBox.Text);
    private void CopyAlerts_Click(object sender, RoutedEventArgs e) => Copy(AlertsUrlBox.Text);
    private void OpenSettingsFolder_Click(object sender, RoutedEventArgs e) => OpenUrl(_services.SettingsService.Folder);

    private void OpenLocalOverlay_Click(object sender, RoutedEventArgs e)
    {
        SaveToSettings();
        _services.Save();
        _services.Windows.Show(() => new LocalChatOverlayWindow());
        _services.Windows.Get<LocalChatOverlayWindow>()?.ApplySettings();
        StatusText.Text = "Локальний чат поверх гри відкрито. Для переміщення вимкніть режим «крізь кліки».";
    }

    private void CloseLocalOverlay_Click(object sender, RoutedEventArgs e)
    {
        _services.Windows.Close<LocalChatOverlayWindow>();
        StatusText.Text = "Локальний чат поверх гри приховано. Автозапуск керується окремою галочкою.";
    }

    private void LocalOverlaySetting_Changed(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;
        SaveToSettings();
        _services.Windows.Get<LocalChatOverlayWindow>()?.ApplySettings();
    }

    private void LocalOverlayOpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (LocalOverlayOpacityText is null) return;
        UpdateLocalOverlayOpacityText();
        if (!IsLoaded) return;
        _services.Settings.Value.LocalChatOverlayBackgroundOpacity = Math.Clamp(e.NewValue, 0, 0.7);
        _services.Windows.Get<LocalChatOverlayWindow>()?.SetBackgroundOpacity(e.NewValue);
    }

    private void UpdateLocalOverlayOpacityText() => LocalOverlayOpacityText.Text = $"{LocalOverlayOpacitySlider.Value * 100:0}%";

    private void OpenUrl(string target)
    {
        try { Process.Start(new ProcessStartInfo(target) { UseShellExecute = true }); }
        catch (Exception ex) { ShowError("Відкриття", ex); }
    }
    private void Copy(string text)
    {
        try { Clipboard.SetText(text); StatusText.Text = "URL скопійовано."; }
        catch (Exception ex) { ShowError("Буфер обміну", ex); }
    }
    private void ShowError(string title, Exception ex)
    {
        _services.Logger.Error(title, ex);
        MessageBox.Show(this, ex.Message, title, MessageBoxButton.OK, MessageBoxImage.Warning);
    }
    private static string ComboText(ComboBox combo) => combo.SelectedItem is ComboBoxItem item ? item.Content?.ToString() ?? string.Empty : combo.Text;
    private static void SetComboByContent(ComboBox combo, string value)
    {
        foreach (var item in combo.Items.OfType<ComboBoxItem>()) if (string.Equals(item.Content?.ToString(), value, StringComparison.OrdinalIgnoreCase)) { combo.SelectedItem = item; return; }
        combo.SelectedIndex = 0;
    }
    private static int ParseInt(string text) => int.TryParse(text, out var value) ? Math.Clamp(value, 0, 100000000) : 0;
    private static string NormalizeColor(string value, string fallback)
    {
        var text = value.Trim();
        if (!text.StartsWith('#')) text = "#" + text;
        return text.Length is 7 or 9 && text.Skip(1).All(Uri.IsHexDigit) ? text.ToUpperInvariant() : fallback;
    }
    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => DragTitle(sender, e);
    private void Minimize_Click(object sender, RoutedEventArgs e) => MinimizeWindow(sender, e);
    private void Maximize_Click(object sender, RoutedEventArgs e) => MaximizeWindow(sender, e);
    private void Close_Click(object sender, RoutedEventArgs e) => CloseWindow(sender, e);
}
