using TiHiY.StreamControlCenter.Models;
using TiHiY.StreamControlCenter.Services;

namespace TiHiY.StreamControlMini;

public sealed class MiniServices : IAsyncDisposable
{
    private int _disposeState;

    public SettingsService SettingsService { get; } = new();
    public AppSettingsAccessor Settings { get; } = new();
    public AppLogger Logger { get; } = new();
    public CredentialService Credentials { get; } = new();
    public ChatService Chat { get; }
    public DonationService Donations { get; }
    public TwitchService Twitch { get; }
    public YouTubeService YouTube { get; }
    public DonatelloService Donatello { get; }

    public event EventHandler? StatusChanged;

    public MiniServices()
    {
        Settings.Value = SettingsService.Load();
        Settings.Value.AutoNoticesEnabled = false;

        Donations = new DonationService(SettingsService.Folder, Logger)
        {
            GoalAmount = Settings.Value.DonationGoalAmount,
            GoalCurrency = string.IsNullOrWhiteSpace(Settings.Value.DonationGoalCurrency) ? "UAH" : Settings.Value.DonationGoalCurrency
        };

        Chat = new ChatService(Settings, SettingsService, Logger);
        Twitch = new TwitchService(Settings, SettingsService, Credentials, Logger);
        YouTube = new YouTubeService(Settings, SettingsService, Credentials, Logger);
        Donatello = new DonatelloService(Settings, SettingsService, Credentials, Logger);

        Chat.MessageSender = SendChatAsync;
        Twitch.MessageReceived += ChannelMessageReceived;
        YouTube.MessageReceived += ChannelMessageReceived;
        Twitch.DonationReceived += ChannelDonationReceived;
        YouTube.DonationReceived += ChannelDonationReceived;
        Donatello.DonationReceived += ChannelDonationReceived;
        Twitch.StatusChanged += ChannelStatusChanged;
        YouTube.StatusChanged += ChannelStatusChanged;
        Donatello.StatusChanged += ChannelStatusChanged;
        Twitch.StatsChanged += (_, _) => RaiseStatusChanged();
        YouTube.StatsChanged += (_, _) => RaiseStatusChanged();
    }

    public async Task InitializeAsync()
    {
        Chat.Start();

        if (Settings.Value.TwitchAutoConnect && Twitch.IsAuthorized)
            _ = RunSafeAsync(() => Twitch.ConnectAsync(), "Twitch автопідключення");
        if (Settings.Value.YouTubeAutoConnect && YouTube.IsAuthorized)
            _ = RunSafeAsync(() => YouTube.ConnectAsync(), "YouTube автопідключення");
        if (Donatello.HasApiToken)
        {
            Settings.Value.DonatelloEnabled = true;
            _ = RunSafeAsync(() => Donatello.StartAsync(), "Donatello автопідключення");
        }

        SettingsService.Save(Settings.Value);
        await Task.CompletedTask;
    }

    private void ChannelMessageReceived(object? sender, ChatMessage message) =>
        Application.Current.Dispatcher.BeginInvoke(new Action(() => Chat.AddIncoming(message)));

    private void ChannelDonationReceived(object? sender, DonationEvent donation) =>
        Application.Current.Dispatcher.BeginInvoke(new Action(() =>
        {
            Donations.Add(donation);
            if (donation.IsHistorical) return;

            if (donation.Source.Contains("DONATELLO", StringComparison.OrdinalIgnoreCase) && Settings.Value.DonatelloShowInChat)
            {
                Chat.AddIncoming(new ChatMessage
                {
                    Platform = "DONATELLO",
                    User = donation.User,
                    Text = donation.Kind.Equals("SUBSCRIPTION", StringComparison.OrdinalIgnoreCase)
                        ? $"Платна підписка • {donation.Message}"
                        : $"{donation.DisplayAmount} • {donation.Message}",
                    Role = "Donor",
                    ExternalId = "money:" + donation.StableId,
                    Time = donation.Time
                });
            }
        }));

    private void ChannelStatusChanged(object? sender, EventArgs e) => RaiseStatusChanged();

    private void RaiseStatusChanged() =>
        Application.Current.Dispatcher.BeginInvoke(new Action(() => StatusChanged?.Invoke(this, EventArgs.Empty)));

    private async Task RunSafeAsync(Func<Task> action, string label)
    {
        try { await action().ConfigureAwait(false); }
        catch (Exception ex) { Logger.Error(label, ex); }
        RaiseStatusChanged();
    }

    public async Task SendChatAsync(string text, string target)
    {
        var errors = new List<string>();
        var sent = 0;
        var wantsTwitch = target.Contains("Twitch", StringComparison.OrdinalIgnoreCase);
        var wantsYouTube = target.Contains("YouTube", StringComparison.OrdinalIgnoreCase);
        var both = wantsTwitch && wantsYouTube;

        if (wantsTwitch && Twitch.IsChatConnected)
        {
            try { await Twitch.SendMessageAsync(text); sent++; }
            catch (Exception ex) { errors.Add("Twitch: " + ex.Message); }
        }
        else if (wantsTwitch && !both) errors.Add("Twitch: чат не підключено.");

        if (wantsYouTube && YouTube.HasLiveChat)
        {
            try { await YouTube.SendMessageAsync(text); sent++; }
            catch (Exception ex) { errors.Add("YouTube: " + ex.Message); }
        }
        else if (wantsYouTube && !both) errors.Add("YouTube: активний live chat не знайдено.");

        if (sent == 0 && errors.Count == 0) errors.Add("Немає підключеного чату для надсилання.");
        if (errors.Count > 0) throw new InvalidOperationException(string.Join(Environment.NewLine, errors));
    }

    public void Save()
    {
        Settings.Value.DonationGoalAmount = Donations.GoalAmount;
        Settings.Value.DonationGoalCurrency = Donations.GoalCurrency;
        SettingsService.Save(Settings.Value);
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposeState, 1) != 0) return;
        try { Chat.Stop(); } catch { }
        try { Save(); } catch { }
        try { await Donatello.DisposeAsync().ConfigureAwait(false); } catch { }
        try { await Twitch.DisposeAsync().ConfigureAwait(false); } catch { }
        try { await YouTube.DisposeAsync().ConfigureAwait(false); } catch { }
    }
}
