using System.Globalization;
using System.Windows.Media.Imaging;
using TiHiY.StreamControlCenter.Models;

namespace TiHiY.StreamControlCenter.Services;

internal static class MainChatAndMeterFinalizer
{
    private static readonly ConditionalWeakTable<MainWindow, Controller> Controllers = new();

    [ModuleInitializer]
    internal static void Register()
    {
        EventManager.RegisterClassHandler(
            typeof(MainWindow),
            FrameworkElement.LoadedEvent,
            new RoutedEventHandler(OnLoaded));
    }

    private static void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is not MainWindow window || Controllers.TryGetValue(window, out _)) return;
        Controllers.Add(window, new Controller(window));
    }

    private sealed class Controller : IDisposable
    {
        private readonly MainWindow _window;
        private readonly DispatcherTimer _timer;

        public Controller(MainWindow window)
        {
            _window = window;
            _window.Closed += Window_Closed;
            _timer = new DispatcherTimer(DispatcherPriority.Render, window.Dispatcher)
            {
                Interval = TimeSpan.FromMilliseconds(140)
            };
            _timer.Tick += Timer_Tick;
            _timer.Start();
            Apply();
        }

        private void Timer_Tick(object? sender, EventArgs e) => Apply();

        private void Apply()
        {
            PatchChatRows();
            PatchMeters();
        }

        private void PatchChatRows()
        {
            foreach (var border in Descendants<Border>(_window).Where(x => x.DataContext is ChatMessage).ToList())
            {
                var message = (ChatMessage)border.DataContext;
                var row = Descendants<Grid>(border).FirstOrDefault(x => x.ColumnDefinitions.Count == 4);
                var platform = row?.Children.OfType<Border>().FirstOrDefault(x => Grid.GetColumn(x) == 1);
                if (platform is null || Equals(platform.Tag, "MainPlatformLogoReady")) continue;
                platform.Child = PlatformVisual(message.Platform);
                platform.ToolTip = message.Platform;
                platform.Tag = "MainPlatformLogoReady";
            }
        }

        private void PatchMeters()
        {
            var mixer = FindNamed<ContentControl>(_window, "MixerBlockPanel");
            if (mixer is null) return;
            foreach (var card in Descendants<Border>(mixer).Where(x => x.DataContext is AudioChannel).ToList())
            {
                var channel = (AudioChannel)card.DataContext;
                var row = Descendants<Grid>(card).FirstOrDefault(x => x.ColumnDefinitions.Count >= 6 && x.Height > 0);
                if (row is null || Equals(row.Tag, "AbsoluteDbMeterReady")) continue;

                foreach (var old in row.Children.OfType<FrameworkElement>()
                             .Where(x => Grid.GetColumn(x) == 2 && x.GetType().Name.Contains("LiveDbMeter", StringComparison.Ordinal))
                             .ToList())
                {
                    if (old is IDisposable disposable) disposable.Dispose();
                    row.Children.Remove(old);
                }

                var meter = new AbsoluteDbMeter(channel)
                {
                    Margin = new Thickness(7, 4, 12, 4),
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Stretch
                };
                Grid.SetColumn(meter, 2);
                row.Children.Add(meter);
                row.Tag = "AbsoluteDbMeterReady";
            }
        }

        private void Window_Closed(object? sender, EventArgs e) => Dispose();

        public void Dispose()
        {
            _timer.Stop();
            _timer.Tick -= Timer_Tick;
            _window.Closed -= Window_Closed;
        }
    }

    private sealed class AbsoluteDbMeter : FrameworkElement, IDisposable
    {
        private readonly AudioChannel _channel;
        private bool _disposed;

        public AbsoluteDbMeter(AudioChannel channel)
        {
            _channel = channel;
            SnapsToDevicePixels = true;
            _channel.PropertyChanged += Channel_PropertyChanged;
            Unloaded += (_, _) => Dispose();
        }

        private void Channel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(AudioChannel.Db) or nameof(AudioChannel.Meter)) InvalidateVisual();
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);
            var width = Math.Max(0, ActualWidth - 1);
            var height = Math.Max(0, ActualHeight - 1);
            if (width < 2 || height < 2) return;

            var rect = new Rect(0.5, 0.5, width, height);
            var line = new Pen(new SolidColorBrush(Color.FromRgb(40, 90, 120)), 1);
            dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromRgb(1, 8, 15)), line, rect, 3, 3);

            var normalized = _channel.Db > -59.95
                ? Math.Clamp((_channel.Db + 60) / 60, 0, 1)
                : Math.Clamp(_channel.Meter, 0, 1);
            var innerWidth = Math.Max(0, rect.Width - 2);
            var fillWidth = innerWidth * normalized;
            if (fillWidth > 0.5)
            {
                var brush = new LinearGradientBrush
                {
                    MappingMode = BrushMappingMode.Absolute,
                    StartPoint = new Point(0, 0),
                    EndPoint = new Point(innerWidth, 0)
                };
                brush.GradientStops.Add(new GradientStop(Color.FromRgb(23, 215, 102), 0));
                brush.GradientStops.Add(new GradientStop(Color.FromRgb(174, 225, 57), 0.62));
                brush.GradientStops.Add(new GradientStop(Color.FromRgb(245, 176, 0), 0.82));
                brush.GradientStops.Add(new GradientStop(Color.FromRgb(255, 76, 88), 1));
                dc.DrawRoundedRectangle(brush, null, new Rect(1.5, 1.5, fillWidth, Math.Max(1, rect.Height - 2)), 2, 2);
            }

            var tickPen = new Pen(new SolidColorBrush(Color.FromArgb(75, 230, 245, 255)), 0.6);
            foreach (var db in new[] { -48d, -36d, -24d, -12d, -6d })
            {
                var x = 1.5 + (db + 60) / 60 * innerWidth;
                dc.DrawLine(tickPen, new Point(x, rect.Height * 0.22), new Point(x, rect.Height * 0.78));
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _channel.PropertyChanged -= Channel_PropertyChanged;
        }
    }

    private static FrameworkElement PlatformVisual(string platform)
    {
        var upper = platform?.Trim().ToUpperInvariant() ?? string.Empty;
        if (upper == "TWITCH")
            return Logo("/TiHiY.StreamControlCenter;component/Assets/Platforms/twitch.png");
        if (upper == "YOUTUBE")
            return Logo("/TiHiY.StreamControlCenter;component/Assets/Platforms/youtube.png");
        if (upper == "DONATELLO")
            return new TextBlock
            {
                Text = "♥",
                Foreground = new SolidColorBrush(Color.FromRgb(255, 210, 41)),
                FontSize = 16,
                FontWeight = FontWeights.Black,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
        return Logo("/TiHiY.StreamControlCenter;component/Assets/AppIcon.png");
    }

    private static Image Logo(string uri)
    {
        var image = new Image { Stretch = Stretch.Uniform, Margin = new Thickness(2) };
        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri(uri, UriKind.RelativeOrAbsolute);
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();
            bitmap.Freeze();
            image.Source = bitmap;
            RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality);
        }
        catch { }
        return image;
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
}