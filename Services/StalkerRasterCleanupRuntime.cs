using System.Runtime.CompilerServices;

namespace TiHiY.StreamControlCenter.Services;

/// <summary>
/// Removes only the broad oval raster overlays from the approved STALKER artwork pass.
/// Functional controls, the central poster, monitoring card and every non-STALKER theme
/// are left untouched and restored exactly when the theme changes.
/// </summary>
internal static class StalkerRasterCleanupRuntime
{
    private static DispatcherTimer? _timer;
    private static readonly ConditionalWeakTable<FrameworkElement, ElementState> States = new();

    private static readonly Brush CleanPanel = CreateCleanPanelBrush();
    private static readonly Brush CleanHeader = CreateCleanHeaderBrush();

    [ModuleInitializer]
    internal static void Initialize() => _ = StartAsync();

    private static async Task StartAsync()
    {
        for (var attempt = 0; attempt < 600; attempt++)
        {
            await Task.Delay(100).ConfigureAwait(false);
            var app = Application.Current;
            if (app is null) continue;

            await app.Dispatcher.InvokeAsync(() =>
            {
                if (_timer is not null || App.Services is null) return;
                _timer = new DispatcherTimer(DispatcherPriority.Loaded, app.Dispatcher)
                {
                    Interval = TimeSpan.FromMilliseconds(250)
                };
                _timer.Tick += (_, _) => Apply();
                _timer.Start();
                Apply();
            });

            if (_timer is not null) return;
        }
    }

    private static void Apply()
    {
        if (Application.Current is null || App.Services is null) return;
        var active = App.Services.Theme.CurrentTheme.Equals("Сталкер", StringComparison.OrdinalIgnoreCase);

        foreach (Window window in Application.Current.Windows)
        {
            foreach (var element in FindVisualChildren<FrameworkElement>(window))
            {
                if (!active)
                {
                    Restore(element);
                    continue;
                }

                if (element is Image image && IsUnwantedRaster(image.Source?.ToString()))
                {
                    Save(element);
                    image.Opacity = 0;
                    image.IsHitTestVisible = false;
                    continue;
                }

                var background = GetBackground(element);
                if (background is not ImageBrush imageBrush || !IsUnwantedRaster(imageBrush.ImageSource?.ToString()))
                {
                    continue;
                }

                Save(element);
                SetBackground(element, IsHeader(element) ? CleanHeader : CleanPanel);
            }
        }
    }

    private static bool IsUnwantedRaster(string? source)
    {
        if (string.IsNullOrWhiteSpace(source)) return false;
        return source.Contains("header-approved", StringComparison.OrdinalIgnoreCase)
               || source.Contains("panel-frame-approved", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsHeader(FrameworkElement element)
    {
        if (element.Name.Contains("Header", StringComparison.OrdinalIgnoreCase)) return true;
        if (element is Grid grid && Grid.GetRow(grid) == 0 && element.ActualWidth > 700 && element.ActualHeight < 160)
        {
            return true;
        }

        return element.ActualWidth > 800 && element.ActualHeight < 170;
    }

    private static Brush CreateCleanHeaderBrush()
    {
        var brush = new LinearGradientBrush(
            new GradientStopCollection
            {
                new(Color.FromRgb(35, 31, 22), 0.00),
                new(Color.FromRgb(19, 20, 16), 0.28),
                new(Color.FromRgb(10, 13, 11), 0.68),
                new(Color.FromRgb(27, 22, 15), 1.00)
            },
            new Point(0, 0),
            new Point(0, 1));
        brush.Freeze();
        return brush;
    }

    private static Brush CreateCleanPanelBrush()
    {
        var brush = new LinearGradientBrush(
            new GradientStopCollection
            {
                new(Color.FromRgb(22, 22, 17), 0.00),
                new(Color.FromRgb(12, 16, 13), 0.46),
                new(Color.FromRgb(8, 12, 10), 0.74),
                new(Color.FromRgb(20, 18, 13), 1.00)
            },
            new Point(0, 0),
            new Point(0, 1));
        brush.Freeze();
        return brush;
    }

    private static Brush? GetBackground(FrameworkElement element) => element switch
    {
        Control control => control.Background,
        Panel panel => panel.Background,
        Border border => border.Background,
        _ => null
    };

    private static void SetBackground(FrameworkElement element, Brush? background)
    {
        switch (element)
        {
            case Control control:
                control.Background = background;
                break;
            case Panel panel:
                panel.Background = background;
                break;
            case Border border:
                border.Background = background;
                break;
        }
    }

    private static void Save(FrameworkElement element)
    {
        if (States.TryGetValue(element, out _)) return;
        States.Add(element, new ElementState(GetBackground(element), element.Opacity, element.IsHitTestVisible));
    }

    private static void Restore(FrameworkElement element)
    {
        if (!States.TryGetValue(element, out var state)) return;
        SetBackground(element, state.Background);
        element.Opacity = state.Opacity;
        element.IsHitTestVisible = state.IsHitTestVisible;
        States.Remove(element);
    }

    private static IEnumerable<T> FindVisualChildren<T>(DependencyObject root) where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            if (child is T match) yield return match;
            foreach (var nested in FindVisualChildren<T>(child)) yield return nested;
        }
    }

    private sealed record ElementState(Brush? Background, double Opacity, bool IsHitTestVisible);
}
