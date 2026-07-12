using System.Collections.ObjectModel;
using System.Text.Json;
using TiHiY.StreamControlCenter.Models;

namespace TiHiY.StreamControlCenter.Services;

public sealed class BotService
{
    private readonly Dictionary<string, DateTime> _lastRun = new(StringComparer.OrdinalIgnoreCase);
    private static readonly string Folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TiHiY StreamControl Center");
    private static readonly string FilePath = Path.Combine(Folder, "commands.json");
    public ObservableCollection<BotCommand> Commands { get; } = new();

    public BotService()
    {
        foreach (var command in Load()) Commands.Add(command);
        if (Commands.Count == 0)
        {
            Commands.Add(new() { Name = "!discord", Reply = "Наш Discord: додайте своє посилання" });
            Commands.Add(new() { Name = "!донат", Reply = "Підтримати канал: donatello.to/TiHiY-DED" });
            Commands.Add(new() { Name = "!гра", Reply = "Сьогодні граємо у Star Citizen" });
            Commands.Add(new() { Name = "!корабель", Reply = "Поточний корабель: налаштуйте відповідь", Platform = "Twitch" });
            Commands.Add(new() { Name = "!ютуб", Reply = "YouTube: TiHiY-DED", Platform = "Twitch" });
            Save();
        }
    }

    public string? TryExecute(string message, string platform)
    {
        var token = message.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        if (string.IsNullOrWhiteSpace(token)) return null;
        var command = Commands.FirstOrDefault(x => x.Enabled && x.Name.Equals(token, StringComparison.OrdinalIgnoreCase) && PlatformMatches(x.Platform, platform));
        if (command is null) return null;
        if (_lastRun.TryGetValue(command.Name, out var last) && DateTime.UtcNow - last < TimeSpan.FromSeconds(command.CooldownSeconds))
            return $"Команда {command.Name} буде доступна через {Math.Ceiling((TimeSpan.FromSeconds(command.CooldownSeconds) - (DateTime.UtcNow - last)).TotalSeconds)} с.";
        _lastRun[command.Name] = DateTime.UtcNow;
        return command.Reply;
    }

    public void Save()
    {
        Directory.CreateDirectory(Folder);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(Commands, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static IEnumerable<BotCommand> Load()
    {
        try { return File.Exists(FilePath) ? JsonSerializer.Deserialize<List<BotCommand>>(File.ReadAllText(FilePath)) ?? [] : []; }
        catch { return []; }
    }

    private static bool PlatformMatches(string configured, string actual) => configured.Contains("+", StringComparison.Ordinal) || configured.Equals(actual, StringComparison.OrdinalIgnoreCase) || actual.Equals("Local", StringComparison.OrdinalIgnoreCase);
}
