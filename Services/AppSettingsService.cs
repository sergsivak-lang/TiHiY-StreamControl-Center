using System.Text.Json;

namespace TiHiY.StreamControlCenter.Services;

public sealed record AppSettings(string ObsUrl = "ws://127.0.0.1:4455", double OverlayOpacity = 0.70);

public static class AppSettingsService
{
    private static readonly string Folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TiHiY StreamControl Center");
    private static readonly string FilePath = Path.Combine(Folder, "settings.json");

    public static AppSettings Load()
    {
        try { return File.Exists(FilePath) ? JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath)) ?? new() : new(); }
        catch { return new(); }
    }

    public static void Save(AppSettings settings)
    {
        Directory.CreateDirectory(Folder);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
    }
}
