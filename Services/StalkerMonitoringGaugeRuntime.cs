using System.Runtime.CompilerServices;

namespace TiHiY.StreamControlCenter.Services;

/// <summary>
/// Applies a segmented industrial STALKER treatment directly to the existing
/// system-monitor gauges. Values, bindings, layout and hit testing remain unchanged.
/// </summary>
internal static class StalkerMonitoringGaugeRuntime
{
    private static DispatcherTimer? _timer;
    private static readonly ConditionalWeakTable<Ellipse, GaugeState> States = new();

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
                _timer = new DispatcherTimer(DispatcherPriority.Background, app.Dispatcher)
                {
                    Interval = TimeSpan.FromMilliseconds(500)
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
            foreach (var ellipse in FindVisualChildren<Ellipse>(window))
            {
                if (!IsSystemGauge(ellipse)) continue;
                if (active) ApplyGaugeStyle(ellipse);
                else RestoreGauge(ellipse);
            }
        }
    }

    private static bool IsSystemGauge(Ellipse ellipse)
    {
        if (!ellipse.IsVisible || ellipse.Opacity < 0.25) return false;
        if (ellipse.ActualWidth < 54 || ellipse.ActualWidth > 105) return false;
        if (ellipse.ActualHeight < 54 || ellipse.ActualHeight > 105) return false;
        if (Math.Abs(ellipse.ActualWidth - ellipse.ActualHeight) > 8) return false;
        if (ellipse.Stroke is null || ellipse.StrokeThickness < 1) return false;

        // The only large, near-square stroked ellipses in the main dashboard are
        // CPU/GPU/RAM/FPS monitor gauges. Smaller avatar, icon and poster ellipses
        // are excluded by the size limits above.
        return ellipse.Fill is null || ellipse.Fill == Brushes.Transparent || ellipse.Fill.Opacity < 0.15;
    }

    private static void ApplyGaugeStyle(Ellipse ellipse)
    {
        if (States.TryGetValue(ellipse, out _)) return;

        States.Add(ellipse, new GaugeState(
            ellipse.StrokeThickness,
            ellipse.StrokeDashArray is null ? null : new DoubleCollection(ellipse.StrokeDashArray),
            ellipse.StrokeDashCap,
            ellipse.Effect));

        ellipse.StrokeThickness = Math.Max(2.4, ellipse.StrokeThickness);
        ellipse.StrokeDashArray = new DoubleCollection { 1.4, 0.75, 0.35, 0.75 };
        ellipse.StrokeDashCap = PenLineCap.Flat;
        ellipse.Effect = new System.Windows.Media.Effects.DropShadowEffect
        {
            Color = Color.FromRgb(12, 12, 8),
            BlurRadius = 7,
            ShadowDepth = 2,
            Opacity = 0.9
        };
    }

    private static void RestoreGauge(Ellipse ellipse)
    {
        if (!States.TryGetValue(ellipse, out var state)) return;
        ellipse.StrokeThickness = state.StrokeThickness;
        ellipse.StrokeDashArray = state.StrokeDashArray;
        ellipse.StrokeDashCap = state.StrokeDashCap;
        ellipse.Effect = state.Effect;
        States.Remove(ellipse);
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

    private sealed record GaugeState(
        double StrokeThickness,
        DoubleCollection? StrokeDashArray,
        PenLineCap StrokeDashCap,
        System.Windows.Media.Effects.Effect? Effect);
}
