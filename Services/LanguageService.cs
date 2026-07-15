using System.Globalization;
using TiHiY.StreamControlCenter.Models;

namespace TiHiY.StreamControlCenter.Services;

public sealed class LanguageService
{
    public sealed record LanguageInfo(string Code, string DisplayName, string EnglishName);

    private readonly AppSettingsAccessor _settings;
    private readonly SettingsService _settingsService;

    public IReadOnlyList<LanguageInfo> Languages { get; } =
    [
        new("uk-UA", "Українська", "Ukrainian"),
        new("en-US", "English", "English")
    ];

    public string CurrentLanguage => Normalize(_settings.Value.UiLanguage);
    public event EventHandler? LanguageChanged;

    public LanguageService(AppSettingsAccessor settings, SettingsService settingsService)
    {
        _settings = settings;
        _settingsService = settingsService;
    }

    public void ApplySavedLanguage() => Apply(_settings.Value.UiLanguage, save: false);

    public void Apply(string? languageCode, bool save = true)
    {
        var code = Normalize(languageCode);
        var application = Application.Current;
        if (application is null) return;

        var dictionaries = application.Resources.MergedDictionaries;
        var current = dictionaries.FirstOrDefault(IsLanguageDictionary);
        var replacement = new ResourceDictionary
        {
            Source = new Uri($"/TiHiY.StreamControlCenter;component/Localization/Strings.{code}.xaml", UriKind.RelativeOrAbsolute)
        };

        if (current is null)
            dictionaries.Add(replacement);
        else
        {
            var index = dictionaries.IndexOf(current);
            dictionaries[index] = replacement;
        }

        var culture = CultureInfo.GetCultureInfo(code);
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;

        _settings.Value.UiLanguage = code;
        if (save)
            _settingsService.Save(_settings.Value);

        LanguageChanged?.Invoke(this, EventArgs.Empty);
    }

    private static bool IsLanguageDictionary(ResourceDictionary dictionary)
    {
        if (dictionary.Contains("LanguageDictionaryMarker")) return true;
        var source = dictionary.Source?.OriginalString ?? string.Empty;
        return source.Contains("Localization/Strings.", StringComparison.OrdinalIgnoreCase);
    }

    private static string Normalize(string? languageCode) =>
        string.Equals(languageCode, "en-US", StringComparison.OrdinalIgnoreCase) ? "en-US" : "uk-UA";
}
