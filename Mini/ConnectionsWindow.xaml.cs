namespace TiHiY.StreamControlMini;

public partial class ConnectionsWindow : Window
{
    private readonly MiniServices _services = App.Services;

    public ConnectionsWindow()
    {
        InitializeComponent();
        TwitchChannelBox.Text = _services.Settings.Value.TwitchChannelName;
        TwitchClientIdBox.Text = _services.Settings.Value.TwitchClientId;
        YouTubeClientIdBox.Text = _services.Settings.Value.YouTubeClientId;
        UpdateStatus();
    }

    private void SaveVisibleSettings()
    {
        _services.Settings.Value.TwitchChannelName = TwitchChannelBox.Text.Trim();
        _services.Settings.Value.TwitchClientId = TwitchClientIdBox.Text.Trim();
        _services.Settings.Value.YouTubeClientId = YouTubeClientIdBox.Text.Trim();
        _services.Save();
    }

    private async void TwitchConnect_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            SaveVisibleSettings();
            StatusText.Text = "Підключення Twitch…";
            await _services.Twitch.ConnectAsync();
            _services.Settings.Value.TwitchAutoConnect = true;
            _services.Save();
        }
        catch (Exception ex) { ShowError("Twitch", ex); }
        UpdateStatus();
    }

    private async void TwitchAuthorize_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            SaveVisibleSettings();
            var secret = TwitchSecretBox.Password.Trim();
            if (string.IsNullOrWhiteSpace(secret)) secret = _services.Credentials.LoadSecret("TWITCH_CLIENT_SECRET");
            StatusText.Text = "Авторизація Twitch відкривається у браузері…";
            await _services.Twitch.AuthorizeAsync(TwitchClientIdBox.Text.Trim(), secret);
            _services.Settings.Value.TwitchAutoConnect = true;
            _services.Save();
            TwitchSecretBox.Clear();
        }
        catch (Exception ex) { ShowError("Twitch OAuth", ex); }
        UpdateStatus();
    }

    private async void TwitchForget_Click(object sender, RoutedEventArgs e)
    {
        try { await _services.Twitch.DisconnectAsync(); } catch { }
        _services.Twitch.ForgetAuthorization();
        UpdateStatus();
    }

    private async void YouTubeConnect_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            SaveVisibleSettings();
            StatusText.Text = "Підключення YouTube…";
            await _services.YouTube.ConnectAsync();
            _services.Settings.Value.YouTubeAutoConnect = true;
            _services.Save();
        }
        catch (Exception ex) { ShowError("YouTube", ex); }
        UpdateStatus();
    }

    private async void YouTubeAuthorize_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            SaveVisibleSettings();
            var secret = YouTubeSecretBox.Password.Trim();
            if (string.IsNullOrWhiteSpace(secret)) secret = _services.Credentials.LoadSecret("YOUTUBE_CLIENT_SECRET");
            StatusText.Text = "Авторизація YouTube відкривається у браузері…";
            await _services.YouTube.AuthorizeAsync(YouTubeClientIdBox.Text.Trim(), secret);
            _services.Settings.Value.YouTubeAutoConnect = true;
            _services.Save();
            YouTubeSecretBox.Clear();
        }
        catch (Exception ex) { ShowError("YouTube OAuth", ex); }
        UpdateStatus();
    }

    private async void YouTubeForget_Click(object sender, RoutedEventArgs e)
    {
        try { await _services.YouTube.DisconnectAsync(); } catch { }
        _services.YouTube.ForgetAuthorization();
        UpdateStatus();
    }

    private async void DonatelloConnect_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var token = DonatelloTokenBox.Password.Trim();
            if (!string.IsNullOrWhiteSpace(token)) _services.Donatello.SaveApiToken(token);
            StatusText.Text = "Підключення Donatello…";
            await _services.Donatello.StartAsync();
            _services.Donations.ExternalTotalAmount = _services.Donatello.ProfileTotalAmount;
            _services.Settings.Value.DonatelloEnabled = true;
            _services.Settings.Value.DonatelloAutoStart = true;
            _services.Save();
            DonatelloTokenBox.Clear();
        }
        catch (Exception ex) { ShowError("Donatello", ex); }
        UpdateStatus();
    }

    private async void DonatelloCheck_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var token = DonatelloTokenBox.Password.Trim();
            if (!string.IsNullOrWhiteSpace(token)) _services.Donatello.SaveApiToken(token);
            StatusText.Text = "Перевірка Donatello API…";
            await _services.Donatello.TestConnectionAsync();
            _services.Donations.ExternalTotalAmount = _services.Donatello.ProfileTotalAmount;
            DonatelloTokenBox.Clear();
        }
        catch (Exception ex) { ShowError("Donatello API", ex); }
        UpdateStatus();
    }

    private async void DonatelloForget_Click(object sender, RoutedEventArgs e)
    {
        try { await _services.Donatello.StopAsync(); } catch { }
        _services.Donatello.ForgetApiToken();
        _services.Settings.Value.DonatelloEnabled = false;
        _services.Settings.Value.DonatelloAutoStart = false;
        _services.Save();
        UpdateStatus();
    }

    private void UpdateStatus()
    {
        StatusText.Text = $"Twitch: {_services.Twitch.Status}  •  YouTube: {_services.YouTube.Status}  •  Donatello: {_services.Donatello.Status}";
    }

    private void ShowError(string service, Exception ex)
    {
        var message = ex.GetBaseException().Message;
        StatusText.Text = $"{service}: {message}";
        _services.Logger.Error(service, ex);
        MessageBox.Show(this, message, service, MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
