using System.Collections.Specialized;
using TiHiY.StreamControlCenter.Models;

namespace TiHiY.StreamControlCenter.Services;

internal static class MainWindowMixerPresentation
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
            _window.QuickAudioPage.CollectionChanged += QuickAudioPage_CollectionChanged;
            _window.Closed += Window_Closed;
            _timer = new DispatcherTimer(DispatcherPriority.Render, window.Dispatcher)
            {
                Interval = TimeSpan.FromMilliseconds(220)
            };
            _timer.Tick += Timer_Tick;
            _timer.Start();
            ScheduleApply();
        }

        private void QuickAudioPage_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => ScheduleApply();
        private void Timer_Tick(object? sender, EventArgs e) => Apply();
        private void Window_Closed(object? sender, EventArgs e) => Dispose();

        private void ScheduleApply()
        {
            _window.Dispatcher.BeginInvoke(new Action(Apply), DispatcherPriority.Loaded);
            _window.Dispatcher.BeginInvoke(new Action(Apply), DispatcherPriority.Render);
            _window.Dispatcher.BeginInvoke(new Action(Apply), DispatcherPriority.ContextIdle);
        }

        private void Apply()
        {
            if (_disposed || !_window.IsLoaded) return;
            var mixer = FindNamed<ContentControl>(_window, "MixerBlockPanel");
            if (mixer is null) return;

            foreach (var slider in Descendants<Slider>(mixer).ToList())
            {
                slider.Visibility = Visibility.Collapsed;
                slider.IsHitTestVisible = false;
                slider.Opacity = 0;
                slider.IsEnabled = false;
                if (slider.Parent is Panel parent)
                    parent.Children.Remove(slider);
            }

            foreach (var meter in Descendants<ProgressBar>(mixer))
            {
                meter.Minimum = 0;
                meter.Maximum = 1;
                meter.Height = 11;
                meter.VerticalAlignment = VerticalAlignment.Center;
                meter.Margin = new Thickness(5, 0, 10, 0);
                meter.Background = new SolidColorBrush(Color.FromRgb(1, 8, 15));
                meter.BorderBrush = new SolidColorBrush(Color.FromRgb(40, 90, 120));
                meter.BorderThickness = new Thickness(1);
                meter.Foreground = MeterBrush();

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

            ApplyConsistentObsCaption(mixer);
        }

        private void ApplyConsistentObsCaption(ContentControl mixer)
        {
            var topStatus = FindNamed<TextBlock>(_window, "ObsStatusText")?.Text ?? string.Empty;
            var displayedConnected = App.Services.Obs.IsConnected ||
                (topStatus.Contains("ПІДКЛЮЧЕНО", StringComparison.OrdinalIgnoreCase) &&
                 !topStatus.Contains("НЕ ПІДКЛЮЧЕНО", StringComparison.OrdinalIgnoreCase));

            foreach (var text in Descendants<TextBlock>(mixer).Where(x =>
                         string.Equals(x.Text, "OBS підключено", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(x.Text, "OBS не підключено", StringComparison.OrdinalIgnoreCase)))
            {
                text.Text = displayedConnected ? "OBS підключено" : "OBS не підключено";
                text.Foreground = new SolidColorBrush(displayedConnected
                    ? Color.FromRgb(23, 215, 102)
                    : Color.FromRgb(255, 76, 88));
                if (text.Parent is StackPanel panel)
                    foreach (var dot in panel.Children.OfType<Ellipse>())
                        dot.Fill = text.Foreground;
            }
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

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _timer.Stop();
            _timer.Tick -= Timer_Tick;
            _window.QuickAudioPage.CollectionChanged -= QuickAudioPage_CollectionChanged;
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
}
