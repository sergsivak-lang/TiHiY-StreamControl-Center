using System.Runtime.CompilerServices;
using System.Windows.Documents;

namespace TiHiY.StreamControlCenter.Services;

/// <summary>
/// Adds non-interactive industrial detailing over large dashboard frames while the
/// STALKER theme is active. The adorner never replaces controls and never receives input.
/// </summary>
internal static class StalkerIndustrialDetailRuntime
{
    private static DispatcherTimer? _timer;
    private static readonly ConditionalWeakTable<FrameworkElement, DetailState> States = new();

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
                    Interval = TimeSpan.FromMilliseconds(450)
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
            foreach (var border in FindVisualChildren<Border>(window))
            {
                if (!IsMajorPanel(border)) continue;
                if (active) EnsureDetail(border);
                else RemoveDetail(border);
            }
        }
    }

    private static bool IsMajorPanel(Border border)
    {
        if (!border.IsVisible || border.ActualWidth < 310 || border.ActualHeight < 70) return false;
        if (border.Opacity < 0.25) return false;

        var name = border.Name ?? string.Empty;
        return border.ActualWidth > 430
               || name.Contains("Panel", StringComparison.OrdinalIgnoreCase)
               || name.Contains("Block", StringComparison.OrdinalIgnoreCase)
               || name.Contains("Frame", StringComparison.OrdinalIgnoreCase)
               || name.Contains("Preview", StringComparison.OrdinalIgnoreCase);
    }

    private static void EnsureDetail(FrameworkElement element)
    {
        if (States.TryGetValue(element, out _)) return;
        var layer = AdornerLayer.GetAdornerLayer(element);
        if (layer is null) return;

        var adorner = new IndustrialFrameAdorner(element);
        layer.Add(adorner);
        States.Add(element, new DetailState(layer, adorner));
    }

    private static void RemoveDetail(FrameworkElement element)
    {
        if (!States.TryGetValue(element, out var state)) return;
        state.Layer.Remove(state.Adorner);
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

    private sealed record DetailState(AdornerLayer Layer, IndustrialFrameAdorner Adorner);

    private sealed class IndustrialFrameAdorner : Adorner
    {
        private static readonly Pen RustPen = CreatePen(Color.FromArgb(215, 126, 83, 37), 1.1);
        private static readonly Pen DarkPen = CreatePen(Color.FromArgb(210, 31, 28, 19), 1.0);
        private static readonly Brush RivetOuter = new SolidColorBrush(Color.FromArgb(235, 57, 48, 31));
        private static readonly Brush RivetInner = new SolidColorBrush(Color.FromArgb(245, 151, 105, 47));
        private static readonly Brush Marking = new SolidColorBrush(Color.FromArgb(150, 202, 145, 44));

        public IndustrialFrameAdorner(UIElement adornedElement) : base(adornedElement)
        {
            IsHitTestVisible = false;
            SnapsToDevicePixels = true;
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            var width = ActualWidth;
            var height = ActualHeight;
            if (width < 20 || height < 20) return;

            var inset = 3.5;
            var rect = new Rect(inset, inset, Math.Max(0, width - inset * 2), Math.Max(0, height - inset * 2));
            drawingContext.DrawRectangle(null, RustPen, rect);

            DrawRivet(drawingContext, new Point(9, 9));
            DrawRivet(drawingContext, new Point(width - 9, 9));
            DrawRivet(drawingContext, new Point(9, height - 9));
            DrawRivet(drawingContext, new Point(width - 9, height - 9));

            var markWidth = Math.Min(66, Math.Max(28, width * 0.10));
            var y = Math.Max(8, height - 10);
            for (var x = width - markWidth - 16; x < width - 16; x += 10)
            {
                drawingContext.DrawLine(RustPen, new Point(x, y), new Point(x + 6, y - 6));
            }

            if (width > 520)
            {
                drawingContext.DrawRectangle(Marking, null, new Rect(17, 7, 38, 2));
                drawingContext.DrawLine(DarkPen, new Point(60, 8), new Point(92, 8));
            }
        }

        private static void DrawRivet(DrawingContext context, Point center)
        {
            context.DrawEllipse(RivetOuter, DarkPen, center, 4.0, 4.0);
            context.DrawEllipse(RivetInner, null, new Point(center.X - 0.8, center.Y - 0.8), 1.55, 1.55);
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
