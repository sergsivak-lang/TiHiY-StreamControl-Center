using System.Diagnostics;
using System.Globalization;
using TiHiY.StreamControlCenter.Services;
using TiHiY.StreamControlCenter.Models;

namespace TiHiY.StreamControlCenter.Windows;

public partial class DonatelloWindow : ModuleWindowBase
{
    private readonly AppServices _services = App.Services;

    public ObservableCollection<DonationEvent> DonationHistory => _services.Donations.History;

    public DonatelloWindow()
    {
        InitializeComponent();
        DataContext = this;
        ConfigureModule(DesignSurface, 1100, 730, "Donatello");
        LoadValues();
        _services.Donatello.StatusChanged += Donatello_StatusChanged;
        _services.Donations.DonationAdded += Donations_DonationAdded;
        Closed += (_, _) =>
        {
            _services.Donatello.StatusChanged -= Donatello_StatusChanged;
            _services.Donations.DonationAdded -= Donations_DonationAdded;
        };
        RefreshStatus();
        RefreshProfile();
    }

    private void LoadValues()
    {
        var settings = _services.Settings.Value;
        PageUrlBox.Text = settings.DonatelloPageUrl;
        GoalTitleBox.Text = settings.DonationGoalTitle;
        PollSecondsBox.Text = settings.DonatelloPollSeconds.ToString(CultureInfo.InvariantCulture);
        GoalAmountBox.Text = settings.DonationGoalAmount.ToString("0.##", CultureInfo.InvariantCulture);
        MinimumOverlayBox.Text = settings.DonatelloMinimumOverlayAmount.ToString("0.##", CultureInfo.InvariantCulture);
        EnabledCheck.IsChecked = settings.DonatelloEnabled;
        AutoStartCheck.IsChecked = settings.DonatelloAutoStart;
        ShowInChatCheck.IsChecked = settings.DonatelloShowInChat;
        SendToPlatformChatsCheck.IsChecked = settings.DonatelloSendToPlatformChats;
        ShowOverlayCheck.IsChecked = settings.DonatelloShowOnOverlay;
        NotifyDiscordCheck.IsChecked = settings.DonatelloNotifyDiscord;
        UpdateTokenState();
    }

    private void SaveValues()
    {
        var settings = _services.Settings.Value;
        settings.DonatelloPageUrl = string.IsNullOrWhiteSpace(PageUrlBox.Text) ? "https://donatello.to/TiHiY-DED" : PageUrlBox.Text.Trim();
        settings.DonationGoalTitle = string.IsNullOrWhiteSpace(GoalTitleBox.Text) ? "Ціль збору" : GoalTitleBox.Text.Trim();
        settings.DonatelloPollSeconds = int.TryParse(PollSecondsBox.Text, out var seconds) ? Math.Clamp(seconds, 5, 120) : 8;
        settings.DonationGoalAmount = ParseDecimal(GoalAmountBox.Text, 5000m);
        settings.DonatelloMinimumOverlayAmount = ParseDecimal(MinimumOverlayBox.Text, 0m);
        settings.DonatelloEnabled = EnabledCheck.IsChecked == true;
        settings.DonatelloAutoStart = AutoStartCheck.IsChecked == true;
        settings.DonatelloShowInChat = ShowInChatCheck.IsChecked == true;
        settings.DonatelloSendToPlatformChats = SendToPlatformChatsCheck.IsChecked == true;
        settings.DonatelloShowOnOverlay = ShowOverlayCheck.IsChecked == true;
        settings.DonatelloNotifyDiscord = NotifyDiscordCheck.IsChecked == true;
        settings.DonationGoalCurrency = "UAH";
        _services.Donations.GoalAmount = settings.DonationGoalAmount;
        _services.Donations.GoalCurrency = settings.DonationGoalCurrency;
        if (!string.IsNullOrWhiteSpace(ApiTokenBox.Password))
        {
            _services.Donatello.SaveApiToken(ApiTokenBox.Password);
            ApiTokenBox.Password = string.Empty;
        }
        _services.Save();
        UpdateTokenState();
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            SaveValues();
            ResultText.Text = "Налаштування Donatello API збережено. Токен захищено Windows Credential Manager.";
        }
        catch (Exception ex) { ShowError("Збереження Donatello", ex); }
    }

    private async void Start_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            SaveValues();
            await _services.Donatello.StartAsync();
            RefreshProfile();
            ResultText.Text = "Donatello API підключено. Програма очікує нові донати та платні підписки.";
        }
        catch (Exception ex) { ShowError("Підключення Donatello", ex); }
    }

    private async void Stop_Click(object sender, RoutedEventArgs e)
    {
        await _services.Donatello.StopAsync();
        ResultText.Text = "Donatello API відключено.";
    }

    private async void TestConnection_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            SaveValues();
            await _services.Donatello.TestConnectionAsync();
            RefreshProfile();
            ResultText.Text = "API Token прийнятий. Профіль Donatello успішно отримано через /api/v1/me.";
        }
        catch (Exception ex) { ShowError("Перевірка Donatello API", ex); }
    }

    private async void SyncNow_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            SaveValues();
            if (!_services.Donatello.IsRunning) await _services.Donatello.StartAsync();
            else await _services.Donatello.CheckNowAsync();
            RefreshProfile();
            ResultText.Text = "Синхронізацію донатів і активних платних підписок виконано.";
        }
        catch (Exception ex) { ShowError("Синхронізація Donatello", ex); }
    }

    private async void ImportRecent_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            SaveValues();
            var count = await _services.Donatello.ImportRecentAsync();
            ResultText.Text = count > 0
                ? $"Імпортовано останніх Donatello-подій: {count}. Вони додані у панель донатів."
                : "Donatello API не повернув донатів для імпорту.";
        }
        catch (Exception ex) { ShowError("Імпорт Donatello", ex); }
    }

    private async void ForgetToken_Click(object sender, RoutedEventArgs e)
    {
        await _services.Donatello.StopAsync();
        _services.Donatello.ForgetApiToken();
        ApiTokenBox.Password = string.Empty;
        UpdateTokenState();
        ResultText.Text = "Donatello API Token видалено з Windows Credential Manager.";
    }

    private void OpenDonatello_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var url = string.IsNullOrWhiteSpace(PageUrlBox.Text) ? "https://donatello.to/TiHiY-DED" : PageUrlBox.Text.Trim();
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex) { ShowError("Donatello", ex); }
    }

    private void Donations_DonationAdded(object? sender, DonationEvent donation) => Dispatcher.BeginInvoke(new Action(() =>
    {
        DonationHistoryList.ScrollIntoView(donation);
        RefreshProfile();
    }));

    private void ReplaySelected_Click(object sender, RoutedEventArgs e)
    {
        if (DonationHistoryList.SelectedItem is not DonationEvent selected)
        {
            ResultText.Text = "Спочатку виберіть донат або платну підписку в історії.";
            return;
        }

        var replay = _services.Donations.ReplayForOverlay(selected);
        DonationHistoryList.ScrollIntoView(replay);
        ResultText.Text = $"Повтор alert запущено: {selected.User} — {selected.DisplayAmount}. У Discord і чати повтор не надсилається.";
    }

    private void Donatello_StatusChanged(object? sender, EventArgs e) => Dispatcher.BeginInvoke(new Action(() =>
    {
        RefreshStatus();
        RefreshProfile();
    }));

    private void RefreshStatus()
    {
        StatusText.Text = _services.Donatello.Status;
        var color = _services.Donatello.ConsecutiveErrors >= 3 ? "Red"
            : _services.Donatello.IsRunning || _services.Donatello.Status.Contains("API ПІДКЛЮЧЕНО", StringComparison.OrdinalIgnoreCase) ? "Green"
            : "Yellow";
        StatusText.Foreground = (Brush)FindResource(color);
        LastSyncText.Text = _services.Donatello.LastSyncAt is DateTime sync
            ? $"Остання успішна синхронізація: {sync:HH:mm:ss}"
            : "Остання успішна синхронізація: —";
        LastErrorText.Text = string.IsNullOrWhiteSpace(_services.Donatello.LastError)
            ? string.Empty
            : $"Остання помилка ({_services.Donatello.ConsecutiveErrors} поспіль): {_services.Donatello.LastError}";
    }

    private void RefreshProfile()
    {
        ProfileNameText.Text = Empty(_services.Donatello.ProfileNickname);
        ProfilePubIdText.Text = Empty(_services.Donatello.ProfilePubId);
        ProfilePageText.Text = Empty(_services.Donatello.ProfilePage);
        ProfileTotalText.Text = _services.Donatello.ProfileTotalAmount > 0 ? $"{_services.Donatello.ProfileTotalAmount:0.##} UAH" : "—";
        ProfileCountText.Text = _services.Donatello.ProfileTotalCount > 0 ? _services.Donatello.ProfileTotalCount.ToString(CultureInfo.InvariantCulture) : "—";
        SubscriberCountText.Text = _services.Donatello.ActiveSubscriberCount.ToString(CultureInfo.InvariantCulture);
        UpdateTokenState();
    }

    private void UpdateTokenState()
    {
        TokenStateText.Text = _services.Donatello.HasApiToken ? "● TOKEN ЗБЕРЕЖЕНО" : "TOKEN НЕ ЗБЕРЕЖЕНО";
        TokenStateText.Foreground = (Brush)FindResource(_services.Donatello.HasApiToken ? "Green" : "Yellow");
    }

    private static decimal ParseDecimal(string text, decimal fallback)
    {
        var normalized = text.Replace(',', '.');
        return decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out var value) ? Math.Max(0, value) : fallback;
    }

    private static string Empty(string value) => string.IsNullOrWhiteSpace(value) ? "—" : value;

    private void ShowError(string title, Exception ex)
    {
        _services.Logger.Error(title, ex);
        var message = ex.GetBaseException().Message;
        ResultText.Text = message;
        MessageBox.Show(this, message, title, MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => DragTitle(sender, e);
    private void Minimize_Click(object sender, RoutedEventArgs e) => MinimizeWindow(sender, e);
    private void Maximize_Click(object sender, RoutedEventArgs e) => MaximizeWindow(sender, e);
    private void Close_Click(object sender, RoutedEventArgs e) => CloseWindow(sender, e);
}
