namespace TiHiY.StreamControlCenter.Services;

public sealed class WindowManager
{
    private readonly Dictionary<Type, Window> _windows = new();
    private readonly AppLogger _logger;

    public WindowManager(AppLogger logger) => _logger = logger;

    public void Show<T>(Func<T> factory, Window? owner = null) where T : Window
    {
        try
        {
            if (_windows.TryGetValue(typeof(T), out var existing))
            {
                PrepareWindow(existing, owner ?? existing.Owner);
                if (existing.WindowState == WindowState.Minimized)
                    existing.WindowState = WindowState.Normal;
                existing.Show();
                existing.Activate();
                existing.Focus();
                return;
            }

            var window = factory();
            PrepareWindow(window, owner);
            _windows[typeof(T)] = window;
            window.Closed += (_, _) =>
            {
                _windows.Remove(typeof(T));
                ReturnFocus(owner);
            };
            window.Show();
            window.Activate();
            window.Focus();
        }
        catch (Exception ex)
        {
            _logger.Error($"Не вдалося відкрити модуль {typeof(T).Name}", ex);
            MessageBox.Show(owner, $"Не вдалося відкрити модуль.\n\n{ex.Message}\n\nПодробиці записані в журнал.", "TiHiY StreamControl Center", MessageBoxButton.OK, MessageBoxImage.Error);
            ReturnFocus(owner);
        }
    }

    private static void PrepareWindow(Window window, Window? owner)
    {
        if (owner is not null && !ReferenceEquals(window, owner))
        {
            window.Owner = owner;
            window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
        }

        window.HorizontalContentAlignment = HorizontalAlignment.Stretch;
        window.VerticalContentAlignment = VerticalAlignment.Stretch;

        var workArea = SystemParameters.WorkArea;
        window.MaxWidth = workArea.Width;
        window.MaxHeight = workArea.Height;
        if (!double.IsNaN(window.Width) && window.Width > workArea.Width)
            window.Width = workArea.Width;
        if (!double.IsNaN(window.Height) && window.Height > workArea.Height)
            window.Height = workArea.Height;

        if (window.ReadLocalValue(Window.BackgroundProperty) == DependencyProperty.UnsetValue)
            window.SetResourceReference(Window.BackgroundProperty, "WindowGradient");
        if (window.ReadLocalValue(Window.ForegroundProperty) == DependencyProperty.UnsetValue)
            window.SetResourceReference(Window.ForegroundProperty, "Text");
    }

    private static void ReturnFocus(Window? owner)
    {
        if (owner is null) return;
        owner.Dispatcher.BeginInvoke(new Action(() =>
        {
            if (!owner.IsVisible) return;
            if (owner.WindowState == WindowState.Minimized)
                owner.WindowState = WindowState.Normal;
            owner.Activate();
            owner.Focus();
        }), DispatcherPriority.ApplicationIdle);
    }

    public T? Get<T>() where T : Window => _windows.TryGetValue(typeof(T), out var window) ? window as T : null;

    public bool IsOpen<T>() where T : Window => _windows.ContainsKey(typeof(T));

    public void Close<T>() where T : Window
    {
        if (!_windows.TryGetValue(typeof(T), out var window)) return;
        try { window.Close(); } catch { }
        _windows.Remove(typeof(T));
    }

    public void CloseAll()
    {
        foreach (var window in _windows.Values.ToList())
            try { window.Close(); } catch { }
        _windows.Clear();
    }
}
