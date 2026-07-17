using System.Runtime.CompilerServices;

namespace TiHiY.StreamControlCenter.Services;

/// <summary>
/// Applies the condensed, weathered STALKER typography to existing text elements.
/// Text content, bindings, commands and hit testing remain unchanged.
/// </summary>
internal static class StalkerTypographyRuntime
{
    private static DispatcherTimer? _timer;
    private static readonly ConditionalWeakTable<System.Windows.Controls.TextBlock, TextState> States = new();
    private static readonly FontFamily Condensed = new("Bahnschrift Condensed, Impact, Segoe UI Semibold");
    private static readonly Brush Bone = new SolidColorBrush(Color.FromRgb(202, 194, 163));
    private static readonly Brush Amber = new SolidColorBrush(Color.FromRgb(205, 150, 55));
    private static readonly Brush Muted = new SolidColorBrush(Color.FromRgb(143, 139, 111));

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
                    Interval = TimeSpan.FromMilliseconds(400)
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
            foreach (var text in FindVisualChildren<System.Windows.Controls.TextBlock>(window))
            {
                if (!active)
                {
                    Restore(text);
                    continue;
                }

                if (!ShouldStyle(text)) continue;
                Save(text);
                text.FontFamily = Condensed;
                text.TextOptions.TextFormattingMode = TextFormattingMode.Display;

                if (text.FontSize >= 24)
                {
                    text.FontWeight = FontWeights.Black;
                    text.Foreground = Bone;
                    text.Effect ??= new System.Windows.Media.Effects.DropShadowEffect
                    {
                        Color = Colors.Black,
                        BlurRadius = 3,
                        ShadowDepth = 2,
                        Opacity = 0.9
                    };
                }
                else if (IsModuleHeading(text))
                {
                    text.FontWeight = FontWeights.Bold;
                    text.Foreground = Amber;
                }
                else if (text.FontSize <= 11)
                {
                    text.Foreground = Muted;
                }
            }
        }
    }

    private static bool ShouldStyle(System.Windows.Controls.TextBlock text)
    {
        if (text.Visibility != Visibility.Visible || text.Opacity < 0.25) return false;
        var value = text.Text ?? string.Empty;
        if (string.IsNullOrWhiteSpace(value)) return false;
        return text.FontSize >= 16 || IsModuleHeading(text);
    }

    private static bool IsModuleHeading(System.Windows.Controls.TextBlock text)
    {
        var value = (text.Text ?? string.Empty).ToUpperInvariant();
        return value.Contains("МУЛЬТИЧАТ")
               || value.Contains("ДОНАТ")
               || value.Contains("СПОВІЩ")
               || value.Contains("МІКШЕР")
               || value.Contains("СТАН СИСТЕМИ")
               || value.Contains("WINDOWS")
               || value.Contains("МОНІТОРИНГ")
               || value.Contains("STREAMCONTROL CENTER");
    }

    private static void Save(System.Windows.Controls.TextBlock text)
    {
        if (States.TryGetValue(text, out _)) return;
        States.Add(text, new TextState(text.FontFamily, text.FontWeight, text.Foreground, text.Effect));
    }

    private static void Restore(System.Windows.Controls.TextBlock text)
    {
        if (!States.TryGetValue(text, out var state)) return;
        text.FontFamily = state.FontFamily;
        text.FontWeight = state.FontWeight;
        text.Foreground = state.Foreground;
        text.Effect = state.Effect;
        States.Remove(text);
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

    private sealed record TextState(FontFamily FontFamily, FontWeight FontWeight, Brush Foreground, Effect? Effect);
}
