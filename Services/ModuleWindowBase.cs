namespace TiHiY.StreamControlCenter.Services;

public class ModuleWindowBase : Window
{
    private FrameworkElement? _designSurface;
    private double _baseWidth;
    private double _baseHeight;

    protected void ConfigureModule(FrameworkElement designSurface, double baseWidth, double baseHeight, string placementKey)
    {
        _designSurface = designSurface;
        _baseWidth = baseWidth;
        _baseHeight = baseHeight;
        App.Services.Placement.Attach(this, placementKey);
        App.Services.UiScale.ScaleChanged += UiScale_ScaleChanged;
        SizeChanged += (_, _) => { if (App.Services.UiScale.Auto) ApplyScale(); };
        Closed += (_, _) => App.Services.UiScale.ScaleChanged -= UiScale_ScaleChanged;
        ApplyScale();
    }

    protected void ApplyScale()
    {
        if (_designSurface is not null) App.Services.UiScale.Apply(_designSurface, this, _baseWidth, _baseHeight);
    }

    protected void DragTitle(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed) return;
        if (e.ClickCount == 2) WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        else DragMove();
    }

    protected void MinimizeWindow(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    protected void MaximizeWindow(object sender, RoutedEventArgs e) => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    protected void CloseWindow(object sender, RoutedEventArgs e) => Close();

    private void UiScale_ScaleChanged(object? sender, EventArgs e) => Dispatcher.BeginInvoke(new Action(ApplyScale));
}
