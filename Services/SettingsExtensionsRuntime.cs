using System.Diagnostics;
using System.Globalization;
using TiHiY.StreamControlCenter.Models;
using TiHiY.StreamControlCenter.Windows;

namespace TiHiY.StreamControlCenter.Services;

internal static class SettingsExtensionsRuntime
{
    private static readonly ConditionalWeakTable<SettingsWindow, Controller> Controllers = new();

    [ModuleInitializer]
    internal static void Register() =>
        EventManager.RegisterClassHandler(typeof(SettingsWindow), FrameworkElement.LoadedEvent, new RoutedEventHandler(OnLoaded));

    private static void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is not SettingsWindow window || Controllers.TryGetValue(window, out _)) return;
        Controllers.Add(window, new Controller(window));
    }

    private sealed class Controller : IDisposable
    {
        private readonly SettingsWindow _window;
        private readonly AppServices _services = App.Services;
        private CheckBox? _botEnabled;
        private CheckBox? _botAutoStart;
        private ComboBox? _botTarget;
        private CheckBox? _botSpam;
        private CheckBox? _botLinks;
        private CheckBox? _botCaps;
        private CheckBox? _botRepeats;
        private TextBox? _botBlockedWords;
        private TextBox? _botDelay;
        private TextBlock? _botStatus;
        private DataGrid? _commandsGrid;
        private DataGrid? _noticesGrid;

        private TextBox? _goalTitle;
        private TextBox? _goalInitial;
        private TextBox? _goalTarget;
        private TextBox? _goalCurrency;
        private TextBox? _goalBarColor;
        private TextBox? _goalTextColor;
        private TextBox? _goalBackgroundColor;
        private ComboBox? _topPeriod;
        private TextBox? _topCount;
        private TextBox? _tickerSpeed;
        private TextBox? _tickerTextColor;
        private TextBox? _tickerBackgroundColor;
        private Slider? _tickerOpacity;
        private TextBox? _goalUrl;
        private TextBox? _topUrl;
        private TextBlock? _donationStatus;

        public Controller(SettingsWindow window)
        {
            _window = window;
            _window.Closed += Window_Closed;
            AlignHeader();
            ExtendTwitch();
            AddChatBotTab();
            RebuildDonationsTab();
            AddMultichatShortcut();
            HookGlobalSave();
        }

        private void AlignHeader()
        {
            var design = FindNamed<Grid>("DesignSurface");
            if (design is null) return;
            if (design.RowDefinitions.Count > 0) design.RowDefinitions[0].Height = new GridLength(116);
            var header = design.Children.OfType<Grid>().FirstOrDefault(x => Grid.GetRow(x) == 0);
            if (header is null) return;
            header.Margin = new Thickness(4, 2, 4, 0);
            header.ClipToBounds = false;

            foreach (var image in Descendants<Image>(header))
            {
                var source = image.Source?.ToString() ?? string.Empty;
                if (source.Contains("header-emblem", StringComparison.OrdinalIgnoreCase))
                {
                    image.Width = 112;
                    image.Height = 112;
                    image.Margin = new Thickness(0);
                    image.VerticalAlignment = VerticalAlignment.Center;
                }
                else if (source.Contains("header-wheat", StringComparison.OrdinalIgnoreCase))
                {
                    image.Width = 154;
                    image.Height = 88;
                    image.Margin = new Thickness(0);
                    image.VerticalAlignment = VerticalAlignment.Center;
                }
                else if (source.Contains("header-map", StringComparison.OrdinalIgnoreCase))
                {
                    image.Width = 184;
                    image.Height = 88;
                    image.Margin = new Thickness(0);
                    image.VerticalAlignment = VerticalAlignment.Center;
                }
            }

            var title = Descendants<TextBlock>(header).FirstOrDefault(x => x.Text == "TiHiY");
            if (title?.Parent is StackPanel titleLine && titleLine.Parent is StackPanel titleStack)
            {
                titleStack.Margin = new Thickness(112, 4, 0, 0);
                titleStack.VerticalAlignment = VerticalAlignment.Center;
            }

            var buttons = header.Children.OfType<StackPanel>().FirstOrDefault(x =>
                Grid.GetColumn(x) == 2 && x.Children.OfType<Button>().Count() >= 3);
            if (buttons is not null)
            {
                buttons.Margin = new Thickness(0);
                buttons.VerticalAlignment = VerticalAlignment.Center;
            }
        }

        private void ExtendTwitch()
        {
            var tabs = FindNamed<TabControl>("SettingsTabs");
            var tab = FindTab(tabs, "Twitch");
            if (tab is null || tab.Tag as string == "Extended") return;
            var root = Descendants<StackPanel>(tab).FirstOrDefault(x => x.Margin.Left >= 10);
            var cardStack = root is null ? null : Descendants<Border>(root).Select(x => x.Child).OfType<StackPanel>().FirstOrDefault();
            if (cardStack is null) return;

            var panel = cardStack.Children.OfType<WrapPanel>().FirstOrDefault();
            if (panel is null)
            {
                panel = new WrapPanel { Margin = new Thickness(0, 10, 0, 0) };
                foreach (var oldButton in cardStack.Children.OfType<Button>().ToList())
                {
                    cardStack.Children.Remove(oldButton);
                    panel.Children.Add(oldButton);
                }
                cardStack.Children.Add(panel);
            }

            panel.Children.Add(ActionButton("TWITCH STUDIO / STREAM MANAGER", "TWITCH", (_, _) =>
                OpenUrl("https://dashboard.twitch.tv/u/tihiy_ded/stream-manager")));
            panel.Children.Add(ActionButton("НАЛАШТУВАННЯ МУЛЬТИЧАТУ", null, (_, _) =>
                _services.Windows.Show(() => new ChatAppearanceSettingsWindow(), _window)));
            tab.Tag = "Extended";
        }

        private void AddMultichatShortcut()
        {
            var broadcast = FindTab(FindNamed<TabControl>("SettingsTabs"), "Трансляція");
            var wrap = broadcast is null ? null : Descendants<WrapPanel>(broadcast).FirstOrDefault();
            if (wrap is null || wrap.Children.OfType<Button>().Any(x => ButtonText(x).Contains("МУЛЬТИЧАТ", StringComparison.OrdinalIgnoreCase))) return;
            wrap.Children.Add(ActionButton("НАЛАШТУВАННЯ МУЛЬТИЧАТУ", null, (_, _) =>
                _services.Windows.Show(() => new ChatAppearanceSettingsWindow(), _window)));
        }

        private void AddChatBotTab()
        {
            var tabs = FindNamed<TabControl>("SettingsTabs");
            if (tabs is null || FindTab(tabs, "Чат-бот") is not null) return;

            var tab = new TabItem { Header = Header("", "Чат-бот", "Chat Bot") };
            var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
            var root = new StackPanel { Margin = new Thickness(14, 0, 0, 0) };
            scroll.Content = root;
            tab.Content = scroll;
            root.Children.Add(Title("ЧАТ-БОТ МУЛЬТИЧАТУ / CHAT BOT"));

            var options = Card();
            var optionGrid = new Grid();
            optionGrid.ColumnDefinitions.Add(new ColumnDefinition());
            optionGrid.ColumnDefinitions.Add(new ColumnDefinition());
            options.Child = optionGrid;
            root.Children.Add(options);

            var left = new StackPanel { Margin = new Thickness(0, 0, 12, 0) };
            var right = new StackPanel { Margin = new Thickness(12, 0, 0, 0) };
            Grid.SetColumn(right, 1);
            optionGrid.Children.Add(left);
            optionGrid.Children.Add(right);

            left.Children.Add(Heading("Стан і платформи"));
            _botEnabled = new CheckBox { Content = "Увімкнути чат-бот", Margin = new Thickness(0, 12, 0, 0) };
            _botAutoStart = new CheckBox { Content = "Автозапуск команд та автоповідомлень", Margin = new Thickness(0, 8, 0, 0) };
            _botTarget = Combo("Twitch + YouTube", "Twitch", "YouTube");
            left.Children.Add(_botEnabled);
            left.Children.Add(_botAutoStart);
            left.Children.Add(Label("Платформи за замовчуванням", 12));
            left.Children.Add(_botTarget);
            _botStatus = new TextBlock { Margin = new Thickness(0, 12, 0, 0), FontWeight = FontWeights.Bold };
            left.Children.Add(_botStatus);

            right.Children.Add(Heading("Захист від спаму"));
            _botSpam = Check("Увімкнути антиспам", 12);
            _botLinks = Check("Ігнорувати посилання", 7);
            _botCaps = Check("Ігнорувати надмірний CAPS", 7);
            _botRepeats = Check("Ігнорувати повтори", 7);
            right.Children.Add(_botSpam);
            right.Children.Add(_botLinks);
            right.Children.Add(_botCaps);
            right.Children.Add(_botRepeats);
            right.Children.Add(Label("Чорний список слів", 10));
            _botBlockedWords = new TextBox { MinHeight = 38, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap };
            right.Children.Add(_botBlockedWords);
            right.Children.Add(Label("Затримка відповіді, мс", 10));
            _botDelay = new TextBox { Width = 160, HorizontalAlignment = HorizontalAlignment.Left };
            right.Children.Add(_botDelay);

            var commands = Card();
            var commandStack = new StackPanel();
            commands.Child = commandStack;
            root.Children.Add(commands);
            commandStack.Children.Add(Heading("КОМАНДИ БОТА", "Cyan"));
            _commandsGrid = CommandsGrid();
            commandStack.Children.Add(_commandsGrid);
            var commandActions = new WrapPanel { Margin = new Thickness(0, 8, 0, 0) };
            commandActions.Children.Add(ActionButton("ДОДАТИ КОМАНДУ", null, (_, _) => _services.Chat.Commands.Add(new BotCommand())));
            commandActions.Children.Add(ActionButton("ВИДАЛИТИ ВИБРАНУ", null, (_, _) =>
            {
                if (_commandsGrid.SelectedItem is BotCommand item) _services.Chat.Commands.Remove(item);
            }));
            commandStack.Children.Add(commandActions);

            var notices = Card();
            var noticeStack = new StackPanel();
            notices.Child = noticeStack;
            root.Children.Add(notices);
            noticeStack.Children.Add(Heading("АВТОМАТИЧНІ ПОВІДОМЛЕННЯ", "Cyan"));
            _noticesGrid = NoticesGrid();
            noticeStack.Children.Add(_noticesGrid);
            var noticeActions = new WrapPanel { Margin = new Thickness(0, 8, 0, 0) };
            noticeActions.Children.Add(ActionButton("ДОДАТИ ПОВІДОМЛЕННЯ", null, (_, _) => _services.Chat.Notices.Add(new ScheduledNotice())));
            noticeActions.Children.Add(ActionButton("ВИДАЛИТИ ВИБРАНЕ", null, (_, _) =>
            {
                if (_noticesGrid.SelectedItem is ScheduledNotice item) _services.Chat.Notices.Remove(item);
            }));
            noticeStack.Children.Add(noticeActions);

            var actions = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 4, 0, 12) };
            actions.Children.Add(ActionButton("ПІДКЛЮЧИТИ", null, (_, _) => { _botEnabled.IsChecked = true; SaveBot(); }));
            actions.Children.Add(ActionButton("ВІДКЛЮЧИТИ", null, (_, _) => { _botEnabled.IsChecked = false; SaveBot(); }));
            actions.Children.Add(ActionButton("ПЕРЕВІРИТИ", null, (_, _) =>
                _services.Chat.AddIncoming("TWITCH", "TiHiY Bot", "Тест чат-бота: налаштування працюють.", "Bot")));
            actions.Children.Add(ActionButton("НАЛАШТУВАННЯ МУЛЬТИЧАТУ", null, (_, _) =>
                _services.Windows.Show(() => new ChatAppearanceSettingsWindow(), _window)));
            actions.Children.Add(ActionButton("ЗБЕРЕГТИ ЧАТ-БОТ", null, (_, _) => SaveBot()));
            root.Children.Add(actions);

            LoadBot();
            var discord = FindTab(tabs, "Discord");
            var index = discord is null ? tabs.Items.Count : tabs.Items.IndexOf(discord);
            tabs.Items.Insert(index, tab);
        }

        private DataGrid CommandsGrid()
        {
            var grid = BaseGrid(_services.Chat.Commands);
            grid.Columns.Add(new DataGridCheckBoxColumn { Header = "ON", Binding = new Binding(nameof(BotCommand.Enabled)) });
            grid.Columns.Add(new DataGridTextColumn { Header = "Команда", Binding = new Binding(nameof(BotCommand.Name)), Width = new DataGridLength(1.1, DataGridLengthUnitType.Star) });
            grid.Columns.Add(new DataGridTextColumn { Header = "Відповідь", Binding = new Binding(nameof(BotCommand.Reply)), Width = new DataGridLength(2.4, DataGridLengthUnitType.Star) });
            grid.Columns.Add(new DataGridTextColumn { Header = "Платформа", Binding = new Binding(nameof(BotCommand.Target)), Width = new DataGridLength(1.2, DataGridLengthUnitType.Star) });
            grid.Columns.Add(new DataGridTextColumn { Header = "Cooldown", Binding = new Binding(nameof(BotCommand.CooldownSeconds)), Width = 90 });
            return grid;
        }

        private DataGrid NoticesGrid()
        {
            var grid = BaseGrid(_services.Chat.Notices);
            grid.Columns.Add(new DataGridCheckBoxColumn { Header = "ON", Binding = new Binding(nameof(ScheduledNotice.Enabled)) });
            grid.Columns.Add(new DataGridTextColumn { Header = "Назва", Binding = new Binding(nameof(ScheduledNotice.Name)), Width = new DataGridLength(1.1, DataGridLengthUnitType.Star) });
            grid.Columns.Add(new DataGridTextColumn { Header = "Текст", Binding = new Binding(nameof(ScheduledNotice.Text)), Width = new DataGridLength(2.3, DataGridLengthUnitType.Star) });
            grid.Columns.Add(new DataGridTextColumn { Header = "Платформа", Binding = new Binding(nameof(ScheduledNotice.Target)), Width = new DataGridLength(1.2, DataGridLengthUnitType.Star) });
            grid.Columns.Add(new DataGridTextColumn { Header = "Хв", Binding = new Binding(nameof(ScheduledNotice.IntervalMinutes)), Width = 60 });
            grid.Columns.Add(new DataGridTextColumn { Header = "Мін. чат", Binding = new Binding(nameof(ScheduledNotice.MinimumChatMessages)), Width = 76 });
            return grid;
        }

        private static DataGrid BaseGrid(IEnumerable source) => new()
        {
            ItemsSource = source,
            AutoGenerateColumns = false,
            CanUserAddRows = false,
            CanUserDeleteRows = false,
            HeadersVisibility = DataGridHeadersVisibility.Column,
            GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
            MinHeight = 150,
            MaxHeight = 235,
            Margin = new Thickness(0, 9, 0, 0),
            Background = Brushes.Transparent,
            BorderBrush = Brush("Line")
        };

        private void LoadBot()
        {
            var s = _services.Settings.Value;
            _botEnabled!.IsChecked = s.ChatBotEnabled;
            _botAutoStart!.IsChecked = s.ChatBotAutoStart;
            SelectCombo(_botTarget!, s.ChatBotDefaultTarget);
            _botSpam!.IsChecked = s.ChatBotSpamProtectionEnabled;
            _botLinks!.IsChecked = s.ChatBotBlockLinks;
            _botCaps!.IsChecked = s.ChatBotBlockCaps;
            _botRepeats!.IsChecked = s.ChatBotBlockRepeats;
            _botBlockedWords!.Text = s.ChatBotBlockedWords;
            _botDelay!.Text = s.ChatBotResponseDelayMilliseconds.ToString(CultureInfo.InvariantCulture);
            UpdateBotStatus();
        }

        private void SaveBot()
        {
            _commandsGrid?.CommitEdit(DataGridEditingUnit.Row, true);
            _noticesGrid?.CommitEdit(DataGridEditingUnit.Row, true);
            var s = _services.Settings.Value;
            s.ChatBotEnabled = _botEnabled?.IsChecked == true;
            s.ChatBotAutoStart = _botAutoStart?.IsChecked == true;
            s.ChatBotDefaultTarget = _botTarget?.SelectedItem?.ToString() ?? "Twitch + YouTube";
            s.ChatBotSpamProtectionEnabled = _botSpam?.IsChecked == true;
            s.ChatBotBlockLinks = _botLinks?.IsChecked == true;
            s.ChatBotBlockCaps = _botCaps?.IsChecked == true;
            s.ChatBotBlockRepeats = _botRepeats?.IsChecked == true;
            s.ChatBotBlockedWords = _botBlockedWords?.Text.Trim() ?? string.Empty;
            s.ChatBotResponseDelayMilliseconds = Int(_botDelay?.Text, 0, 0, 10000);
            s.AutoNoticesEnabled = s.ChatBotEnabled && s.ChatBotAutoStart;
            _services.Chat.SaveAll();
            _services.Save();
            UpdateBotStatus();
        }

        private void UpdateBotStatus()
        {
            if (_botStatus is null) return;
            var enabled = _services.Settings.Value.ChatBotEnabled;
            _botStatus.Text = enabled
                ? $"● Активний • Twitch: {(_services.Twitch.IsChatConnected ? "ON" : "OFF")} • YouTube: {(_services.YouTube.HasLiveChat ? "ON" : "OFF")}" 
                : "● Чат-бот вимкнено";
            _botStatus.Foreground = Brush(enabled ? "Green" : "Muted");
        }

        private void RebuildDonationsTab()
        {
            var tab = FindTab(FindNamed<TabControl>("SettingsTabs"), "Донати");
            if (tab is null) return;
            var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
            var root = new StackPanel { Margin = new Thickness(14, 0, 0, 0) };
            scroll.Content = root;
            tab.Content = scroll;
            root.Children.Add(Title("ДОНАТИ ТА OBS-ВІДЖЕТИ / DONATIONS & WIDGETS"));

            var columns = new Grid();
            columns.ColumnDefinitions.Add(new ColumnDefinition());
            columns.ColumnDefinitions.Add(new ColumnDefinition());
            root.Children.Add(columns);

            var goalCard = Card();
            goalCard.Margin = new Thickness(0, 0, 6, 10);
            var goal = new StackPanel();
            goalCard.Child = goal;
            columns.Children.Add(goalCard);
            goal.Children.Add(Heading("ЦІЛЬ ЗБОРУ"));
            _goalTitle = Field(goal, "Назва цілі");
            var amounts = Grid3();
            goal.Children.Add(amounts);
            _goalInitial = GridField(amounts, 0, "Початкова сума");
            _goalTarget = GridField(amounts, 1, "Цільова сума", new Thickness(6, 0, 6, 0));
            _goalCurrency = GridField(amounts, 2, "Валюта");
            var colors = Grid3();
            goal.Children.Add(colors);
            _goalBarColor = GridField(colors, 0, "Колір шкали");
            _goalTextColor = GridField(colors, 1, "Колір тексту", new Thickness(6, 0, 6, 0));
            _goalBackgroundColor = GridField(colors, 2, "Колір фону");
            _goalUrl = UrlField(goal, "URL окремого віджета цілі");
            var goalActions = new WrapPanel { Margin = new Thickness(0, 7, 0, 0) };
            goalActions.Children.Add(ActionButton("СКИНУТИ ПОЧАТКОВУ СУМУ", null, (_, _) => { _goalInitial.Text = "0"; SaveDonations(); }));
            goalActions.Children.Add(ActionButton("ВІДКРИТИ", null, (_, _) => { SaveDonations(); OpenUrl(_goalUrl.Text); }));
            goalActions.Children.Add(ActionButton("КОПІЮВАТИ URL", null, (_, _) => Copy(_goalUrl.Text)));
            goal.Children.Add(goalActions);

            var tickerCard = Card();
            tickerCard.Margin = new Thickness(6, 0, 0, 10);
            Grid.SetColumn(tickerCard, 1);
            columns.Children.Add(tickerCard);
            var ticker = new StackPanel();
            tickerCard.Child = ticker;
            ticker.Children.Add(Heading("ТОП ДОНАТЕРИ • РУХОМИЙ РЯДОК"));
            var tickerOptions = Grid3();
            ticker.Children.Add(tickerOptions);
            _topPeriod = Combo("Весь час", "Поточний стрім", "Сьогодні", "Поточний місяць");
            GridControl(tickerOptions, 0, "Період", _topPeriod);
            _topCount = GridField(tickerOptions, 1, "Кількість", new Thickness(6, 0, 6, 0));
            _tickerSpeed = GridField(tickerOptions, 2, "Швидкість px/с");
            var tickerColors = new Grid { Margin = new Thickness(0, 8, 0, 0) };
            tickerColors.ColumnDefinitions.Add(new ColumnDefinition());
            tickerColors.ColumnDefinitions.Add(new ColumnDefinition());
            ticker.Children.Add(tickerColors);
            _tickerTextColor = GridField(tickerColors, 0, "Колір тексту");
            _tickerBackgroundColor = GridField(tickerColors, 1, "Колір фону", new Thickness(6, 0, 0, 0));
            ticker.Children.Add(Label("Прозорість фону", 10));
            _tickerOpacity = new Slider { Minimum = 0, Maximum = 0.95, TickFrequency = 0.05 };
            ticker.Children.Add(_tickerOpacity);
            _topUrl = UrlField(ticker, "URL рухомого рядка");
            var tickerActions = new WrapPanel { Margin = new Thickness(0, 7, 0, 0) };
            tickerActions.Children.Add(ActionButton("ВІДКРИТИ", null, (_, _) => { SaveDonations(); OpenUrl(_topUrl.Text); }));
            tickerActions.Children.Add(ActionButton("КОПІЮВАТИ URL", null, (_, _) => Copy(_topUrl.Text)));
            ticker.Children.Add(tickerActions);

            var bottom = Card();
            var bottomGrid = new Grid();
            bottomGrid.ColumnDefinitions.Add(new ColumnDefinition());
            bottomGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            bottom.Child = bottomGrid;
            root.Children.Add(bottom);
            _donationStatus = new TextBlock { Foreground = Brush("Green"), VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap };
            bottomGrid.Children.Add(_donationStatus);
            var actions = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Right };
            Grid.SetColumn(actions, 1);
            bottomGrid.Children.Add(actions);
            actions.Children.Add(ActionButton("ВІДКРИТИ DONATELLO API", null, (_, _) => _services.Windows.Show(() => new DonatelloWindow(), _window)));
            actions.Children.Add(ActionButton("ЗБЕРЕГТИ ВІДЖЕТИ", null, (_, _) => SaveDonations()));
            LoadDonations();
        }

        private void LoadDonations()
        {
            var s = _services.Settings.Value;
            _goalTitle!.Text = s.DonationGoalTitle;
            _goalInitial!.Text = s.DonationGoalInitialAmount.ToString("0.##", CultureInfo.InvariantCulture);
            _goalTarget!.Text = s.DonationGoalAmount.ToString("0.##", CultureInfo.InvariantCulture);
            _goalCurrency!.Text = s.DonationGoalCurrency;
            _goalBarColor!.Text = s.DonationGoalBarColor;
            _goalTextColor!.Text = s.DonationGoalTextColor;
            _goalBackgroundColor!.Text = s.DonationGoalBackgroundColor;
            _topCount!.Text = s.DonationTopDonorCount.ToString(CultureInfo.InvariantCulture);
            _tickerSpeed!.Text = s.DonationTickerSpeed.ToString("0.#", CultureInfo.InvariantCulture);
            _tickerTextColor!.Text = s.DonationTickerTextColor;
            _tickerBackgroundColor!.Text = s.DonationTickerBackgroundColor;
            _tickerOpacity!.Value = s.DonationTickerBackgroundOpacity;
            _topPeriod!.SelectedIndex = s.DonationTopDonorPeriod.ToLowerInvariant() switch { "stream" => 1, "day" => 2, "month" => 3, _ => 0 };
            UpdateDonationUrls();
            UpdateDonationStatus();
        }

        private void SaveDonations()
        {
            var s = _services.Settings.Value;
            s.DonationGoalTitle = string.IsNullOrWhiteSpace(_goalTitle?.Text) ? "Ціль збору" : _goalTitle.Text.Trim();
            s.DonationGoalInitialAmount = Decimal(_goalInitial?.Text, 0, 0, 1_000_000_000);
            s.DonationGoalAmount = Decimal(_goalTarget?.Text, 10000, 1, 1_000_000_000);
            s.DonationGoalCurrency = string.IsNullOrWhiteSpace(_goalCurrency?.Text) ? "UAH" : _goalCurrency.Text.Trim().ToUpperInvariant();
            s.DonationGoalBarColor = ColorText(_goalBarColor?.Text, "#FFD329");
            s.DonationGoalTextColor = ColorText(_goalTextColor?.Text, "#F4F8FF");
            s.DonationGoalBackgroundColor = ColorText(_goalBackgroundColor?.Text, "#06172A");
            s.DonationTopDonorCount = Int(_topCount?.Text, 8, 1, 30);
            s.DonationTopDonorPeriod = _topPeriod?.SelectedIndex switch { 1 => "Stream", 2 => "Day", 3 => "Month", _ => "All" };
            s.DonationTickerSpeed = Double(_tickerSpeed?.Text, 70, 20, 250);
            s.DonationTickerTextColor = ColorText(_tickerTextColor?.Text, "#FFD329");
            s.DonationTickerBackgroundColor = ColorText(_tickerBackgroundColor?.Text, "#06172A");
            s.DonationTickerBackgroundOpacity = Math.Clamp(_tickerOpacity?.Value ?? 0.35, 0, 0.95);
            _services.Donations.GoalInitialAmount = s.DonationGoalInitialAmount;
            _services.Donations.GoalAmount = s.DonationGoalAmount;
            _services.Donations.GoalCurrency = s.DonationGoalCurrency;
            _services.Save();
            UpdateDonationUrls();
            UpdateDonationStatus();
        }

        private void UpdateDonationUrls()
        {
            var port = _services.Overlay.IsRunning ? _services.Overlay.Port : _services.Settings.Value.OverlayPort;
            if (_goalUrl is not null) _goalUrl.Text = $"http://127.0.0.1:{port}/overlay/goal";
            if (_topUrl is not null) _topUrl.Text = $"http://127.0.0.1:{port}/overlay/top-donors";
        }

        private void UpdateDonationStatus()
        {
            if (_donationStatus is null) return;
            var d = _services.Donations;
            _donationStatus.Text = $"Прогрес: {d.TotalAmount:N2} {d.GoalCurrency} / {d.GoalAmount:N2} {d.GoalCurrency} • {d.GoalProgress * 100:0}%";
        }

        private void HookGlobalSave()
        {
            foreach (var button in Descendants<Button>(_window).Where(x =>
                         ButtonText(x).Contains("Застосувати", StringComparison.OrdinalIgnoreCase) ||
                         ButtonText(x).Contains("Зберегти", StringComparison.OrdinalIgnoreCase)))
            {
                button.PreviewMouseLeftButtonDown += (_, _) =>
                {
                    if (_botEnabled is not null) SaveBot();
                    if (_goalTitle is not null) SaveDonations();
                };
            }
        }

        private Border Card()
        {
            var border = new Border();
            border.SetResourceReference(FrameworkElement.StyleProperty, "SettingsCard");
            return border;
        }

        private TextBlock Title(string text)
        {
            var title = new TextBlock { Text = text };
            title.SetResourceReference(FrameworkElement.StyleProperty, "SettingsSectionTitle");
            return title;
        }

        private static TextBlock Heading(string text, string brush = "Amber") => new()
        {
            Text = text,
            FontSize = 17,
            FontWeight = FontWeights.Bold,
            Foreground = Brush(brush)
        };

        private static StackPanel Header(string glyph, string title, string sub)
        {
            var panel = new StackPanel { Orientation = Orientation.Horizontal };
            panel.Children.Add(new TextBlock { Text = glyph, FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = 23, Width = 40, Foreground = Brush("Amber"), VerticalAlignment = VerticalAlignment.Center });
            var text = new StackPanel();
            text.Children.Add(new TextBlock { Text = title, FontSize = 15, FontWeight = FontWeights.SemiBold });
            text.Children.Add(new TextBlock { Text = sub, FontSize = 12, Opacity = 0.78 });
            panel.Children.Add(text);
            return panel;
        }

        private Button ActionButton(string text, string? platform, RoutedEventHandler click)
        {
            var button = new Button { Margin = new Thickness(3), MinHeight = 34, Padding = new Thickness(12, 5, 12, 5) };
            if (platform is null) button.Content = text;
            else
            {
                var panel = new StackPanel { Orientation = Orientation.Horizontal };
                panel.Children.Add(new PlatformVectorIcon(platform) { Width = 23, Height = 21, Margin = new Thickness(0, 0, 8, 0) });
                panel.Children.Add(new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center });
                button.Content = panel;
            }
            button.Click += click;
            return button;
        }

        private static CheckBox Check(string text, double top) => new() { Content = text, Margin = new Thickness(0, top, 0, 0) };
        private static TextBlock Label(string text, double top = 8) => new() { Text = text, Foreground = Brush("Muted"), Margin = new Thickness(0, top, 0, 3) };

        private static ComboBox Combo(params string[] items)
        {
            var combo = new ComboBox();
            foreach (var item in items) combo.Items.Add(item);
            combo.SelectedIndex = 0;
            return combo;
        }

        private static void SelectCombo(ComboBox combo, string value)
        {
            combo.SelectedItem = combo.Items.Cast<object>().FirstOrDefault(x => string.Equals(x?.ToString(), value, StringComparison.OrdinalIgnoreCase)) ?? combo.Items[0];
        }

        private static Grid Grid3()
        {
            var grid = new Grid { Margin = new Thickness(0, 8, 0, 0) };
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            return grid;
        }

        private static TextBox Field(Panel panel, string label)
        {
            panel.Children.Add(Label(label, 10));
            var box = new TextBox();
            panel.Children.Add(box);
            return box;
        }

        private static TextBox GridField(Grid grid, int column, string label, Thickness? margin = null)
        {
            var stack = new StackPanel { Margin = margin ?? new Thickness(0) };
            Grid.SetColumn(stack, column);
            grid.Children.Add(stack);
            stack.Children.Add(Label(label, 0));
            var box = new TextBox();
            stack.Children.Add(box);
            return box;
        }

        private static void GridControl(Grid grid, int column, string label, Control control)
        {
            var stack = new StackPanel();
            Grid.SetColumn(stack, column);
            grid.Children.Add(stack);
            stack.Children.Add(Label(label, 0));
            stack.Children.Add(control);
        }

        private static TextBox UrlField(Panel panel, string label)
        {
            panel.Children.Add(Label(label, 10));
            var box = new TextBox { IsReadOnly = true };
            panel.Children.Add(box);
            return box;
        }

        private static TabItem? FindTab(TabControl? tabs, string text) => tabs?.Items.OfType<TabItem>().FirstOrDefault(x =>
        {
            if (x.Header is DependencyObject root)
                return Descendants<TextBlock>(root).Any(t => t.Text.Contains(text, StringComparison.OrdinalIgnoreCase));
            return x.Header?.ToString()?.Contains(text, StringComparison.OrdinalIgnoreCase) == true;
        });

        private T? FindNamed<T>(string name) where T : FrameworkElement => Descendants<T>(_window).FirstOrDefault(x => x.Name == name);
        private static string ButtonText(Button button) => button.Content switch { string s => s, TextBlock t => t.Text, _ => string.Empty };
        private static Brush Brush(string key) => Application.Current.TryFindResource(key) as Brush ?? Brushes.White;

        private void OpenUrl(string url)
        {
            try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
            catch (Exception ex)
            {
                _services.Logger.Error("Відкриття посилання", ex);
                MessageBox.Show(_window, ex.GetBaseException().Message, "Відкриття", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void Copy(string text)
        {
            try { Clipboard.SetText(text); }
            catch (Exception ex) { _services.Logger.Error("Буфер обміну", ex); }
        }

        private static int Int(string? text, int fallback, int min, int max) =>
            int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? Math.Clamp(value, min, max) : fallback;

        private static double Double(string? text, double fallback, double min, double max)
        {
            if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) &&
                !double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value)) return fallback;
            return Math.Clamp(value, min, max);
        }

        private static decimal Decimal(string? text, decimal fallback, decimal min, decimal max)
        {
            if (!decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var value) &&
                !decimal.TryParse(text, NumberStyles.Number, CultureInfo.CurrentCulture, out value)) return fallback;
            return Math.Clamp(value, min, max);
        }

        private static string ColorText(string? value, string fallback)
        {
            var text = value?.Trim() ?? string.Empty;
            if (!text.StartsWith('#')) text = "#" + text;
            return text.Length is 7 or 9 && text.Skip(1).All(Uri.IsHexDigit) ? text.ToUpperInvariant() : fallback;
        }

        private void Window_Closed(object? sender, EventArgs e) => Dispose();
        public void Dispose() => _window.Closed -= Window_Closed;
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        var visited = new HashSet<DependencyObject>();
        var stack = new Stack<DependencyObject>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            var current = stack.Pop();
            if (!visited.Add(current)) continue;
            if (current is T match) yield return match;
            try
            {
                for (var index = 0; index < VisualTreeHelper.GetChildrenCount(current); index++) stack.Push(VisualTreeHelper.GetChild(current, index));
            }
            catch { }
            try
            {
                foreach (var child in LogicalTreeHelper.GetChildren(current).OfType<DependencyObject>()) stack.Push(child);
            }
            catch { }
        }
    }
}