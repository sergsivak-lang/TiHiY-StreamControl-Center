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
                if (existing.WindowState == WindowState.Minimized) existing.WindowState = WindowState.Normal;
                existing.Show();
                existing.Activate();
                return;
            }
            var window = factory();
            if (owner is not null) window.Owner = owner;
            _windows[typeof(T)] = window;
            window.Closed += (_, _) => _windows.Remove(typeof(T));
            window.Show();
            window.Activate();
        }
        catch (Exception ex)
        {
            _logger.Error($"Не вдалося відкрити модуль {typeof(T).Name}", ex);
            MessageBox.Show(owner, $"Не вдалося відкрити модуль.\n\n{ex.Message}\n\nПодробиці записані в журнал.", "TiHiY StreamControl Center", MessageBoxButton.OK, MessageBoxImage.Error);
        }
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
