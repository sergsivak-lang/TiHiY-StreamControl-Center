using System.Diagnostics;
using TiHiY.StreamControlCenter.Models;
using TiHiY.StreamControlCenter.Services;

namespace TiHiY.StreamControlCenter.Windows;

public partial class YouTubeStreamSettingsWindow : ModuleWindowBase
{
    private readonly AppServices _services = App.Services;
    private readonly ObservableCollection<YouTubeBroadcastSettings> _broadcasts = new();

    public YouTubeStreamSettingsWindow()
    {
        InitializeComponent();
        ConfigureModule(DesignSurface, 1030, 730, "YouTubeStreamSettings");
        BroadcastCombo.ItemsSource = _broadcasts;
        Loaded += Window_Loaded;
    }


    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        UpdateAuthorizationUi();
        if (_services.YouTube.IsAuthorized) await RefreshAsync();
        else StatusText.Text = "YouTube не авторизовано. Натисніть «ПІДКЛЮЧИТИ / АВТОРИЗУВАТИ».";
    }

    private void UpdateAuthorizationUi()
    {
        var enabled = _services.YouTube.IsAuthorized;
        BroadcastCombo.IsEnabled = enabled;
        TitleBox.IsEnabled = enabled;
        DescriptionBox.IsEnabled = enabled;
        PrivacyCombo.IsEnabled = enabled;
        ScheduledStartBox.IsEnabled = enabled;
    }

    private async Task RefreshAsync()
    {
        if (!_services.YouTube.IsAuthorized)
        {
            UpdateAuthorizationUi();
            StatusText.Text = "YouTube не авторизовано. Відкрийте модуль «Канали».";
            return;
        }
        try
        {
            StatusText.Text = "Отримання трансляцій YouTube...";
            var items = await _services.YouTube.GetBroadcastsAsync();
            _broadcasts.Clear();
            foreach (var item in items) _broadcasts.Add(item);
            BroadcastCombo.SelectedIndex = _broadcasts.Count > 0 ? 0 : -1;
            StatusText.Text = _broadcasts.Count > 0 ? $"Знайдено трансляцій: {_broadcasts.Count}" : "Активних або запланованих трансляцій не знайдено.";
        }
        catch (Exception ex) { ShowError("YouTube трансляції", ex); }
    }

    private void BroadcastCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (BroadcastCombo.SelectedItem is not YouTubeBroadcastSettings item) return;
        TitleBox.Text = item.Title;
        DescriptionBox.Text = item.Description;
        ScheduledStartBox.Text = item.ScheduledStartTime.ToString("yyyy-MM-dd HH:mm");
        LifeCycleText.Text = item.LifeCycleStatus.ToUpperInvariant();
        StreamUrlBox.Text = $"https://www.youtube.com/watch?v={item.Id}";
        PrivacyCombo.SelectedIndex = item.PrivacyStatus switch { "public" => 0, "unlisted" => 1, _ => 2 };
    }

    private async void RefreshBroadcasts_Click(object sender, RoutedEventArgs e) => await RefreshAsync();

    private void OpenChannels_Click(object sender, RoutedEventArgs e)
    {
        _services.Windows.Show(() => new ChannelConnectionsWindow(), this);
        StatusText.Text = "Після OAuth-авторизації поверніться сюди та натисніть «ОНОВИТИ СПИСОК».";
    }

    private async void SaveBroadcast_Click(object sender, RoutedEventArgs e)
    {
        if (BroadcastCombo.SelectedItem is not YouTubeBroadcastSettings item) { MessageBox.Show(this, "Оберіть трансляцію.", "YouTube", MessageBoxButton.OK, MessageBoxImage.Information); return; }
        try
        {
            if (!DateTime.TryParse(ScheduledStartBox.Text, out var scheduled)) throw new InvalidOperationException("Невірний формат дати. Використовуйте РРРР-ММ-ДД ГГ:ХХ.");
            item.Title = TitleBox.Text.Trim();
            item.Description = DescriptionBox.Text;
            item.ScheduledStartTime = scheduled;
            item.PrivacyStatus = (PrivacyCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "private";
            await _services.YouTube.UpdateBroadcastAsync(item);
            StatusText.Text = "Налаштування трансляції збережено на YouTube.";
            await RefreshAsync();
        }
        catch (Exception ex) { ShowError("Збереження YouTube", ex); }
    }

    private void OpenYouTube_Click(object sender, RoutedEventArgs e)
    {
        const string studioUrl = "https://studio.youtube.com/channel/UC4-t_7-LD_E15LXazQmsq_g/livestreaming/dashboard";
        try { Process.Start(new ProcessStartInfo(studioUrl) { UseShellExecute = true }); }
        catch (Exception ex) { ShowError("Відкриття YouTube Studio", ex); }
    }

    private void ShowError(string title, Exception ex)
    {
        _services.Logger.Error(title, ex);
        var message = FriendlyYouTubeError(ex);
        StatusText.Text = message;
        MessageBox.Show(this, message, title, MessageBoxButton.OK, MessageBoxImage.Warning);
    }
    private static string FriendlyYouTubeError(Exception ex)
    {
        var message = ex.GetBaseException().Message;
        if (message.Contains("401", StringComparison.OrdinalIgnoreCase) || message.Contains("UNAUTHENTICATED", StringComparison.OrdinalIgnoreCase) || message.Contains("Login Required", StringComparison.OrdinalIgnoreCase))
            return "YouTube не авторизовано. Відкрийте «Канали» та повторіть OAuth-авторизацію.";
        return message.Length > 500 ? message[..500] + "…" : message;
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => DragTitle(sender, e);
    private void Minimize_Click(object sender, RoutedEventArgs e) => MinimizeWindow(sender, e);
    private void Maximize_Click(object sender, RoutedEventArgs e) => MaximizeWindow(sender, e);
    private void Close_Click(object sender, RoutedEventArgs e) => CloseWindow(sender, e);
}
