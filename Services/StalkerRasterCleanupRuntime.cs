using System.Runtime.CompilerServices;

namespace TiHiY.StreamControlCenter.Services;

/// <summary>
/// Replaces the broad oval artwork backgrounds on the outer STALKER dashboard surfaces
/// with clean industrial metal. Inner lists, cards, the central poster and all controls
/// keep their existing content and behaviour. Original visuals are restored on theme exit.
/// </summary>
internal static class StalkerRasterCleanupRuntime
{
    private static DispatcherTimer? _timer;
    private static readonly ConditionalWeakTable<FrameworkElement, ElementState> States = new();

    private static readonly Brush CleanPanel = CreateCleanPanelBrush();
    private static readonly Brush CleanHeader = CreateCleanHeaderBrush();

    [ModuleInitializer]
    internal static void Initialize() => _ = StartAsync();

    private static async Task StartAsync()
    {
        for (var attempt = 0; attempt < 600; attempt++)
        {
            await Task.Delay(100).ConfigureAwait(false);
            var app = Application.Current;
            if (app is null) continue;

            await app.Dispatcher.InvokeAsync(() =>
            {
                if (_timer is not null || App.Services is null) return;
                _timer = new DispatcherTimer(DispatcherPriority.ContextIdle, app.Dispatcher)
                {
                    Interval = TimeSpan.FromMilliseconds(180)
                };
                _timer.Tick += (_, _) => Apply();
                _timer.Start();
                Apply();
            });

            if (_timer is not null) return;
        }
    }

    private static void Apply()
    {
        if (Application.Current is null || App.Services is null) return;
        var active = App.Services.Theme.CurrentTheme.Equals("Сталкер", StringComparison.OrdinalIgnoreCase);

        foreach (Window window in Application.Current.Windows)
        {
            var header = FindHeader(window);
            if (header is not null)
            {
                if (active) ApplyBackground(header, CleanHeader);
                else Restore(header);
            }

            foreach (var element in FindVisualChildren<FrameworkElement>(window))
            {
                if (!active)
                {
                    Restore(element);
                    continue;
                }

                if (!IsOuterDashboardSurface(element) || ReferenceEquals(element, header)) continue;
                ApplyBackground(element, CleanPanel);
            }
        }
    }

    private static FrameworkElement? FindHeader(Window window)
    {
        if (window.FindName("DesignSurface") is not Grid surface) return null;

        return surface.Children
            .OfType<FrameworkElement>()
            .FirstOrDefault(element =>
                Grid.GetRow(element) == 0
                && element.ActualWidth > 700
                && element.ActualHeight is > 45 and < 170);
    }

    private static bool IsOuterDashboardSurface(FrameworkElement element)
    {
        if (!element.IsVisible || element.Opacity < 0.25) return false;
        if (element.ActualWidth < 330 || element.ActualHeight < 72) return false;
        if (element is TextBox or ListBox or Button or Image) return false;

        var name = element.Name ?? string.Empty;
        if (ContainsAny(name, "Poster", "Portrait", "MonitorCard", "DonationCard", "ChatItem", "List", "Items"))
        {
            return false;
        }

        if (ContainsAny(name,
                "BlockPanel", "Chat", "Donation", "Mixer", "Notification", "System", "Aida", "Monitoring", "Monitor"))
        {
            return element is Border or Panel or ContentControl;
        }

        // Template-generated outer frames usually have no name. Limit the fallback to
        // broad, shallow dashboard frames so inner cards and the centre poster are spared.
        return string.IsNullOrWhiteSpace(name)
               && element is Border
               && element.ActualWidth > 430
               && element.ActualHeight > 90
               && element.ActualHeight < 430;
    }

    private static bool ContainsAny(string source, params string[] values) =>
        values.Any(value => source.Contains(value, StringComparison.OrdinalIgnoreCase));

    private static void ApplyBackground(FrameworkElement element, Brush brush)
    {
        if (!CanSetBackground(element)) return;
        Save(element);
        SetBackground(element, brush);
    }

    private static Brush CreateCleanHeaderBrush()
    {
        var brush = new LinearGradientBrush(
            new GradientStopCollection
            {
                new(Color.FromRgb(39, 34, 23), 0.00),
                new(Color.FromRgb(22, 22, 17), 0.16),
                new(Color.FromRgb(11, 14, 12), 0.58),
                new(Color.FromRgb(20, 18, 13), 0.88),
                new(Color.FromRgb(51, 34, 18), 1.00)
            },
            new Point(0, 0),
            new Point(0, 1));
        brush.Freeze();
        return brush;
    }

    private static Brush CreateCleanPanelBrush()
    {
        var brush = new LinearGradientBrush(
            new GradientStopCollection
            {
                new(Color.FromRgb(24, 23, 17), 0.00),
                new(Color.FromRgb(14, 17, 14), 0.20),
                new(Color.FromRgb(8, 12, 10), 0.72),
                new(Color.FromRgb(20, 18, 13), 1.00)
            },
            new Point(0, 0),
            new Point(0, 1));
        brush.Freeze();
        return brush;
    }

    private static bool CanSetBackground(FrameworkElement element) =>
        element is Control or Panel or Border;

    private static Brush? GetBackground(FrameworkElement element) => element switch
    {
        Control control => control.Background,
        Panel panel => panel.Background,
        Border border => border.Background,
        _ => null
    };

    private static void SetBackground(FrameworkElement element, Brush? background)
    {
        switch (element)
        {
            case Control control:
                control.Background = background;
                break;
            case Panel panel:
                panel.Background = background;
                break;
            case Border border:
                border.Background = background;
                break;
        }
    }

    private static void Save(FrameworkElement element)
    {
        if (States.TryGetValue(element, out _)) return;
        States.Add(element, new ElementState(GetBackground(element)));
    }

    private static void Restore(FrameworkElement element)
    {
        if (!States.TryGetValue(element, out var state)) return;
        SetBackground(element, state.Background);
        States.Remove(element);
    }

    private static IEnumerable<T> FindVisualChildren<T>(DependencyObject root) where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            if (child is T match) yield return match;
            foreach (var nested in FindVisualChildren<T>(child)) yield return nested;
        }
    }

    private sealed record ElementState(Brush? Background);
}
