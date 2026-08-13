using TiHiY.StreamControlCenter.Services;
namespace TiHiY.StreamControlCenter.Windows;
public partial class TwitchModesWindow : Window
{
    private readonly AppServices _services = App.Services;
    private static readonly string[] Modes = { "Twitch HD 1080p60", "Twitch 2K 1440p60", "Twitch 2K + Vertical 1080×1920", "Twitch Enhanced Broadcasting" };
    public TwitchModesWindow()
    {
        InitializeComponent();
        var s = _services.Settings.Value;
        AutoApplyBox.IsChecked = s.TwitchAutoApplyModeBeforeStart;
        Profile1080Box.Text = s.TwitchMode1080p60Profile;
        Profile1440Box.Text = s.TwitchMode1440p60Profile;
        ProfileVerticalBox.Text = s.TwitchMode1440VerticalProfile;
        ProfileEnhancedBox.Text = s.TwitchModeEnhancedProfile;
        var i = Array.IndexOf(Modes, s.TwitchLastMode); ModeBox.SelectedIndex = i >= 0 ? i : 0;
        Loaded += async (_, _) => await CheckAsync();
    }
    private void SaveSettings()
    {
        var s = _services.Settings.Value;
        s.TwitchAutoApplyModeBeforeStart = AutoApplyBox.IsChecked == true;
        s.TwitchMode1080p60Profile = Profile1080Box.Text.Trim();
        s.TwitchMode1440p60Profile = Profile1440Box.Text.Trim();
        s.TwitchMode1440VerticalProfile = ProfileVerticalBox.Text.Trim();
        s.TwitchModeEnhancedProfile = ProfileEnhancedBox.Text.Trim();
        s.TwitchLastMode = Modes[Math.Clamp(ModeBox.SelectedIndex, 0, Modes.Length - 1)];
        _services.Save();
    }
    private string SelectedMode => Modes[Math.Clamp(ModeBox.SelectedIndex, 0, Modes.Length - 1)];
    private string SelectedProfile => SelectedMode switch
    {
        "Twitch HD 1080p60" => Profile1080Box.Text.Trim(),
        "Twitch 2K 1440p60" => Profile1440Box.Text.Trim(),
        "Twitch 2K + Vertical 1080×1920" => ProfileVerticalBox.Text.Trim(),
        "Twitch Enhanced Broadcasting" => ProfileEnhancedBox.Text.Trim(), _ => string.Empty
    };
    private async void Apply_Click(object sender, RoutedEventArgs e) { try { SaveSettings(); await ApplyModeAsync(); } catch (Exception ex) { ShowError(ex); } }
    private async void StartTwitch_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            SaveSettings();
            if (!_services.Obs.IsConnected) throw new InvalidOperationException("OBS WebSocket не підключено. Спочатку підключіть OBS у головному вікні.");
            if (_services.Obs.IsStreaming) throw new InvalidOperationException("OBS вже транслює. Спочатку зупиніть поточний стрім.");
            if (AutoApplyBox.IsChecked == true) await ApplyModeAsync(); else await CheckAsync();
            if (SelectedMode.Contains("Vertical", StringComparison.OrdinalIgnoreCase) && !await _services.Obs.IsAitumVerticalInstalledAsync()) throw new InvalidOperationException("Aitum Vertical не знайдено. Стрім не запускаю.");
            await _services.Obs.StartStreamAsync();
            ActionLogText.Text = $"START TWITCH виконано • {SelectedMode}";
        }
        catch (Exception ex) { ShowError(ex); }
    }
    private async Task ApplyModeAsync()
    {
        if (!_services.Obs.IsConnected) throw new InvalidOperationException("OBS WebSocket не підключено.");
        var profile = SelectedProfile;
        if (string.IsNullOrWhiteSpace(profile)) throw new InvalidOperationException("Для режиму не задано OBS Profile.");
        var profiles = await _services.Obs.GetProfilesAsync();
        var match = profiles.FirstOrDefault(x => string.Equals(x, profile, StringComparison.OrdinalIgnoreCase));
        if (match is null) throw new InvalidOperationException($"OBS Profile «{profile}» не знайдено. Доступні: {string.Join(", ", profiles)}");
        await _services.Obs.SetCurrentProfileAsync(match); await Task.Delay(500); SaveSettings();
        var v = await _services.Obs.GetVideoSettingsAsync();
        VideoStatusText.Text = $"Video: {v.baseWidth}×{v.baseHeight} → {v.outputWidth}×{v.outputHeight} • FPS {v.fpsNumerator}/{v.fpsDenominator}";
        ActionLogText.Text = $"Застосовано OBS Profile: {match}";
        await CheckAsync();
    }
    private async void Check_Click(object sender, RoutedEventArgs e) { try { await CheckAsync(); } catch (Exception ex) { ShowError(ex); } }
    private async Task CheckAsync()
    {
        if (!_services.Obs.IsConnected) { ObsStatusText.Text = "OBS: НЕ ПІДКЛЮЧЕНО"; ProfileStatusText.Text = "Profiles: недоступні без OBS WebSocket."; return; }
        ObsStatusText.Text = _services.Obs.IsStreaming ? "OBS: ПІДКЛЮЧЕНО • STREAMING" : "OBS: ПІДКЛЮЧЕНО • READY";
        var p = await _services.Obs.GetProfilesAsync(); var current = await _services.Obs.GetCurrentProfileAsync();
        ProfileStatusText.Text = $"Profiles: {p.Count} • поточний: {current}\n1080p60: {Exists(Profile1080Box.Text,p)} • 1440p60: {Exists(Profile1440Box.Text,p)}\n1440 + Vertical: {Exists(ProfileVerticalBox.Text,p)} • Enhanced: {Exists(ProfileEnhancedBox.Text,p)}";
        var v = await _services.Obs.GetVideoSettingsAsync(); VideoStatusText.Text = $"Video: {v.baseWidth}×{v.baseHeight} → {v.outputWidth}×{v.outputHeight} • FPS {v.fpsNumerator}/{v.fpsDenominator}";
        var a = await _services.Obs.IsAitumVerticalInstalledAsync(); AitumStatusText.Text = a ? "Aitum Vertical: ЗНАЙДЕНО" : "Aitum Vertical: НЕ ЗНАЙДЕНО";
        var eb = await _services.Obs.CheckEnhancedBroadcastingAsync(); EnhancedStatusText.Text = $"Enhanced Broadcasting: {(eb.twitchServiceDetected ? "Twitch-профіль знайдено" : "Twitch-профіль не знайдено")}\n{eb.explanation}";
    }
    private static string Exists(string n, IReadOnlyList<string> p) => p.Any(x => string.Equals(x,n.Trim(),StringComparison.OrdinalIgnoreCase)) ? "OK" : "НЕМАЄ";
    private void ShowError(Exception ex) { ActionLogText.Text = "ПОМИЛКА: " + ex.GetBaseException().Message; MessageBox.Show(this, ex.GetBaseException().Message, "Twitch Streaming Modes", MessageBoxButton.OK, MessageBoxImage.Warning); }
}
