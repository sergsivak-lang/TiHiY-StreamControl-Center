using TiHiY.StreamControlCenter.Models;

namespace TiHiY.StreamControlCenter.Services;

public sealed class ThemeService
{
    private readonly AppSettingsAccessor _settings;
    private readonly SettingsService _settingsService;

    public event EventHandler? ThemeChanged;

    public IReadOnlyList<ThemeDefinition> Themes { get; } = CreateThemes();

    public ThemeDefinition Current =>
        Themes.FirstOrDefault(x => x.Id.Equals(_settings.Value.UiTheme, StringComparison.OrdinalIgnoreCase))
        ?? Themes[0];

    public ThemeService(AppSettingsAccessor settings, SettingsService settingsService)
    {
        _settings = settings;
        _settingsService = settingsService;
    }

    public void ApplySavedTheme() => Apply(_settings.Value.UiTheme, save: false);

    public void Apply(string? themeId, bool save = true)
    {
        var theme = Themes.FirstOrDefault(x => x.Id.Equals(themeId, StringComparison.OrdinalIgnoreCase)) ?? Themes[0];
        _settings.Value.UiTheme = theme.Id;
        if (_settings.Value.UseThemeForOverlay)
            _settings.Value.OverlayTheme = theme.DisplayName;

        var resources = Application.Current?.Resources;
        if (resources is not null)
        {
            foreach (var pair in theme.Colors)
            {
                var color = (Color)ColorConverter.ConvertFromString(pair.Value)!;

                // Brush keys are used by existing windows. Color keys drive the
                // gradient/effect resources in App.xaml, so the whole shell,
                // controls, icons, borders and meters change together.
                resources[pair.Key] = new SolidColorBrush(color);
                resources[$"{pair.Key}Color"] = color;
            }

            var primary = (Color)ColorConverter.ConvertFromString(theme.PrimaryHex)!;
            var accent = (Color)ColorConverter.ConvertFromString(theme.AccentHex)!;
            resources["ThemePrimary"] = new SolidColorBrush(primary);
            resources["ThemeAccent"] = new SolidColorBrush(accent);
            resources["ThemePrimaryColor"] = primary;
            resources["ThemeAccentColor"] = accent;

            var backdropName = theme.Id switch
            {
                "CyberBlue" => "BackdropCyberBlue.png",
                "EmeraldTech" => "BackdropEmeraldTech.png",
                "PurpleNeon" => "BackdropPurpleNeon.png",
                "MinimalDark" => "BackdropMinimalDark.png",
                _ => "BackdropCyberAmber.png"
            };
            var backdrop = new BitmapImage(new Uri($"pack://application:,,,/TiHiY.StreamControlCenter;component/Assets/{backdropName}", UriKind.Absolute));
            resources["CircuitBackdropBrush"] = new ImageBrush(backdrop)
            {
                Stretch = Stretch.UniformToFill,
                Opacity = theme.Id == "MinimalDark" ? 0.08 : 0.13
            };
        }

        if (save) _settingsService.Save(_settings.Value);
        ThemeChanged?.Invoke(this, EventArgs.Empty);
    }

    private static IReadOnlyList<ThemeDefinition> CreateThemes()
    {
        return new List<ThemeDefinition>
        {
            Theme(
                "CyberAmber", "CYBER AMBER", "Кібер-бурштинова тема з синіми HUD-лініями та помаранчевим неоном.",
                "#27D9FF", "#FFAA00",
                bg0: "#02070D", bg1: "#06111B", panel: "#071823", panel2: "#0B2230", hover: "#15313D",
                line: "#6A430A", primary: "#27D9FF", accent: "#FFAA00", green: "#2BEB82", warning: "#FFD24A",
                red: "#FF4B55", text: "#F3F8FC", muted: "#8CA4B3", input: "#030D16", header: "#040C14",
                blue: "#0E5CB4", purple: "#6E3CC7", danger: "#7B1F27", success: "#123B2D", selection: "#4A2D00"),
            Theme(
                "CyberBlue", "CYBER BLUE", "Холодна синя тема з яскравими кібернетичними акцентами.",
                "#33D8FF", "#168DFF",
                bg0: "#02080F", bg1: "#061522", panel: "#081C2B", panel2: "#0C2A3E", hover: "#123C55",
                line: "#245D78", primary: "#33D8FF", accent: "#168DFF", green: "#28E58A", warning: "#FFD24A",
                red: "#FF5261", text: "#F0F8FF", muted: "#8FA9B9", input: "#04111B", header: "#04101A",
                blue: "#0F66C8", purple: "#6542C7", danger: "#7B222C", success: "#103B31", selection: "#082F59"),
            Theme(
                "EmeraldTech", "EMERALD TECH", "Темна техно-тема з бірюзовими та смарагдовими акцентами.",
                "#39E6D0", "#00C878",
                bg0: "#020B0A", bg1: "#061714", panel: "#08211C", panel2: "#0E3028", hover: "#16453A",
                line: "#236A58", primary: "#39E6D0", accent: "#00C878", green: "#35F18E", warning: "#F3D34A",
                red: "#FF5967", text: "#F0FFF9", muted: "#8DB3A6", input: "#04140F", header: "#04110E",
                blue: "#087EA0", purple: "#5B49B8", danger: "#76252C", success: "#0C4B31", selection: "#06442B"),
            Theme(
                "PurpleNeon", "PURPLE NEON", "Неонова фіолетова тема з блакитним контрастом.",
                "#8FE8FF", "#B35CFF",
                bg0: "#080510", bg1: "#130A20", panel: "#180D28", panel2: "#26143C", hover: "#3A2055",
                line: "#634080", primary: "#8FE8FF", accent: "#B35CFF", green: "#39E88B", warning: "#FFD24A",
                red: "#FF5870", text: "#FBF4FF", muted: "#B2A0BF", input: "#10091A", header: "#0D0715",
                blue: "#365FC7", purple: "#7D3FD0", danger: "#7B2434", success: "#173D31", selection: "#3E1D5A"),
            Theme(
                "MinimalDark", "MINIMAL DARK", "Спокійна темна тема без сильного світіння та насичених акцентів.",
                "#C6D2DB", "#7F93A3",
                bg0: "#0B0D10", bg1: "#101419", panel: "#151A20", panel2: "#1B222A", hover: "#27313A",
                line: "#3C4852", primary: "#C6D2DB", accent: "#7F93A3", green: "#46D28A", warning: "#E3C961",
                red: "#E15C66", text: "#EFF2F4", muted: "#9EA9B1", input: "#0F1317", header: "#0D1014",
                blue: "#365C7A", purple: "#62567A", danger: "#622B31", success: "#233A31", selection: "#303A42")
        };
    }

    private static ThemeDefinition Theme(
        string id, string name, string description, string primaryHex, string accentHex,
        string bg0, string bg1, string panel, string panel2, string hover, string line,
        string primary, string accent, string green, string warning, string red, string text, string muted,
        string input, string header, string blue, string purple, string danger, string success, string selection)
    {
        return new ThemeDefinition
        {
            Id = id,
            DisplayName = name,
            Description = description,
            PrimaryHex = primaryHex,
            AccentHex = accentHex,
            Colors = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Bg0"] = bg0,
                ["Bg1"] = bg1,
                ["Panel"] = panel,
                ["Panel2"] = panel2,
                ["PanelHover"] = hover,
                ["Line"] = line,
                ["Cyan"] = primary,
                ["Accent"] = accent,
                ["AccentSoft"] = selection,
                ["AccentLine"] = accent,
                ["Green"] = green,
                ["Yellow"] = warning,
                ["Red"] = red,
                ["Text"] = text,
                ["Muted"] = muted,
                ["InputBg"] = input,
                ["HeaderBg"] = header,
                ["Blue"] = blue,
                ["Purple"] = purple,
                ["DangerBg"] = danger,
                ["CloseBg"] = danger,
                ["SuccessBg"] = success,
                ["SelectionBg"] = selection,
                ["PanelDark"] = bg1,
                ["ButtonBorder"] = line,
                ["ListSeparator"] = line,
                ["ThemePrimary"] = primaryHex,
                ["ThemeAccent"] = accentHex
            }
        };
    }
}
