using System.Windows.Threading;

namespace TiHiY.StreamControlCenter.Services;

public static class MainWindowVisualTuner
{
    private sealed class Controller : IDisposable
    {
        private readonly MainWindow _window;
        private readonly DispatcherTimer _guardTimer;
        private bool _disposed;

        public Controller(MainWindow window)
        {
            _window = window;
            _guardTimer = new DispatcherTimer(DispatcherPriority.Background, window.Dispatcher)
            {
                Interval = TimeSpan.FromMilliseconds(750)
            };
            _guardTimer.Tick += GuardTimer_Tick;
            _window.Loaded += Window_Loaded;
            _window.SizeChanged += Window_SizeChanged;
            _window.Closed += Window_Closed;

            if (_window.IsLoaded)
                ApplyNow();
            _guardTimer.Start();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e) => ApplyNow();
        private void Window_SizeChanged(object sender, SizeChangedEventArgs e) => ApplyNow();
        private void GuardTimer_Tick(object? sender, EventArgs e) => EnforceAidaHeader();
        private void Window_Closed(object? sender, EventArgs e) => Dispose();

        public void ApplyNow()
        {
            if (_disposed) return;
            TunePatrioticCard();
            TuneAidaCard();
            EnforceAidaHeader();
        }

        private void TunePatrioticCard()
        {
            var gloryLine = FindDescendants<TextBlock>(_window).FirstOrDefault(x =>
                x.Text.Contains("СЛАВА УКРАЇНІ", StringComparison.OrdinalIgnoreCase) ||
                x.Text.Contains("GLORY TO UKRAINE", StringComparison.OrdinalIgnoreCase));
            if (gloryLine?.Parent is not StackPanel textStack || textStack.Parent is not Grid cardGrid)
                return;

            textStack.VerticalAlignment = VerticalAlignment.Bottom;
            textStack.HorizontalAlignment = HorizontalAlignment.Center;
            textStack.Margin = new Thickness(8, 0, 8, 6);

            foreach (var text in textStack.Children.OfType<TextBlock>())
            {
                text.FontSize = 14;
                text.LineHeight = 16;
                text.Margin = new Thickness(0);
                text.TextAlignment = TextAlignment.Center;
            }

            var emblem = cardGrid.Children.OfType<Image>().FirstOrDefault();
            if (emblem is not null)
            {
                emblem.Width = 92;
                emblem.Height = 92;
                emblem.VerticalAlignment = VerticalAlignment.Top;
                emblem.HorizontalAlignment = HorizontalAlignment.Center;
                emblem.Margin = new Thickness(0, -5, 0, 0);
            }

            if (FindAncestor<ContentControl>(cardGrid) is { } card)
                card.Padding = new Thickness(6);
        }

        private void TuneAidaCard()
        {
            if (_window.FindName("SystemMonitorPanel") is ContentControl panel)
                panel.Padding = new Thickness(10, 7);

            foreach (var name in new[]
                     {
                         "CpuTemperatureMonitorText",
                         "GpuTemperatureMonitorText",
                         "GpuLoadMonitorText",
                         "ObsFpsText"
                     })
            {
                if (_window.FindName(name) is not TextBlock valueText) continue;
                if (FindAncestor<Border>(valueText) is not { } circle) continue;
                circle.Width = 72;
                circle.Height = 72;
                circle.CornerRadius = new CornerRadius(36);
                circle.Margin = new Thickness(3, 0, 3, 1);
                valueText.FontSize = name == "ObsFpsText" ? 10 : 15;
            }

            if (_window.FindName("AidaStatusText") is TextBlock header)
            {
                header.FontSize = 15;
                header.FontWeight = FontWeights.Bold;
            }
        }

        private void EnforceAidaHeader()
        {
            if (_disposed || !_window.IsVisible) return;
            if (_window.FindName("AidaStatusText") is not TextBlock header) return;
            header.Text = "AIDA64 LIVE";
            if (_window.TryFindResource("Amber") is Brush amber)
                header.Foreground = amber;
            if (_window.FindName("AidaStatusDot") is Ellipse dot && _window.TryFindResource("Green") is Brush green)
                dot.Fill = green;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _guardTimer.Stop();
            _guardTimer.Tick -= GuardTimer_Tick;
            _window.Loaded -= Window_Loaded;
            _window.SizeChanged -= Window_SizeChanged;
            _window.Closed -= Window_Closed;
        }
    }

    private static readonly ConditionalWeakTable<MainWindow, Controller> Controllers = new();

    public static IDisposable Attach(MainWindow window)
    {
        if (Controllers.TryGetValue(window, out var existing))
            return existing;
        var controller = new Controller(window);
        Controllers.Add(window, controller);
        return controller;
    }

    public static void ApplyNow(MainWindow window)
    {
        if (Controllers.TryGetValue(window, out var controller))
            controller.ApplyNow();
        else
            _ = Attach(window);
    }

    private static IEnumerable<T> FindDescendants<T>(DependencyObject root) where T : DependencyObject
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
                var count = VisualTreeHelper.GetChildrenCount(current);
                for (var index = 0; index < count; index++)
                    pending.Push(VisualTreeHelper.GetChild(current, index));
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
        for (var current = source; current is not null; current = GetParent(current))
            if (current is T match) return match;
        return null;
    }

    private static DependencyObject? GetParent(DependencyObject current)
    {
        if (current is FrameworkElement element && element.Parent is not null)
            return element.Parent;
        if (current is FrameworkContentElement contentElement)
            return contentElement.Parent;
        try { return VisualTreeHelper.GetParent(current); }
        catch { return null; }
    }
}
