using System.Diagnostics;
using TiHiY.StreamControlCenter.Services;

namespace TiHiY.StreamControlCenter.Windows;

public partial class ChannelConnectionsWindow : ModuleWindowBase
{
    private readonly AppServices _services = App.Services;

    public ChannelConnectionsWindow()
    {
        InitializeComponent();
        ConfigureModule(DesignSurface, 1260, 790, "Channels");
        LoadValues();
        _services.Twitch.StatusChanged += Twitch_StatusChanged;
        _services.YouTube.StatusChanged += YouTube_StatusChanged;
        Closed += (_, _) =>
        {
            _services.Twitch.StatusChanged -= Twitch_StatusChanged;
            _services.YouTube.StatusChanged -= YouTube_StatusChanged;
        };
        RefreshStatuses();
    }

    private void LoadValues()
    {
        var s = _services.Settings.Value;
        TwitchChannelBox.Text = s.TwitchChannelName;
        TwitchClientIdBox.Text = s.TwitchClientId;
        TwitchClientSecretBox.Password = _services.Credentials.LoadSecret("TWITCH_CLIENT_SECRET");
        TwitchAutoConnectCheck.IsChecked = s.TwitchAutoConnect;
        YouTubeChannelBox.Text = s.YouTubeChannelName;
        YouTubeClientIdBox.Text = s.YouTubeClientId;
        YouTubeClientSecretBox.Password = _services.Credentials.LoadSecret("YOUTUBE_CLIENT_SECRET");
        YouTubeAutoConnectCheck.IsChecked = s.YouTubeAutoConnect;
        DiscordApplicationIdBox.Text = s.DiscordApplicationId;
        DiscordTokenBox.Password = _services.Discord.LoadBotToken();
        DiscordChannelsBox.Text = s.DiscordChannelIds;
        DiscordEnabledCheck.IsChecked = s.DiscordNotificationsEnabled;
        DiscordTwitchCheck.IsChecked = s.DiscordNotifyTwitch;
        DiscordYouTubeCheck.IsChecked = s.DiscordNotifyYouTube;
        DiscordTemplateBox.Text = s.DiscordMessageTemplate;
    }

    private void SaveGeneral()
    {
        var s = _services.Settings.Value;
        s.TwitchChannelName = TwitchChannelBox.Text.Trim().TrimStart('#');
        s.TwitchClientId = TwitchClientIdBox.Text.Trim();
        s.TwitchAutoConnect = TwitchAutoConnectCheck.IsChecked == true;
        s.YouTubeChannelName = YouTubeChannelBox.Text.Trim();
        s.YouTubeClientId = YouTubeClientIdBox.Text.Trim();
        s.YouTubeAutoConnect = YouTubeAutoConnectCheck.IsChecked == true;
        s.DiscordApplicationId = DiscordApplicationIdBox.Text.Trim();
        s.DiscordChannelIds = DiscordChannelsBox.Text.Trim();
        s.DiscordNotificationsEnabled = DiscordEnabledCheck.IsChecked == true;
        s.DiscordNotifyTwitch = DiscordTwitchCheck.IsChecked == true;
        s.DiscordNotifyYouTube = DiscordYouTubeCheck.IsChecked == true;
        s.DiscordMessageTemplate = string.IsNullOrWhiteSpace(DiscordTemplateBox.Text) ? "🔴 {platform}: трансляція почалася!\n{title}\n{url}" : DiscordTemplateBox.Text;
        _services.Save();
    }

    private async void AuthorizeTwitch_Click(object sender, RoutedEventArgs e)
    {
        try { SaveGeneral(); await _services.Twitch.AuthorizeAsync(TwitchClientIdBox.Text, TwitchClientSecretBox.Password); FooterStatusText.Text = "Twitch авторизовано і підключено."; }
        catch (Exception ex) { ShowError("Twitch OAuth", ex); }
    }
    private async void ConnectTwitch_Click(object sender, RoutedEventArgs e)
    {
        try { SaveGeneral(); await _services.Twitch.ConnectAsync(); }
        catch (Exception ex) { ShowError("Twitch", ex); }
    }
    private async void DisconnectTwitch_Click(object sender, RoutedEventArgs e) => await _services.Twitch.DisconnectAsync();
    private async void ForgetTwitch_Click(object sender, RoutedEventArgs e)
    {
        await _services.Twitch.DisconnectAsync(); _services.Twitch.ForgetAuthorization(); TwitchClientSecretBox.Password = string.Empty;
    }

    private async void AuthorizeYouTube_Click(object sender, RoutedEventArgs e)
    {
        try { SaveGeneral(); await _services.YouTube.AuthorizeAsync(YouTubeClientIdBox.Text, YouTubeClientSecretBox.Password); FooterStatusText.Text = "YouTube авторизовано і підключено."; }
        catch (Exception ex) { ShowError("YouTube OAuth", ex); }
    }
    private async void ConnectYouTube_Click(object sender, RoutedEventArgs e)
    {
        try { SaveGeneral(); await _services.YouTube.ConnectAsync(); }
        catch (Exception ex) { ShowError("YouTube", ex); }
    }
    private async void DisconnectYouTube_Click(object sender, RoutedEventArgs e) => await _services.YouTube.DisconnectAsync();
    private async void ForgetYouTube_Click(object sender, RoutedEventArgs e)
    {
        await _services.YouTube.DisconnectAsync(); _services.YouTube.ForgetAuthorization(); YouTubeClientSecretBox.Password = string.Empty;
    }

    private void OpenDiscordPortal_Click(object sender, RoutedEventArgs e)
    {
        Process.Start(new ProcessStartInfo("https://discord.com/developers/applications") { UseShellExecute = true });
        DiscordStatusText.Text = "Створіть Application → Bot, потім скопіюйте Application ID і Bot Token.";
    }

    private void OpenDiscordInvite_Click(object sender, RoutedEventArgs e)
    {
        SaveGeneral();
        var appId = DiscordApplicationIdBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(appId))
        {
            MessageBox.Show(this, "Введіть Application ID Discord-бота.", "Discord", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        var url = $"https://discord.com/oauth2/authorize?client_id={Uri.EscapeDataString(appId)}&scope=bot&permissions=274877975552";
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }

    private void SaveDiscord_Click(object sender, RoutedEventArgs e)
    {
        try { SaveGeneral(); if (!string.IsNullOrWhiteSpace(DiscordTokenBox.Password)) _services.Discord.SaveBotToken(DiscordTokenBox.Password); DiscordStatusText.Text = "Discord налаштування збережено."; }
        catch (Exception ex) { ShowError("Discord", ex); }
    }
    private async void TestDiscord_Click(object sender, RoutedEventArgs e)
    {
        try { SaveGeneral(); if (!string.IsNullOrWhiteSpace(DiscordTokenBox.Password)) _services.Discord.SaveBotToken(DiscordTokenBox.Password); await _services.Discord.TestAsync(); DiscordStatusText.Text = "Тестове повідомлення надіслано в усі вибрані канали."; }
        catch (Exception ex) { ShowError("Discord тест", ex); }
    }
    private void ForgetDiscord_Click(object sender, RoutedEventArgs e) { _services.Discord.ForgetBotToken(); DiscordTokenBox.Password = string.Empty; DiscordStatusText.Text = "Токен Discord видалено."; }
    private void SaveGeneral_Click(object sender, RoutedEventArgs e) { SaveGeneral(); FooterStatusText.Text = "Налаштування збережено."; }

    private void Twitch_StatusChanged(object? sender, EventArgs e) => Dispatcher.BeginInvoke(new Action(RefreshStatuses));
    private void YouTube_StatusChanged(object? sender, EventArgs e) => Dispatcher.BeginInvoke(new Action(RefreshStatuses));
    private void RefreshStatuses()
    {
        TwitchStatusText.Text = $"Статус: {_services.Twitch.Status}\nКанал: {_services.Settings.Value.TwitchChannelName}";
        TwitchStatusText.Foreground = (Brush)FindResource(_services.Twitch.IsChatConnected ? "Green" : "Yellow");
        YouTubeStatusText.Text = $"Статус: {_services.YouTube.Status}\nКанал: {_services.Settings.Value.YouTubeChannelName}";
        YouTubeStatusText.Foreground = (Brush)FindResource(_services.YouTube.IsConnected ? "Green" : "Yellow");
    }

    private void ShowError(string title, Exception ex)
    {
        _services.Logger.Error(title, ex);
        var message = ex.GetBaseException().Message;
        if (title.Contains("YouTube", StringComparison.OrdinalIgnoreCase) && (message.Contains("401") || message.Contains("UNAUTHENTICATED") || message.Contains("Login Required")))
            message = "YouTube не авторизовано. Перевірте Client ID типу Desktop app і натисніть «АВТОРИЗУВАТИ».";
        MessageBox.Show(this, message.Length > 600 ? message[..600] + "…" : message, title, MessageBoxButton.OK, MessageBoxImage.Warning);
    }
    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => DragTitle(sender, e);
    private void Minimize_Click(object sender, RoutedEventArgs e) => MinimizeWindow(sender, e);
    private void Maximize_Click(object sender, RoutedEventArgs e) => MaximizeWindow(sender, e);
    private void Close_Click(object sender, RoutedEventArgs e) => CloseWindow(sender, e);
}
