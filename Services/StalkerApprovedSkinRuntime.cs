using System.Runtime.CompilerServices;

namespace TiHiY.StreamControlCenter.Services;

/// <summary>
/// Applies the approved STALKER artwork to the existing functional controls.
/// It never replaces buttons, indicators, chat, donations or notification modules;
/// only their visual resources are changed while the STALKER theme is active.
/// </summary>
internal static class StalkerApprovedSkinRuntime
{
    private static DispatcherTimer? _timer;
    private static readonly ConditionalWeakTable<FrameworkElement, ElementState> States = new();

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
                _timer = new DispatcherTimer(DispatcherPriority.Background, app.Dispatcher)
                {
                    Interval = TimeSpan.FromMilliseconds(300)
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
            ApplyWindow(window, active);
        }
    }

    private static void ApplyWindow(Window window, bool active)
    {
        var headerBrush = window.TryFindResource("StalkerApprovedHeaderArtwork") as Brush;
        var panelBrush = window.TryFindResource("StalkerApprovedPanelArtwork") as Brush;
        var innerBrush = window.TryFindResource("StalkerApprovedInnerArtwork") as Brush;
        var panelStyle = window.TryFindResource("StalkerHudPanel") as Style;
        var buttonStyle = window.TryFindResource("StalkerActionButton") as Style;
        var textBoxStyle = window.TryFindResource("StalkerTextBox") as Style;
        var listStyle = window.TryFindResource("StalkerListBox") as Style;

        foreach (var element in FindVisualChildren<FrameworkElement>(window))
        {
            if (!active)
            {
                Restore(element);
                continue;
            }

            Save(element);

            if (element is ContentControl content && IsDashboardModule(content))
            {
                if (panelStyle is not null) content.Style = panelStyle;
                if (panelBrush is not null) content.Background = panelBrush;
            }
            else if (element is Button button && !IsWindowChromeButton(button))
            {
                if (buttonStyle is not null) button.Style = buttonStyle;
            }
            else if (element is TextBox textBox)
            {
                if (textBoxStyle is not null) textBox.Style = textBoxStyle;
                if (innerBrush is not null) textBox.Background = innerBrush;
            }
            else if (element is ListBox listBox)
            {
                if (listStyle is not null) listBox.Style = listStyle;
                if (innerBrush is not null) listBox.Background = innerBrush;
            }
        }

        if (active && headerBrush is not null && window.FindName("DesignSurface") is Grid surface)
        {
            var header = surface.Children.OfType<Grid>().FirstOrDefault(x => Grid.GetRow(x) == 0);
            if (header is not null)
            {
                Save(header);
                header.Background = headerBrush;
            }
        }
    }

    private static bool IsDashboardModule(ContentControl control)
    {
        var name = control.Name ?? string.Empty;
        return name.Contains("BlockPanel", StringComparison.OrdinalIgnoreCase)
               || name.Contains("Chat", StringComparison.OrdinalIgnoreCase)
               || name.Contains("Donation", StringComparison.OrdinalIgnoreCase)
               || name.Contains("Mixer", StringComparison.OrdinalIgnoreCase)
               || name.Contains("Notification", StringComparison.OrdinalIgnoreCase)
               || name.Contains("System", StringComparison.OrdinalIgnoreCase)
               || name.Contains("Aida", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsWindowChromeButton(Button button)
    {
        var text = button.Content?.ToString() ?? string.Empty;
        return text is "×" or "—" or "□";
    }

    private static void Save(FrameworkElement element)
    {
        if (States.TryGetValue(element, out _)) return;
        States.Add(element, new ElementState(
            element.Style,
            element is Control control ? control.Background : null));
    }

    private static void Restore(FrameworkElement element)
    {
        if (!States.TryGetValue(element, out var state)) return;
        element.Style = state.Style;
        if (element is Control control) control.Background = state.Background;
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

    private sealed record ElementState(Style? Style, Brush? Background);
}
