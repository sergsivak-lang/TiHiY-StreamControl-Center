namespace TiHiY.StreamControlCenter.Services;

public static class StalkerTextureThemeManager
{
    private static readonly Uri TextureSourceUri = new(
        "pack://application:,,,/TiHiY.StreamControlCenter;component/Themes/StalkerTextureRuntime.xaml",
        UriKind.Absolute);
    private static readonly Uri WindowSkinSourceUri = new(
        "pack://application:,,,/TiHiY.StreamControlCenter;component/Themes/StalkerWindowRuntime.xaml",
        UriKind.Absolute);
    private static readonly Uri ControlSourceUri = new(
        "pack://application:,,,/TiHiY.StreamControlCenter;component/Themes/StalkerControlOverrides.xaml",
        UriKind.Absolute);

    private static ResourceDictionary? _textureDictionary;
    private static ResourceDictionary? _windowSkinDictionary;
    private static ResourceDictionary? _controlDictionary;
    private static readonly Dictionary<object, object?> PreviousPrimaryValues = new();
    private static readonly HashSet<object> AddedPrimaryKeys = new();

    public static void ApplyFor(string? themeName)
    {
        var resources = Application.Current?.Resources;
        if (resources is null) return;

        var shouldEnable = string.Equals(themeName, "Сталкер", StringComparison.OrdinalIgnoreCase);
        if (!shouldEnable)
        {
            RestorePrimaryResources(resources);
            Remove(resources, ref _controlDictionary);
            Remove(resources, ref _windowSkinDictionary);
            Remove(resources, ref _textureDictionary);
            return;
        }

        Add(resources, ref _textureDictionary, TextureSourceUri);
        Add(resources, ref _windowSkinDictionary, WindowSkinSourceUri);
        Add(resources, ref _controlDictionary, ControlSourceUri);

        InstallPrimary(resources, "WindowGradient", _windowSkinDictionary!["WindowGradient"]);
        InstallPrimary(resources, "PanelGradient", _textureDictionary!["PanelGradient"]);
        InstallPrimary(resources, "ButtonGradient", _textureDictionary["BlueButtonBackground"]);
        InstallPrimary(resources, "AmberButtonGradient", _textureDictionary["AmberButtonBackground"]);

        // App.xaml already contains primary resources from CyberAmber/Compatibility.
        // A merged dictionary cannot beat those values, so copy every STALKER control
        // style into the primary dictionary while the theme is active.
        foreach (var key in _controlDictionary!.Keys)
            InstallPrimary(resources, key, _controlDictionary[key]);
    }

    private static void InstallPrimary(ResourceDictionary resources, object key, object value)
    {
        if (!PreviousPrimaryValues.ContainsKey(key) && !AddedPrimaryKeys.Contains(key))
        {
            if (resources.Contains(key)) PreviousPrimaryValues[key] = resources[key];
            else AddedPrimaryKeys.Add(key);
        }
        resources[key] = value;
    }

    private static void RestorePrimaryResources(ResourceDictionary resources)
    {
        foreach (var pair in PreviousPrimaryValues) resources[pair.Key] = pair.Value;
        foreach (var key in AddedPrimaryKeys) resources.Remove(key);
        PreviousPrimaryValues.Clear();
        AddedPrimaryKeys.Clear();
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