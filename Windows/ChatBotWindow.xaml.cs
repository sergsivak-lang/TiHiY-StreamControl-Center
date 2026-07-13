using TiHiY.StreamControlCenter.Models;
using TiHiY.StreamControlCenter.Services;

namespace TiHiY.StreamControlCenter.Windows;

public partial class ChatBotWindow : ModuleWindowBase
{
    private readonly AppServices _services = App.Services;
    private ScheduledNotice? _selectedNotice;
    private BotCommand? _selectedCommand;
    private int _noticePageIndex;
    private int _commandPageIndex;

    public ObservableCollection<ChatMessage> VisibleMessages { get; } = new();
    public ObservableCollection<ScheduledNotice> NoticePage { get; } = new();
    public ObservableCollection<BotCommand> CommandPage { get; } = new();

    public ChatBotWindow()
    {
        InitializeComponent();
        DataContext = this;
        ConfigureModule(DesignSurface, 1280, 780, "ChatBot");
        AutoNoticesCheck.IsChecked = _services.Settings.Value.AutoNoticesEnabled;
        _services.Chat.MessageAdded += Chat_MessageAdded;
        Closed += (_, _) => _services.Chat.MessageAdded -= Chat_MessageAdded;
        RefreshVisibleMessages();
        ShowNoticePage();
        ShowCommandPage();
    }

    private void Chat_MessageAdded(object? sender, ChatMessage e) => Dispatcher.BeginInvoke(new Action(RefreshVisibleMessages));

    private void RefreshVisibleMessages()
    {
        VisibleMessages.Clear();
        foreach (var message in _services.Chat.Messages.TakeLast(300)) VisibleMessages.Add(message);
        if (VisibleMessages.Count > 0) Dispatcher.BeginInvoke(new Action(() => ChatMessagesList.ScrollIntoView(VisibleMessages[^1])), DispatcherPriority.Background);
    }

    private void ShowChat_Click(object sender, RoutedEventArgs e) { ChatPanel.Visibility = Visibility.Visible; NoticesPanel.Visibility = Visibility.Collapsed; CommandsPanel.Visibility = Visibility.Collapsed; }
    private void ShowNotices_Click(object sender, RoutedEventArgs e) { ChatPanel.Visibility = Visibility.Collapsed; NoticesPanel.Visibility = Visibility.Visible; CommandsPanel.Visibility = Visibility.Collapsed; }
    private void ShowCommands_Click(object sender, RoutedEventArgs e) { ChatPanel.Visibility = Visibility.Collapsed; NoticesPanel.Visibility = Visibility.Collapsed; CommandsPanel.Visibility = Visibility.Visible; }

    private void SendTwitch_Click(object sender, RoutedEventArgs e) => SendManual("Twitch");
    private void SendYouTube_Click(object sender, RoutedEventArgs e) => SendManual("YouTube");
    private void SendBoth_Click(object sender, RoutedEventArgs e) => SendManual("Twitch + YouTube");
    private void SendManual(string target)
    {
        var text = ManualMessageTextBox.Text.Trim();
        if (text.Length == 0) return;
        _services.Chat.SendManual(text, target);
        ManualMessageTextBox.Clear();
    }

    private void AddMention_Click(object sender, RoutedEventArgs e) => _services.Chat.AddIncoming("TWITCH", "TestViewer", "TiHiY-DED, перевірка виділення звернення!", "Viewer");
    private void AddSubscriber_Click(object sender, RoutedEventArgs e) => _services.Chat.AddIncoming("YOUTUBE", "MemberUA", "Повідомлення підписника", "Subscriber");
    private void AddModerator_Click(object sender, RoutedEventArgs e) => _services.Chat.AddIncoming("TWITCH", "ModeratorUA", "Повідомлення модератора", "Moderator");

    private async void MuteChatUser_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { Tag: ChatMessage message }) return;
        await RunModerationAsync(message, "мут", () => _services.ModerateChatUserAsync(message, false, 600));
    }

    private async void BanChatUser_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { Tag: ChatMessage message }) return;
        if (MessageBox.Show(this, $"Забанити {message.User} на {message.Platform}?", "Модерація чату", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        await RunModerationAsync(message, "бан", () => _services.ModerateChatUserAsync(message, true));
    }

    private async void DeleteChatMessage_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { Tag: ChatMessage message }) return;
        await RunModerationAsync(message, "видалення повідомлення", async () =>
        {
            await _services.DeleteChatMessageAsync(message);
            _services.Chat.Messages.Remove(message);
            VisibleMessages.Remove(message);
        });
    }

    private async Task RunModerationAsync(ChatMessage message, string action, Func<Task> operation)
    {
        try
        {
            await operation();
            _services.Logger.Info($"Чат {message.Platform}: {action} — {message.User}");
        }
        catch (Exception ex)
        {
            _services.Logger.Error($"Модерація {message.Platform}", ex);
            MessageBox.Show(this, ex.GetBaseException().Message + "\n\nДля Twitch після оновлення повторіть OAuth-авторизацію, щоб надати права модерації.", "Модерація чату", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private int NoticePageCount => Math.Max(1, (int)Math.Ceiling(_services.Chat.Notices.Count / 5.0));
    private void ShowNoticePage()
    {
        _noticePageIndex = Math.Clamp(_noticePageIndex, 0, NoticePageCount - 1);
        NoticePage.Clear();
        foreach (var item in _services.Chat.Notices.Skip(_noticePageIndex * 5).Take(5)) NoticePage.Add(item);
        NoticePageText.Text = $"{_noticePageIndex + 1} / {NoticePageCount}";
    }
    private void PreviousNoticePage_Click(object sender, RoutedEventArgs e) { if (_noticePageIndex > 0) { _noticePageIndex--; ShowNoticePage(); } }
    private void NextNoticePage_Click(object sender, RoutedEventArgs e) { if (_noticePageIndex + 1 < NoticePageCount) { _noticePageIndex++; ShowNoticePage(); } }
    private void SelectNotice_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: ScheduledNotice notice }) return;
        _selectedNotice = notice;
        NoticeNameBox.Text = notice.Name;
        NoticeTextBox.Text = notice.Text;
        NoticeIntervalBox.Text = notice.IntervalMinutes.ToString();
        NoticeMinMessagesBox.Text = notice.MinimumChatMessages.ToString();
        NoticeEnabledCheck.IsChecked = notice.Enabled;
        SetComboByContent(NoticeTargetCombo, notice.Target);
    }
    private void NewNotice_Click(object sender, RoutedEventArgs e)
    {
        _selectedNotice = null;
        NoticeNameBox.Text = "Нове сповіщення";
        NoticeTextBox.Clear();
        NoticeIntervalBox.Text = "20";
        NoticeMinMessagesBox.Text = "0";
        NoticeEnabledCheck.IsChecked = true;
        NoticeTargetCombo.SelectedIndex = 2;
    }
    private void SaveNotice_Click(object sender, RoutedEventArgs e)
    {
        var notice = _selectedNotice ?? new ScheduledNotice();
        notice.Name = string.IsNullOrWhiteSpace(NoticeNameBox.Text) ? "Сповіщення" : NoticeNameBox.Text.Trim();
        notice.Text = NoticeTextBox.Text.Trim();
        notice.Target = ComboText(NoticeTargetCombo);
        notice.IntervalMinutes = ParseInt(NoticeIntervalBox.Text, 20, 1, 1440);
        notice.MinimumChatMessages = ParseInt(NoticeMinMessagesBox.Text, 0, 0, 10000);
        notice.Enabled = NoticeEnabledCheck.IsChecked == true;
        notice.NextRun = DateTime.Now.AddMinutes(notice.IntervalMinutes);
        if (_selectedNotice is null) _services.Chat.Notices.Add(notice);
        _selectedNotice = notice;
        _services.Chat.SaveAll();
        ShowNoticePage();
        _services.Logger.Info($"Автосповіщення збережено: {notice.Name}");
    }
    private void DeleteNotice_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedNotice is null) return;
        _services.Chat.Notices.Remove(_selectedNotice);
        _selectedNotice = null;
        _services.Chat.SaveAll();
        ShowNoticePage();
        NewNotice_Click(sender, e);
    }
    private void SendNoticeNow_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(NoticeTextBox.Text)) return;
        _services.Chat.SendManual(NoticeTextBox.Text.Trim(), ComboText(NoticeTargetCombo));
    }
    private void AutoNoticesCheck_Changed(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;
        _services.Settings.Value.AutoNoticesEnabled = AutoNoticesCheck.IsChecked == true;
        _services.Save();
    }

    private int CommandPageCount => Math.Max(1, (int)Math.Ceiling(_services.Chat.Commands.Count / 6.0));
    private void ShowCommandPage()
    {
        _commandPageIndex = Math.Clamp(_commandPageIndex, 0, CommandPageCount - 1);
        CommandPage.Clear();
        foreach (var item in _services.Chat.Commands.Skip(_commandPageIndex * 6).Take(6)) CommandPage.Add(item);
        CommandPageText.Text = $"{_commandPageIndex + 1} / {CommandPageCount}";
    }
    private void PreviousCommandPage_Click(object sender, RoutedEventArgs e) { if (_commandPageIndex > 0) { _commandPageIndex--; ShowCommandPage(); } }
    private void NextCommandPage_Click(object sender, RoutedEventArgs e) { if (_commandPageIndex + 1 < CommandPageCount) { _commandPageIndex++; ShowCommandPage(); } }
    private void SelectCommand_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: BotCommand command }) return;
        _selectedCommand = command;
        CommandNameBox.Text = command.Name;
        CommandReplyBox.Text = command.Reply;
        CommandCooldownBox.Text = command.CooldownSeconds.ToString();
        CommandEnabledCheck.IsChecked = command.Enabled;
        SetComboByContent(CommandTargetCombo, command.Target);
    }
    private void NewCommand_Click(object sender, RoutedEventArgs e)
    {
        _selectedCommand = null;
        CommandNameBox.Text = "!команда";
        CommandReplyBox.Text = "Відповідь бота";
        CommandCooldownBox.Text = "10";
        CommandEnabledCheck.IsChecked = true;
        CommandTargetCombo.SelectedIndex = 2;
    }
    private void SaveCommand_Click(object sender, RoutedEventArgs e)
    {
        var command = _selectedCommand ?? new BotCommand();
        command.Name = string.IsNullOrWhiteSpace(CommandNameBox.Text) ? "!команда" : CommandNameBox.Text.Trim();
        if (!command.Name.StartsWith("!", StringComparison.Ordinal)) command.Name = "!" + command.Name;
        command.Reply = CommandReplyBox.Text.Trim();
        command.Target = ComboText(CommandTargetCombo);
        command.CooldownSeconds = ParseInt(CommandCooldownBox.Text, 10, 0, 86400);
        command.Enabled = CommandEnabledCheck.IsChecked == true;
        if (_selectedCommand is null) _services.Chat.Commands.Add(command);
        _selectedCommand = command;
        _services.Chat.SaveAll();
        ShowCommandPage();
        _services.Logger.Info($"Команду збережено: {command.Name}");
    }
    private void DeleteCommand_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedCommand is null) return;
        _services.Chat.Commands.Remove(_selectedCommand);
        _selectedCommand = null;
        _services.Chat.SaveAll();
        ShowCommandPage();
        NewCommand_Click(sender, e);
    }
    private void TestCommand_Click(object sender, RoutedEventArgs e)
    {
        var commandName = string.IsNullOrWhiteSpace(CommandNameBox.Text) ? "!команда" : CommandNameBox.Text.Trim();
        _services.Chat.AddIncoming("TWITCH", "TestViewer", commandName, "Viewer");
        _services.Chat.SendManual(CommandReplyBox.Text.Replace("{song}", App.Services.Music.CurrentTrack?.Display ?? "нічого", StringComparison.OrdinalIgnoreCase), ComboText(CommandTargetCombo));
    }

    private static string ComboText(ComboBox combo) => combo.SelectedItem is ComboBoxItem item ? item.Content?.ToString() ?? string.Empty : combo.Text;
    private static void SetComboByContent(ComboBox combo, string value)
    {
        foreach (var item in combo.Items.OfType<ComboBoxItem>())
            if (string.Equals(item.Content?.ToString(), value, StringComparison.OrdinalIgnoreCase)) { combo.SelectedItem = item; return; }
    }
    private static int ParseInt(string text, int fallback, int min, int max) => int.TryParse(text, out var value) ? Math.Clamp(value, min, max) : fallback;

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => DragTitle(sender, e);
    private void Minimize_Click(object sender, RoutedEventArgs e) => MinimizeWindow(sender, e);
    private void Maximize_Click(object sender, RoutedEventArgs e) => MaximizeWindow(sender, e);
    private void Close_Click(object sender, RoutedEventArgs e) => CloseWindow(sender, e);
}
