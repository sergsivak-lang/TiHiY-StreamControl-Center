using System.Runtime.CompilerServices;

namespace TiHiY.StreamControlCenter.Services;

/// <summary>
/// Adds the heavy, worn industrial frame treatment from the approved STALKER mock-up
/// to the existing live WPF panels. No control is replaced and hit testing is unchanged.
/// </summary>
internal static class StalkerHeavyFrameRuntime
{
    private static DispatcherTimer? _timer;
    private static readonly ConditionalWeakTable<Border, BorderState> BorderStates = new();
    private static readonly ConditionalWeakTable<Button, ButtonState> ButtonStates = new();

    private static readonly Brush OuterRust = Freeze(new LinearGradientBrush(
        Color.FromRgb(111, 76, 34), Color.FromRgb(40, 34, 22), 90));
    private static readonly Brush InnerRust = Freeze(new SolidColorBrush(Color.FromRgb(84, 57, 29)));
    private static readonly Brush ButtonNormal = Freeze(new LinearGradientBrush(
        Color.FromRgb(47, 42, 29), Color.FromRgb(22, 23, 18), 90));
    private static readonly Brush ButtonHover = Freeze(new LinearGradientBrush(
        Color.FromRgb(105, 68, 31), Color.FromRgb(44, 35, 22), 90));
    private static readonly Brush ButtonPressed = Freeze(new LinearGradientBrush(
        Color.FromRgb(33, 29, 21), Color.FromRgb(92, 57, 27), 90));
    private static readonly Brush ButtonText = Freeze(new SolidColorBrush(Color.FromRgb(225, 198, 133)));
    private static readonly Brush ButtonTextHot = Freeze(new SolidColorBrush(Color.FromRgb(255, 221, 139)));

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
                    Interval = TimeSpan.FromMilliseconds(120)
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
            foreach (var border in FindVisualChildren<Border>(window))
            {
                if (!active)
                {
                    Restore(border);
                    continue;
                }

                if (!IsMajorFrame(border)) continue;
                Save(border);
                border.BorderBrush = border.ActualWidth > 500 ? OuterRust : InnerRust;
                border.BorderThickness = border.ActualWidth > 500
                    ? new Thickness(2.2)
                    : new Thickness(1.35);
                border.CornerRadius = new CornerRadius(1.5);
                border.SnapsToDevicePixels = true;
                border.Effect ??= new System.Windows.Media.Effects.DropShadowEffect
                {
                    Color = Color.FromRgb(0, 0, 0),
                    BlurRadius = 5,
                    ShadowDepth = 1,
                    Opacity = 0.72
                };
            }

            foreach (var button in FindVisualChildren<Button>(window))
            {
                if (IsWindowChromeButton(button)) continue;
                if (!active)
                {
                    Restore(button);
                    continue;
                }

                Save(button);
                button.BorderThickness = button.IsPressed ? new Thickness(2.0) : new Thickness(1.4);
                button.BorderBrush = new LinearGradientBrush(
                    button.IsMouseOver ? Color.FromRgb(177, 116, 46) : Color.FromRgb(127, 87, 39),
                    button.IsPressed ? Color.FromRgb(91, 54, 24) : Color.FromRgb(54, 42, 24),
                    90);
                button.Background = button.IsPressed
                    ? ButtonPressed
                    : button.IsMouseOver ? ButtonHover : ButtonNormal;
                button.Foreground = button.IsMouseOver ? ButtonTextHot : ButtonText;
                button.Padding = new Thickness(
                    Math.Max(button.Padding.Left, 10),
                    Math.Max(button.Padding.Top, 5),
                    Math.Max(button.Padding.Right, 10),
                    Math.Max(button.Padding.Bottom, 5));
                button.Opacity = button.IsEnabled ? 1.0 : 0.52;
            }
        }
    }

    private static bool IsMajorFrame(Border border)
    {
        if (border.ActualWidth < 180 || border.ActualHeight < 42) return false;
        if (border.Opacity < 0.2 || border.Visibility != Visibility.Visible) return false;
        var name = border.Name ?? string.Empty;
        return border.ActualWidth > 340
               || name.Contains("Panel", StringComparison.OrdinalIgnoreCase)
               || name.Contains("Block", StringComparison.OrdinalIgnoreCase)
               || name.Contains("Frame", StringComparison.OrdinalIgnoreCase)
               || name.Contains("Preview", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsWindowChromeButton(Button button)
    {
        var text = button.Content?.ToString() ?? string.Empty;
        return text is "×" or "—" or "□";
    }

    private static void Save(Border border)
    {
        if (BorderStates.TryGetValue(border, out _)) return;
        BorderStates.Add(border, new BorderState(
            border.BorderBrush,
            border.BorderThickness,
            border.CornerRadius,
            border.Effect));
    }

    private static void Restore(Border border)
    {
        if (!BorderStates.TryGetValue(border, out var state)) return;
        border.BorderBrush = state.BorderBrush;
        border.BorderThickness = state.BorderThickness;
        border.CornerRadius = state.CornerRadius;
        border.Effect = state.Effect;
        BorderStates.Remove(border);
    }

    private static void Save(Button button)
    {
        if (ButtonStates.TryGetValue(button, out _)) return;
        ButtonStates.Add(button, new ButtonState(
            button.BorderBrush,
            button.BorderThickness,
            button.Padding,
            button.Background,
            button.Foreground,
            button.Opacity));
    }

    private static void Restore(Button button)
    {
        if (!ButtonStates.TryGetValue(button, out var state)) return;
        button.BorderBrush = state.BorderBrush;
        button.BorderThickness = state.BorderThickness;
        button.Padding = state.Padding;
        button.Background = state.Background;
        button.Foreground = state.Foreground;
        button.Opacity = state.Opacity;
        ButtonStates.Remove(button);
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

    private static T Freeze<T>(T freezable) where T : Freezable
    {
        freezable.Freeze();
        return freezable;
    }

    private sealed record BorderState(
        Brush? BorderBrush,
        Thickness BorderThickness,
        CornerRadius CornerRadius,
        System.Windows.Media.Effects.Effect? Effect);

    private sealed record ButtonState(
        Brush? BorderBrush,
        Thickness BorderThickness,
        Thickness Padding,
        Brush? Background,
        Brush Foreground,
        double Opacity);
}
