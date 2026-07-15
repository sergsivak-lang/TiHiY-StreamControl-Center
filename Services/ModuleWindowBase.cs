namespace TiHiY.StreamControlCenter.Services;

public class ModuleWindowBase : Window
{
    private FrameworkElement? _designSurface;
    private double _baseWidth;
    private double _baseHeight;

    public ModuleWindowBase()
    {
        // Service windows use the same cyber button family, but remain compact.
        if (Application.Current?.TryFindResource("UtilityButton") is Style utilityButton)
            Resources[typeof(Button)] = utilityButton;

        PreviewMouseLeftButtonDown += ModuleWindow_PreviewMouseLeftButtonDown;
    }

    protected void ConfigureModule(FrameworkElement designSurface, double baseWidth, double baseHeight, string placementKey)
    {
        _designSurface = designSurface;
        _baseWidth = baseWidth;
        _baseHeight = baseHeight;
        App.Services.Placement.Attach(this, placementKey);
        App.Services.UiScale.ScaleChanged += UiScale_ScaleChanged;

        Loaded += ModuleWindow_Loaded;
        SizeChanged += ModuleWindow_SizeChanged;
        Closed += ModuleWindow_Closed;

        if (IsLoaded)
            Dispatcher.BeginInvoke(new Action(ApplyScale), DispatcherPriority.Loaded);
    }

    protected void ApplyScale()
    {
        if (_designSurface is null || ActualWidth < 40 || ActualHeight < 40) return;
        App.Services.UiScale.Apply(_designSurface, this, _baseWidth, _baseHeight);
    }


    private void ModuleWindow_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || IsInteractiveDragSource(e.OriginalSource)) return;
        if (e.ClickCount == 2)
        {
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
            e.Handled = true;
            return;
        }
        try
        {
            DragMove();
            e.Handled = true;
        }
        catch (InvalidOperationException) { }
    }

    private static bool IsInteractiveDragSource(object? source)
    {
        for (var current = source as DependencyObject; current is not null; current = GetInputParent(current))
        {
            if (current is ButtonBase or TextBoxBase or PasswordBox or Slider or Thumb or ScrollBar or GridSplitter or Selector or MenuItem)
                return true;
            if (current is ListBoxItem or ComboBoxItem)
                return true;
        }
        return false;
    }

    private static DependencyObject? GetInputParent(DependencyObject current)
    {
        if (current is FrameworkContentElement contentElement) return contentElement.Parent;
        if (current is FrameworkElement element && element.Parent is not null) return element.Parent;
        try { return VisualTreeHelper.GetParent(current); }
        catch { return null; }
    }

    protected void DragTitle(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed) return;
        if (e.ClickCount == 2)
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        else
            DragMove();
    }

    protected void MinimizeWindow(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    protected void MaximizeWindow(object sender, RoutedEventArgs e) => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    protected void CloseWindow(object sender, RoutedEventArgs e) => Close();

    private void ModuleWindow_Loaded(object sender, RoutedEventArgs e) =>
        Dispatcher.BeginInvoke(new Action(ApplyScale), DispatcherPriority.Loaded);

    private void ModuleWindow_SizeChanged(object sender, SizeChangedEventArgs e) => ApplyScale();

    private void ModuleWindow_Closed(object? sender, EventArgs e)
    {
        App.Services.UiScale.ScaleChanged -= UiScale_ScaleChanged;
        Loaded -= ModuleWindow_Loaded;
        SizeChanged -= ModuleWindow_SizeChanged;
        Closed -= ModuleWindow_Closed;
        PreviewMouseLeftButtonDown -= ModuleWindow_PreviewMouseLeftButtonDown;
    }

    private void UiScale_ScaleChanged(object? sender, EventArgs e) =>
        Dispatcher.BeginInvoke(new Action(ApplyScale), DispatcherPriority.Loaded);
}
