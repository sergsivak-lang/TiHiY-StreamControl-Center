using TiHiY.StreamControlCenter.Models;
using TiHiY.StreamControlCenter.Windows;

namespace TiHiY.StreamControlCenter.Services;

internal static class PlatformBadgeFinalizer
{
    private static readonly ConditionalWeakTable<Window, Controller> Controllers = new();

    [ModuleInitializer]
    internal static void Register()
    {
        EventManager.RegisterClassHandler(typeof(MainWindow), FrameworkElement.LoadedEvent, new RoutedEventHandler(OnLoaded));
        EventManager.RegisterClassHandler(typeof(ChatBotWindow), FrameworkElement.LoadedEvent, new RoutedEventHandler(OnLoaded));
        EventManager.RegisterClassHandler(typeof(LocalChatOverlayWindow), FrameworkElement.LoadedEvent, new RoutedEventHandler(OnLoaded));
    }

    private static void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is not Window window || Controllers.TryGetValue(window, out _)) return;
        Controllers.Add(window, new Controller(window));
    }

    private sealed class Controller : IDisposable
    {
        private readonly Window _window;
        private readonly DispatcherTimer _timer;

        public Controller(Window window)
        {
            _window = window;
            _window.Closed += Window_Closed;
            _timer = new DispatcherTimer(DispatcherPriority.Render, window.Dispatcher)
            {
                Interval = TimeSpan.FromMilliseconds(180)
            };
            _timer.Tick += Timer_Tick;
            _timer.Start();
            Apply();
        }

        private void Timer_Tick(object? sender, EventArgs e) => Apply();

        private void Apply()
        {
            foreach (var messageHost in Descendants<FrameworkElement>(_window)
                         .Where(x => x.DataContext is ChatMessage)
                         .ToList())
            {
                var message = (ChatMessage)messageHost.DataContext;
                var badge = Descendants<Border>(messageHost)
                    .Where(x => x.Child is Image or PlatformVectorIcon)
                    .OrderBy(x => Math.Max(x.ActualWidth, double.IsNaN(x.Width) ? 0 : x.Width))
                    .FirstOrDefault(x =>
                    {
                        if (x.Child is PlatformVectorIcon) return true;
                        if (x.Child is not Image image) return false;
                        var source = image.Source?.ToString() ?? string.Empty;
                        return source.Contains("Platforms", StringComparison.OrdinalIgnoreCase) ||
                               source.Contains("PlatformIcon", StringComparison.OrdinalIgnoreCase) ||
                               ((double.IsNaN(x.Width) || x.Width <= 34) && (double.IsNaN(x.Height) || x.Height <= 30));
                    });

                if (badge is null || badge.Child is PlatformVectorIcon) continue;
                var availableWidth = double.IsNaN(badge.Width) || badge.Width <= 0 ? 21 : badge.Width;
                var availableHeight = double.IsNaN(badge.Height) || badge.Height <= 0 ? 21 : badge.Height;
                badge.Child = new PlatformVectorIcon(message.Platform)
                {
                    Width = Math.Max(14, availableWidth - 4),
                    Height = Math.Max(14, availableHeight - 4),
                    Margin = new Thickness(1),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                badge.ToolTip = message.Platform;
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
                for (var index = 0; index < VisualTreeHelper.GetChildrenCount(current); index++)
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
}