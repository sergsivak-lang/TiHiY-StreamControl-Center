using System.Text.Json;
using TiHiY.StreamControlCenter.Models;

namespace TiHiY.StreamControlCenter.Services;

public sealed class SettingsService
{
    private readonly string _folder;
    private readonly string _file;
    private readonly JsonSerializerOptions _options = new() { WriteIndented = true };
    private readonly object _gate = new();

    public SettingsService()
    {
        _folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TiHiY", "StreamControlCenter");
        _file = Path.Combine(_folder, "settings.json");
    }

    public string Folder => _folder;

    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(_file)) return CreateDefaults();
            var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(_file), _options) ?? CreateDefaults();
            settings.SelectedAudioInputs ??= new List<string>();
            settings.DonatelloRecentEventIds ??= new List<string>();
            settings.DonatelloSubscriberPayments ??= new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            if (settings.SelectedAudioInputs.Count == 0)
                settings.SelectedAudioInputs = new List<string> { "Гра звук", "Медіа", "Мікро", "Чат" };
            return settings;
        }
        catch
        {
            return CreateDefaults();
        }
    }

    public void Save(AppSettings settings)
    {
        lock (_gate)
        {
            Directory.CreateDirectory(_folder);
            var temp = _file + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(settings, _options));
            File.Move(temp, _file, true);
        }
    }

    private static AppSettings CreateDefaults()
    {
        var result = new AppSettings();
        result.SelectedAudioInputs.AddRange(new[] { "Гра звук", "Медіа", "Мікро", "Чат" });
        result.BotCommands.Add(new BotCommand { Name = "!song", Reply = "Зараз грає: {song}", Target = "Twitch + YouTube", CooldownSeconds = 10 });
        result.ScheduledNotices.Add(new ScheduledNotice
        {
            Name = "Підтримка каналу",
            Text = "Підтримати канал: donatello.to/TiHiY-DED",
            Target = "Twitch + YouTube",
            IntervalMinutes = 25,
            MinimumChatMessages = 10,
            Enabled = false,
            NextRun = DateTime.Now.AddMinutes(25)
        });
        return result;
    }
}
