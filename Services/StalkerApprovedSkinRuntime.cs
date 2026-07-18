using System.Runtime.CompilerServices;

namespace TiHiY.StreamControlCenter.Services;

/// <summary>
/// Applies the approved STALKER treatment to the existing functional controls.
/// It never replaces buttons, indicators, chat, donations or notification modules;
/// only their visual resources are changed while the STALKER theme is active.
/// </summary>
internal static class StalkerApprovedSkinRuntime
{
    private static DispatcherTimer? _timer;
    private static readonly ConditionalWeakTable<FrameworkElement, ElementState> States = new();

    private static readonly Brush WornHeaderMetal = CreateHeaderBrush();
    private static readonly Brush WornPanelMetal = CreatePanelBrush();

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
                content.Background = WornPanelMetal;
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

        if (window.FindName("DesignSurface") is Grid surface)
        {
            var header = surface.Children.OfType<Grid>().FirstOrDefault(x => Grid.GetRow(x) == 0);
            if (header is not null)
            {
                if (active)
                {
                    Save(header);
                    header.Background = WornHeaderMetal;
                }
                else
                {
                    Restore(header);
                }
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

    private static Brush CreateHeaderBrush()
    {
        var group = new DrawingGroup();
        group.Children.Add(new GeometryDrawing(
            new LinearGradientBrush(
                Color.FromRgb(29, 28, 21),
                Color.FromRgb(12, 15, 13),
                90),
            null,
            new RectangleGeometry(new Rect(0, 0, 1, 1))));

        group.Children.Add(new GeometryDrawing(
            new LinearGradientBrush(
                new GradientStopCollection
                {
                    new(Color.FromArgb(130, 126, 77, 31), 0.00),
                    new(Color.FromArgb(35, 126, 77, 31), 0.09),
                    new(Colors.Transparent, 0.22),
                    new(Colors.Transparent, 0.76),
                    new(Color.FromArgb(30, 112, 70, 29), 0.91),
                    new(Color.FromArgb(115, 112, 70, 29), 1.00)
                },
                new Point(0, 0),
                new Point(1, 0)),
            null,
            new RectangleGeometry(new Rect(0, 0, 1, 1))));

        var brush = new DrawingBrush(group)
        {
            Stretch = Stretch.Fill,
            Viewbox = new Rect(0, 0, 1, 1),
            ViewboxUnits = BrushMappingMode.Absolute
        };
        brush.Freeze();
        return brush;
    }

    private static Brush CreatePanelBrush()
    {
        var group = new DrawingGroup();
        group.Children.Add(new GeometryDrawing(
            new LinearGradientBrush(
                new GradientStopCollection
                {
                    new(Color.FromRgb(19, 21, 17), 0.00),
                    new(Color.FromRgb(11, 15, 13), 0.46),
                    new(Color.FromRgb(17, 18, 14), 1.00)
                },
                new Point(0, 0),
                new Point(1, 1)),
            null,
            new RectangleGeometry(new Rect(0, 0, 1, 1))));

        group.Children.Add(new GeometryDrawing(
            new LinearGradientBrush(
                new GradientStopCollection
                {
                    new(Color.FromArgb(85, 127, 78, 31), 0.00),
                    new(Color.FromArgb(18, 127, 78, 31), 0.07),
                    new(Colors.Transparent, 0.18),
                    new(Colors.Transparent, 0.82),
                    new(Color.FromArgb(16, 115, 69, 27), 0.93),
                    new(Color.FromArgb(70, 115, 69, 27), 1.00)
                },
                new Point(0, 0),
                new Point(1, 0)),
            null,
            new RectangleGeometry(new Rect(0, 0, 1, 1))));

        var brush = new DrawingBrush(group)
        {
            Stretch = Stretch.Fill,
            Viewbox = new Rect(0, 0, 1, 1),
            ViewboxUnits = BrushMappingMode.Absolute
        };
        brush.Freeze();
        return brush;
    }

    private static void Save(FrameworkElement element)
    {
        if (States.TryGetValue(element, out _)) return;
        States.Add(element, new ElementState(
            element.Style,
            GetBackground(element)));
    }

    private static void Restore(FrameworkElement element)
    {
        if (!States.TryGetValue(element, out var state)) return;
        element.Style = state.Style;
        SetBackground(element, state.Background);
        States.Remove(element);
    }

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
