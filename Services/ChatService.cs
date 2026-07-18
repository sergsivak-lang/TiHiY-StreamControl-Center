using TiHiY.StreamControlCenter.Models;

namespace TiHiY.StreamControlCenter.Services;

public sealed class ChatService
{
    private readonly AppSettingsAccessor _settings;
    private readonly SettingsService _settingsService;
    private readonly AppLogger _logger;
    private readonly DispatcherTimer _timer;
    private readonly Dictionary<string, DateTime> _commandLastRun = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _seenMessageIds = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _lastMessageByAuthor = new(StringComparer.OrdinalIgnoreCase);
    private int _messagesSinceLastNotice;
    public Func<string>? SongProvider { get; set; }
    public Func<string, string, Task>? MessageSender { get; set; }

    public ObservableCollection<ChatMessage> Messages { get; } = new();
    public ObservableCollection<ScheduledNotice> Notices { get; } = new();
    public ObservableCollection<BotCommand> Commands { get; } = new();
    public event EventHandler<ChatMessage>? MessageAdded;

    public ChatService(AppSettingsAccessor settings, SettingsService settingsService, AppLogger logger)
    {
        _settings = settings;
        _settingsService = settingsService;
        _logger = logger;
        foreach (var command in settings.Value.BotCommands) Commands.Add(command);
        foreach (var notice in settings.Value.ScheduledNotices)
        {
            if (notice.NextRun <= DateTime.Now)
                notice.NextRun = DateTime.Now.AddMinutes(Math.Max(1, notice.IntervalMinutes));
            Notices.Add(notice);
        }
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += Timer_Tick;
    }

    public void Start()
    {
        if (!_timer.IsEnabled) _timer.Start();
    }

    public void Stop() => _timer.Stop();

    public void AddIncoming(string platform, string user, string text, string role) =>
        AddIncoming(new ChatMessage { Platform = platform, User = user, Text = text, Role = role });

    public void AddIncoming(ChatMessage incoming)
    {
        if (!string.IsNullOrWhiteSpace(incoming.ExternalId) && !_seenMessageIds.Add(incoming.ExternalId)) return;
        var message = BuildMessage(incoming.Platform, incoming.User, incoming.Text, incoming.Role);
        message.Time = incoming.Time;
        message.ExternalId = incoming.ExternalId;
        message.AuthorId = incoming.AuthorId;
        message.Emotes = incoming.Emotes
            .OrderBy(x => x.Start)
            .ThenByDescending(x => x.Length)
            .ToList();
        Messages.Add(message);
        while (Messages.Count > 300) Messages.RemoveAt(0);
        _messagesSinceLastNotice++;
        MessageAdded?.Invoke(this, message);
        if (!string.Equals(message.Role, "Bot", StringComparison.OrdinalIgnoreCase))
            TryExecuteCommand(message);
    }

    private void TryExecuteCommand(ChatMessage message)
    {
        var settings = _settings.Value;
        if (!settings.ChatBotEnabled || ShouldSuppressBot(message)) return;

        var text = message.Text.Trim();
        var command = Commands.FirstOrDefault(c => c.Enabled && string.Equals(c.Name, text, StringComparison.OrdinalIgnoreCase));
        if (command is null) return;
        if (!(command.Target.Contains(message.Platform, StringComparison.OrdinalIgnoreCase) ||
              command.Target.Contains("Twitch + YouTube", StringComparison.OrdinalIgnoreCase))) return;

        var now = DateTime.Now;
        if (_commandLastRun.TryGetValue(command.Name, out var last) &&
            (now - last).TotalSeconds < command.CooldownSeconds) return;

        _commandLastRun[command.Name] = now;
        var reply = command.Reply.Replace("{song}", SongProvider?.Invoke() ?? "нічого", StringComparison.OrdinalIgnoreCase);
        var target = string.IsNullOrWhiteSpace(command.Target) ? settings.ChatBotDefaultTarget : command.Target;
        _ = SendBotReplyAsync(reply, target, settings.ChatBotResponseDelayMilliseconds);
        _logger.Info($"Команда чат-бота виконана: {command.Name}");
    }

    private bool ShouldSuppressBot(ChatMessage message)
    {
        var settings = _settings.Value;
        if (!settings.ChatBotSpamProtectionEnabled) return false;
        var text = message.Text?.Trim() ?? string.Empty;
        if (text.Length == 0) return true;

        if (settings.ChatBotBlockLinks &&
            (text.Contains("http://", StringComparison.OrdinalIgnoreCase) ||
             text.Contains("https://", StringComparison.OrdinalIgnoreCase) ||
             text.Contains("www.", StringComparison.OrdinalIgnoreCase)))
        {
            _logger.Info($"Чат-бот: посилання проігноровано ({message.User}).");
            return true;
        }

        if (settings.ChatBotBlockCaps)
        {
            var letters = text.Where(char.IsLetter).ToArray();
            if (letters.Length >= 8 && letters.Count(char.IsUpper) >= letters.Length * 0.75)
            {
                _logger.Info($"Чат-бот: повідомлення CAPS проігноровано ({message.User}).");
                return true;
            }
        }

        if (settings.ChatBotBlockRepeats)
        {
            var authorKey = string.IsNullOrWhiteSpace(message.AuthorId) ? $"{message.Platform}:{message.User}" : message.AuthorId;
            if (_lastMessageByAuthor.TryGetValue(authorKey, out var previous) &&
                string.Equals(previous, text, StringComparison.OrdinalIgnoreCase))
            {
                _logger.Info($"Чат-бот: повтор повідомлення проігноровано ({message.User}).");
                return true;
            }
            _lastMessageByAuthor[authorKey] = text;
        }

        var blockedWords = settings.ChatBotBlockedWords
            .Split(new[] { ',', ';', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (blockedWords.Any(word => text.Contains(word, StringComparison.OrdinalIgnoreCase)))
        {
            _logger.Info($"Чат-бот: повідомлення з чорного списку проігноровано ({message.User}).");
            return true;
        }

        return false;
    }

    private async Task SendBotReplyAsync(string text, string target, int delayMilliseconds)
    {
        try
        {
            if (delayMilliseconds > 0)
                await Task.Delay(Math.Clamp(delayMilliseconds, 0, 10000));
            await SendManualAsync(text, target);
        }
        catch (Exception ex)
        {
            _logger.Error($"Чат-бот: надсилання в {target}", ex);
        }
    }

    public void SendManual(string text, string target) => _ = SendManualSilentlyAsync(text, target);

    private async Task SendManualSilentlyAsync(string text, string target)
    {
        try { await SendManualAsync(text, target); }
        catch { }
    }

    public async Task SendManualAsync(string text, string target)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        if (MessageSender is null) throw new InvalidOperationException("Канали чату ще не налаштовані.");

        try
        {
            await MessageSender(text.Trim(), target);
            _logger.Info($"Повідомлення надіслано: {target}");
        }
        catch (Exception ex)
        {
            _logger.Error($"Надсилання в {target}", ex);
            throw;
        }
    }

    public void SaveAll()
    {
        _settings.Value.ScheduledNotices = Notices.ToList();
        _settings.Value.BotCommands = Commands.ToList();
        _settingsService.Save(_settings.Value);
    }

    private void Timer_Tick(object? sender, EventArgs e)
    {
        var settings = _settings.Value;
        if (!settings.ChatBotEnabled || !settings.ChatBotAutoStart || !settings.AutoNoticesEnabled) return;
        var now = DateTime.Now;
        foreach (var notice in Notices.Where(n => n.Enabled && n.NextRun <= now).ToList())
        {
            if (_messagesSinceLastNotice < notice.MinimumChatMessages)
            {
                notice.NextRun = now.AddMinutes(1);
                continue;
            }
            var target = string.IsNullOrWhiteSpace(notice.Target) ? settings.ChatBotDefaultTarget : notice.Target;
            _ = SendBotReplyAsync(notice.Text, target, settings.ChatBotResponseDelayMilliseconds);
            notice.LastSent = now;
            notice.NextRun = now.AddMinutes(Math.Max(1, notice.IntervalMinutes));
            _messagesSinceLastNotice = 0;
            _logger.Info($"Автоповідомлення чат-бота: {notice.Name} → {target}");
        }
    }

    private ChatMessage BuildMessage(string platform, string user, string text, string role)
    {
        var highlighted = GetHighlightWords().Any(word => text.Contains(word, StringComparison.OrdinalIgnoreCase));
        var foreground = role.ToLowerInvariant() switch
        {
            "owner" => _settings.Value.OwnerColor,
            "moderator" => _settings.Value.ModeratorColor,
            "subscriber" or "member" => _settings.Value.SubscriberColor,
            "vip" => _settings.Value.VipColor,
            "bot" => _settings.Value.BotColor,
            "donor" => _settings.Value.OwnerColor,
            _ => _settings.Value.ViewerColor
        };
        return new ChatMessage
        {
            Platform = platform,
            User = user,
            Text = text,
            Role = role,
            IsHighlighted = highlighted,
            Foreground = highlighted ? _settings.Value.HighlightTextColor : foreground,
            Background = highlighted ? _settings.Value.HighlightBackgroundColor : "Transparent"
        };
    }

    private IEnumerable<string> GetHighlightWords() =>
        _settings.Value.HighlightWords.Split(new[] { ',', ';', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
