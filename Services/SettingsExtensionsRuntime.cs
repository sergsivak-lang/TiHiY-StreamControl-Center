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
        private readonly TextBlock _botStatus = new();
        private CheckBox? _botEnabled;
        private CheckBox? _botAutoStart;
        private ComboBox? _botTarget;
        private CheckBox? _botSpam;
        private CheckBox? _botLinks;
        private CheckBox? _botCaps;
        private CheckBox? _botRepeats;
        private TextBox? _botBlockedWords;
        private TextBox? _botDelay;
        private DataGrid? _commandsGrid;
        private DataGrid? _noticesGrid;

        private TextBox? _goalTitle;
        private TextBox? _goalTarget;
        private TextBox? _goalInitial;
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
            ExtendTwitchTab();
            AddChatBotTab();
            RebuildDonationsTab();
            AddChatAppearanceAccess();
            HookGlobalSaveButtons();
        }

        private void AlignHeader()
        {
            var design = FindNamed<Grid>("DesignSurface");
            if (design is null) return;
            if (design.RowDefinitions.Count >= 3)
                design.RowDefinitions[0].Height = new GridLength(116);

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

        private void ExtendTwitchTab()
        {
            var tabs = FindNamed<TabControl>("SettingsTabs");
            var twitchTab = tabs?.Items.OfType<TabItem>().FirstOrDefault(x => HeaderContains(x, "Twitch"));
            if (twitchTab?.Content is not StackPanel root) return;
            var card = root.Children.OfType<Border>().FirstOrDefault();
            var stack = card?.Child as StackPanel;
            if (stack is null || stack.Tag as string == "ExtendedTwitch") return;

            var panel = stack.Children.OfType<WrapPanel>().FirstOrDefault();
            if (panel is null)
            {
                panel = new WrapPanel { Margin = new Thickness(0, 12, 0, 0) };
                var existing = stack.Children.OfType<Button>().ToList();
                foreach (var button in existing)
                {
                    stack.Children.Remove(button);
                    button.Margin = new Thickness(3);
                    panel.Children.Add(button);
                }
                stack.Children.Add(panel);
            }

            var studio = ActionButton("TWITCH STUDIO / STREAM MANAGER", "TWITCH", (_, _) => OpenUrl("https://dashboard.twitch.tv/u/tihiy_ded/stream-manager"));
            studio.ToolTip = "Відкрити Twitch Creator Dashboard — Stream Manager";
            panel.Children.Add(studio);
            panel.Children.Add(ActionButton("НАЛАШТУВАННЯ МУЛЬТИЧАТУ", null, (_, _) =>
                _services.Windows.Show(() => new ChatAppearanceSettingsWindow(), _window)));
            stack.Tag = "ExtendedTwitch";
        }

        private void AddChatAppearanceAccess()
        {
            var tabs = FindNamed<TabControl>("SettingsTabs");
            var broadcast = tabs?.Items.OfType<TabItem>().FirstOrDefault(x => HeaderContains(x, "Трансляція"));
            var wrap = broadcast is null ? null : Descendants<WrapPanel>(broadcast).FirstOrDefault();
            if (wrap is not null && !wrap.Children.OfType<Button>().Any(x => ContentText(x).Contains("МУЛЬТИЧАТ", StringComparison.OrdinalIgnoreCase)))
                wrap.Children.Add(ActionButton("НАЛАШТУВАННЯ МУЛЬТИЧАТУ", null, (_, _) =>
                    _services.Windows.Show(() => new ChatAppearanceSettingsWindow(), _window)));
        }

        private void AddChatBotTab()
        {
            var tabs = FindNamed<TabControl>("SettingsTabs");
            if (tabs is null || tabs.Items.OfType<TabItem>().Any(x => HeaderContains(x, "Чат-бот"))) return;

            var tab = new TabItem { Header = BuildHeader("", "Чат-бот", "Chat Bot") };
            var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
            var root = new StackPanel { Margin = new Thickness(14, 0, 0, 0) };
            scroll.Content = root;
            tab.Content = scroll;
            root.Children.Add(SectionTitle("ЧАТ-БОТ МУЛЬТИЧАТУ / CHAT BOT"));

            var settingsCard = Card();
            var settingsGrid = new Grid();
            settingsGrid.ColumnDefinitions.Add(new ColumnDefinition());
            settingsGrid.ColumnDefinitions.Add(new ColumnDefinition());
            settingsCard.Child = settingsGrid;
            root.Children.Add(settingsCard);

            var left = new StackPanel { Margin = new Thickness(0, 0, 12, 0) };
            var right = new StackPanel { Margin = new Thickness(12, 0, 0, 0) };
            Grid.SetColumn(right, 1);
            settingsGrid.Children.Add(left);
            settingsGrid.Children.Add(right);

            left.Children.Add(new TextBlock { Text = "Стан і платформи", FontSize = 17, FontWeight = FontWeights.Bold, Foreground = ResourceBrush("Amber") });
            _botEnabled = new CheckBox { Content = "Увімкнути чат-бот", Margin = new Thickness(0, 12, 0, 0) };
            _botAutoStart = new CheckBox { Content = "Автозапуск команд і повідомлень", Margin = new Thickness(0, 8, 0, 0) };
            _botTarget = Combo("Twitch + YouTube", "Twitch", "YouTube");
            left.Children.Add(_botEnabled);
            left.Children.Add(_botAutoStart);
            left.Children.Add(Label("Платформи за замовчуванням", 12));
            left.Children.Add(_botTarget);
            _botStatus.Foreground = ResourceBrush("Green");
            _botStatus.FontWeight = FontWeights.Bold;
            _botStatus.Margin = new Thickness(0, 12, 0, 0);
            left.Children.Add(_botStatus);

            right.Children.Add(new TextBlock { Text = "Антиспам і модераційні фільтри", FontSize = 17, FontWeight = FontWeights.Bold, Foreground = ResourceBrush("Amber") });
            _botSpam = new CheckBox { Content = "Увімкнути захист від спаму", Margin = new Thickness(0, 12, 0, 0) };
            _botLinks = new CheckBox { Content = "Ігнорувати повідомлення з посиланнями", Margin = new Thickness(0, 7, 0, 0) };
            _botCaps = new CheckBox { Content = "Ігнорувати надмірний CAPS", Margin = new Thickness(0, 7, 0, 0) };
            _botRepeats = new CheckBox { Content = "Ігнорувати повтори", Margin = new Thickness(0, 7, 0, 0) };
            right.Children.Add(_botSpam);
            right.Children.Add(_botLinks);
            right.Children.Add(_botCaps);
            right.Children.Add(_botRepeats);
            right.Children.Add(Label("Чорний список слів (через кому)", 10));
            _botBlockedWords = new TextBox { MinHeight = 38, TextWrapping = TextWrapping.Wrap, AcceptsReturn = true };
            right.Children.Add(_botBlockedWords);
            right.Children.Add(Label("Затримка відповіді, мс", 10));
            _botDelay = new TextBox { Width = 160, HorizontalAlignment = HorizontalAlignment.Left };
            right.Children.Add(_botDelay);

            var commandsCard = Card();
            var commandsStack = new StackPanel();
            commandsCard.Child = commandsStack;
            root.Children.Add(commandsCard);
            commandsStack.Children.Add(new TextBlock { Text = "КОМАНДИ БОТА", FontSize = 17, FontWeight = FontWeights.Bold, Foreground = ResourceBrush("Cyan") });
            _commandsGrid = BuildCommandsGrid();
            commandsStack.Children.Add(_commandsGrid);
            var commandButtons = new WrapPanel { Margin = new Thickness(0, 8, 0, 0) };
            commandButtons.Children.Add(ActionButton("ДОДАТИ КОМАНДУ", null, (_, _) => _services.Chat.Commands.Add(new BotCommand())));
            commandButtons.Children.Add(ActionButton("ВИДАЛИТИ ВИБРАНУ", null, (_, _) => { if (_commandsGrid.SelectedItem is BotCommand item) _services.Chat.Commands.Remove(item); }));
            commandsStack.Children.Add(commandButtons);

            var noticesCard = Card();
            var noticesStack = new StackPanel();
            noticesCard.Child = noticesStack;
            root.Children.Add(noticesCard);
            noticesStack.Children.Add(new TextBlock { Text = "АВТОМАТИЧНІ ПОВІДОМЛЕННЯ", FontSize = 17, FontWeight = FontWeights.Bold, Foreground = ResourceBrush("Cyan") });
            _noticesGrid = BuildNoticesGrid();
            noticesStack.Children.Add(_noticesGrid);
            var noticeButtons = new WrapPanel { Margin = new Thickness(0, 8, 0, 0) };
            noticeButtons.Children.Add(ActionButton("ДОДАТИ ПОВІДОМЛЕННЯ", null, (_, _) => _services.Chat.Notices.Add(new ScheduledNotice())));
            noticeButtons.Children.Add(ActionButton("ВИДАЛИТИ ВИБРАНЕ", null, (_, _) => { if (_noticesGrid.SelectedItem is ScheduledNotice item) _services.Chat.Notices.Remove(item); }));
            noticesStack.Children.Add(noticeButtons);

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
            var discordIndex = tabs.Items.OfType<TabItem>().ToList().FindIndex(x => HeaderContains(x, "Discord"));
            tabs.Items.Insert(discordIndex >= 0 ? discordIndex : tabs.Items.Count, tab);
        }

        private DataGrid BuildCommandsGrid()
        {
            var grid = BaseGrid(_services.Chat.Commands);
            grid.Columns.Add(new DataGridCheckBoxColumn { Header = "ON", Binding = new Binding(nameof(BotCommand.Enabled)) });
            grid.Columns.Add(new DataGridTextColumn { Header = "Команда", Binding = new Binding(nameof(BotCommand.Name)), Width = new DataGridLength(1.1, DataGridLengthUnitType.Star) });
            grid.Columns.Add(new DataGridTextColumn { Header = "Відповідь", Binding = new Binding(nameof(BotCommand.Reply)), Width = new DataGridLength(2.4, DataGridLengthUnitType.Star) });
            grid.Columns.Add(new DataGridTextColumn { Header = "Платформа", Binding = new Binding(nameof(BotCommand.Target)), Width = new DataGridLength(1.2, DataGridLengthUnitType.Star) });
            grid.Columns.Add(new DataGridTextColumn { Header = "Cooldown", Binding = new Binding(nameof(BotCommand.CooldownSeconds)), Width = 90 });
            return grid;
        }

        private DataGrid BuildNoticesGrid()
        {
            var grid = BaseGrid(_services.Chat.Notices);
            grid.Columns.Add(new DataGridCheckBoxColumn { Header = "ON", Binding = new Binding(nameof(ScheduledNotice.Enabled)) });
            grid.Columns.Add(new DataGridTextColumn { Header = "Назва", Binding = new Binding(nameof(ScheduledNotice.Name)), Width = new DataGridLength(1.1, DataGridLengthUnitType.Star) });
            grid.Columns.Add(new DataGridTextColumn { Header = "Текст", Binding = new Binding(nameof(ScheduledNotice.Text)), Width = new DataGridLength(2.2, DataGridLengthUnitType.Star) });
            grid.Columns.Add(new DataGridTextColumn { Header = "Платформа", Binding = new Binding(nameof(ScheduledNotice.Target)), Width = new DataGridLength(1.2, DataGridLengthUnitType.Star) });
            grid.Columns.Add(new DataGridTextColumn { Header = "Хв", Binding = new Binding(nameof(ScheduledNotice.IntervalMinutes)), Width = 60 });
            grid.Columns.Add(new DataGridTextColumn { Header = "Мін. чат", Binding = new Binding(nameof(ScheduledNotice.MinimumChatMessages)), Width = 76 });
            return grid;
        }

        private static DataGrid BaseGrid(object itemsSource) => new()
        {
            ItemsSource = (IEnumerable)itemsSource,
            AutoGenerateColumns = false,
            CanUserAddRows = false,
            CanUserDeleteRows = false,
            HeadersVisibility = DataGridHeadersVisibility.Column,
            GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
            MinHeight = 150,
            MaxHeight = 235,
            Margin = new Thickness(0, 9, 0, 0),
            Background = Brushes.Transparent,
            BorderBrush = ResourceBrush("Line")
        };

        private void LoadBot()
        {
            var s = _services.Settings.Value;
            _botEnabled!.IsChecked = s.ChatBotEnabled;
            _botAutoStart!.IsChecked = s.ChatBotAutoStart;
            _botTarget!.SelectedItem = _botTarget.Items.Cast<object>().FirstOrDefault(x => string.Equals(x?.ToString(), s.ChatBotDefaultTarget, StringComparison.OrdinalIgnoreCase)) ?? _botTarget.Items[0];
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
            s.ChatBotResponseDelayMilliseconds = ParseInt(_botDelay?.Text, 0, 0, 10000);
            s.AutoNoticesEnabled = s.ChatBotEnabled && s.ChatBotAutoStart;
            _services.Chat.SaveAll();
            _services.Save();
            UpdateBotStatus();
        }

        private void UpdateBotStatus()
        {
            var s = _services.Settings.Value;
            var platforms = $"Twitch: {(_services.Twitch.IsChatConnected ? "ON" : "OFF")} • YouTube: {(_services.YouTube.HasLiveChat ? "ON" : "OFF")}";
            _botStatus.Text = s.ChatBotEnabled ? $"● Чат-бот активний • {platforms}" : "● Чат-бот вимкнено";
            _botStatus.Foreground = ResourceBrush(s.ChatBotEnabled ? "Green" : "Muted");
        }

        private void RebuildDonationsTab()
        {
            var tabs = FindNamed<TabControl>("SettingsTabs");
            var tab = tabs?.Items.OfType<TabItem>().FirstOrDefault(x => HeaderContains(x, "Донати"));
            if (tab is null) return;

            var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
            var root = new StackPanel { Margin = new Thickness(14, 0, 0, 0) };
            scroll.Content = root;
            tab.Content = scroll;
            root.Children.Add(SectionTitle("ДОНАТИ ТА OBS-ВІДЖЕТИ / DONATIONS & WIDGETS"));

            var columns = new Grid();
            columns.ColumnDefinitions.Add(new ColumnDefinition());
            columns.ColumnDefinitions.Add(new ColumnDefinition());
            root.Children.Add(columns);

            var goalCard = Card();
            goalCard.Margin = new Thickness(0, 0, 6, 10);
            var goal = new StackPanel();
            goalCard.Child = goal;
            columns.Children.Add(goalCard);
            goal.Children.Add(new TextBlock { Text = "ЦІЛЬ ЗБОРУ", FontSize = 18, FontWeight = FontWeights.Bold, Foreground = ResourceBrush("Amber") });
            _goalTitle = AddTextField(goal, "Назва цілі");
            var goalAmounts = new Grid { Margin = new Thickness(0, 8, 0, 0) };
            goalAmounts.ColumnDefinitions.Add(new ColumnDefinition());
            goalAmounts.ColumnDefinitions.Add(new ColumnDefinition());
            goalAmounts.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });
            goal.Children.Add(goalAmounts);
            _goalInitial = AddGridField(goalAmounts, 0, "Початкова / вже зібрана сума");
            _goalTarget = AddGridField(goalAmounts, 1, "Цільова сума", new Thickness(6, 0, 6, 0));
            _goalCurrency = AddGridField(goalAmounts, 2, "Валюта");
            var goalColors = new Grid { Margin = new Thickness(0, 8, 0, 0) };
            goalColors.ColumnDefinitions.Add(new ColumnDefinition());
            goalColors.ColumnDefinitions.Add(new ColumnDefinition());
            goalColors.ColumnDefinitions.Add(new ColumnDefinition());
            goal.Children.Add(goalColors);
            _goalBarColor = AddGridField(goalColors, 0, "Колір шкали");
            _goalTextColor = AddGridField(goalColors, 1, "Колір тексту", new Thickness(6, 0, 6, 0));
            _goalBackgroundColor = AddGridField(goalColors, 2, "Колір фону");
            _goalUrl = AddReadOnlyUrl(goal, "URL окремого віджета цілі збору");
            var goalButtons = new WrapPanel { Margin = new Thickness(0, 7, 0, 0) };
            goalButtons.Children.Add(ActionButton("СКИНУТИ ПОЧАТКОВУ СУМУ", null, (_, _) => { _goalInitial.Text = "0"; SaveDonations(); }));
            goalButtons.Children.Add(ActionButton("ВІДКРИТИ ВІДЖЕТ", null, (_, _) => { SaveDonations(); OpenUrl(_goalUrl.Text); }));
            goalButtons.Children.Add(ActionButton("СКОПІЮВАТИ URL", null, (_, _) => Copy(_goalUrl.Text)));
            goal.Children.Add(goalButtons);

            var tickerCard = Card();
            tickerCard.Margin = new Thickness(6, 0, 0, 10);
            Grid.SetColumn(tickerCard, 1);
            columns.Children.Add(tickerCard);
            var ticker = new StackPanel();
            tickerCard.Child = ticker;
            ticker.Children.Add(new TextBlock { Text = "ТОП ДОНАТЕРИ • РУХОМИЙ РЯДОК", FontSize = 18, FontWeight = FontWeights.Bold, Foreground = ResourceBrush("Amber") });
            var tickerGrid = new Grid { Margin = new Thickness(0, 10, 0, 0) };
            tickerGrid.ColumnDefinitions.Add(new ColumnDefinition());
            tickerGrid.ColumnDefinitions.Add(new ColumnDefinition());
            tickerGrid.ColumnDefinitions.Add(new ColumnDefinition());
            ticker.Children.Add(tickerGrid);
            _topPeriod = Combo("Весь час", "Поточний стрім", "Сьогодні", "Поточний місяць");
            AddGridControl(tickerGrid, 0, "Період рейтингу", _topPeriod);
            _topCount = AddGridField(tickerGrid, 1, "Кількість", new Thickness(6, 0, 6, 0));
            _tickerSpeed = AddGridField(tickerGrid, 2, "Швидкість px/с");
            var tickerColors = new Grid { Margin = new Thickness(0, 8, 0, 0) };
            tickerColors.ColumnDefinitions.Add(new ColumnDefinition());
            tickerColors.ColumnDefinitions.Add(new ColumnDefinition());
            ticker.Children.Add(tickerColors);
            _tickerTextColor = AddGridField(tickerColors, 0, "Колір тексту");
            _tickerBackgroundColor = AddGridField(tickerColors, 1, "Колір фону", new Thickness(6, 0, 0, 0));
            ticker.Children.Add(Label("Прозорість фону", 10));
            _tickerOpacity = new Slider { Minimum = 0, Maximum = 0.95, TickFrequency = 0.05 };
            ticker.Children.Add(_tickerOpacity);
            _topUrl = AddReadOnlyUrl(ticker, "URL рухомого рядка топ-донатерів");
            var tickerButtons = new WrapPanel { Margin = new Thickness(0, 7, 0, 0) };
            tickerButtons.Children.Add(ActionButton("ВІДКРИТИ РЯДОК", null, (_, _) => { SaveDonations(); OpenUrl(_topUrl.Text); }));
            tickerButtons.Children.Add(ActionButton("СКОПІЮВАТИ URL", null, (_, _) => Copy(_topUrl.Text)));
            ticker.Children.Add(tickerButtons);

            var actionsCard = Card();
            var actions = new Grid();
            actions.ColumnDefinitions.Add(new ColumnDefinition());
            actions.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            actionsCard.Child = actions;
            root.Children.Add(actionsCard);
            _donationStatus = new TextBlock { Foreground = ResourceBrush("Green"), VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap };
            actions.Children.Add(_donationStatus);
            var actionButtons = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Right };
            Grid.SetColumn(actionButtons, 1);
            actions.Children.Add(actionButtons);
            actionButtons.Children.Add(ActionButton("ВІДКРИТИ DONATELLO API", null, (_, _) => _services.Windows.Show(() => new DonatelloWindow(), _window)));
            actionButtons.Children.Add(ActionButton("ЗБЕРЕГТИ ВІДЖЕТИ", null, (_, _) => SaveDonations()));

            LoadDonations();
        }

        private void LoadDonations()
        {
            var s = _services.Settings.Value;
            _goalTitle!.Text = s.DonationGoalTitle;
            _goalTarget!.Text = s.DonationGoalAmount.ToString("0.##", CultureInfo.InvariantCulture);
            _goalInitial!.Text = s.DonationGoalInitialAmount.ToString("0.##", CultureInfo.InvariantCulture);
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
            s.DonationGoalAmount = ParseDecimal(_goalTarget?.Text, 10000m, 1m, 1_000_000_000m);
            s.DonationGoalInitialAmount = ParseDecimal(_goalInitial?.Text, 0m, 0m, 1_000_000_000m);
            s.DonationGoalCurrency = string.IsNullOrWhiteSpace(_goalCurrency?.Text) ? "UAH" : _goalCurrency.Text.Trim().ToUpperInvariant();
            s.DonationGoalBarColor = NormalizeColor(_goalBarColor?.Text, "#FFD329");
            s.DonationGoalTextColor = NormalizeColor(_goalTextColor?.Text, "#F4F8FF");
            s.DonationGoalBackgroundColor = NormalizeColor(_goalBackgroundColor?.Text, "#06172A");
            s.DonationTopDonorCount = ParseInt(_topCount?.Text, 8, 1, 30);
            s.DonationTopDonorPeriod = _topPeriod?.SelectedIndex switch { 1 => "Stream", 2 => "Day", 3 => "Month", _ => "All" };
            s.DonationTickerSpeed = ParseDouble(_tickerSpeed?.Text, 70, 20, 250);
            s.DonationTickerTextColor = NormalizeColor(_tickerTextColor?.Text, "#FFD329");
            s.DonationTickerBackgroundColor = NormalizeColor(_tickerBackgroundColor?.Text, "#06172A");
            s.DonationTickerBackgroundOpacity = Math.Clamp(_tickerOpacity?.Value ?? 0.35, 0, 0.95);
            _services.Donations.GoalAmount = s.DonationGoalAmount;
            _services.Donations.GoalInitialAmount = s.DonationGoalInitialAmount;
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
            _donationStatus.Text = $"Поточний прогрес: {d.TotalAmount:N2} {d.GoalCurrency} / {d.GoalAmount:N2} {d.GoalCurrency} ({d.GoalProgress * 100:0}%)";
        }

        private void HookGlobalSaveButtons()
        {
            foreach (var button in Descendants<Button>(_window).Where(x =>
                         ContentText(x).Contains("Застосувати", StringComparison.OrdinalIgnoreCase) ||
                         ContentText(x).Contains("Зберегти", StringComparison.OrdinalIgnoreCase)))
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

        private TextBlock SectionTitle(string text)
        {
            var block = new TextBlock { Text = text };
            block.SetResourceReference(FrameworkElement.StyleProperty, "SettingsSectionTitle");
            return block;
        }

        private static StackPanel BuildHeader(string glyph, string title, string subtitle)
        {
            var panel = new StackPanel { Orientation = Orientation.Horizontal };
            panel.Children.Add(new TextBlock { Text = glyph, FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = 23, Width = 40, Foreground = ResourceBrush("Amber"), VerticalAlignment = VerticalAlignment.Center });
            var text = new StackPanel();
            text.Children.Add(new TextBlock { Text = title, FontSize = 15, FontWeight = FontWeights.SemiBold });
            text.Children.Add(new TextBlock { Text = subtitle, FontSize = 12, Opacity = 0.78 });
            panel.Children.Add(text);
            return panel;
        }

        private Button ActionButton(string text, string? platform, RoutedEventHandler click)
        {
            var button = new Button { Margin = new Thickness(3), MinHeight = 34, Padding = new Thickness(12, 5) };
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

        private static TextBlock Label(string text, double top = 8) => new()
        {
            Text = text,
            Foreground = ResourceBrush("Muted"),
            Margin = new Thickness(0, top, 0, 3)
        };

        private static ComboBox Combo(params string[] items)
        {
            var combo = new ComboBox();
            foreach (var item in items) combo.Items.Add(item);
            combo.SelectedIndex = 0;
            return combo;
        }

        private static TextBox AddTextField(Panel parent, string label)
        {
            parent.Children.Add(Label(label, 10));
            var box = new TextBox();
            parent.Children.Add(box);
            return box;
        }

        private static TextBox AddGridField(Grid parent, int column, string label, Thickness? margin = null)
        {
            var stack = new StackPanel { Margin = margin ?? new Thickness(0) };
            Grid.SetColumn(stack, column);
            parent.Children.Add(stack);
            stack.Children.Add(Label(label, 0));
            var box = new TextBox();
            stack.Children.Add(box);
            return box;
        }

        private static void AddGridControl(Grid parent, int column, string label, Control control)
        {
            var stack = new StackPanel();
            Grid.SetColumn(stack, column);
            parent.Children.Add(stack);
            stack.Children.Add(Label(label, 0));
            stack.Children.Add(control);
        }

        private static TextBox AddReadOnlyUrl(Panel parent, string label)
        {
            parent.Children.Add(Label(label, 10));
            var box = new TextBox { IsReadOnly = true };
            parent.Children.Add(box);
            return box;
        }

        private static string ContentText(Button button) => button.Content switch
        {
            string text => text,
            TextBlock text => text.Text,
            _ => string.Empty
        };

        private static bool HeaderContains(TabItem tab, string text) =>
            Descendants<TextBlock>(tab.Header as DependencyObject ?? tab)
                .Any(x => x.Text.Contains(text, StringComparison.OrdinalIgnoreCase));

        private T? FindNamed<T>(string name) where T : FrameworkElement =>
            Descendants<T>(_window).FirstOrDefault(x => string.Equals(x.Name, name, StringComparison.Ordinal));

        private static Brush ResourceBrush(string key) =>
            Application.Current.TryFindResource(key) as Brush ?? Brushes.White;

        private void OpenUrl(string target)
        {
            try { Process.Start(new ProcessStartInfo(target) { UseShellExecute = true }); }
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

        private static int ParseInt(string? text, int fallback, int min, int max) =>
            int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? Math.Clamp(value, min, max) : fallback;

        private static double ParseDouble(string? text, double fallback, double min, double max)
        {
            if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) &&
                !double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value)) return fallback;
            return Math.Clamp(value, min, max);
        }

        private static decimal ParseDecimal(string? text, decimal fallback, decimal min, decimal max)
        {
            if (!decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var value) &&
                !decimal.TryParse(text, NumberStyles.Number, CultureInfo.CurrentCulture, out value)) return fallback;
            return Math.Clamp(value, min, max);
        }

        private static string NormalizeColor(string? value, string fallback)
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
                for (var index = 0; index < VisualTreeHelper.GetChildrenCount(current); index++)
                    stack.Push(VisualTreeHelper.GetChild(current, index));
            }
            catch { }
            try
            {
                foreach (var child in LogicalTreeHelper.GetChildren(current).OfType<DependencyObject>())
                    stack.Push(child);
            }
            catch { }
        }
    }
}