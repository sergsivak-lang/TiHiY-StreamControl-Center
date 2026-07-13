using System.Collections.ObjectModel;
using System.Diagnostics;
using TiHiY.StreamControlCenter.Services;

namespace TiHiY.StreamControlCenter.Windows;

public partial class StreamNotificationsWindow : ModuleWindowBase
{
    private readonly AppServices _services = App.Services;
    private readonly ObservableCollection<string> _logLines = new();

    public StreamNotificationsWindow()
    {
        InitializeComponent();
        ConfigureModule(DesignSurface, 1160, 700, "StreamNotifications");
        BotLogList.ItemsSource = _logLines;
        LoadValues();
        _services.Notifications.StatusChanged += Notifications_StatusChanged;
        _services.Notifications.Log += Notifications_Log;
        _services.Twitch.StatusChanged += Channel_StatusChanged;
        _services.YouTube.StatusChanged += Channel_StatusChanged;
        Closed += Window_Closed;
        RefreshState();
        AddLog("Модуль інтегрованого TiHiY Stream Notify Bot відкрито.");
    }

    private void LoadValues()
    {
        var settings = _services.Settings.Value;
        ApplicationIdBox.Text = settings.DiscordApplicationId;
        ChannelIdsBox.Text = settings.DiscordChannelIds;
        MentionBox.Text = settings.DiscordMention;
        EnabledCheck.IsChecked = settings.DiscordNotificationsEnabled;
        AutoStartCheck.IsChecked = settings.NotificationBotAutoStart;
        TwitchCheck.IsChecked = settings.DiscordNotifyTwitch;
        YouTubeCheck.IsChecked = settings.DiscordNotifyYouTube;
        TemplateBox.Text = settings.DiscordMessageTemplate;
        MonetizationChannelIdsBox.Text = settings.DiscordMonetizationChannelIds;
        MonetizationMentionBox.Text = settings.DiscordMonetizationMention;
        MonetizationEnabledCheck.IsChecked = settings.DiscordMonetizationEnabled;
        DonatelloMoneyCheck.IsChecked = settings.DiscordNotifyDonatelloMonetization;
        TwitchMoneyCheck.IsChecked = settings.DiscordNotifyTwitchMonetization;
        YouTubeMoneyCheck.IsChecked = settings.DiscordNotifyYouTubeMonetization;
        ConfigurationStateText.Text = _services.Discord.HasBotToken
            ? "Discord Bot Token збережено у Windows Credential Manager."
            : "Discord Bot Token ще не збережено.";
    }

    private void SaveSettings()
    {
        var settings = _services.Settings.Value;
        settings.DiscordApplicationId = ApplicationIdBox.Text.Trim();
        settings.DiscordChannelIds = ChannelIdsBox.Text.Trim();
        settings.DiscordMention = MentionBox.Text.Trim();
        settings.DiscordNotificationsEnabled = EnabledCheck.IsChecked == true;
        settings.NotificationBotAutoStart = AutoStartCheck.IsChecked == true;
        settings.DiscordNotifyTwitch = TwitchCheck.IsChecked == true;
        settings.DiscordNotifyYouTube = YouTubeCheck.IsChecked == true;
        settings.DiscordMessageTemplate = string.IsNullOrWhiteSpace(TemplateBox.Text)
            ? "🔴 {platform}: трансляція почалася!\n{title}\n{url}"
            : TemplateBox.Text.Trim();
        settings.DiscordMonetizationChannelIds = MonetizationChannelIdsBox.Text.Trim();
        settings.DiscordMonetizationMention = MonetizationMentionBox.Text.Trim();
        settings.DiscordMonetizationEnabled = MonetizationEnabledCheck.IsChecked == true;
        settings.DiscordNotifyDonatelloMonetization = DonatelloMoneyCheck.IsChecked == true;
        settings.DiscordNotifyTwitchMonetization = TwitchMoneyCheck.IsChecked == true;
        settings.DiscordNotifyYouTubeMonetization = YouTubeMoneyCheck.IsChecked == true;

        if (!string.IsNullOrWhiteSpace(BotTokenBox.Password))
        {
            _services.Discord.SaveBotToken(BotTokenBox.Password);
            BotTokenBox.Password = string.Empty;
        }

        _services.Save();
        ConfigurationStateText.Text = _services.Discord.HasBotToken
            ? "Налаштування збережено. Токен захищено Windows Credential Manager."
            : "Налаштування збережено, але Bot Token відсутній.";
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            SaveSettings();
            LastActionText.Text = "Налаштування бота збережено.";
            AddLog("Налаштування збережено.");
        }
        catch (Exception ex) { ShowError("Збереження налаштувань", ex); }
    }

    private async void StartBot_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            SaveSettings();
            await _services.Notifications.StartAsync();
            LastActionText.Text = "Бот запущено. Він стежить за реальними станами Twitch і YouTube.";
        }
        catch (Exception ex) { ShowError("Запуск бота", ex); }
    }

    private async void StopBot_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await _services.Notifications.StopAsync();
            LastActionText.Text = "Бот зупинено. Канали та чат можуть продовжувати працювати.";
        }
        catch (Exception ex) { ShowError("Зупинка бота", ex); }
    }

    private async void CheckNow_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            SaveSettings();
            if (!_services.Notifications.IsRunning) await _services.Notifications.StartAsync();
            else await _services.Notifications.CheckNowAsync();
            LastActionText.Text = "Перевірку каналів виконано.";
        }
        catch (Exception ex) { ShowError("Перевірка трансляцій", ex); }
    }

    private async void TestDiscord_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            SaveSettings();
            await _services.Notifications.SendTestAsync();
            LastActionText.Text = "Тестове повідомлення надіслано у вибрані Discord-канали.";
        }
        catch (Exception ex) { ShowError("Тест Discord", ex); }
    }

    private async void TestMonetization_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            SaveSettings();
            await _services.Discord.TestMonetizationAsync();
            LastActionText.Text = "Тестове повідомлення надіслано в окремий канал донатів і платних підписок.";
            AddLog("Тест каналу монетизації надіслано.");
        }
        catch (Exception ex) { ShowError("Тест каналу донатів", ex); }
    }

    private void ForgetToken_Click(object sender, RoutedEventArgs e)
    {
        _services.Discord.ForgetBotToken();
        BotTokenBox.Password = string.Empty;
        ConfigurationStateText.Text = "Discord Bot Token видалено з Windows Credential Manager.";
        AddLog("Discord Bot Token видалено.");
    }

    private void OpenDiscordPortal_Click(object sender, RoutedEventArgs e) =>
        Process.Start(new ProcessStartInfo("https://discord.com/developers/applications") { UseShellExecute = true });

    private void OpenDiscordInvite_Click(object sender, RoutedEventArgs e)
    {
        SaveSettings();
        var appId = ApplicationIdBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(appId))
        {
            MessageBox.Show(this, "Введіть Application ID створеного Discord-бота.", "Discord", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        var url = $"https://discord.com/oauth2/authorize?client_id={Uri.EscapeDataString(appId)}&scope=bot&permissions=274877975552";
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }

    private void OpenChannels_Click(object sender, RoutedEventArgs e) =>
        _services.Windows.Show(() => new ChannelConnectionsWindow(), this);

    private void Notifications_StatusChanged(object? sender, EventArgs e) => Dispatcher.BeginInvoke(new Action(RefreshState));
    private void Channel_StatusChanged(object? sender, EventArgs e) => Dispatcher.BeginInvoke(new Action(RefreshState));
    private void Notifications_Log(object? sender, string line) => Dispatcher.BeginInvoke(new Action(() => AddLog(line)));

    private void RefreshState()
    {
        var running = _services.Notifications.IsRunning;
        BotStatusText.Text = running ? "ПРАЦЮЄ" : "ЗУПИНЕНО";
        BotStatusText.Foreground = (Brush)FindResource(running ? "Green" : "Yellow");
        BotStatusDot.Fill = (Brush)FindResource(running ? "Green" : "Yellow");
        TwitchStateText.Text = _services.Twitch.IsChatConnected
            ? "  канал підключено"
            : $"  {_services.Twitch.Status.ToLowerInvariant()}";
        YouTubeStateText.Text = _services.YouTube.IsConnected
            ? "  канал підключено"
            : $"  {_services.YouTube.Status.ToLowerInvariant()}";
    }

    private void AddLog(string line)
    {
        _logLines.Add(line);
        while (_logLines.Count > 10) _logLines.RemoveAt(0);
    }

    private void ShowError(string title, Exception ex)
    {
        _services.Logger.Error(title, ex);
        var message = ex.GetBaseException().Message;
        LastActionText.Text = message;
        AddLog($"ПОМИЛКА: {message}");
        MessageBox.Show(this, message, title, MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    private void Window_Closed(object? sender, EventArgs e)
    {
        _services.Notifications.StatusChanged -= Notifications_StatusChanged;
        _services.Notifications.Log -= Notifications_Log;
        _services.Twitch.StatusChanged -= Channel_StatusChanged;
        _services.YouTube.StatusChanged -= Channel_StatusChanged;
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => DragTitle(sender, e);
    private void Minimize_Click(object sender, RoutedEventArgs e) => MinimizeWindow(sender, e);
    private void Maximize_Click(object sender, RoutedEventArgs e) => MaximizeWindow(sender, e);
    private void Close_Click(object sender, RoutedEventArgs e) => CloseWindow(sender, e);
}
