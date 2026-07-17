using System.Runtime.CompilerServices;

namespace TiHiY.StreamControlCenter.Services;

internal static class StalkerDecorationRuntime
{
    private const string CenterBlockKey = "UkraineCenterBlock";
    private static readonly ConditionalWeakTable<Image, ImageState> ImageStates = new();
    private static readonly ConditionalWeakTable<Grid, CenterHostState> CenterStates = new();
    private static readonly ConditionalWeakTable<Grid, FrameworkElement> HeaderStates = new();
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

            if (stalker)
            {
                ReplaceCenterHost(window);
                EnsureHeaderArtwork(window);
            }
            else
            {
                RestoreCenterHost(window);
                RestoreHeaderArtwork(window);
            }
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

        var overlay = BuildCenterOverlay(window);
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
        var runtimeHost = FindVisualChildren<Grid>(window)
            .FirstOrDefault(grid => string.Equals(grid.Tag?.ToString(), CenterBlockKey, StringComparison.Ordinal));
        if (runtimeHost is not null) return runtimeHost;

        if (window.FindName("FooterBlocksGrid") is Grid footer)
        {
            var center = footer.Children.OfType<ContentControl>()
                .FirstOrDefault(control => Grid.GetColumn(control) == 2 && string.IsNullOrWhiteSpace(control.Name));
            if (center is not null)
                return new Grid { Tag = CenterBlockKey, Visibility = Visibility.Collapsed };
        }
        return null;
    }

    private static FrameworkElement BuildCenterOverlay(FrameworkElement resourceOwner)
    {
        var panelBrush = resourceOwner.TryFindResource("StalkerExactPanelTexture") as Brush ?? CreateFallbackMetalBrush();
        var hazardBrush = resourceOwner.TryFindResource("StalkerHazardBrush") as Brush;

        var root = new Grid { ClipToBounds = true };
        root.Children.Add(new Border
        {
            Background = panelBrush,
            BorderBrush = new SolidColorBrush(Color.FromRgb(142, 79, 32)),
            BorderThickness = new Thickness(3),
            CornerRadius = new CornerRadius(2)
        });
        root.Children.Add(new Border
        {
            Margin = new Thickness(5),
            BorderBrush = new SolidColorBrush(Color.FromRgb(78, 67, 45)),
            BorderThickness = new Thickness(1)
        });
        if (hazardBrush is not null)
        {
            root.Children.Add(new Rectangle
            {
                Fill = hazardBrush,
                Height = 8,
                Width = 96,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 7, 8, 0),
                Opacity = 0.65
            });
        }

        var content = new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        content.Children.Add(new TextBlock
        {
            Text = "☢",
            FontFamily = new FontFamily("Segoe UI Symbol"),
            FontSize = 54,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(Color.FromRgb(126, 212, 67)),
            HorizontalAlignment = HorizontalAlignment.Center,
            Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                Color = Color.FromRgb(87, 190, 43),
                BlurRadius = 16,
                ShadowDepth = 0,
                Opacity = 0.82
            }
        });
        content.Children.Add(new TextBlock
        {
            Text = "ЗОНА КОНТРОЛЮ",
            FontSize = 21,
            FontWeight = FontWeights.Black,
            Foreground = new SolidColorBrush(Color.FromRgb(222, 158, 52)),
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, -5, 0, 0)
        });
        content.Children.Add(new TextBlock
        {
            Text = "ТИХО. НЕБЕЗПЕКА ПОРУЧ.",
            FontSize = 10,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(166, 158, 137)),
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 3, 0, 0)
        });
        root.Children.Add(content);
        return root;
    }

    private static void EnsureHeaderArtwork(Window window)
    {
        if (window.FindName("DesignSurface") is not Grid surface) return;
        var header = surface.Children.OfType<Grid>().FirstOrDefault(grid => Grid.GetRow(grid) == 0);
        if (header is null || HeaderStates.TryGetValue(header, out _)) return;

        var headerBrush = window.TryFindResource("StalkerHeaderPlateTexture") as Brush ?? CreateFallbackMetalBrush();
        var artwork = new Grid { IsHitTestVisible = false, ClipToBounds = true };
        Panel.SetZIndex(artwork, -50);
        artwork.Children.Add(new Border
        {
            Background = headerBrush,
            BorderBrush = new SolidColorBrush(Color.FromRgb(128, 72, 31)),
            BorderThickness = new Thickness(0, 0, 0, 2)
        });

        var emblem = new Grid
        {
            Width = 118,
            Height = 76,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(5, 0, 0, 0)
        };
        emblem.Children.Add(new System.Windows.Shapes.Path
        {
            Data = Geometry.Parse("M8,68 L59,8 L110,68 Z"),
            Fill = new SolidColorBrush(Color.FromArgb(205, 29, 31, 22)),
            Stroke = new SolidColorBrush(Color.FromRgb(203, 147, 42)),
            StrokeThickness = 3
        });
        emblem.Children.Add(new TextBlock
        {
            Text = "☢",
            FontFamily = new FontFamily("Segoe UI Symbol"),
            FontSize = 38,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(Color.FromRgb(128, 214, 70)),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 12, 0, 0),
            Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                Color = Color.FromRgb(79, 185, 42),
                BlurRadius = 11,
                ShadowDepth = 0,
                Opacity = 0.7
            }
        });
        artwork.Children.Add(emblem);
        header.Children.Insert(0, artwork);
        HeaderStates.Add(header, artwork);
    }

    private static void RestoreHeaderArtwork(Window window)
    {
        if (window.FindName("DesignSurface") is not Grid surface) return;
        var header = surface.Children.OfType<Grid>().FirstOrDefault(grid => Grid.GetRow(grid) == 0);
        if (header is null || !HeaderStates.TryGetValue(header, out var artwork)) return;
        header.Children.Remove(artwork);
        HeaderStates.Remove(header);
    }

    private static Brush CreateFallbackMetalBrush()
    {
        var group = new DrawingGroup();
        group.Children.Add(new GeometryDrawing(
            new LinearGradientBrush(Color.FromRgb(31, 29, 22), Color.FromRgb(8, 10, 7), 90),
            null,
            new RectangleGeometry(new Rect(0, 0, 1, 1))));
        group.Children.Add(new GeometryDrawing(
            new SolidColorBrush(Color.FromArgb(80, 120, 56, 20)),
            null,
            new EllipseGeometry(new Point(0.22, 0.28), 0.18, 0.12)));
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
