namespace TiHiY.StreamControlCenter.Services;

public static class StalkerTextureThemeManager
{
    private static readonly Uri TextureSourceUri = new(
        "pack://application:,,,/TiHiY.StreamControlCenter;component/Themes/StalkerTextureTheme.xaml",
        UriKind.Absolute);
    private static readonly Uri WindowSkinSourceUri = new(
        "pack://application:,,,/TiHiY.StreamControlCenter;component/Themes/StalkerWindowSkin.xaml",
        UriKind.Absolute);
    private static readonly Uri ControlSourceUri = new(
        "pack://application:,,,/TiHiY.StreamControlCenter;component/Themes/StalkerControlOverrides.xaml",
        UriKind.Absolute);

    private static ResourceDictionary? _textureDictionary;
    private static ResourceDictionary? _windowSkinDictionary;
    private static ResourceDictionary? _controlDictionary;

    public static void ApplyFor(string? themeName)
    {
        var resources = Application.Current?.Resources;
        if (resources is null) return;

        var shouldEnable = string.Equals(themeName, "Сталкер", StringComparison.OrdinalIgnoreCase);

        if (!shouldEnable)
        {
            Remove(resources, ref _controlDictionary);
            Remove(resources, ref _windowSkinDictionary);
            Remove(resources, ref _textureDictionary);
            return;
        }

        Add(resources, ref _textureDictionary, TextureSourceUri);
        Add(resources, ref _windowSkinDictionary, WindowSkinSourceUri);
        Add(resources, ref _controlDictionary, ControlSourceUri);

        // ThemeService stores these four semantic brushes directly in the primary
        // application dictionary. Primary resources win over merged dictionaries,
        // therefore the textured variants must be installed there as well.
        resources["WindowGradient"] = _windowSkinDictionary!["WindowGradient"];
        resources["PanelGradient"] = _textureDictionary!["PanelGradient"];
        resources["ButtonGradient"] = _textureDictionary["BlueButtonBackground"];
        resources["AmberButtonGradient"] = _textureDictionary["AmberButtonBackground"];
    }

    private static void Add(ResourceDictionary resources, ref ResourceDictionary? dictionary, Uri source)
    {
        if (dictionary is not null && resources.MergedDictionaries.Contains(dictionary)) return;
        dictionary = new ResourceDictionary { Source = source };
        resources.MergedDictionaries.Add(dictionary);
    }

    private static void Remove(ResourceDictionary resources, ref ResourceDictionary? dictionary)
    {
        if (dictionary is null) return;
        resources.MergedDictionaries.Remove(dictionary);
        dictionary = null;
    }
}
