using System.Runtime.CompilerServices;

namespace TiHiY.StreamControlCenter.Services;

/// <summary>
/// Adds the approved STALKER poster to the existing functional center module.
/// The original Ukraine content is only hidden while the STALKER theme is active
/// and is restored unchanged when another theme is selected.
/// </summary>
internal static class StalkerApprovedPosterRuntime
{
    private const string CenterBlockKey = "UkraineCenterBlock";
    private static readonly ConditionalWeakTable<Grid, PosterState> States = new();
    private static DispatcherTimer? _timer;

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
                    Interval = TimeSpan.FromMilliseconds(300)
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
            var host = FindVisualChildren<Grid>(window)
                .FirstOrDefault(x => string.Equals(x.Tag?.ToString(), CenterBlockKey, StringComparison.Ordinal));
            if (host is null) continue;
            if (active) EnsurePoster(host);
            else Restore(host);
        }
    }

    private static void EnsurePoster(Grid host)
    {
        if (States.TryGetValue(host, out _)) return;

        var visibility = host.Children.Cast<UIElement>().ToDictionary(x => x, x => x.Visibility);
        foreach (var child in host.Children.Cast<UIElement>()) child.Visibility = Visibility.Hidden;

        var poster = BuildPoster();
        Panel.SetZIndex(poster, 5000);
        host.Children.Add(poster);
        States.Add(host, new PosterState(visibility, poster));
    }

    private static void Restore(Grid host)
    {
        if (!States.TryGetValue(host, out var state)) return;
        host.Children.Remove(state.Poster);
        foreach (var pair in state.Visibility) pair.Key.Visibility = pair.Value;
        States.Remove(host);
    }

    private static FrameworkElement BuildPoster()
    {
        var root = new Grid { ClipToBounds = true, IsHitTestVisible = false };

        var background = new DrawingBrush
        {
            Stretch = Stretch.Fill,
            Drawing = new DrawingGroup
            {
                Children =
                {
                    new GeometryDrawing(new LinearGradientBrush(
                        Color.FromRgb(20, 20, 14), Color.FromRgb(4, 6, 4), 90), null,
                        new RectangleGeometry(new Rect(0, 0, 1, 1))),
                    new GeometryDrawing(new SolidColorBrush(Color.FromArgb(120, 92, 55, 24)), null,
                        Geometry.Parse("M0,0.12 C0.18,0.01 0.37,0.17 0.55,0.07 C0.72,-0.02 0.88,0.13 1,0.03 L1,0.48 C0.78,0.37 0.62,0.52 0.43,0.4 C0.24,0.29 0.13,0.43 0,0.35 Z")),
                    new GeometryDrawing(null, new Pen(new SolidColorBrush(Color.FromArgb(90, 143, 108, 59)), 0.004),
                        Geometry.Parse("M0.05,0.16 L0.92,0.12 M0.12,0.34 L0.83,0.30 M0.03,0.66 L0.94,0.61 M0.18,0.86 L0.78,0.81"))
                }
            }
        };
        root.Children.Add(new Border
        {
            Background = background,
            BorderBrush = new SolidColorBrush(Color.FromRgb(115, 69, 34)),
            BorderThickness = new Thickness(3)
        });
        root.Children.Add(new Border
        {
            Margin = new Thickness(6),
            BorderBrush = new SolidColorBrush(Color.FromRgb(67, 59, 39)),
            BorderThickness = new Thickness(1)
        });

        var skyline = new Canvas { VerticalAlignment = VerticalAlignment.Top, Height = 72, Opacity = 0.72 };
        for (var index = 0; index < 13; index++)
        {
            var height = 22 + (index % 4) * 11;
            skyline.Children.Add(new Rectangle
            {
                Width = 10 + (index % 3) * 5,
                Height = height,
                Fill = new SolidColorBrush(Color.FromRgb(8, 10, 8)),
                Stroke = new SolidColorBrush(Color.FromRgb(64, 58, 39)),
                StrokeThickness = 1,
                Margin = new Thickness(14 + index * 31, 65 - height, 0, 0)
            });
        }
        root.Children.Add(skyline);

        var figure = new Grid
        {
            Width = 118,
            Height = 176,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(24, 0, 0, 8)
        };
        figure.Children.Add(new System.Windows.Shapes.Path
        {
            Data = Geometry.Parse("M58,12 C38,12 27,28 29,49 C30,62 35,71 43,78 L31,94 C18,111 13,139 14,174 H103 C104,139 99,111 86,94 L74,78 C82,71 87,62 88,49 C90,28 78,12 58,12 Z"),
            Fill = new SolidColorBrush(Color.FromArgb(235, 11, 13, 10)),
            Stroke = new SolidColorBrush(Color.FromRgb(94, 78, 45)),
            StrokeThickness = 2
        });
        figure.Children.Add(new Ellipse
        {
            Width = 24,
            Height = 20,
            Fill = new SolidColorBrush(Color.FromRgb(116, 150, 64)),
            Stroke = new SolidColorBrush(Color.FromRgb(54, 69, 34)),
            StrokeThickness = 3,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(32, 38, 0, 0),
            Effect = new System.Windows.Media.Effects.DropShadowEffect { Color = Color.FromRgb(103, 190, 55), BlurRadius = 9, ShadowDepth = 0 }
        });
        figure.Children.Add(new Ellipse
        {
            Width = 24,
            Height = 20,
            Fill = new SolidColorBrush(Color.FromRgb(116, 150, 64)),
            Stroke = new SolidColorBrush(Color.FromRgb(54, 69, 34)),
            StrokeThickness = 3,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 38, 32, 0),
            Effect = new System.Windows.Media.Effects.DropShadowEffect { Color = Color.FromRgb(103, 190, 55), BlurRadius = 9, ShadowDepth = 0 }
        });
        figure.Children.Add(new System.Windows.Shapes.Path
        {
            Data = Geometry.Parse("M48,67 L68,67 L75,92 L41,92 Z"),
            Fill = new SolidColorBrush(Color.FromRgb(22, 25, 17)),
            Stroke = new SolidColorBrush(Color.FromRgb(95, 82, 49)),
            StrokeThickness = 2
        });
        root.Children.Add(figure);

        var content = new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(78, 18, 8, 0)
        };
        content.Children.Add(new TextBlock
        {
            Text = "S.T.A.L.K.E.R.",
            FontFamily = new FontFamily("Bahnschrift Condensed, Impact"),
            FontSize = 42,
            FontWeight = FontWeights.Black,
            Foreground = new SolidColorBrush(Color.FromRgb(220, 213, 183)),
            CharacterSpacing = 70,
            Effect = new System.Windows.Media.Effects.DropShadowEffect { Color = Colors.Black, BlurRadius = 4, ShadowDepth = 2, Opacity = 0.9 }
        });
        content.Children.Add(new TextBlock
        {
            Text = "ЗОНА ЧЕКАЄ НА ТЕБЕ...",
            HorizontalAlignment = HorizontalAlignment.Center,
            FontSize = 15,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(Color.FromRgb(190, 136, 54)),
            Margin = new Thickness(0, -2, 0, 8)
        });
        content.Children.Add(new TextBlock
        {
            Text = "☢",
            FontFamily = new FontFamily("Segoe UI Symbol"),
            FontSize = 34,
            FontWeight = FontWeights.Bold,
            HorizontalAlignment = HorizontalAlignment.Center,
            Foreground = new SolidColorBrush(Color.FromRgb(205, 153, 57))
        });
        root.Children.Add(content);
        return root;
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

    private sealed record PosterState(Dictionary<UIElement, Visibility> Visibility, FrameworkElement Poster);
}
