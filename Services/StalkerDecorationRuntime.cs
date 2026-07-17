using System.Runtime.CompilerServices;

namespace TiHiY.StreamControlCenter.Services;

internal static class StalkerDecorationRuntime
{
    private const string CenterBlockKey = "UkraineCenterBlock";
    private static readonly ConditionalWeakTable<Image, ImageState> ImageStates = new();
    private static readonly ConditionalWeakTable<Grid, CenterState> CenterStates = new();
    private static readonly ConditionalWeakTable<Grid, FrameworkElement> HeaderStates = new();
    private static DispatcherTimer? _timer;

    [ModuleInitializer]
    internal static void InitializeModule() => _ = StartAsync();

    private static async Task StartAsync()
    {
        for (var attempt = 0; attempt < 600; attempt++)
        {
            await Task.Delay(100).ConfigureAwait(false);
            var app = Application.Current;
            if (app is null) continue;
            try
            {
                await app.Dispatcher.InvokeAsync(() =>
                {
                    if (_timer is not null || App.Services is null) return;
                    _timer = new DispatcherTimer(DispatcherPriority.Background, app.Dispatcher)
                    {
                        Interval = TimeSpan.FromMilliseconds(250)
                    };
                    _timer.Tick += (_, _) => Apply();
                    _timer.Start();
                    Apply();
                });
                if (_timer is not null) return;
            }
            catch
            {
                // Windows and services are still being created.
            }
        }
    }

    private static void Apply()
    {
        if (Application.Current is null || App.Services is null) return;
        var stalker = App.Services.Theme.CurrentTheme.Equals("Сталкер", StringComparison.OrdinalIgnoreCase);

        foreach (Window window in Application.Current.Windows)
        {
            foreach (var image in FindVisualChildren<Image>(window).Where(IsUkraineDecoration).ToList())
            {
                if (stalker) HideImage(image);
                else RestoreImage(image);
            }

            if (stalker)
            {
                EnsureCenterPoster(window);
                EnsureHeaderArtwork(window);
            }
            else
            {
                RestoreCenter(window);
                RestoreHeaderArtwork(window);
            }
        }
    }

    private static bool IsUkraineDecoration(Image image) =>
        (image.Source?.ToString() ?? string.Empty).Contains("UkraineExact", StringComparison.OrdinalIgnoreCase);

    private static void HideImage(Image image)
    {
        if (!ImageStates.TryGetValue(image, out _))
            ImageStates.Add(image, new ImageState(image.Opacity, image.Visibility));
        image.Opacity = 0;
        image.Visibility = Visibility.Hidden;
    }

    private static void RestoreImage(Image image)
    {
        if (!ImageStates.TryGetValue(image, out var state)) return;
        image.Opacity = state.Opacity;
        image.Visibility = state.Visibility;
        ImageStates.Remove(image);
    }

    private static Grid? FindCenterHost(Window window) =>
        FindVisualChildren<Grid>(window)
            .FirstOrDefault(grid => string.Equals(grid.Tag?.ToString(), CenterBlockKey, StringComparison.Ordinal));

    private static void EnsureCenterPoster(Window window)
    {
        var host = FindCenterHost(window);
        if (host is null || CenterStates.TryGetValue(host, out _)) return;

        var visibility = host.Children.Cast<UIElement>().ToDictionary(x => x, x => x.Visibility);
        foreach (var child in host.Children.Cast<UIElement>()) child.Visibility = Visibility.Hidden;

        var poster = BuildPoster();
        Panel.SetZIndex(poster, 5000);
        host.Children.Add(poster);
        CenterStates.Add(host, new CenterState(visibility, poster));
    }

    private static void RestoreCenter(Window window)
    {
        var host = FindCenterHost(window);
        if (host is null || !CenterStates.TryGetValue(host, out var state)) return;
        host.Children.Remove(state.Poster);
        foreach (var pair in state.Visibility) pair.Key.Visibility = pair.Value;
        CenterStates.Remove(host);
    }

    private static FrameworkElement BuildPoster()
    {
        var root = new Grid { ClipToBounds = true, IsHitTestVisible = false };
        root.Children.Add(new Border
        {
            Background = new LinearGradientBrush(Color.FromRgb(31, 27, 18), Color.FromRgb(5, 7, 5), 90),
            BorderBrush = new SolidColorBrush(Color.FromRgb(139, 79, 31)),
            BorderThickness = new Thickness(3)
        });
        root.Children.Add(new Border
        {
            Margin = new Thickness(6),
            BorderBrush = new SolidColorBrush(Color.FromRgb(76, 66, 43)),
            BorderThickness = new Thickness(1)
        });

        var wear = new Canvas { IsHitTestVisible = false, Opacity = 0.5 };
        wear.Children.Add(new Rectangle { Width = 190, Height = 44, Fill = new SolidColorBrush(Color.FromArgb(90, 132, 60, 21)), Margin = new Thickness(12, 12, 0, 0) });
        wear.Children.Add(new Rectangle { Width = 160, Height = 34, Fill = new SolidColorBrush(Color.FromArgb(70, 91, 77, 48)), Margin = new Thickness(230, 115, 0, 0) });
        root.Children.Add(wear);

        var figure = new Grid
        {
            Width = 116,
            Height = 170,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(22, 0, 0, 6)
        };
        figure.Children.Add(new System.Windows.Shapes.Path
        {
            Data = Geometry.Parse("M58,10 C37,10 26,27 28,49 C29,63 35,72 43,79 L31,95 C17,112 12,140 13,169 H103 C104,140 99,112 85,95 L73,79 C81,72 87,63 88,49 C90,27 79,10 58,10 Z"),
            Fill = new SolidColorBrush(Color.FromArgb(240, 9, 11, 8)),
            Stroke = new SolidColorBrush(Color.FromRgb(104, 83, 45)),
            StrokeThickness = 2
        });
        foreach (var left in new[] { true, false })
        {
            figure.Children.Add(new Ellipse
            {
                Width = 24,
                Height = 20,
                Fill = new SolidColorBrush(Color.FromRgb(124, 164, 68)),
                Stroke = new SolidColorBrush(Color.FromRgb(53, 69, 34)),
                StrokeThickness = 3,
                HorizontalAlignment = left ? HorizontalAlignment.Left : HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = left ? new Thickness(31, 37, 0, 0) : new Thickness(0, 37, 31, 0),
                Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    Color = Color.FromRgb(102, 204, 52), BlurRadius = 10, ShadowDepth = 0, Opacity = 0.9
                }
            });
        }
        figure.Children.Add(new System.Windows.Shapes.Path
        {
            Data = Geometry.Parse("M47,67 L69,67 L76,94 L40,94 Z"),
            Fill = new SolidColorBrush(Color.FromRgb(21, 24, 16)),
            Stroke = new SolidColorBrush(Color.FromRgb(97, 81, 47)),
            StrokeThickness = 2
        });
        root.Children.Add(figure);

        var text = new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(95, 8, 8, 0)
        };
        text.Children.Add(new TextBlock
        {
            Text = "S.T.A.L.K.E.R.",
            FontFamily = new FontFamily("Impact"),
            FontSize = 38,
            FontWeight = FontWeights.Black,
            Foreground = new SolidColorBrush(Color.FromRgb(224, 217, 188)),
            Effect = new System.Windows.Media.Effects.DropShadowEffect { Color = Colors.Black, BlurRadius = 5, ShadowDepth = 2, Opacity = 0.95 }
        });
        text.Children.Add(new TextBlock
        {
            Text = "ЗОНА ЧЕКАЄ НА ТЕБЕ...",
            HorizontalAlignment = HorizontalAlignment.Center,
            FontSize = 14,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(Color.FromRgb(200, 143, 54)),
            Margin = new Thickness(0, 0, 0, 6)
        });
        text.Children.Add(new TextBlock
        {
            Text = "☢",
            FontFamily = new FontFamily("Segoe UI Symbol"),
            FontSize = 31,
            FontWeight = FontWeights.Bold,
            HorizontalAlignment = HorizontalAlignment.Center,
            Foreground = new SolidColorBrush(Color.FromRgb(207, 156, 58))
        });
        root.Children.Add(text);
        return root;
    }

    private static void EnsureHeaderArtwork(Window window)
    {
        if (window.FindName("DesignSurface") is not Grid surface) return;
        var header = surface.Children.OfType<Grid>().FirstOrDefault(grid => Grid.GetRow(grid) == 0);
        if (header is null || HeaderStates.TryGetValue(header, out _)) return;

        var artwork = new Grid { IsHitTestVisible = false, ClipToBounds = true };
        Panel.SetZIndex(artwork, -50);
        artwork.Children.Add(new Border
        {
            Background = window.TryFindResource("StalkerHeaderPlateTexture") as Brush
                         ?? new LinearGradientBrush(Color.FromRgb(42, 34, 22), Color.FromRgb(12, 13, 9), 90),
            BorderBrush = new SolidColorBrush(Color.FromRgb(128, 72, 31)),
            BorderThickness = new Thickness(0, 0, 0, 2)
        });
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

    private static IEnumerable<T> FindVisualChildren<T>(DependencyObject root) where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            if (child is T match) yield return match;
            foreach (var nested in FindVisualChildren<T>(child)) yield return nested;
        }
    }

    private sealed record ImageState(double Opacity, Visibility Visibility);
    private sealed record CenterState(Dictionary<UIElement, Visibility> Visibility, FrameworkElement Poster);
}
