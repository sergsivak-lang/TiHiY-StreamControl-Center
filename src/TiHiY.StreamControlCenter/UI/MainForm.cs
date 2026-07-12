using System.ComponentModel;
using TiHiY.StreamControlCenter.Models;
using TiHiY.StreamControlCenter.Services;

namespace TiHiY.StreamControlCenter.UI;

public sealed class MainForm : Form
{
    private readonly SettingsService _settingsService;
    private readonly OverlayController _overlayController;
    private readonly AppSettings _settings;
    private readonly BindingList<ChatMessage> _chatMessages = [];
    private readonly BindingList<BotCommand> _commands;

    private readonly DataGridView _chatGrid = new();
    private readonly DataGridView _commandsGrid = new();
    private readonly TextBox _messageText = new();
    private readonly TextBox _authorText = new();
    private readonly ComboBox _platformCombo = new();
    private readonly TrackBar _opacityTrack = new();
    private readonly CheckBox _clickThroughCheck = new();
    private readonly CheckBox _topMostCheck = new();
    private readonly CheckBox _autoStartCheck = new();
    private readonly NumericUpDown _viewersInput = new();
    private readonly NumericUpDown _likesInput = new();
    private readonly NumericUpDown _overlayWidthInput = new();
    private readonly NumericUpDown _overlayHeightInput = new();
    private readonly Label _overlayStateLabel = new();
    private readonly RichTextBox _eventLog = new();
    private bool _closing;

    public MainForm(SettingsService settingsService, OverlayController overlayController)
    {
        _settingsService = settingsService;
        _overlayController = overlayController;
        _settings = _settingsService.Load();
        _commands = new BindingList<BotCommand>(_settings.BotCommands);

        Text = "TiHiY StreamControl Center";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(1080, 720);
        Size = new Size(1320, 850);
        BackColor = Theme.Window;
        ForeColor = Theme.Text;
        Font = new Font("Segoe UI", 9.5f);

        BuildInterface();
        LoadSettingsIntoControls();
        WireEvents();

        AddLog("Програму запущено.");

        Shown += (_, _) =>
        {
            if (_settings.OverlayAutoStart)
            {
                ShowOverlay();
            }
        };
    }

    private void BuildInterface()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 3,
            ColumnCount = 1,
            BackColor = Theme.Window,
            Padding = new Padding(14)
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 76));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        Controls.Add(root);

        root.Controls.Add(BuildHeader(), 0, 0);

        var tabs = new TabControl
        {
            Dock = DockStyle.Fill,
            Appearance = TabAppearance.Normal,
            ItemSize = new Size(150, 36),
            SizeMode = TabSizeMode.Fixed,
            Padding = new Point(16, 6)
        };

        tabs.TabPages.Add(BuildChatTab());
        tabs.TabPages.Add(BuildOverlayTab());
        tabs.TabPages.Add(BuildBotTab());
        tabs.TabPages.Add(BuildSettingsTab());
        root.Controls.Add(tabs, 0, 1);

        var footer = Theme.CreateLabel("TiHiY-DED • тестова версія Chat Core", true);
        footer.Dock = DockStyle.Fill;
        footer.TextAlign = ContentAlignment.MiddleRight;
        root.Controls.Add(footer, 0, 2);
    }

    private Control BuildHeader()
    {
        var panel = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Panel, Padding = new Padding(18, 12, 18, 12) };
        var title = new Label
        {
            Text = "TiHiY StreamControl Center",
            AutoSize = true,
            ForeColor = Theme.Text,
            Font = new Font("Segoe UI Semibold", 19f, FontStyle.Bold),
            Location = new Point(18, 10)
        };
        var subtitle = new Label
        {
            Text = "Twitch + YouTube • бот • команди • overlay • журнал подій",
            AutoSize = true,
            ForeColor = Theme.MutedText,
            Font = new Font("Segoe UI", 9.5f),
            Location = new Point(21, 45)
        };

        _overlayStateLabel.AutoSize = true;
        _overlayStateLabel.Font = new Font("Segoe UI Semibold", 10f, FontStyle.Bold);
        _overlayStateLabel.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _overlayStateLabel.Location = new Point(panel.Width - 190, 26);
        panel.Resize += (_, _) => _overlayStateLabel.Location = new Point(panel.ClientSize.Width - _overlayStateLabel.Width - 20, 26);

        panel.Controls.Add(title);
        panel.Controls.Add(subtitle);
        panel.Controls.Add(_overlayStateLabel);
        UpdateOverlayStateLabel();
        return panel;
    }

    private TabPage BuildChatTab()
    {
        var page = CreateTabPage("ЧАТ");
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Padding = new Padding(12) };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 72));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 28));
        page.Controls.Add(layout);

        ConfigureChatGrid();
        layout.Controls.Add(_chatGrid, 0, 0);

        var side = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 10, ColumnCount = 1, Padding = new Padding(12), BackColor = Theme.Panel };
        side.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        side.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        side.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        side.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        side.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        side.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        side.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        side.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        side.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        side.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        side.Controls.Add(Theme.CreateLabel("Тестове повідомлення"), 0, 0);
        _platformCombo.DropDownStyle = ComboBoxStyle.DropDownList;
        _platformCombo.Items.AddRange(["Twitch", "YouTube"]);
        _platformCombo.SelectedIndex = 0;
        _platformCombo.Dock = DockStyle.Top;
        side.Controls.Add(_platformCombo, 0, 1);

        _authorText.Text = "TiHiY-DED";
        _authorText.Dock = DockStyle.Top;
        side.Controls.Add(_authorText, 0, 2);

        _messageText.Multiline = true;
        _messageText.Height = 100;
        _messageText.Text = "Тестове повідомлення для overlay";
        _messageText.Dock = DockStyle.Top;
        side.Controls.Add(_messageText, 0, 3);

        var sendButton = Theme.CreateButton("Додати в чат");
        sendButton.Click += (_, _) => AddUserMessage();
        side.Controls.Add(sendButton, 0, 4);

        var botTestButton = Theme.CreateButton("Перевірити команду");
        botTestButton.Click += (_, _) => TestBotCommand();
        side.Controls.Add(botTestButton, 0, 5);

        side.Controls.Add(Theme.CreateLabel("Журнал подій", true), 0, 6);
        _eventLog.Dock = DockStyle.Fill;
        _eventLog.ReadOnly = true;
        _eventLog.BackColor = Color.FromArgb(12, 15, 20);
        _eventLog.ForeColor = Theme.MutedText;
        _eventLog.BorderStyle = BorderStyle.FixedSingle;
        side.Controls.Add(_eventLog, 0, 8);

        var clearButton = Theme.CreateButton("Очистити журнал");
        clearButton.Click += (_, _) => _eventLog.Clear();
        side.Controls.Add(clearButton, 0, 9);

        layout.Controls.Add(side, 1, 0);
        return page;
    }

    private TabPage BuildOverlayTab()
    {
        var page = CreateTabPage("OVERLAY");
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Padding = new Padding(16) };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 52));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 48));
        page.Controls.Add(root);

        var settingsPanel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 12, Padding = new Padding(18), BackColor = Theme.Panel };
        settingsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 48));
        settingsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 52));

        settingsPanel.Controls.Add(Theme.CreateLabel("Прозорість лише фону"), 0, 0);
        _opacityTrack.Minimum = 0;
        _opacityTrack.Maximum = 235;
        _opacityTrack.TickFrequency = 20;
        _opacityTrack.Dock = DockStyle.Fill;
        settingsPanel.Controls.Add(_opacityTrack, 1, 0);

        _clickThroughCheck.Text = "Пропускати кліки крізь overlay";
        _clickThroughCheck.AutoSize = true;
        _clickThroughCheck.ForeColor = Theme.Text;
        settingsPanel.Controls.Add(_clickThroughCheck, 0, 1);
        settingsPanel.SetColumnSpan(_clickThroughCheck, 2);

        _topMostCheck.Text = "Завжди поверх інших вікон";
        _topMostCheck.AutoSize = true;
        _topMostCheck.ForeColor = Theme.Text;
        settingsPanel.Controls.Add(_topMostCheck, 0, 2);
        settingsPanel.SetColumnSpan(_topMostCheck, 2);

        _autoStartCheck.Text = "Запускати overlay разом із програмою";
        _autoStartCheck.AutoSize = true;
        _autoStartCheck.ForeColor = Theme.Text;
        settingsPanel.Controls.Add(_autoStartCheck, 0, 3);
        settingsPanel.SetColumnSpan(_autoStartCheck, 2);

        ConfigureNumber(_overlayWidthInput, 320, 1600);
        ConfigureNumber(_overlayHeightInput, 300, 1400);
        settingsPanel.Controls.Add(Theme.CreateLabel("Ширина overlay"), 0, 4);
        settingsPanel.Controls.Add(_overlayWidthInput, 1, 4);
        settingsPanel.Controls.Add(Theme.CreateLabel("Висота overlay"), 0, 5);
        settingsPanel.Controls.Add(_overlayHeightInput, 1, 5);

        var showButton = Theme.CreateButton("Показати overlay");
        showButton.Click += (_, _) => ShowOverlay();
        var hideButton = Theme.CreateButton("Сховати overlay");
        hideButton.Click += (_, _) => _overlayController.Hide();
        var closeButton = Theme.CreateButton("Закрити overlay", true);
        closeButton.Click += (_, _) => _overlayController.Close();

        var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, FlowDirection = FlowDirection.LeftToRight };
        actions.Controls.Add(showButton);
        actions.Controls.Add(hideButton);
        actions.Controls.Add(closeButton);
        settingsPanel.Controls.Add(actions, 0, 7);
        settingsPanel.SetColumnSpan(actions, 2);

        var resetPosition = Theme.CreateButton("Повернути стандартний розмір");
        resetPosition.Click += (_, _) =>
        {
            _settings.OverlayX = 40;
            _settings.OverlayY = 80;
            _overlayWidthInput.Value = 560;
            _overlayHeightInput.Value = 760;
            ApplyOverlaySettings();
        };
        settingsPanel.Controls.Add(resetPosition, 0, 8);
        settingsPanel.SetColumnSpan(resetPosition, 2);

        var explanation = Theme.CreateLabel(
            "Важливо: повзунок змінює alpha лише темної підкладки. Текст, імена та нижні лічильники залишаються чіткими. Коли ввімкнено пропуск кліків, вимкнути його завжди можна тут — у головній програмі.",
            true);
        explanation.MaximumSize = new Size(620, 0);
        settingsPanel.Controls.Add(explanation, 0, 10);
        settingsPanel.SetColumnSpan(explanation, 2);

        root.Controls.Add(settingsPanel, 0, 0);
        root.Controls.Add(BuildStatisticsPanel(), 1, 0);
        return page;
    }

    private Control BuildStatisticsPanel()
    {
        var panel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 9, Padding = new Padding(18), BackColor = Theme.PanelAlt };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

        var title = Theme.CreateLabel("СТАТИСТИКА В OVERLAY");
        title.Font = new Font("Segoe UI Semibold", 13f, FontStyle.Bold);
        panel.Controls.Add(title, 0, 0);
        panel.SetColumnSpan(title, 2);

        ConfigureNumber(_viewersInput, 0, 10_000_000);
        ConfigureNumber(_likesInput, 0, 10_000_000);
        panel.Controls.Add(Theme.CreateLabel("Глядачі"), 0, 1);
        panel.Controls.Add(_viewersInput, 1, 1);
        panel.Controls.Add(Theme.CreateLabel("Лайки"), 0, 2);
        panel.Controls.Add(_likesInput, 1, 2);

        var applyButton = Theme.CreateButton("Оновити лічильники");
        applyButton.Click += (_, _) => ApplyOverlaySettings();
        panel.Controls.Add(applyButton, 0, 3);
        panel.SetColumnSpan(applyButton, 2);

        var plusViewers = Theme.CreateButton("+10 глядачів");
        plusViewers.Click += (_, _) => _viewersInput.Value = Math.Min(_viewersInput.Maximum, _viewersInput.Value + 10);
        var plusLikes = Theme.CreateButton("+25 лайків");
        plusLikes.Click += (_, _) => _likesInput.Value = Math.Min(_likesInput.Maximum, _likesInput.Value + 25);
        panel.Controls.Add(plusViewers, 0, 4);
        panel.Controls.Add(plusLikes, 1, 4);

        var note = Theme.CreateLabel("Поки Twitch і YouTube API не підключені, значення можна тестувати вручну. Наступний модуль замінить це реальними даними платформ.", true);
        note.MaximumSize = new Size(560, 0);
        panel.Controls.Add(note, 0, 6);
        panel.SetColumnSpan(note, 2);
        return panel;
    }

    private TabPage BuildBotTab()
    {
        var page = CreateTabPage("БОТ І КОМАНДИ");
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1, Padding = new Padding(14) };
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
        page.Controls.Add(layout);

        _commandsGrid.Dock = DockStyle.Fill;
        _commandsGrid.AutoGenerateColumns = false;
        _commandsGrid.BackgroundColor = Theme.Panel;
        _commandsGrid.BorderStyle = BorderStyle.None;
        _commandsGrid.RowHeadersVisible = false;
        _commandsGrid.AllowUserToAddRows = false;
        _commandsGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _commandsGrid.DataSource = _commands;
        _commandsGrid.Columns.Add(new DataGridViewCheckBoxColumn { DataPropertyName = nameof(BotCommand.Enabled), HeaderText = "Увімкнено", Width = 90 });
        _commandsGrid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(BotCommand.Trigger), HeaderText = "Команда", Width = 180 });
        _commandsGrid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(BotCommand.Response), HeaderText = "Відповідь", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
        layout.Controls.Add(_commandsGrid, 0, 0);

        var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight };
        var addButton = Theme.CreateButton("Додати команду");
        addButton.Click += (_, _) => _commands.Add(new BotCommand { Trigger = "!нова", Response = "Нова відповідь" });
        var removeButton = Theme.CreateButton("Видалити команду", true);
        removeButton.Click += (_, _) =>
        {
            if (_commandsGrid.CurrentRow?.DataBoundItem is BotCommand command)
            {
                _commands.Remove(command);
            }
        };
        var saveButton = Theme.CreateButton("Зберегти команди");
        saveButton.Click += (_, _) => SaveSettings();
        actions.Controls.Add(addButton);
        actions.Controls.Add(removeButton);
        actions.Controls.Add(saveButton);
        layout.Controls.Add(actions, 0, 1);
        return page;
    }

    private TabPage BuildSettingsTab()
    {
        var page = CreateTabPage("НАЛАШТУВАННЯ");
        var layout = new TableLayoutPanel { Dock = DockStyle.Top, ColumnCount = 1, RowCount = 5, Padding = new Padding(24), AutoSize = true };
        layout.Controls.Add(Theme.CreateLabel("ЛОКАЛЬНЕ ЗБЕРЕЖЕННЯ"), 0, 0);
        layout.Controls.Add(Theme.CreateLabel("Налаштування зберігаються у %AppData%\\TiHiY\\StreamControlCenter\\settings.json", true), 0, 1);
        var save = Theme.CreateButton("Зберегти всі налаштування");
        save.Click += (_, _) => SaveSettings();
        layout.Controls.Add(save, 0, 2);
        var reset = Theme.CreateButton("Скинути тестові повідомлення", true);
        reset.Click += (_, _) =>
        {
            _chatMessages.Clear();
            AddLog("Список тестових повідомлень очищено.");
        };
        layout.Controls.Add(reset, 0, 3);
        page.Controls.Add(layout);
        return page;
    }

    private static TabPage CreateTabPage(string title)
    {
        return new TabPage(title) { BackColor = Theme.Window, ForeColor = Theme.Text, Padding = new Padding(4) };
    }

    private void ConfigureChatGrid()
    {
        _chatGrid.Dock = DockStyle.Fill;
        _chatGrid.BackgroundColor = Theme.Panel;
        _chatGrid.BorderStyle = BorderStyle.None;
        _chatGrid.RowHeadersVisible = false;
        _chatGrid.AllowUserToAddRows = false;
        _chatGrid.AllowUserToDeleteRows = false;
        _chatGrid.ReadOnly = true;
        _chatGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _chatGrid.AutoGenerateColumns = false;
        _chatGrid.DataSource = _chatMessages;
        _chatGrid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(ChatMessage.Timestamp), HeaderText = "Час", Width = 84, DefaultCellStyle = new DataGridViewCellStyle { Format = "HH:mm:ss" } });
        _chatGrid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(ChatMessage.Platform), HeaderText = "Платформа", Width = 110 });
        _chatGrid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(ChatMessage.Author), HeaderText = "Автор", Width = 170 });
        _chatGrid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(ChatMessage.Text), HeaderText = "Повідомлення", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
    }

    private static void ConfigureNumber(NumericUpDown control, decimal minimum, decimal maximum)
    {
        control.Minimum = minimum;
        control.Maximum = maximum;
        control.ThousandsSeparator = true;
        control.Dock = DockStyle.Fill;
        control.BackColor = Theme.PanelAlt;
        control.ForeColor = Theme.Text;
    }

    private void LoadSettingsIntoControls()
    {
        _opacityTrack.Value = Math.Clamp(_settings.OverlayBackgroundOpacity, _opacityTrack.Minimum, _opacityTrack.Maximum);
        _clickThroughCheck.Checked = _settings.OverlayClickThrough;
        _topMostCheck.Checked = _settings.OverlayTopMost;
        _autoStartCheck.Checked = _settings.OverlayAutoStart;
        _viewersInput.Value = Math.Clamp(_settings.ViewerCount, (int)_viewersInput.Minimum, (int)_viewersInput.Maximum);
        _likesInput.Value = Math.Clamp(_settings.LikeCount, (int)_likesInput.Minimum, (int)_likesInput.Maximum);
        _overlayWidthInput.Value = Math.Clamp(_settings.OverlayWidth, (int)_overlayWidthInput.Minimum, (int)_overlayWidthInput.Maximum);
        _overlayHeightInput.Value = Math.Clamp(_settings.OverlayHeight, (int)_overlayHeightInput.Minimum, (int)_overlayHeightInput.Maximum);
    }

    private void WireEvents()
    {
        _overlayController.VisibilityChanged += (_, _) => UpdateOverlayStateLabel();
        _opacityTrack.ValueChanged += (_, _) => ApplyOverlaySettings();
        _clickThroughCheck.CheckedChanged += (_, _) => ApplyOverlaySettings();
        _topMostCheck.CheckedChanged += (_, _) => ApplyOverlaySettings();
        _autoStartCheck.CheckedChanged += (_, _) => SaveSettings();
        _viewersInput.ValueChanged += (_, _) => ApplyOverlaySettings();
        _likesInput.ValueChanged += (_, _) => ApplyOverlaySettings();
        _overlayWidthInput.ValueChanged += (_, _) => ApplyOverlaySettings();
        _overlayHeightInput.ValueChanged += (_, _) => ApplyOverlaySettings();

        FormClosing += MainFormOnFormClosing;
        FormClosed += (_, _) => _overlayController.Close();
    }

    private void AddUserMessage()
    {
        var text = _messageText.Text.Trim();
        if (text.Length == 0)
        {
            return;
        }

        var author = string.IsNullOrWhiteSpace(_authorText.Text) ? "Глядач" : _authorText.Text.Trim();
        var platform = _platformCombo.SelectedItem?.ToString() ?? "Twitch";
        var message = new ChatMessage(DateTime.Now, platform, author, text);
        AddMessage(message);
        ProcessBotCommand(message);
    }

    private void TestBotCommand()
    {
        if (!_messageText.Text.TrimStart().StartsWith('!'))
        {
            _messageText.Text = "!youtube";
        }

        AddUserMessage();
    }

    private void ProcessBotCommand(ChatMessage message)
    {
        var trigger = message.Text.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        if (trigger is null)
        {
            return;
        }

        var command = _commands.FirstOrDefault(c =>
            c.Enabled && c.Trigger.Equals(trigger, StringComparison.OrdinalIgnoreCase));

        if (command is null)
        {
            return;
        }

        var botMessage = new ChatMessage(DateTime.Now, message.Platform, "TiHiY Bot", command.Response, true);
        AddMessage(botMessage);
        AddLog($"Бот виконав команду {command.Trigger}.");
    }

    private void AddMessage(ChatMessage message)
    {
        _chatMessages.Add(message);
        while (_chatMessages.Count > 500)
        {
            _chatMessages.RemoveAt(0);
        }

        if (!_overlayController.IsVisible)
        {
            ShowOverlay();
        }

        _overlayController.AddMessage(message);
        AddLog($"{message.Platform}: {message.Author} — {message.Text}");
    }

    private void ShowOverlay()
    {
        ReadControlsIntoSettings();
        _overlayController.Show(_settings);
        AddLog("Overlay показано.");
    }

    private void ApplyOverlaySettings()
    {
        if (_closing)
        {
            return;
        }

        ReadControlsIntoSettings();
        _overlayController.ApplySettings(_settings);
        SaveSettings();
    }

    private void ReadControlsIntoSettings()
    {
        _settings.OverlayBackgroundOpacity = _opacityTrack.Value;
        _settings.OverlayClickThrough = _clickThroughCheck.Checked;
        _settings.OverlayTopMost = _topMostCheck.Checked;
        _settings.OverlayAutoStart = _autoStartCheck.Checked;
        _settings.ViewerCount = (int)_viewersInput.Value;
        _settings.LikeCount = (int)_likesInput.Value;
        _settings.OverlayWidth = (int)_overlayWidthInput.Value;
        _settings.OverlayHeight = (int)_overlayHeightInput.Value;
        _settings.BotCommands = [.. _commands];

        var bounds = _overlayController.GetBounds();
        if (bounds.HasValue)
        {
            _settings.OverlayX = bounds.Value.X;
            _settings.OverlayY = bounds.Value.Y;
        }
    }

    private void SaveSettings()
    {
        if (_closing)
        {
            return;
        }

        ReadControlsIntoSettings();
        _settingsService.Save(_settings);
    }

    private void UpdateOverlayStateLabel()
    {
        _overlayStateLabel.Text = _overlayController.IsVisible ? "● OVERLAY ПРАЦЮЄ" : "○ OVERLAY ВИМКНЕНО";
        _overlayStateLabel.ForeColor = _overlayController.IsVisible ? Theme.Accent : Theme.MutedText;
    }

    private void AddLog(string text)
    {
        _eventLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {text}{Environment.NewLine}");
        _eventLog.SelectionStart = _eventLog.TextLength;
        _eventLog.ScrollToCaret();
    }

    private void MainFormOnFormClosing(object? sender, FormClosingEventArgs e)
    {
        if (_closing)
        {
            return;
        }

        _closing = true;
        ReadControlsIntoSettings();
        _settingsService.Save(_settings);

        // Критичне виправлення: overlay закривається до завершення головної програми.
        _overlayController.Close();
    }
}
