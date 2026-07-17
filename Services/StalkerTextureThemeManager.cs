namespace TiHiY.StreamControlCenter.Services;

public static class StalkerTextureThemeManager
{
    private static readonly Uri TextureSourceUri = new(
        "pack://application:,,,/TiHiY.StreamControlCenter;component/Themes/StalkerTextureTheme.xaml",
        UriKind.Absolute);
    private static readonly Uri ControlSourceUri = new(
        "pack://application:,,,/TiHiY.StreamControlCenter;component/Themes/StalkerControlOverrides.xaml",
        UriKind.Absolute);

    private static ResourceDictionary? _textureDictionary;
    private static ResourceDictionary? _controlDictionary;

    public static void ApplyFor(string? themeName)
    {
        var resources = Application.Current?.Resources;
        if (resources is null) return;

        var shouldEnable = string.Equals(themeName, "Сталкер", StringComparison.OrdinalIgnoreCase);

        if (!shouldEnable)
        {
            Remove(resources, ref _controlDictionary);
            Remove(resources, ref _textureDictionary);
            return;
        }

        if (_textureDictionary is null || !resources.MergedDictionaries.Contains(_textureDictionary))
        {
            _textureDictionary = new ResourceDictionary { Source = TextureSourceUri };
            resources.MergedDictionaries.Add(_textureDictionary);
        }

        if (_controlDictionary is null || !resources.MergedDictionaries.Contains(_controlDictionary))
        {
            _controlDictionary = new ResourceDictionary { Source = ControlSourceUri };
            resources.MergedDictionaries.Add(_controlDictionary);
        }
    }

    private static void Remove(ResourceDictionary resources, ref ResourceDictionary? dictionary)
    {
        if (dictionary is null) return;
        resources.MergedDictionaries.Remove(dictionary);
        dictionary = null;
    }
}
