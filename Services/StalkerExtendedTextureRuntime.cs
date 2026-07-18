using System.Runtime.CompilerServices;

namespace TiHiY.StreamControlCenter.Services;

internal static class StalkerExtendedTextureRuntime
{
    private static readonly ConditionalWeakTable<FrameworkElement, StyleState> States = new();
    private static DispatcherTimer? _timer;

    [ModuleInitializer]
    internal static void InitializeModule() => _ = WaitForApplicationAsync();

    private static async Task WaitForApplicationAsync()
    {
        for (var attempt = 0; attempt < 600; attempt++)
        {
            await Task.Delay(100).ConfigureAwait(false);
            var app = Application.Current;
            if (app is null) continue;
            try
            {
                await app.Dispatcher.InvokeAsync(() =>
                {
                    if (_timer is not null || App.Services is null) return;
                    _timer = new DispatcherTimer(DispatcherPriority.Background, app.Dispatcher)
                    {
                        Interval = TimeSpan.FromMilliseconds(550)
                    };
                    _timer.Tick += (_, _) => ApplyAll();
                    _timer.Start();
                    ApplyAll();
                });
                if (_timer is not null) return;
            }
            catch
            {
                // Application is still creating its windows.
            }
        }
    }

    private static void ApplyAll()
    {
        if (Application.Current is null || App.Services is null) return;
        var enabled = App.Services.Theme.CurrentTheme.Equals("Сталкер", StringComparison.OrdinalIgnoreCase);
        foreach (Window window in Application.Current.Windows)
        {
            if (!window.IsLoaded) continue;
            if (enabled) ApplyTree(window);
            else RestoreTree(window);
        }
    }

    private static void ApplyTree(DependencyObject root)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is FrameworkElement element) ApplyStyle(element);
            ApplyTree(child);
        }
    }

    private static void RestoreTree(DependencyObject root)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is FrameworkElement element) Restore(element);
            RestoreTree(child);
        }
    }

    private static void ApplyStyle(FrameworkElement element)
    {
        var key = element switch
        {
            PasswordBox => "StalkerPasswordBox",
            RadioButton => "StalkerRadioButton",
            DataGrid => "StalkerDataGrid",
            DataGridColumnHeader => "StalkerDataGridColumnHeader",
            DataGridRow => "StalkerDataGridRow",
            TreeView => "StalkerTreeView",
            TreeViewItem => "StalkerTreeViewItem",
            ScrollBar => "StalkerScrollBar",
            Expander => "StalkerExpander",
            Menu => "StalkerMenu",
            MenuItem => "StalkerMenuItem",
            _ => null
        };

        if (key is null || element.TryFindResource(key) is not Style) return;
        if (!States.TryGetValue(element, out _))
            States.Add(element, new StyleState(element.ReadLocalValue(FrameworkElement.StyleProperty)));
        element.SetResourceReference(FrameworkElement.StyleProperty, key);
    }

    private static void Restore(FrameworkElement element)
    {
        if (!States.TryGetValue(element, out var state)) return;
        if (state.Style == DependencyProperty.UnsetValue) element.ClearValue(FrameworkElement.StyleProperty);
        else element.SetValue(FrameworkElement.StyleProperty, state.Style);
        States.Remove(element);
    }

    private sealed record StyleState(object Style);
}