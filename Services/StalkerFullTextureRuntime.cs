using System.Runtime.CompilerServices;

namespace TiHiY.StreamControlCenter.Services;

internal static class StalkerFullTextureRuntime
{
    private static readonly ConditionalWeakTable<FrameworkElement, StyleState> States = new();
    private static readonly ConditionalWeakTable<Window, HeaderState> HeaderStates = new();
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
                        Interval = TimeSpan.FromMilliseconds(450)
                    };
                    _timer.Tick += (_, _) => ApplyAll();
                    _timer.Start();
                    ApplyAll();
                });
                if (_timer is not null) return;
            }
            catch
            {
                // Windows and services are still loading.
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
            if (enabled)
            {
                ApplyHeader(window);
                ApplyTree(window);
            }
            else
            {
                RestoreHeader(window);
                RestoreTree(window);
            }
        }
    }

    private static void ApplyTree(DependencyObject root)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is FrameworkElement element)
                ApplyStyle(element);
            ApplyTree(child);
        }
    }

    private static void RestoreTree(DependencyObject root)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is FrameworkElement element)
                Restore(element);
            RestoreTree(child);
        }
    }

    private static void ApplyStyle(FrameworkElement element)
    {
        var key = element switch
        {
            ListBox => "StalkerListBox",
            TabControl => "StalkerTabControl",
            TabItem => "StalkerTabItem",
            CheckBox => "StalkerCheckBox",
            ProgressBar => "StalkerProgressBar",
            Slider => "StalkerSlider",
            GroupBox => "StalkerGroupBox",
            Border border when IsInnerPanel(border) => "StalkerInnerBorder",
            _ => null
        };

        if (key is null || element.TryFindResource(key) is not Style) return;
        Remember(element);
        element.SetResourceReference(FrameworkElement.StyleProperty, key);
    }

    private static bool IsInnerPanel(Border border)
    {
        if (border.Child is null) return false;
        if (border.ActualWidth < 100 || border.ActualHeight < 28) return false;
        if (border.Parent is Button or ContentControl) return false;

        var source = border.Background?.ToString() ?? string.Empty;
        var hasMeaningfulFrame = border.BorderThickness.Left > 0 || border.BorderThickness.Top > 0 ||
                                 border.BorderThickness.Right > 0 || border.BorderThickness.Bottom > 0;
        return hasMeaningfulFrame || source.Contains("Gradient", StringComparison.OrdinalIgnoreCase);
    }

    private static void ApplyHeader(Window window)
    {
        if (HeaderStates.TryGetValue(window, out _)) return;

        var header = FindHeaderBorder(window);
        if (header is null || header.TryFindResource("StalkerWindowHeaderPanel") is not Style) return;

        HeaderStates.Add(window, new HeaderState(header, header.ReadLocalValue(FrameworkElement.StyleProperty)));
        header.SetResourceReference(FrameworkElement.StyleProperty, "StalkerWindowHeaderPanel");
    }

    private static Border? FindHeaderBorder(Window window)
    {
        if (window.FindName("DesignSurface") is Grid design)
        {
            var titleGrid = design.Children.OfType<Grid>().FirstOrDefault(x => Grid.GetRow(x) == 0);
            if (titleGrid is not null)
            {
                var wrapper = new Border
                {
                    Child = null,
                    IsHitTestVisible = false,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Stretch
                };
                Grid.SetRow(wrapper, 0);
                Panel.SetZIndex(wrapper, -1);
                if (!design.Children.OfType<Border>().Any(x => Equals(x.Tag, "StalkerHeaderPlate")))
                {
                    wrapper.Tag = "StalkerHeaderPlate";
                    design.Children.Add(wrapper);
                }
                return wrapper;
            }
        }

        return FindVisualChildren<Border>(window)
            .FirstOrDefault(x => x.ActualHeight is >= 45 and <= 130 && x.ActualWidth > 300);
    }

    private static void RestoreHeader(Window window)
    {
        if (!HeaderStates.TryGetValue(window, out var state)) return;
        RestoreValue(state.Border, FrameworkElement.StyleProperty, state.Style);
        if (Equals(state.Border.Tag, "StalkerHeaderPlate") && state.Border.Parent is Panel panel)
            panel.Children.Remove(state.Border);
        HeaderStates.Remove(window);
    }

    private static void Remember(FrameworkElement element)
    {
        if (States.TryGetValue(element, out _)) return;
        States.Add(element, new StyleState(element.ReadLocalValue(FrameworkElement.StyleProperty)));
    }

    private static void Restore(FrameworkElement element)
    {
        if (!States.TryGetValue(element, out var state)) return;
        RestoreValue(element, FrameworkElement.StyleProperty, state.Style);
        States.Remove(element);
    }

    private static void RestoreValue(DependencyObject element, DependencyProperty property, object value)
    {
        if (value == DependencyProperty.UnsetValue) element.ClearValue(property);
        else element.SetValue(property, value);
    }

    private static IEnumerable<T> FindVisualChildren<T>(DependencyObject root) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T match) yield return match;
            foreach (var nested in FindVisualChildren<T>(child)) yield return nested;
        }
    }

    private sealed record StyleState(object Style);
    private sealed record HeaderState(Border Border, object Style);
}