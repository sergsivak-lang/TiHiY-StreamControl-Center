using TiHiY.StreamControlCenter.Models;

namespace TiHiY.StreamControlCenter.Services;

public sealed class WindowPlacementService
{
    private readonly AppSettingsAccessor _settings;
    private readonly SettingsService _settingsService;

    public WindowPlacementService(AppSettingsAccessor settings, SettingsService settingsService)
    {
        _settings = settings;
        _settingsService = settingsService;
    }

    public void Attach(Window window, string key)
    {
        window.SourceInitialized += (_, _) => Restore(window, key);
        window.Closing += (_, _) => Save(window, key);
    }

    private void Restore(Window window, string key)
    {
        if (!_settings.Value.WindowPlacements.TryGetValue(key, out var p)) return;
        if (p.Width >= window.MinWidth && p.Height >= window.MinHeight)
        {
            window.Width = p.Width;
            window.Height = p.Height;
        }
        if (IsVisibleOnAnyScreen(p.Left, p.Top, p.Width, p.Height))
        {
            window.Left = p.Left;
            window.Top = p.Top;
            window.WindowStartupLocation = WindowStartupLocation.Manual;
        }
        if (p.Maximized) window.WindowState = WindowState.Maximized;
    }

    private void Save(Window window, string key)
    {
        var bounds = window.RestoreBounds;
        _settings.Value.WindowPlacements[key] = new WindowPlacement
        {
            Left = bounds.Left,
            Top = bounds.Top,
            Width = bounds.Width,
            Height = bounds.Height,
            Maximized = window.WindowState == WindowState.Maximized
        };
        try { _settingsService.Save(_settings.Value); } catch { }
    }

    private static bool IsVisibleOnAnyScreen(double left, double top, double width, double height)
    {
        var right = left + Math.Max(100, width);
        var bottom = top + Math.Max(100, height);
        var virtualLeft = SystemParameters.VirtualScreenLeft;
        var virtualTop = SystemParameters.VirtualScreenTop;
        var virtualRight = virtualLeft + SystemParameters.VirtualScreenWidth;
        var virtualBottom = virtualTop + SystemParameters.VirtualScreenHeight;
        return right > virtualLeft && left < virtualRight && bottom > virtualTop && top < virtualBottom;
    }
}
