using System.Windows.Media;
using System.Windows.Media.Imaging;
using TiHiY.StreamControlCenter.Models;

namespace TiHiY.StreamControlCenter.Services;

public sealed class ThemeService
{
    public sealed record ThemeInfo(string Name, string Description, string OverlayTheme, ThemePalette Palette);
    public sealed record ThemePalette(
        Color Bg0, Color Bg1, Color Panel, Color Panel2, Color PanelHover,
        Color Line, Color LineSoft, Color Cyan, Color Cyan2, Color Amber, Color Amber2,
        Color Green, Color Yellow, Color Red, Color Purple, Color Text, Color Muted,
        Color WindowMid, Color PanelTop, Color PanelBottom, Color ButtonTop, Color ButtonMid, Color ButtonBottom,
        Color AmberButtonTop, Color AmberButtonMid, Color AmberButtonBottom);

    private readonly AppSettingsAccessor _settings;
    private readonly SettingsService _settingsService;

    public IReadOnlyList<ThemeInfo> Themes { get; }
    public string CurrentTheme => _settings.Value.UiTheme;
    public event EventHandler? ThemeChanged;

    public Uri? GetPreviewUri(string? themeName)
    {
        var file = ThemeTextureFile(themeName ?? string.Empty);
        return string.IsNullOrWhiteSpace(file)
            ? null
            : new Uri($"pack://application:,,,/TiHiY.StreamControlCenter;component/Assets/Themes/{file}", UriKind.Absolute);
    }

    public ThemeService(AppSettingsAccessor settings, SettingsService settingsService)
    {
        _settings = settings;
        _settingsService = settingsService;
        Themes = BuildThemes();
    }

    public void ApplySavedTheme()
    {
        var saved = string.IsNullOrWhiteSpace(_settings.Value.UiTheme)
            ? "TiHiY Default / Cyber Amber"
            : _settings.Value.UiTheme;
        Apply(saved, save: false);
    }

    public void Apply(string? themeName, bool save = true)
    {
        var theme = Themes.FirstOrDefault(x => string.Equals(x.Name, themeName, StringComparison.OrdinalIgnoreCase))
                    ?? Themes[0];
        var resources = Application.Current?.Resources;
        if (resources is null) return;

        SetColor(resources, "ColorBg0", theme.Palette.Bg0);
        SetColor(resources, "ColorBg1", theme.Palette.Bg1);
        SetColor(resources, "ColorPanel", theme.Palette.Panel);
        SetColor(resources, "ColorPanel2", theme.Palette.Panel2);
        SetColor(resources, "ColorPanelHover", theme.Palette.PanelHover);
        SetColor(resources, "ColorLine", theme.Palette.Line);
        SetColor(resources, "ColorLineSoft", theme.Palette.LineSoft);
        SetColor(resources, "ColorCyan", theme.Palette.Cyan);
        SetColor(resources, "ColorCyan2", theme.Palette.Cyan2);
        SetColor(resources, "ColorAmber", theme.Palette.Amber);
        SetColor(resources, "ColorAmber2", theme.Palette.Amber2);
        SetColor(resources, "ColorGreen", theme.Palette.Green);
        SetColor(resources, "ColorYellow", theme.Palette.Yellow);
        SetColor(resources, "ColorRed", theme.Palette.Red);
        SetColor(resources, "ColorPurple", theme.Palette.Purple);
        SetColor(resources, "ColorText", theme.Palette.Text);
        SetColor(resources, "ColorMuted", theme.Palette.Muted);

        SetBrush(resources, "Bg0", theme.Palette.Bg0);
        SetBrush(resources, "Bg1", theme.Palette.Bg1);
        SetBrush(resources, "Panel", theme.Palette.Panel);
        SetBrush(resources, "Panel2", theme.Palette.Panel2);
        SetBrush(resources, "PanelHover", theme.Palette.PanelHover);
        SetBrush(resources, "Line", theme.Palette.Line);
        SetBrush(resources, "LineSoft", theme.Palette.LineSoft);
        SetBrush(resources, "Cyan", theme.Palette.Cyan);
        SetBrush(resources, "Cyan2", theme.Palette.Cyan2);
        SetBrush(resources, "Amber", theme.Palette.Amber);
        SetBrush(resources, "Amber2", theme.Palette.Amber2);
        SetBrush(resources, "Green", theme.Palette.Green);
        SetBrush(resources, "Yellow", theme.Palette.Yellow);
        SetBrush(resources, "Red", theme.Palette.Red);
        SetBrush(resources, "Purple", theme.Palette.Purple);
        SetBrush(resources, "Text", theme.Palette.Text);
        SetBrush(resources, "Muted", theme.Palette.Muted);

        ApplySurfaceBrushes(resources, theme);
        ApplyThemeSymbol(resources, theme.Name);
        SetGradient(resources, "ButtonGradient", theme.Palette.ButtonTop, theme.Palette.ButtonMid, theme.Palette.ButtonBottom);
        SetGradient(resources, "AmberButtonGradient", theme.Palette.AmberButtonTop, theme.Palette.AmberButtonMid, theme.Palette.AmberButtonBottom);

        _settings.Value.UiTheme = theme.Name;
        _settings.Value.OverlayTheme = theme.OverlayTheme;
        if (save) _settingsService.Save(_settings.Value);
        ThemeChanged?.Invoke(this, EventArgs.Empty);
    }

    private static ResourceDictionary FindDictionary(ResourceDictionary root, string key)
    {
        if (root.Contains(key)) return root;
        foreach (var merged in root.MergedDictionaries)
        {
            var found = FindDictionaryOrNull(merged, key);
            if (found is not null) return found;
        }
        return root;
    }

    private static ResourceDictionary? FindDictionaryOrNull(ResourceDictionary root, string key)
    {
        if (root.Contains(key)) return root;
        foreach (var merged in root.MergedDictionaries)
        {
            var found = FindDictionaryOrNull(merged, key);
            if (found is not null) return found;
        }
        return null;
    }

    private static void SetColor(ResourceDictionary resources, string key, Color value)
    {
        FindDictionary(resources, key)[key] = value;
    }

    private static void SetBrush(ResourceDictionary resources, string key, Color value)
    {
        var dictionary = FindDictionary(resources, key);
        if (dictionary[key] is SolidColorBrush brush && !brush.IsFrozen)
            brush.Color = value;
        else
            dictionary[key] = new SolidColorBrush(value);
    }


    private static string? ThemeTextureFile(string themeName) => themeName switch
    {
        "Космічна" => "space.png",
        "Драйв" => "drive.png",
        "Неон" => "neon.png",
        "Військова" => "military_tactical.png",
        "Україна" => "Ukraine/ukraine-main-background-premium.png",
        "Синтвейв" => "synthwave.png",
        "Кіберпанк" => "cyberpunk.png",
        "Сталкер" => "stalker_zone.png",
        _ => null
    };

    private static string? ThemePanelTextureFile(string themeName) => themeName switch
    {
        "Україна" => "Ukraine/ukraine-panel-premium.png",
        _ => ThemeTextureFile(themeName)
    };

    private static string? ThemeSymbolFile(string themeName) => themeName switch
    {
        "Військова" => "military_symbol.png",
        "Україна" => "Ukraine/ukraine-trident.png",
        "Сталкер" => "stalker_symbol.png",
        _ => null
    };

    private static string? ThemeOrnamentFile(string themeName) => themeName switch
    {
        "Україна" => "Ukraine/ukraine-header-premium.png",
        _ => null
    };

    private static string? ThemeCornerFile(string themeName) => themeName switch
    {
        "Україна" => "Ukraine/ukraine-corner-premium.png",
        _ => null
    };

    private static string? ThemeWheatFile(string themeName) => themeName switch
    {
        "Україна" => "Ukraine/ukraine-wheat.png",
        _ => null
    };

    private static string? ThemeMapFile(string themeName) => themeName switch
    {
        "Україна" => "Ukraine/ukraine-map.png",
        _ => null
    };

    private static void ApplySurfaceBrushes(ResourceDictionary resources, ThemeInfo theme)
    {
        var windowTexture = ThemeTextureFile(theme.Name);
        var panelTexture = ThemePanelTextureFile(theme.Name);
        if (string.IsNullOrWhiteSpace(windowTexture))
        {
            SetGradient(resources, "WindowGradient", theme.Palette.Bg0, theme.Palette.WindowMid, theme.Palette.Bg1);
            SetGradient(resources, "PanelGradient", theme.Palette.PanelTop, theme.Palette.Panel, theme.Palette.PanelBottom);
            return;
        }

        if (theme.Name == "Україна")
        {
            // The approved Ukraine concept is a composed full-window background,
            // not a repeated texture. Panels use a separate subtle seamless texture.
            SetTextureBrush(resources, "WindowGradient", windowTexture,
                opacity: 1.0, tile: false, fallback: theme.Palette.Bg0);
            SetTextureBrush(resources, "PanelGradient", panelTexture!,
                opacity: 0.34, tile: true, fallback: theme.Palette.Panel);
            return;
        }

        var isRichTheme = theme.Name is "Військова" or "Сталкер";
        SetTextureBrush(resources, "WindowGradient", windowTexture,
            opacity: isRichTheme ? 0.30 : 0.18,
            tile: isRichTheme,
            fallback: theme.Palette.Bg0);
        SetTextureBrush(resources, "PanelGradient", panelTexture!,
            opacity: isRichTheme ? 0.16 : 0.08,
            tile: isRichTheme,
            fallback: theme.Palette.Panel);
    }

    private static void ApplyThemeSymbol(ResourceDictionary resources, string themeName)
    {
        SetImageResource(resources, "ThemeSymbolImage", ThemeSymbolFile(themeName));
        var symbolDictionary = FindDictionary(resources, "ThemeSymbolOpacity");
        symbolDictionary["ThemeSymbolOpacity"] = themeName == "Україна" ? 0.10d
            : string.IsNullOrWhiteSpace(ThemeSymbolFile(themeName)) ? 0d : 0.12d;

        SetImageResource(resources, "ThemeOrnamentImage", ThemeOrnamentFile(themeName));
        var ornamentDictionary = FindDictionary(resources, "ThemeOrnamentOpacity");
        ornamentDictionary["ThemeOrnamentOpacity"] = themeName == "Україна" ? 0.24d : 0d;
        SetImageResource(resources, "ThemeCornerImage", ThemeCornerFile(themeName));
        SetImageResource(resources, "ThemeWheatImage", ThemeWheatFile(themeName));
        SetImageResource(resources, "ThemeMapImage", ThemeMapFile(themeName));
        FindDictionary(resources, "ThemePremiumAccentOpacity")["ThemePremiumAccentOpacity"] = themeName == "Україна" ? 0.72d : 0d;
    }

    private static void SetImageResource(ResourceDictionary resources, string key, string? fileName)
    {
        var dictionary = FindDictionary(resources, key);
        if (string.IsNullOrWhiteSpace(fileName))
        {
            var transparent = new BitmapImage();
            transparent.BeginInit();
            transparent.UriSource = new Uri("pack://application:,,,/TiHiY.StreamControlCenter;component/Assets/Themes/transparent.png", UriKind.Absolute);
            transparent.CacheOption = BitmapCacheOption.OnLoad;
            transparent.EndInit();
            transparent.Freeze();
            dictionary[key] = transparent;
            return;
        }

        var image = new BitmapImage();
        image.BeginInit();
        image.UriSource = new Uri($"pack://application:,,,/TiHiY.StreamControlCenter;component/Assets/Themes/{fileName}", UriKind.Absolute);
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.EndInit();
        image.Freeze();
        dictionary[key] = image;
    }

    private static void SetTextureBrush(ResourceDictionary resources, string key, string fileName, double opacity, bool tile, Color fallback)
    {
        var dictionary = FindDictionary(resources, key);
        var image = new BitmapImage();
        image.BeginInit();
        image.UriSource = new Uri($"pack://application:,,,/TiHiY.StreamControlCenter;component/Assets/Themes/{fileName}", UriKind.Absolute);
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.EndInit();
        image.Freeze();

        var textureBrush = new ImageBrush
        {
            ImageSource = image,
            Stretch = tile ? Stretch.Fill : Stretch.UniformToFill,
            TileMode = tile ? TileMode.Tile : TileMode.None,
            ViewportUnits = tile ? BrushMappingMode.Absolute : BrushMappingMode.RelativeToBoundingBox,
            Viewport = tile ? new Rect(0, 0, 512, 512) : new Rect(0, 0, 1, 1),
            Opacity = opacity,
            AlignmentX = AlignmentX.Center,
            AlignmentY = AlignmentY.Center
        };

        var group = new DrawingBrush
        {
            Stretch = Stretch.Fill,
            Drawing = new DrawingGroup
            {
                Children =
                {
                    new GeometryDrawing(new SolidColorBrush(fallback), null, new RectangleGeometry(new Rect(0, 0, 1, 1))),
                    new GeometryDrawing(textureBrush, null, new RectangleGeometry(new Rect(0, 0, 1, 1)))
                }
            }
        };
        dictionary[key] = group;
    }

    private static void SetGradient(ResourceDictionary resources, string key, params Color[] colors)
    {
        var dictionary = FindDictionary(resources, key);
        if (dictionary[key] is not LinearGradientBrush brush || brush.IsFrozen || brush.GradientStops.Count != colors.Length)
        {
            var replacement = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 1) };
            for (var i = 0; i < colors.Length; i++)
                replacement.GradientStops.Add(new GradientStop(colors[i], colors.Length == 1 ? 0 : (double)i / (colors.Length - 1)));
            dictionary[key] = replacement;
            return;
        }
        for (var i = 0; i < colors.Length; i++) brush.GradientStops[i].Color = colors[i];
    }

    private static IReadOnlyList<ThemeInfo> BuildThemes() => new[]
    {
        T("TiHiY Default / Cyber Amber", "Стандартна темна cyan/amber тема без текстури поверх контенту", "Star Citizen MFD",
            "#02070B","#06121A","#071721","#0B2634","#12384A","#20A9D8","#17485A","#42D7FF","#16A9E8","#FFB400","#FF7B00","#28E57D","#FFD84A","#FF4C58","#A970FF","#EAF7FD","#91A8B4","#071824","#0A202C","#02090D","#103A54","#0A2433","#031018","#6F4508","#402706","#1B1002"),
        T("Космічна", "Глибокий космос, синьо-фіолетове світіння", "Star Citizen MFD",
            "#02040D","#071126","#071326","#101D3A","#172A4E","#2668D8","#203C72","#43D9FF","#6A8CFF","#A85CFF","#7152FF","#31E58B","#FFD84D","#FF5272","#B778FF","#E9F3FF","#8C9AB7","#101A38","#0B1731","#020613","#142847","#0C1932","#040916","#51308A","#301B55","#140A28"),
        T("Драйв", "Графіт, швидкість і помаранчеві акценти", "Compact Gaming",
            "#050505","#15110C","#15110C","#241A0E","#332514","#C87513","#593B15","#FFD06A","#FF9A1F","#FF9B1F","#FF5E00","#41E47E","#FFD54A","#FF4A3D","#B678FF","#F3EFE8","#A39A8E","#251B0E","#1C140B","#080604","#36240E","#211506","#0A0703","#6B3909","#3A1F05","#160B02"),
        T("Неон", "Яскравий cyan + magenta для нічного стріму", "Neon",
            "#04020B","#14051F","#120720","#220A32","#361048","#C51DE8","#552164","#35EAFF","#00AFFF","#FF35D3","#B500FF","#25F292","#FFE94A","#FF3D70","#B36CFF","#F9EEFF","#A993B5","#220A36","#1D0A31","#07020D","#35104C","#200A32","#08020F","#7D0A6C","#430638","#1A0217"),
        T("Військова", "Тактичний HUD з камуфляжем, військовим шевроном і польовою палітрою", "Military",
            "#050805","#10150C","#10170D","#1A2414","#26331C","#7B8D34","#3B4927","#B8D66A","#6FAF55","#D4B642","#9A7A1C","#62DB79","#E4D85A","#D85848","#9B7ED1","#E7EAD8","#929982","#1D2718","#172012","#070B06","#28341C","#182012","#090D07","#5B4D17","#332B0C","#141104"),
        T("Україна", "Преміальна темно-синя тема з тризубом, золотими рамками та українськими акцентами", "TiHiY-DED Ukraine",
            "#020915","#06172C","#071C34","#0A2848","#0E3B63","#1AA8E8","#15466D","#48CCFF","#1B8ED4","#F7B81E","#D98A08","#35E58A","#FFD95A","#FF5364","#A970FF","#F4F8FF","#93A9BF","#061C34","#082440","#020812","#0C3154","#071E36","#020914","#76520A","#422D05","#191003"),
        T("Синтвейв", "Фіолетово-рожевий ретро-футуризм", "Synthwave",
            "#05021A","#12062E","#10072A","#241043","#36165A","#8E34E8","#47206D","#36E7FF","#3C8DFF","#FF35B5","#8D43FF","#36EC9C","#FFD84A","#FF4D76","#C277FF","#F5EDFF","#9F91B3","#1F0B43","#1A0B37","#07021A","#321455","#1D0C3B","#08031C","#71115F","#3D0A35","#160415"),
        T("Кіберпанк", "Жорсткий cyan/red/magenta HUD", "Cyberpunk",
            "#03070A","#0D1217","#0C151C","#152633","#203A48","#1AA7D8","#174A5E","#20E0FF","#00A4D7","#FF2E8A","#FF5B00","#35F08B","#FFD84A","#FF3F57","#D051FF","#EAF9FF","#8FA5B0","#112834","#0E222D","#03080C","#153849","#0D2532","#030A0E","#6C1743","#3A0B24","#17030E"),
        T("Сталкер", "Індустріальний HUD Зони: іржа, радіація, небезпека та зелений PDA", "Stalker",
            "#060707","#15130F","#161610","#252319","#332F22","#7E7650","#46402D","#B7C79A","#708A65","#C8893B","#945018","#6ACB75","#D5C66B","#D1644A","#9B7FB7","#E6E2D4","#999386","#29251C","#211E17","#090907","#363126","#242018","#0B0A08","#5E3B17","#36220D","#171006")
    };

    private static ThemeInfo T(string name, string description, string overlay, params string[] c)
    {
        Color C(int i) => (Color)ColorConverter.ConvertFromString(c[i]);
        return new ThemeInfo(name, description, overlay, new ThemePalette(
            C(0),C(1),C(2),C(3),C(4),C(5),C(6),C(7),C(8),C(9),C(10),C(11),C(12),C(13),C(14),C(15),C(16),C(17),C(18),C(19),C(20),C(21),C(22),C(23),C(24),C(25)));
    }
}
