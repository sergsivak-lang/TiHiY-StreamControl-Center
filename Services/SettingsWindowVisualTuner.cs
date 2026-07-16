using TiHiY.StreamControlCenter.Windows;

namespace TiHiY.StreamControlCenter.Services;

public static class SettingsWindowVisualTuner
{
    private sealed class Controller : IDisposable
    {
        private readonly SettingsWindow _window;
        private bool _disposed;

        public Controller(SettingsWindow window)
        {
            _window = window;
            _window.Loaded += Window_Loaded;
            _window.ContentRendered += Window_ContentRendered;
            _window.Closed += Window_Closed;
            if (_window.IsLoaded) Apply();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e) => Apply();
        private void Window_ContentRendered(object? sender, EventArgs e) =>
            _window.Dispatcher.BeginInvoke(new Action(Apply), DispatcherPriority.ContextIdle);
        private void Window_Closed(object? sender, EventArgs e) => Dispose();

        private void Apply()
        {
            if (_disposed) return;

            var ciCapture = Environment.GetCommandLineArgs()
                .Any(x => x.StartsWith("--ci-screenshot=", StringComparison.OrdinalIgnoreCase));

            _window.SizeToContent = SizeToContent.Manual;

            if (ciCapture)
            {
                // The GitHub runner has a small virtual desktop. Render the approved
                // 1648×928 window logically instead of clamping it to the work area.
                _window.MinWidth = 1180;
                _window.MinHeight = 720;
                _window.MaxWidth = 4096;
                _window.MaxHeight = 2160;
                _window.Width = 1648;
                _window.Height = 928;
                _window.Left = 0;
                _window.Top = 0;
            }
            else
            {
                var work = SystemParameters.WorkArea;
                _window.MinWidth = Math.Min(1180, work.Width);
                _window.MinHeight = Math.Min(720, work.Height);
                _window.MaxWidth = work.Width;
                _window.MaxHeight = work.Height;

                if (_window.Width > work.Width || _window.Width < 1180)
                    _window.Width = Math.Min(1648, work.Width);
                if (_window.Height > work.Height || _window.Height < 720)
                    _window.Height = Math.Min(928, work.Height);
            }

            if (FindNamed<Grid>("DesignSurface") is { } design)
            {
                design.HorizontalAlignment = HorizontalAlignment.Center;
                design.VerticalAlignment = VerticalAlignment.Center;
            }

            if (FindNamed<Image>("ThemePreviewImage") is { } preview)
            {
                preview.Stretch = Stretch.Uniform;
                preview.HorizontalAlignment = HorizontalAlignment.Stretch;
                preview.VerticalAlignment = VerticalAlignment.Stretch;
                preview.SnapsToDevicePixels = true;
                RenderOptions.SetBitmapScalingMode(preview, BitmapScalingMode.HighQuality);
            }
        }

        private T? FindNamed<T>(string name) where T : FrameworkElement =>
            FindDescendants<T>(_window).FirstOrDefault(x => string.Equals(x.Name, name, StringComparison.Ordinal));

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _window.Loaded -= Window_Loaded;
            _window.ContentRendered -= Window_ContentRendered;
            _window.Closed -= Window_Closed;
        }
    }

    private static readonly ConditionalWeakTable<SettingsWindow, Controller> Controllers = new();

    public static IDisposable Attach(SettingsWindow window)
    {
        if (Controllers.TryGetValue(window, out var existing)) return existing;
        var controller = new Controller(window);
        Controllers.Add(window, controller);
        return controller;
    }

    private static IEnumerable<T> FindDescendants<T>(DependencyObject root) where T : DependencyObject
    {
        var visited = new HashSet<DependencyObject>();
        var stack = new Stack<DependencyObject>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            var current = stack.Pop();
            if (!visited.Add(current)) continue;
            if (current is T match) yield return match;
            try
            {
                for (var index = 0; index < VisualTreeHelper.GetChildrenCount(current); index++)
                    stack.Push(VisualTreeHelper.GetChild(current, index));
            }
            catch { }
            try
            {
                foreach (var child in LogicalTreeHelper.GetChildren(current).OfType<DependencyObject>())
                    stack.Push(child);
            }
            catch { }
        }
    }
}