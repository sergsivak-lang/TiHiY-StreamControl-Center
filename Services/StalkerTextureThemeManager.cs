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
    private static readonly Uri OverlaySourceUri = new(
        "pack://application:,,,/TiHiY.StreamControlCenter;component/Themes/StalkerWindowOverlay.xaml",
        UriKind.Absolute);

    private static ResourceDictionary? _textureDictionary;
    private static ResourceDictionary? _windowSkinDictionary;
    private static ResourceDictionary? _controlDictionary;
    private static ResourceDictionary? _overlayDictionary;
    private static readonly Dictionary<object, object?> PreviousPrimaryValues = new();
    private static readonly HashSet<object> AddedPrimaryKeys = new();
    private static readonly Dictionary<Window, Style?> PreviousWindowStyles = new();
    private static bool _active;
    private static bool _activationHooked;

    public static void ApplyFor(string? themeName)
    {
        var app = Application.Current;
        var resources = app?.Resources;
        if (resources is null) return;

        _active = string.Equals(themeName, "Сталкер", StringComparison.OrdinalIgnoreCase);
        if (!_active)
        {
            RestoreWindowStyles();
            RestorePrimaryResources(resources);
            Remove(resources, ref _overlayDictionary);
            Remove(resources, ref _controlDictionary);
            Remove(resources, ref _windowSkinDictionary);
            Remove(resources, ref _textureDictionary);
            return;
        }

        Add(resources, ref _textureDictionary, TextureSourceUri);
        Add(resources, ref _windowSkinDictionary, WindowSkinSourceUri);
        Add(resources, ref _controlDictionary, ControlSourceUri);
        Add(resources, ref _overlayDictionary, OverlaySourceUri);

        InstallPrimary(resources, "WindowGradient", _windowSkinDictionary!["WindowGradient"]);
        InstallPrimary(resources, "PanelGradient", _textureDictionary!["PanelGradient"]);
        InstallPrimary(resources, "ButtonGradient", _textureDictionary["BlueButtonBackground"]);
        InstallPrimary(resources, "AmberButtonGradient", _textureDictionary["AmberButtonBackground"]);

        foreach (var key in _controlDictionary!.Keys)
            InstallPrimary(resources, key, _controlDictionary[key]);

        if (!_activationHooked)
        {
            app!.Activated += OnApplicationActivated;
            _activationHooked = true;
        }
        ApplyOverlayToOpenWindows();
    }

    private static void OnApplicationActivated(object? sender, EventArgs e)
    {
        if (_active) ApplyOverlayToOpenWindows();
    }

    private static void ApplyOverlayToOpenWindows()
    {
        var app = Application.Current;
        if (!_active || app is null || _overlayDictionary is null) return;
        if (_overlayDictionary["StalkerWindowOverlayStyle"] is not Style style) return;

        foreach (Window window in app.Windows)
        {
            if (!PreviousWindowStyles.ContainsKey(window))
                PreviousWindowStyles[window] = window.Style;
            if (!ReferenceEquals(window.Style, style))
                window.Style = style;
        }
    }

    private static void RestoreWindowStyles()
    {
        foreach (var pair in PreviousWindowStyles.ToArray())
        {
            try { pair.Key.Style = pair.Value; } catch { }
        }
        PreviousWindowStyles.Clear();
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