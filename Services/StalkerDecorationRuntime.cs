using System.Runtime.CompilerServices;

namespace TiHiY.StreamControlCenter.Services;

internal static class StalkerDecorationRuntime
{
    private const string CenterBlockKey = "UkraineCenterBlock";
    private static readonly ConditionalWeakTable<Image, ImageState> ImageStates = new();
    private static readonly ConditionalWeakTable<Grid, CenterHostState> CenterStates = new();
    private static DispatcherTimer? _timer;

    [ModuleInitializer]
    internal static void InitializeModule() => _ = WaitForApplicationAsync();

    private static async Task WaitForApplicationAsync()
    {
        for (var attempt = 0; attempt < 600; attempt++)
        {
            await Task.Delay(100).ConfigureAwait(false);
            var application = Application.Current;
            if (application is null) continue;
            try
            {
                await application.Dispatcher.InvokeAsync(() =>
                {
                    if (_timer is not null || App.Services is null) return;
                    _timer = new DispatcherTimer(DispatcherPriority.Background, application.Dispatcher)
                    {
                        Interval = TimeSpan.FromMilliseconds(350)
                    };
                    _timer.Tick += (_, _) => Apply();
                    _timer.Start();
                    Apply();
                });
                if (_timer is not null) return;
            }
            catch
            {
                // Services and the freeform dashboard are still being created.
            }
        }
    }

    private static void Apply()
    {
        var application = Application.Current;
        if (application is null || App.Services is null) return;
        var stalker = App.Services.Theme.CurrentTheme.Equals("Сталкер", StringComparison.OrdinalIgnoreCase);

        foreach (Window window in application.Windows)
        {
            foreach (var image in FindVisualChildren<Image>(window).Where(IsUkraineDecoration).ToList())
            {
                if (stalker)
                {
                    if (!ImageStates.TryGetValue(image, out _))
                        ImageStates.Add(image, new ImageState(image.Opacity, image.Visibility));
                    image.Opacity = 0;
                    image.Visibility = Visibility.Hidden;
                }
                else
                {
                    RestoreImage(image);
                }
            }

            if (stalker) ReplaceCenterHost(window);
            else RestoreCenterHost(window);
        }
    }

    private static bool IsUkraineDecoration(Image image) =>
        (image.Source?.ToString() ?? string.Empty).Contains("UkraineExact", StringComparison.OrdinalIgnoreCase);

    private static void ReplaceCenterHost(Window window)
    {
        var host = FindCenterHost(window);
        if (host is null || CenterStates.TryGetValue(host, out _)) return;

        var originalChildren = host.Children.Cast<UIElement>().ToList();
        var originalVisibility = originalChildren.ToDictionary(x => x, x => x.Visibility);
        foreach (var child in originalChildren) child.Visibility = Visibility.Hidden;

        var overlay = BuildOverlay();
        Panel.SetZIndex(overlay, 1000);
        host.Children.Add(overlay);
        CenterStates.Add(host, new CenterHostState(originalVisibility, overlay));
    }

    private static void RestoreCenterHost(Window window)
    {
        var host = FindCenterHost(window);
        if (host is null || !CenterStates.TryGetValue(host, out var state)) return;

        host.Children.Remove(state.Overlay);
        foreach (var pair in state.Visibility) pair.Key.Visibility = pair.Value;
        CenterStates.Remove(host);
    }

    private static Grid? FindCenterHost(Window window)
    {
        // MainWindowVisualTuner creates this exact direct host after detaching the
        // original footer ContentControl into the freeform Canvas.
        var runtimeHost = FindVisualChildren<Grid>(window)
            .FirstOrDefault(grid => string.Equals(grid.Tag?.ToString(), CenterBlockKey, StringComparison.Ordinal));
        if (runtimeHost is not null) return runtimeHost;

        // Fallback before the freeform dashboard is built.
        if (window.FindName("FooterBlocksGrid") is Grid footer)
        {
            var center = footer.Children.OfType<ContentControl>()
                .FirstOrDefault(control => Grid.GetColumn(control) == 2 && string.IsNullOrWhiteSpace(control.Name));
            if (center is not null)
            {
                return new Grid
                {
                    Tag = CenterBlockKey,
                    Visibility = Visibility.Collapsed
                };
            }
        }

        return null;
    }

    private static FrameworkElement BuildOverlay()
    {
        var frame = new Border
        {
            CornerRadius = new CornerRadius(4),
            BorderThickness = new Thickness(2),
            BorderBrush = new SolidColorBrush(Color.FromRgb(151, 88, 28)),
            Background = CreateZoneBrush(),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            SnapsToDevicePixels = true
        };

        var content = new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        content.Children.Add(new TextBlock
        {
            Text = "☢",
            FontFamily = new FontFamily("Segoe UI Symbol"),
            FontSize = 52,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(Color.FromRgb(121, 206, 57)),
            HorizontalAlignment = HorizontalAlignment.Center,
            Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                Color = Color.FromRgb(87, 190, 43),
                BlurRadius = 14,
                ShadowDepth = 0,
                Opacity = 0.75
            }
        });
        content.Children.Add(new TextBlock
        {
            Text = "ЗОНА КОНТРОЛЮ",
            FontSize = 21,
            FontWeight = FontWeights.Black,
            Foreground = new SolidColorBrush(Color.FromRgb(222, 158, 52)),
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, -4, 0, 0)
        });
        content.Children.Add(new TextBlock
        {
            Text = "ТИХО. НЕБЕЗПЕКА ПОРУЧ.",
            FontSize = 10,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(159, 153, 131)),
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 3, 0, 0)
        });
        frame.Child = content;
        return frame;
    }

    private static Brush CreateZoneBrush()
    {
        var group = new DrawingGroup();
        group.Children.Add(new GeometryDrawing(
            new LinearGradientBrush(Color.FromRgb(26, 25, 18), Color.FromRgb(7, 9, 6), 90),
            null,
            new RectangleGeometry(new Rect(0, 0, 1, 1))));
        var linePen = new Pen(new SolidColorBrush(Color.FromArgb(80, 112, 91, 45)), 0.012);
        for (var offset = -1.0; offset < 2.0; offset += 0.18)
            group.Children.Add(new GeometryDrawing(null, linePen, new LineGeometry(new Point(offset, 1), new Point(offset + 1, 0))));
        var brush = new DrawingBrush(group) { Stretch = Stretch.Fill };
        brush.Freeze();
        return brush;
    }

    private static void RestoreImage(Image image)
    {
        if (!ImageStates.TryGetValue(image, out var state)) return;
        image.Opacity = state.Opacity;
        image.Visibility = state.Visibility;
        ImageStates.Remove(image);
    }

    private static IEnumerable<T> FindVisualChildren<T>(DependencyObject root) where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            if (child is T match) yield return match;
            foreach (var descendant in FindVisualChildren<T>(child)) yield return descendant;
        }
    }

    private sealed record ImageState(double Opacity, Visibility Visibility);
    private sealed record CenterHostState(Dictionary<UIElement, Visibility> Visibility, FrameworkElement Overlay);
}
