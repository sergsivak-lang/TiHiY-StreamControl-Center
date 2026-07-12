using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.Diagnostics;
using System.Windows.Interop;
using TiHiY.StreamControlCenter.Services;
using TiHiY.StreamControlCenter.Models;
using Microsoft.Win32;
using System.Text.Json;

namespace TiHiY.StreamControlCenter;

public partial class MainWindow : Window
{
    private const int HotkeyShowOverlay = 0x5401;
    private const int HotkeyClickThrough = 0x5402;
    private const int WmHotkey = 0x0312;
    private const uint ModControl = 0x0002, ModShift = 0x0004;
    [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint modifiers, uint key);
    [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
    private readonly DemoChatService _chat = new();
    private OverlayWindow? _overlay;
    private readonly DispatcherTimer _statsTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly Stopwatch _streamClock = new();
    private readonly Random _random = new();
    private readonly ObsWebSocketService _obs = new();
    private readonly BotService _bot = new();
    private readonly AppSettings _settings;
    private long _frames;
    private long _droppedFrames;
    private string _currentModule = "";

    public MainWindow()
    {
        InitializeComponent();
        _settings = AppSettingsService.Load();
        ObsUrlTextBox.Text = _settings.ObsUrl;
        DataContext = _chat;
        CommandsGrid.ItemsSource = _bot.Commands;
        _statsTimer.Tick += StatsTimer_Tick;
        SourceInitialized += MainWindow_SourceInitialized;
        Closed += MainWindow_Closed;
    }

    private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
    {
        for (var i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
            if (child is T found) return found;
            var nested = FindVisualChild<T>(child); if (nested != null) return nested;
        }
        return null;
    }

    private void ShowView(UIElement view) { HomeView.Visibility = Visibility.Collapsed; CommandsView.Visibility = Visibility.Collapsed; SettingsView.Visibility = Visibility.Collapsed; ModuleView.Visibility = Visibility.Collapsed; view.Visibility = Visibility.Visible; }
    private void Home_Click(object sender, RoutedEventArgs e) => ShowView(HomeView);
    private void Commands_Click(object sender, RoutedEventArgs e) => ShowView(CommandsView);
    private void Settings_Click(object sender, RoutedEventArgs e) => ShowView(SettingsView);
    private void AlertsMenu_Click(object sender, RoutedEventArgs e) => OpenModule("alerts", "АЛЕРТИ", "Налаштування звуку, тривалості та тестування сповіщень про підписки, фоловерів і донати.", "Гучність алертів", "ТЕСТОВИЙ АЛЕРТ");
    private void OverlayMenu_Click(object sender, RoutedEventArgs e) => OpenModule("overlay", "ІГРОВИЙ OVERLAY", "Прозорий об’єднаний чат поверх гри. Ctrl+Shift+C — показати, Ctrl+Shift+L — пропускання кліків.", "Прозорість overlay", "ВІДКРИТИ OVERLAY");
    private void ObsMenu_Click(object sender, RoutedEventArgs e) { ShowView(SettingsView); ObsUrlTextBox.Focus(); }
    private void DonationsMenu_Click(object sender, RoutedEventArgs e) => OpenModule("donations", "DONATELLO", "Алерти, озвучення та журнал донатів каналу donatello.to/TiHiY-DED.", "Гучність TTS", "ТЕСТОВИЙ ДОНАТ");
    private void OpenModule(string key, string title, string description, string sliderLabel, string testLabel)
    {
        _currentModule = key; ModuleTitleText.Text = title; ModuleDescriptionText.Text = description; ModuleSliderLabel.Text = sliderLabel; ModuleTestButton.Content = testLabel; ModuleStateText.Text = "● ГОТОВО";
        ModuleSlider.Value = key == "overlay" ? _settings.OverlayOpacity * 100 : 70; ShowView(ModuleView);
    }
    private void SaveModule_Click(object sender, RoutedEventArgs e)
    {
        if (_currentModule == "overlay")
        {
            EnsureOverlay(); _overlay!.SetOpacity(ModuleSlider.Value / 100d);
            AppSettingsService.Save(new AppSettings(ObsUrlTextBox.Text.Trim(), ModuleSlider.Value / 100d));
        }
        ModuleStateText.Text = ModuleEnabledCheck.IsChecked == true ? "● УВІМКНЕНО" : "● ВИМКНЕНО";
        ModuleStateText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(ModuleEnabledCheck.IsChecked == true ? "#22D878" : "#FF5960"));
        StatusText.Text = "НАЛАШТУВАННЯ МОДУЛЯ ЗБЕРЕЖЕНО";
    }
    private void TestModule_Click(object sender, RoutedEventArgs e)
    {
        switch (_currentModule)
        {
            case "overlay": ToggleOverlay(); break;
            case "donations": TestDonation_Click(sender, e); break;
            case "alerts": _chat.Add("Alert", "SYSTEM", "Тестовий алерт: новий підписник TiHiY-DED!", "#FFD329"); ShowView(HomeView); break;
        }
    }
    private void Overlay_Click(object sender, RoutedEventArgs e) => ToggleOverlay();
    private void Send_Click(object sender, RoutedEventArgs e) => SendMessage();
    private void MessageInput_KeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Enter) SendMessage(); }
    private void SendMessage()
    {
        var text = MessageInput.Text.Trim(); if (text.Length == 0) return;
        _chat.Add("Local", "TiHiY-DED", text, "#FFD329");
        var reply = _bot.TryExecute(text, "Local");
        if (!string.IsNullOrWhiteSpace(reply)) _chat.Add("Bot", "TiHiY-BOT", reply, "#22D878");
        MessageInput.Clear(); ChatList.ScrollIntoView(_chat.Messages[^1]);
    }

    private void AddCommand_Click(object sender, RoutedEventArgs e)
    {
        CommandsGrid.SelectedItem = null; CommandNameTextBox.Text = "!команда"; CommandReplyTextBox.Text = "Відповідь бота";
        CommandPlatformCombo.SelectedIndex = 0; CommandCooldownTextBox.Text = "10"; CommandEnabledCheck.IsChecked = true;
    }
    private void CommandsGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CommandsGrid.SelectedItem is not BotCommand command) return;
        CommandNameTextBox.Text = command.Name; CommandReplyTextBox.Text = command.Reply;
        CommandPlatformCombo.SelectedIndex = command.Platform == "Twitch" ? 1 : command.Platform == "YouTube" ? 2 : 0;
        CommandCooldownTextBox.Text = command.CooldownSeconds.ToString(); CommandEnabledCheck.IsChecked = command.Enabled;
    }
    private void SaveCommand_Click(object sender, RoutedEventArgs e)
    {
        var name = CommandNameTextBox.Text.Trim(); var reply = CommandReplyTextBox.Text.Trim();
        if (!name.StartsWith('!') || name.Contains(' ')) { MessageBox.Show("Команда має починатися з ! і не містити пробілів.", "Команди", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
        if (reply.Length == 0) { MessageBox.Show("Введи відповідь бота.", "Команди", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
        if (!int.TryParse(CommandCooldownTextBox.Text, out var cooldown) || cooldown < 0 || cooldown > 3600) { MessageBox.Show("Затримка має бути від 0 до 3600 секунд.", "Команди", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
        var platform = (CommandPlatformCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Twitch + YouTube";
        var command = CommandsGrid.SelectedItem as BotCommand;
        if (command is null)
        {
            if (_bot.Commands.Any(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase))) { MessageBox.Show("Така команда вже існує.", "Команди"); return; }
            command = new BotCommand(); _bot.Commands.Add(command); CommandsGrid.SelectedItem = command;
        }
        command.Name = name; command.Reply = reply; command.Platform = platform; command.CooldownSeconds = cooldown; command.Enabled = CommandEnabledCheck.IsChecked == true;
        _bot.Save(); CommandsGrid.Items.Refresh(); StatusText.Text = "КОМАНДУ ЗБЕРЕЖЕНО";
    }
    private void DeleteCommand_Click(object sender, RoutedEventArgs e)
    {
        if (CommandsGrid.SelectedItem is not BotCommand command) return;
        if (MessageBox.Show($"Видалити {command.Name}?", "Команди", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        _bot.Commands.Remove(command); _bot.Save(); AddCommand_Click(sender, e);
    }
    private void TestCommand_Click(object sender, RoutedEventArgs e)
    {
        var name = CommandNameTextBox.Text.Trim(); var command = _bot.Commands.FirstOrDefault(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        var reply = command is null ? CommandReplyTextBox.Text.Trim() : _bot.TryExecute(command.Name, "Local");
        _chat.Add("Bot", "TiHiY-BOT", string.IsNullOrWhiteSpace(reply) ? "Команда не відповіла." : reply, "#22D878"); ShowView(HomeView);
    }
    private void ImportCommands_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Title = "Імпорт команд", Filter = "JSON або CSV|*.json;*.csv|Усі файли|*.*" };
        if (dialog.ShowDialog() != true) return;
        try
        {
            var imported = dialog.FileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
                ? JsonSerializer.Deserialize<List<BotCommand>>(File.ReadAllText(dialog.FileName)) ?? []
                : File.ReadAllLines(dialog.FileName).Skip(1).Select(ParseCsvCommand).Where(x => x != null).Cast<BotCommand>().ToList();
            foreach (var item in imported)
                if (!_bot.Commands.Any(x => x.Name.Equals(item.Name, StringComparison.OrdinalIgnoreCase))) _bot.Commands.Add(item);
            _bot.Save(); CommandsGrid.Items.Refresh(); StatusText.Text = $"ІМПОРТОВАНО: {imported.Count}";
        }
        catch (Exception ex) { ShowError("Помилка імпорту команд", ex); }
    }
    private static BotCommand? ParseCsvCommand(string line)
    {
        var parts = line.Split(';');
        if (parts.Length < 2) return null;
        return new BotCommand { Name = parts[0].Trim(), Reply = parts[1].Trim(), Platform = parts.Length > 2 ? parts[2].Trim() : "Twitch + YouTube", CooldownSeconds = parts.Length > 3 && int.TryParse(parts[3], out var value) ? value : 10 };
    }
    private void TestDonation_Click(object sender, RoutedEventArgs e) { _chat.Add("Donatello", "Тест", "Тестовий донат — 100 UAH", "#22D878"); StatusText.Text = "Тестовий донат успішний"; }
    private async void StartStream_Click(object sender, RoutedEventArgs e)
    {
        if (_obs.IsConnected)
        {
            try { await _obs.StartStreamAsync(); }
            catch (Exception ex) { ShowError("Не вдалося запустити стрім", ex); return; }
        }
        _frames = 0; _droppedFrames = 0; _streamClock.Restart(); _statsTimer.Start();
        _chat.Events.Insert(0, $"{DateTime.Now:HH:mm:ss}  OBS  Команда запуску стріму (демо)");
        StatusText.Text = "LIVE • v0.4.0";
    }
    private async void StopStream_Click(object sender, RoutedEventArgs e)
    {
        if (_obs.IsConnected)
        {
            try { await _obs.StopStreamAsync(); }
            catch (Exception ex) { ShowError("Не вдалося завершити стрім", ex); return; }
        }
        _statsTimer.Stop(); _streamClock.Stop();
        _chat.Events.Insert(0, $"{DateTime.Now:HH:mm:ss}  OBS  Команда завершення стріму (демо)");
        StatusText.Text = "TSC v0.4.0";
    }
    private void TopmostCheck_Click(object sender, RoutedEventArgs e) => Topmost = TopmostCheck.IsChecked == true;
    private void ServiceStatus_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Content is not StackPanel row) return;
        var isOn = Equals(button.Tag, "ON");
        button.Tag = isOn ? "OFF" : "ON";
        var dot = row.Children.OfType<Ellipse>().FirstOrDefault();
        var state = row.Children.OfType<TextBlock>().LastOrDefault();
        if (dot != null) dot.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString(isOn ? "#FF3B43" : "#22D878"));
        if (state != null)
        {
            state.Text = isOn ? " OFF" : " ON";
            state.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(isOn ? "#FF5960" : "#22D878"));
        }
        var online = new[] { TwitchStatus, YouTubeStatus, ObsStatus, DonatelloStatus }.Count(x => Equals(x.Tag, "ON"));
        GlobalConnectionText.Text = online == 0 ? "●  OFFLINE     DEMO MODE" : $"●  ONLINE {online}/4     DEMO MODE";
        GlobalConnectionText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(online == 0 ? "#FF5960" : "#22D878"));
        _chat.Events.Insert(0, $"{DateTime.Now:HH:mm:ss}  SYSTEM  {row.Children.OfType<TextBlock>().First().Text}: {(isOn ? "OFF" : "ON")}");
    }

    private async void StatsTimer_Tick(object? sender, EventArgs e)
    {
        if (_obs.IsConnected)
        {
            try
            {
                var stats = await _obs.GetStatsAsync();
                _frames = stats.TotalFrames; _droppedFrames = stats.SkippedFrames;
                FpsText.Text = stats.Fps.ToString("0.0");
                StreamTimeText.Text = TimeSpan.FromMilliseconds(stats.OutputDurationMs).ToString(@"hh\:mm\:ss");
                var seconds = Math.Max(1, stats.OutputDurationMs / 1000d);
                BitrateText.Text = $"{stats.OutputBytes * 8d / seconds / 1000d:0} kb/s";
            }
            catch (Exception ex)
            {
                _statsTimer.Stop(); SetObsState(false, "З’єднання з OBS втрачено");
                _chat.Events.Insert(0, $"{DateTime.Now:HH:mm:ss}  OBS  {ex.Message}");
                return;
            }
        }
        _frames += 60;
        if (_random.NextDouble() < 0.04) _droppedFrames++;
        var droppedPercent = _frames == 0 ? 0 : _droppedFrames * 100d / _frames;
        var twitchLoss = Equals(TwitchStatus.Tag, "ON") ? Math.Round(_random.NextDouble() * 0.25, 1) : 0;
        var youtubeLoss = Equals(YouTubeStatus.Tag, "ON") ? Math.Round(_random.NextDouble() * 0.20, 1) : 0;
        if (!_obs.IsConnected)
        {
            var bitrate = 5950 + _random.Next(-120, 121);
            StreamTimeText.Text = _streamClock.Elapsed.ToString(@"hh\:mm\:ss");
            BitrateText.Text = $"{bitrate:N0} kb/s";
            FpsText.Text = "60";
        }
        DroppedFramesText.Text = $"{_droppedFrames} ({droppedPercent:0.0}%)";
        TwitchLossText.Text = $"{twitchLoss:0.0}%";
        YouTubeLossText.Text = $"{youtubeLoss:0.0}%";
        var worst = Math.Max(droppedPercent, Math.Max(twitchLoss, youtubeLoss));
        var color = worst < 1 ? "#22D878" : worst < 3 ? "#FFD329" : "#FF5960";
        QualityText.Text = worst < 1 ? "ВІДМІННА" : worst < 3 ? "ДОБРА" : "ПОГАНА";
        QualityText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
        DroppedFramesText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
        TwitchLossText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(twitchLoss < 1 ? "#22D878" : twitchLoss < 3 ? "#FFD329" : "#FF5960"));
        YouTubeLossText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(youtubeLoss < 1 ? "#22D878" : youtubeLoss < 3 ? "#FFD329" : "#FF5960"));
    }

    private async void ConnectObs_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            ObsConnectionMessage.Text = "Підключення…";
            await _obs.ConnectAsync(ObsUrlTextBox.Text.Trim(), ObsPasswordBox.Password);
            SetObsState(true, "OBS підключено");
            AppSettingsService.Save(new AppSettings(ObsUrlTextBox.Text.Trim(), _settings.OverlayOpacity));
            _chat.Events.Insert(0, $"{DateTime.Now:HH:mm:ss}  OBS  Підключено через WebSocket");
        }
        catch (Exception ex) { SetObsState(false, "OBS не підключено"); ShowError("Помилка підключення OBS", ex); }
    }

    private void SaveSettings_Click(object sender, RoutedEventArgs e)
    {
        AppSettingsService.Save(new AppSettings(ObsUrlTextBox.Text.Trim(), _settings.OverlayOpacity));
        StatusText.Text = "НАЛАШТУВАННЯ ЗБЕРЕЖЕНО";
    }

    private void SetObsState(bool on, string message)
    {
        ObsStatus.Tag = on ? "ON" : "OFF";
        ObsDot.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString(on ? "#22D878" : "#FF3B43"));
        ObsState.Text = on ? " ON" : " OFF";
        ObsState.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(on ? "#22D878" : "#FF5960"));
        ObsConnectionMessage.Text = message;
        ObsConnectionMessage.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(on ? "#22D878" : "#FF5960"));
    }

    private static void ShowError(string title, Exception ex) => MessageBox.Show(ex.Message, title, MessageBoxButton.OK, MessageBoxImage.Error);

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left) return;
        if (e.ClickCount == 2) ToggleMaximize(); else DragMove();
    }
    private void MinimizeWindow_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void MaximizeWindow_Click(object sender, RoutedEventArgs e) => ToggleMaximize();
    private void CloseWindow_Click(object sender, RoutedEventArgs e) => Close();
    private void ToggleMaximize()
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        MaximizeButton.Content = WindowState == WindowState.Maximized ? "❐" : "□";
    }

    private void MainWindow_SourceInitialized(object? sender, EventArgs e)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        HwndSource.FromHwnd(hwnd)?.AddHook(WindowMessageHook);
        var showOk = RegisterHotKey(hwnd, HotkeyShowOverlay, ModControl | ModShift, (uint)KeyInterop.VirtualKeyFromKey(Key.C));
        var clickOk = RegisterHotKey(hwnd, HotkeyClickThrough, ModControl | ModShift, (uint)KeyInterop.VirtualKeyFromKey(Key.L));
        if (!showOk || !clickOk) _chat.Events.Insert(0, $"{DateTime.Now:HH:mm:ss}  SYSTEM  Не вдалося зареєструвати одну з гарячих клавіш overlay");
    }

    private IntPtr WindowMessageHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg != WmHotkey) return IntPtr.Zero;
        if (wParam.ToInt32() == HotkeyShowOverlay) ToggleOverlay();
        else if (wParam.ToInt32() == HotkeyClickThrough)
        {
            EnsureOverlay(); _overlay!.ToggleClickThrough();
            StatusText.Text = _overlay.ClickThroughEnabled ? "OVERLAY: КЛІКИ ПРОХОДЯТЬ" : "OVERLAY: РЕДАГУВАННЯ";
        }
        handled = true; return IntPtr.Zero;
    }

    private void EnsureOverlay() => _overlay ??= new OverlayWindow(_chat.Messages, _settings.OverlayOpacity);
    private void ToggleOverlay()
    {
        EnsureOverlay();
        if (_overlay!.IsVisible) _overlay.Hide();
        else { _overlay.Show(); _overlay.Activate(); }
    }

    private async void MainWindow_Closed(object? sender, EventArgs e)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        UnregisterHotKey(hwnd, HotkeyShowOverlay); UnregisterHotKey(hwnd, HotkeyClickThrough);
        if (_overlay != null)
            AppSettingsService.Save(new AppSettings(ObsUrlTextBox.Text.Trim(), _overlay.OverlayOpacity));
        await _obs.DisposeAsync();
    }
}
