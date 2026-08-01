using TiHiY.StreamControlCenter.Models;

namespace TiHiY.StreamControlMini;

public partial class MainWindow : Window
{
    private readonly MiniServices _services = App.Services;
    private bool _closing;

    public ObservableCollection<ChatMessage> ChatMessages { get; } = new();
    public ObservableCollection<DonationEvent> Donations { get; } = new();
    public ObservableCollection<NotificationItem> Notifications { get; } = new();

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;

        _services.Chat.MessageAdded += Chat_MessageAdded;
        _services.Donations.DonationAdded += Donations_DonationAdded;
        _services.StatusChanged += Services_StatusChanged;
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        foreach (var message in _services.Chat.Messages.TakeLast(300)) ChatMessages.Add(message);
        RefreshDonations();
        UpdateStatus();
        AddNotification("SYSTEM", "StreamControl MINI готовий", "Мультичат, донати та сповіщення запущені.");
    }

    private void Chat_MessageAdded(object? sender, ChatMessage message) => Dispatcher.BeginInvoke(new Action(() =>
    {
        ChatMessages.Add(message);
        while (ChatMessages.Count > 300) ChatMessages.RemoveAt(0);
        ChatList.ScrollIntoView(message);

        var text = message.Text ?? string.Empty;
        if (message.IsHighlighted ||
            text.Contains("підпис", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("follow", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("member", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("super chat", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("bits", StringComparison.OrdinalIgnoreCase))
        {
            AddNotification(message.Platform, $"{message.User}", text);
        }
        UpdateStatus();
    }));

    private void Donations_DonationAdded(object? sender, DonationEvent donation) => Dispatcher.BeginInvoke(new Action(() =>
    {
        RefreshDonations();
        if (!donation.IsHistorical)
            AddNotification(donation.Source, $"{donation.KindLabel}: {donation.User}", donation.EventSummary);
    }));

    private void Services_StatusChanged(object? sender, EventArgs e) => Dispatcher.BeginInvoke(new Action(UpdateStatus));

    private void RefreshDonations()
    {
        Donations.Clear();
        foreach (var donation in _services.Donations.History.TakeLast(30).Reverse()) Donations.Add(donation);
        DonationTotalText.Text = $"Зібрано: {_services.Donations.TotalAmount:0.##} {_services.Donations.GoalCurrency}";
        DonationSummaryText.Text = $"{_services.Donatello.Status} • Super Chat • Bits";
    }

    private void AddNotification(string source, string title, string message)
    {
        var item = new NotificationItem { Source = source, Title = title, Message = message, Time = DateTime.Now };
        Notifications.Insert(0, item);
        while (Notifications.Count > 100) Notifications.RemoveAt(Notifications.Count - 1);
        NotificationList.ScrollIntoView(item);
    }

    private void UpdateStatus()
    {
        TwitchStatusText.Text = _services.Twitch.IsChatConnected
            ? (_services.Settings.Value.TwitchLive ? "TWITCH: LIVE" : "TWITCH: CHAT ON")
            : "TWITCH: " + _services.Twitch.Status;
        TwitchStatusText.Foreground = _services.Twitch.IsChatConnected ? (Brush)FindResource("Green") : (Brush)FindResource("Muted");

        YouTubeStatusText.Text = _services.YouTube.HasLiveChat
            ? (_services.Settings.Value.YouTubeLive ? "YOUTUBE: LIVE" : "YOUTUBE: CHAT ON")
            : "YOUTUBE: " + _services.YouTube.Status;
        YouTubeStatusText.Foreground = _services.YouTube.HasLiveChat ? (Brush)FindResource("Green") : (Brush)FindResource("Muted");

        DonatelloStatusText.Text = "DONATELLO: " + _services.Donatello.Status;
        DonatelloStatusText.Foreground = _services.Donatello.IsHealthy ? (Brush)FindResource("Green") : (Brush)FindResource("Muted");

        SendTwitchButton.IsEnabled = _services.Twitch.IsChatConnected;
        SendYouTubeButton.IsEnabled = _services.YouTube.HasLiveChat;
        SendBothButton.IsEnabled = _services.Twitch.IsChatConnected || _services.YouTube.HasLiveChat;
        ChatInput.IsEnabled = SendBothButton.IsEnabled;

        var twitch = _services.Twitch.IsChatConnected ? "Twitch підключено" : "Twitch off";
        var youtube = _services.YouTube.HasLiveChat ? "YouTube підключено" : "YouTube off";
        ChatStatusText.Text = $"{twitch} • {youtube} • {ChatMessages.Count} повідомлень";
        RefreshDonations();
    }

    private async Task SendChatAsync(string target)
    {
        var text = ChatInput.Text.Trim();
        if (string.IsNullOrWhiteSpace(text)) return;
        try
        {
            await _services.SendChatAsync(text, target);
            ChatInput.Clear();
            ChatInput.Focus();
        }
        catch (Exception ex)
        {
            AddNotification("SYSTEM", "Не вдалося надіслати повідомлення", ex.GetBaseException().Message);
        }
    }

    private async void ChatInput_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) return;
        e.Handled = true;
        await SendChatAsync("Twitch + YouTube");
    }

    private async void SendTwitch_Click(object sender, RoutedEventArgs e) => await SendChatAsync("Twitch");
    private async void SendYouTube_Click(object sender, RoutedEventArgs e) => await SendChatAsync("YouTube");
    private async void SendBoth_Click(object sender, RoutedEventArgs e) => await SendChatAsync("Twitch + YouTube");

    private void TestDonation_Click(object sender, RoutedEventArgs e) => _services.Donations.AddTestDonation();

    private void Connections_Click(object sender, RoutedEventArgs e)
    {
        var window = new ConnectionsWindow { Owner = this };
        window.ShowDialog();
        UpdateStatus();
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (_closing) return;
        _closing = true;
        _services.Chat.MessageAdded -= Chat_MessageAdded;
        _services.Donations.DonationAdded -= Donations_DonationAdded;
        _services.StatusChanged -= Services_StatusChanged;
        _services.Save();
    }
}
