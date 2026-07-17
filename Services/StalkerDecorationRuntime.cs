using System.Runtime.CompilerServices;

namespace TiHiY.StreamControlCenter.Services;

internal static class StalkerDecorationRuntime
{
    private static readonly ConditionalWeakTable<Image, ImageState> ImageStates = new();
    private static readonly ConditionalWeakTable<ContentControl, CenterContentState> CenterStates = new();
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
                        Interval = TimeSpan.FromMilliseconds(500)
                    };
                    _timer.Tick += (_, _) => Apply();
                    _timer.Start();
                    Apply();
                });
                if (_timer is not null) return;
            }
            catch
            {
                // Application services and windows are still being created.
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
            var ukraineImages = FindVisualChildren<Image>(window)
                .Where(IsUkraineDecoration)
                .ToList();

            if (stalker)
            {
                foreach (var image in ukraineImages)
                {
                    if (!ImageStates.TryGetValue(image, out _))
                        ImageStates.Add(image, new ImageState(image.Opacity, image.Visibility));
                    image.Opacity = 0;
                    image.Visibility = Visibility.Hidden;
                }

                ReplaceFooterCenter(window);
            }
            else
            {
                foreach (var image in ukraineImages)
                    RestoreImage(image);
                RestoreFooterCenter(window);
            }
        }
    }

    private static bool IsUkraineDecoration(Image image)
    {
        var source = image.Source?.ToString() ?? string.Empty;
        return source.Contains("UkraineExact", StringComparison.OrdinalIgnoreCase);
    }

    private static void ReplaceFooterCenter(Window window)
    {
        if (window.FindName("FooterBlocksGrid") is not Grid footer) return;

        var center = footer.Children
            .OfType<ContentControl>()
            .FirstOrDefault(control => Grid.GetColumn(control) == 2);
        if (center is null || CenterStates.TryGetValue(center, out _)) return;

        CenterStates.Add(center, new CenterContentState(center.Content));
        center.Content = BuildOverlay();
    }

    private static void RestoreFooterCenter(Window window)
    {
        if (window.FindName("FooterBlocksGrid") is not Grid footer) return;

        var center = footer.Children
            .OfType<ContentControl>()
            .FirstOrDefault(control => Grid.GetColumn(control) == 2);
        if (center is null || !CenterStates.TryGetValue(center, out var state)) return;

        center.Content = state.Content;
        CenterStates.Remove(center);
    }

    private static FrameworkElement BuildOverlay()
    {
        var frame = new Border
        {
            Margin = new Thickness(0),
            CornerRadius = new CornerRadius(4),
            BorderThickness = new Thickness(2),
            BorderBrush = new SolidColorBrush(Color.FromRgb(151, 88, 28)),
            Background = CreateZoneBrush(),
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

        var brush = new DrawingBrush(group)
        {
            Stretch = Stretch.Fill,
            TileMode = TileMode.None
        };
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
    private sealed record CenterContentState(object? Content);
}
