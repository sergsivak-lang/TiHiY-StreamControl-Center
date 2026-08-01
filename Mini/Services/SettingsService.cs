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
        _folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TiHiY", "StreamControlMini");
        _file = Path.Combine(_folder, "settings.json");
    }

    public string Folder => _folder;

    public AppSettings Load()
    {
        try
        {
            if (File.Exists(_file))
                return Normalize(JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(_file), _options) ?? CreateDefaults());

            var legacy = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TiHiY", "StreamControlCenter", "settings.json");
            if (File.Exists(legacy))
            {
                var imported = Normalize(JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(legacy), _options) ?? CreateDefaults());
                imported.AutoNoticesEnabled = false;
                Save(imported);
                return imported;
            }
        }
        catch { }

        var defaults = CreateDefaults();
        Save(defaults);
        return defaults;
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

    private static AppSettings Normalize(AppSettings settings)
    {
        settings.SelectedAudioInputs ??= new();
        settings.PinnedAudioInputs ??= new();
        settings.ScheduledNotices ??= new();
        settings.BotCommands ??= new();
        settings.MusicPlaylistPaths ??= new();
        settings.WindowPlacements ??= new(StringComparer.OrdinalIgnoreCase);
        settings.DonatelloRecentEventIds ??= new();
        settings.DonatelloSubscriberPayments ??= new(StringComparer.OrdinalIgnoreCase);
        return settings;
    }

    private static AppSettings CreateDefaults() => Normalize(new AppSettings
    {
        TwitchChannelName = "tihiy_ded",
        YouTubeChannelName = "TiHiY-DED",
        TwitchAutoConnect = true,
        YouTubeAutoConnect = true,
        DonatelloEnabled = true,
        DonatelloAutoStart = true,
        AutoNoticesEnabled = false
    });
}
