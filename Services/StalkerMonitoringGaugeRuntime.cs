using System.Runtime.CompilerServices;
using System.Windows.Documents;

namespace TiHiY.StreamControlCenter.Services;

/// <summary>
/// Adds non-interactive industrial brackets and scale marks around the existing
/// circular system-monitor gauges. Gauge values, bindings and hit testing remain unchanged.
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
                if (active) EnsureGaugeFrame(ellipse);
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

        var parentText = FindAncestorText(ellipse);
        return parentText.Contains("CPU", StringComparison.OrdinalIgnoreCase)
               || parentText.Contains("GPU", StringComparison.OrdinalIgnoreCase)
               || parentText.Contains("RAM", StringComparison.OrdinalIgnoreCase)
               || parentText.Contains("FPS", StringComparison.OrdinalIgnoreCase)
               || parentText.Contains("OBS", StringComparison.OrdinalIgnoreCase);
    }

    private static string FindAncestorText(DependencyObject start)
    {
        DependencyObject? current = start;
        for (var depth = 0; depth < 5 && current is not null; depth++)
        {
            var texts = FindVisualChildren<TextBlock>(current)
                .Select(x => x.Text)
                .Where(x => !string.IsNullOrWhiteSpace(x));
            var joined = string.Join(" ", texts);
            if (!string.IsNullOrWhiteSpace(joined)) return joined;
            current = VisualTreeHelper.GetParent(current);
        }
        return string.Empty;
    }

    private static void EnsureGaugeFrame(Ellipse ellipse)
    {
        if (States.TryGetValue(ellipse, out _)) return;
        var layer = AdornerLayer.GetAdornerLayer(ellipse);
        if (layer is null) return;

        var adorner = new GaugeAdorner(ellipse);
        layer.Add(adorner);
        States.Add(ellipse, new GaugeState(layer, adorner, ellipse.StrokeThickness));
        ellipse.StrokeThickness = Math.Max(2.0, ellipse.StrokeThickness);
    }

    private static void RestoreGauge(Ellipse ellipse)
    {
        if (!States.TryGetValue(ellipse, out var state)) return;
        state.Layer.Remove(state.Adorner);
        ellipse.StrokeThickness = state.StrokeThickness;
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

    private sealed record GaugeState(AdornerLayer Layer, GaugeAdorner Adorner, double StrokeThickness);

    private sealed class GaugeAdorner : Adorner
    {
        private static readonly Pen FramePen = CreatePen(Color.FromArgb(220, 117, 79, 36), 1.25);
        private static readonly Pen TickPen = CreatePen(Color.FromArgb(205, 201, 147, 48), 1.0);
        private static readonly Pen DarkPen = CreatePen(Color.FromArgb(230, 21, 22, 16), 2.0);
        private static readonly Brush PlateBrush = new SolidColorBrush(Color.FromArgb(70, 9, 11, 8));

        public GaugeAdorner(UIElement adornedElement) : base(adornedElement)
        {
            IsHitTestVisible = false;
            SnapsToDevicePixels = true;
        }

        protected override void OnRender(DrawingContext dc)
        {
            var width = ActualWidth;
            var height = ActualHeight;
            if (width < 40 || height < 40) return;

            var pad = 5.5;
            var rect = new Rect(-pad, -pad, width + pad * 2, height + pad * 2);
            dc.DrawRectangle(PlateBrush, DarkPen, rect);

            const double corner = 14;
            DrawCorner(dc, rect.TopLeft, 1, 1, corner);
            DrawCorner(dc, rect.TopRight, -1, 1, corner);
            DrawCorner(dc, rect.BottomLeft, 1, -1, corner);
            DrawCorner(dc, rect.BottomRight, -1, -1, corner);

            var center = new Point(width / 2, height / 2);
            var radius = Math.Min(width, height) / 2 + 1;
            for (var degree = 0; degree < 360; degree += 30)
            {
                var radians = degree * Math.PI / 180.0;
                var outer = new Point(center.X + Math.Cos(radians) * (radius + 5), center.Y + Math.Sin(radians) * (radius + 5));
                var inner = new Point(center.X + Math.Cos(radians) * (radius + 1), center.Y + Math.Sin(radians) * (radius + 1));
                dc.DrawLine(TickPen, inner, outer);
            }

            dc.DrawLine(FramePen, new Point(center.X - 4, center.Y), new Point(center.X + 4, center.Y));
            dc.DrawLine(FramePen, new Point(center.X, center.Y - 4), new Point(center.X, center.Y + 4));
        }

        private static void DrawCorner(DrawingContext dc, Point p, double xDirection, double yDirection, double length)
        {
            dc.DrawLine(FramePen, p, new Point(p.X + xDirection * length, p.Y));
            dc.DrawLine(FramePen, p, new Point(p.X, p.Y + yDirection * length));
        }

        private static Pen CreatePen(Color color, double thickness)
        {
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            var pen = new Pen(brush, thickness);
            pen.Freeze();
            return pen;
        }
    }
}
