using System.Reflection;
using System.Windows.Media.Imaging;
using TiHiY.StreamControlCenter.Models;

namespace TiHiY.StreamControlCenter.Services;

internal static class MainWindowMainPolish
{
    private static readonly ConditionalWeakTable<MainWindow, Controller> Controllers = new();

    [ModuleInitializer]
    internal static void Register()
    {
        EventManager.RegisterClassHandler(
            typeof(MainWindow),
            FrameworkElement.LoadedEvent,
            new RoutedEventHandler(OnMainWindowLoaded));
    }

    private static void OnMainWindowLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is not MainWindow window || Controllers.TryGetValue(window, out _)) return;
        var controller = new Controller(window);
        Controllers.Add(window, controller);
    }

    private sealed class Controller : IDisposable
    {
        private readonly MainWindow _window;
        private readonly DispatcherTimer _timer;
        private bool _disposed;

        public Controller(MainWindow window)
        {
            _window = window;
            _timer = new DispatcherTimer(DispatcherPriority.Render, window.Dispatcher)
            {
                Interval = TimeSpan.FromMilliseconds(180)
            };
            _timer.Tick += Timer_Tick;
            _window.Closed += Window_Closed;
            _timer.Start();
            _window.Dispatcher.BeginInvoke(new Action(Apply), DispatcherPriority.ContextIdle);
        }

        private void Timer_Tick(object? sender, EventArgs e) => Apply();
        private void Window_Closed(object? sender, EventArgs e) => Dispose();

        private void Apply()
        {
            if (_disposed || !_window.IsLoaded) return;
            ApplyApprovedCenterArtwork();
            ApplySeparateCounters();
            ApplyLiveMeterOnlyMixer();
            ApplyMuteStateColours();
            ApplyGoldAidaTypography();
            ShiftTitlesAwayFromOrnaments();
        }

        private void ApplyApprovedCenterArtwork()
        {
            var canvas = FindNamed<Canvas>(_window, "FreeformDashboardCanvas");
            if (canvas is null) return;

            var host = canvas.Children.OfType<Grid>()
                .FirstOrDefault(x => string.Equals(x.Tag as string, "UkraineCenterBlock", StringComparison.Ordinal));
            var panel = host?.Children.OfType<ContentControl>().FirstOrDefault();
            if (panel is null || panel.Tag as string == "ApprovedCenterArtwork") return;

            var image = new Image
            {
                Source = new BitmapImage(new Uri(
                    "/TiHiY.StreamControlCenter;component/Assets/Themes/UkraineExact/central-glory.jpg",
                    UriKind.Relative)),
                Stretch = Stretch.UniformToFill,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                SnapsToDevicePixels = true
            };
            panel.Padding = new Thickness(0);
            panel.Content = image;
            panel.Tag = "ExactCenterTexture";
        }

        private void ApplySeparateCounters()
        {
            var twitch = FindNamed<TextBlock>(_window, "TwitchViewerText");
            var youtube = FindNamed<TextBlock>(_window, "YouTubeViewerText");
            var likes = FindNamed<TextBlock>(_window, "YouTubeLikesText");
            if (twitch is null || youtube is null || likes is null) return;
            if (twitch.Tag as string == "ApprovedSeparateCounters") return;

            var originalTwitchBorder = FindAncestor<Border>(twitch);
            var panel = originalTwitchBorder?.Parent as StackPanel;
            if (panel is null) return;

            Detach(twitch);
            Detach(youtube);
            Detach(likes);
            panel.Children.Clear();
            panel.Orientation = Orientation.Horizontal;
            panel.VerticalAlignment = VerticalAlignment.Center;

            panel.Children.Add(CounterCard(
                "/TiHiY.StreamControlCenter;component/Assets/Platforms/twitch.png",
                twitch,
                Color.FromRgb(143, 79, 226),
                Color.FromRgb(45, 23, 66)));
            panel.Children.Add(CounterCard(
                "/TiHiY.StreamControlCenter;component/Assets/Platforms/youtube.png",
                youtube,
                Color.FromRgb(211, 57, 66),
                Color.FromRgb(53, 18, 25)));
            panel.Children.Add(LikeCard(likes));
            twitch.Tag = "ApprovedSeparateCounters";
        }

        private static Border CounterCard(string iconPath, TextBlock value, Color line, Color background)
        {
            value.FontSize = 17;
            value.FontWeight = FontWeights.Bold;
            value.Margin = new Thickness(7, 0, 2, 0);
            value.VerticalAlignment = VerticalAlignment.Center;

            var row = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            row.Children.Add(new Image
            {
                Source = new BitmapImage(new Uri(iconPath, UriKind.Relative)),
                Width = 21,
                Height = 21,
                Stretch = Stretch.Uniform
            });
            row.Children.Add(value);

            return new Border
            {
                Background = new SolidColorBrush(background),
                BorderBrush = new SolidColorBrush(line),
                BorderThickness = new Thickness(1.2),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(9, 4, 9, 4),
                Margin = new Thickness(3, 0, 3, 0),
                MinWidth = 70,
                Child = row
            };
        }

        private static Border LikeCard(TextBlock value)
        {
            value.FontSize = 17;
            value.FontWeight = FontWeights.Bold;
            value.Margin = new Thickness(7, 0, 2, 0);
            value.VerticalAlignment = VerticalAlignment.Center;

            var row = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            row.Children.Add(new TextBlock
            {
                Text = "♥",
                Foreground = new SolidColorBrush(Color.FromRgb(255, 55, 66)),
                FontSize = 21,
                FontWeight = FontWeights.Black,
                VerticalAlignment = VerticalAlignment.Center
            });
            row.Children.Add(value);

            return new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(30, 10, 18)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(146, 36, 48)),
                BorderThickness = new Thickness(1.2),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(9, 4, 9, 4),
                Margin = new Thickness(3, 0, 3, 0),
                MinWidth = 70,
                Child = row
            };
        }

        private void ApplyLiveMeterOnlyMixer()
        {
            var mixer = FindNamed<ContentControl>(_window, "MixerBlockPanel");
            if (mixer is null) return;

            foreach (var slider in Descendants<Slider>(mixer))
            {
                slider.Visibility = Visibility.Collapsed;
                slider.IsHitTestVisible = false;
                slider.Width = 0;
                slider.Height = 0;
            }

            foreach (var meter in Descendants<ProgressBar>(mixer))
            {
                meter.Minimum = 0;
                meter.Maximum = 1;
                meter.Height = 11;
                meter.Margin = new Thickness(5, 0, 10, 0);
                meter.VerticalAlignment = VerticalAlignment.Center;
                meter.Background = new SolidColorBrush(Color.FromRgb(1, 8, 15));
                meter.BorderBrush = new SolidColorBrush(Color.FromRgb(40, 90, 120));
                meter.BorderThickness = new Thickness(1);
                meter.Foreground = MeterBrush();
                meter.Template = MeterTemplate();

                if (meter.DataContext is AudioChannel channel)
                {
                    if (channel.Meter <= 0 && channel.Db > -60)
                        channel.Meter = Math.Clamp((channel.Db + 60) / 60, 0, 1);
                    meter.SetBinding(RangeBase.ValueProperty, new Binding(nameof(AudioChannel.Meter))
                    {
                        Source = channel,
                        Mode = BindingMode.OneWay
                    });
                }
            }
        }

        private static ControlTemplate MeterTemplate()
        {
            var template = new ControlTemplate(typeof(ProgressBar));
            var root = new FrameworkElementFactory(typeof(Grid));
            root.SetValue(UIElement.ClipToBoundsProperty, true);

            var track = new FrameworkElementFactory(typeof(Border));
            track.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Control.BackgroundProperty));
            track.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Control.BorderBrushProperty));
            track.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Control.BorderThicknessProperty));
            track.SetValue(Border.CornerRadiusProperty, new CornerRadius(3));
            root.AppendChild(track);

            var indicator = new FrameworkElementFactory(typeof(Border), "PART_Indicator");
            indicator.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Control.ForegroundProperty));
            indicator.SetValue(Border.CornerRadiusProperty, new CornerRadius(3));
            indicator.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Left);
            root.AppendChild(indicator);

            template.VisualTree = root;
            return template;
        }

        private static LinearGradientBrush MeterBrush()
        {
            var brush = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 0) };
            brush.GradientStops.Add(new GradientStop(Color.FromRgb(23, 215, 102), 0));
            brush.GradientStops.Add(new GradientStop(Color.FromRgb(174, 225, 57), 0.60));
            brush.GradientStops.Add(new GradientStop(Color.FromRgb(245, 176, 0), 0.82));
            brush.GradientStops.Add(new GradientStop(Color.FromRgb(255, 76, 88), 1));
            return brush;
        }

        private void ApplyMuteStateColours()
        {
            var mixer = FindNamed<ContentControl>(_window, "MixerBlockPanel");
            if (mixer is null) return;

            foreach (var button in Descendants<Button>(mixer).Where(x => x.Tag is AudioChannel))
            {
                var channel = (AudioChannel)button.Tag;
                if (channel.IsMuted)
                {
                    button.Background = new SolidColorBrush(Color.FromRgb(92, 21, 26));
                    button.BorderBrush = new SolidColorBrush(Color.FromRgb(255, 57, 70));
                    button.Foreground = Brushes.White;
                    button.ToolTip = "MUTE: ON";
                }
                else
                {
                    button.SetResourceReference(Control.BackgroundProperty, "ButtonGradient");
                    button.SetResourceReference(Control.BorderBrushProperty, "Line");
                    button.Foreground = Brushes.White;
                    button.ToolTip = "MUTE";
                }
            }
        }

        private void ApplyGoldAidaTypography()
        {
            var gold = new SolidColorBrush(Color.FromRgb(255, 190, 20));
            foreach (var name in new[]
                     {
                         "AidaStatusText", "CpuTemperatureMonitorText", "GpuTemperatureMonitorText",
                         "GpuLoadMonitorText", "ObsFpsText"
                     })
            {
                if (FindNamed<TextBlock>(_window, name) is not { } text) continue;
                text.Foreground = gold;
                text.FontWeight = FontWeights.Black;
                text.FontSize = name == "AidaStatusText" ? 18 : name == "ObsFpsText" ? 21 : 25;
            }
        }

        private void ShiftTitlesAwayFromOrnaments()
        {
            var titles = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "МУЛЬТИЧАТ • TWITCH + YOUTUBE", "ДОНАТИ", "ШВИДКИЙ МІКШЕР • AUDIO MIXER OBS",
                "СПОВІЩЕННЯ", "СТАН СИСТЕМИ", "AIDA64 LIVE"
            };
            foreach (var text in Descendants<TextBlock>(_window).Where(x => titles.Contains(x.Text ?? string.Empty)))
                text.Margin = new Thickness(Math.Max(12, text.Margin.Left), text.Margin.Top, text.Margin.Right, text.Margin.Bottom);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _timer.Stop();
            _timer.Tick -= Timer_Tick;
            _window.Closed -= Window_Closed;
        }
    }

    private static T? FindNamed<T>(DependencyObject root, string name) where T : FrameworkElement =>
        Descendants<T>(root).FirstOrDefault(x => string.Equals(x.Name, name, StringComparison.Ordinal));

    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        var visited = new HashSet<DependencyObject>();
        var pending = new Stack<DependencyObject>();
        pending.Push(root);
        while (pending.Count > 0)
        {
            var current = pending.Pop();
            if (!visited.Add(current)) continue;
            if (current is T match) yield return match;
            try
            {
                for (var i = 0; i < VisualTreeHelper.GetChildrenCount(current); i++)
                    pending.Push(VisualTreeHelper.GetChild(current, i));
            }
            catch { }
            try
            {
                foreach (var child in LogicalTreeHelper.GetChildren(current).OfType<DependencyObject>())
                    pending.Push(child);
            }
            catch { }
        }
    }

    private static T? FindAncestor<T>(DependencyObject source) where T : DependencyObject
    {
        for (var current = source; current is not null; current = Parent(current))
            if (current is T match) return match;
        return null;
    }

    private static DependencyObject? Parent(DependencyObject current)
    {
        if (current is FrameworkElement element && element.Parent is not null) return element.Parent;
        if (current is FrameworkContentElement contentElement) return contentElement.Parent;
        try { return VisualTreeHelper.GetParent(current); }
        catch { return null; }
    }

    private static void Detach(UIElement element)
    {
        switch (element)
        {
            case FrameworkElement { Parent: Panel panel }:
                panel.Children.Remove(element);
                break;
            case FrameworkElement { Parent: ContentControl content } when ReferenceEquals(content.Content, element):
                content.Content = null;
                break;
            case FrameworkElement { Parent: Decorator decorator } when ReferenceEquals(decorator.Child, element):
                decorator.Child = null;
                break;
        }
    }
}
