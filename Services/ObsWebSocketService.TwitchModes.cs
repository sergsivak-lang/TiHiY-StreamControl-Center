using System.Diagnostics;
using System.Text.Json.Nodes;

namespace TiHiY.StreamControlCenter.Services;

public sealed partial class ObsWebSocketService
{
    public async Task<IReadOnlyList<string>> GetProfilesAsync()
    {
        var data = await RequestAsync("GetProfileList");
        var result = new List<string>();
        if (data["profiles"] is JsonArray profiles)
            foreach (var item in profiles)
            {
                var name = item?.GetValue<string>();
                if (!string.IsNullOrWhiteSpace(name)) result.Add(name);
            }
        return result;
    }

    public async Task<string> GetCurrentProfileAsync()
    {
        var data = await RequestAsync("GetProfileList");
        return data["currentProfileName"]?.GetValue<string>() ?? string.Empty;
    }

    public Task SetCurrentProfileAsync(string profileName)
    {
        if (string.IsNullOrWhiteSpace(profileName)) throw new ArgumentException("Назва OBS Profile не задана.", nameof(profileName));
        return RequestAsync("SetCurrentProfile", new JsonObject { ["profileName"] = profileName });
    }

    public async Task<(int baseWidth, int baseHeight, int outputWidth, int outputHeight, int fpsNumerator, int fpsDenominator)> GetVideoSettingsAsync()
    {
        var data = await RequestAsync("GetVideoSettings");
        return (
            data["baseWidth"]?.GetValue<int>() ?? 0,
            data["baseHeight"]?.GetValue<int>() ?? 0,
            data["outputWidth"]?.GetValue<int>() ?? 0,
            data["outputHeight"]?.GetValue<int>() ?? 0,
            data["fpsNumerator"]?.GetValue<int>() ?? 0,
            data["fpsDenominator"]?.GetValue<int>() ?? 1);
    }

    public async Task<string> GetObsVersionAsync()
    {
        var data = await RequestAsync("GetVersion");
        return data["obsVersion"]?.GetValue<string>() ?? string.Empty;
    }

    public async Task<bool> IsAitumVerticalInstalledAsync()
    {
        var candidates = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "obs-studio", "obs-plugins", "64bit", "aitum-vertical.dll"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "obs-studio", "obs-plugins", "64bit", "aitum-vertical.dll"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "obs-studio", "plugins", "aitum-vertical")
        };
        if (candidates.Any(File.Exists) || candidates.Any(Directory.Exists)) return true;
        try
        {
            var inputs = await GetInputsAsync();
            return inputs.Any(x => x.kind.Contains("aitum", StringComparison.OrdinalIgnoreCase) || x.kind.Contains("vertical", StringComparison.OrdinalIgnoreCase));
        }
        catch { return false; }
    }

    public async Task<(bool obsReady, bool twitchServiceDetected, string explanation)> CheckEnhancedBroadcastingAsync()
    {
        try
        {
            var version = await GetObsVersionAsync();
            var profiles = await GetProfilesAsync();
            var twitchProfile = profiles.Any(x => x.Contains("twitch", StringComparison.OrdinalIgnoreCase));
            var explanation = twitchProfile
                ? $"OBS {version}: Twitch-профіль знайдено. Enhanced Broadcasting залежить від підтримки OBS/Twitch і налаштувань каналу."
                : $"OBS {version}: Twitch-профіль не знайдено. Створіть окремий Twitch profile в OBS для Enhanced Broadcasting.";
            return (true, twitchProfile, explanation);
        }
        catch (Exception ex)
        {
            return (false, false, $"Перевірка Enhanced Broadcasting недоступна: {ex.Message}");
        }
    }

    public async Task<(bool found, string path)> FindObsExecutableAsync()
    {
        var candidates = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "obs-studio", "bin", "64bit", "obs64.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "obs-studio", "bin", "64bit", "obs64.exe")
        };
        var path = candidates.FirstOrDefault(File.Exists) ?? string.Empty;
        return (!string.IsNullOrWhiteSpace(path), path);
    }
}
