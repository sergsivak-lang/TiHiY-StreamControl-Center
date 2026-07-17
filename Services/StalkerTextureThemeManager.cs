namespace TiHiY.StreamControlCenter.Services;

public static class StalkerTextureThemeManager
{
    private static readonly Uri SourceUri = new(
        "pack://application:,,,/TiHiY.StreamControlCenter;component/Themes/StalkerTextureTheme.xaml",
        UriKind.Absolute);

    private static ResourceDictionary? _activeDictionary;

    public static void ApplyFor(string? themeName)
    {
        var resources = Application.Current?.Resources;
        if (resources is null) return;

        var shouldEnable = string.Equals(themeName, "Сталкер", StringComparison.OrdinalIgnoreCase);

        if (!shouldEnable)
        {
            if (_activeDictionary is not null)
            {
                resources.MergedDictionaries.Remove(_activeDictionary);
                _activeDictionary = null;
            }
            return;
        }

        if (_activeDictionary is not null && resources.MergedDictionaries.Contains(_activeDictionary))
            return;

        _activeDictionary = new ResourceDictionary { Source = SourceUri };
        resources.MergedDictionaries.Add(_activeDictionary);
    }
}
