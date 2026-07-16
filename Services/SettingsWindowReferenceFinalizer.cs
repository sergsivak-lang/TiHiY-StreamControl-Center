using TiHiY.StreamControlCenter.Windows;

namespace TiHiY.StreamControlCenter.Services;

internal static class SettingsWindowReferenceFinalizer
{
    private static readonly ConditionalWeakTable<SettingsWindow, Controller> Controllers = new();

    [ModuleInitializer]
    internal static void Register()
    {
        EventManager.RegisterClassHandler(
            typeof(SettingsWindow),
            FrameworkElement.LoadedEvent,
            new RoutedEventHandler(OnLoaded));
    }

    private static void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is not SettingsWindow window || Controllers.TryGetValue(window, out _)) return;
        var controller = new Controller(window);
        Controllers.Add(window, controller);
        controller.Apply();
    }

    private sealed class Controller : IDisposable
    {
        private readonly SettingsWindow _window;
        private bool _disposed;

        public Controller(SettingsWindow window)
        {
            _window = window;
            _window.Closed += Window_Closed;
            App.Services.Language.LanguageChanged += Language_Changed;
        }

        public void Apply()
        {
            if (_disposed) return;
            _window.Dispatcher.BeginInvoke(new Action(() =>
            {
                ConfigureLanguageSelector();
                ConfigureReferenceGeometry();
                FixConnectionRows();
            }), DispatcherPriority.ApplicationIdle);
        }

        private void ConfigureLanguageSelector()
        {
            if (FindNamed<ComboBox>("LanguageCombo") is not { } combo) return;

            var selected = App.Services.Settings.Value.UiLanguage;
            combo.ItemsSource = null;
            combo.DisplayMemberPath = string.Empty;
            combo.SelectedValuePath = "Tag";
            combo.Items.Clear();
            combo.Items.Add(new ComboBoxItem
            {
                Content = "🇺🇦  Українська / English",
                Tag = "uk-UA"
            });
            combo.Items.Add(new ComboBoxItem
            {
                Content = "🇬🇧  English / Українська",
                Tag = "en-US"
            });
            combo.SelectedValue = selected;
        }

        private void ConfigureReferenceGeometry()
        {
            var ciCapture = Environment.GetCommandLineArgs()
                .Any(x => x.StartsWith("--ci-screenshot=", StringComparison.OrdinalIgnoreCase));

            if (ciCapture)
            {
                // Render the approved 1648×928 reference instead of compressing it to
                // the GitHub runner's small virtual desktop work area.
                _window.MaxWidth = 4096;
                _window.MaxHeight = 2160;
                _window.Width = 1648;
                _window.Height = 928;
                _window.Left = 0;
                _window.Top = 0;
            }

            if (FindNamed<TabControl>("SettingsTabs") is { } tabs)
            {
                tabs.HorizontalContentAlignment = HorizontalAlignment.Stretch;
                tabs.VerticalContentAlignment = VerticalAlignment.Stretch;
            }

            if (FindNamed<Image>("ThemePreviewImage") is { } preview)
            {
                preview.Stretch = Stretch.Uniform;
                preview.SnapsToDevicePixels = true;
                RenderOptions.SetBitmapScalingMode(preview, BitmapScalingMode.HighQuality);
            }
        }

        private void FixConnectionRows()
        {
            if (FindNamed<TextBlock>("TwitchConnectionText") is not { } twitch ||
                FindNamed<TextBlock>("YouTubeConnectionText") is not { } youtube ||
                FindAncestor<Grid>(twitch) is not { } grid)
                return;

            if (grid.RowDefinitions.Count == 0)
            {
                grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            }
            Grid.SetRow(twitch, 0);
            Grid.SetRow(youtube, 1);
            twitch.Margin = new Thickness(0, 0, 0, 3);
            youtube.Margin = new Thickness(0, 3, 0, 0);
        }

        private void Language_Changed(object? sender, EventArgs e) => Apply();
        private void Window_Closed(object? sender, EventArgs e) => Dispose();

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _window.Closed -= Window_Closed;
            App.Services.Language.LanguageChanged -= Language_Changed;
        }

        private T? FindNamed<T>(string name) where T : FrameworkElement =>
            FindDescendants<T>(_window).FirstOrDefault(x => string.Equals(x.Name, name, StringComparison.Ordinal));
    }

    private static T? FindAncestor<T>(DependencyObject? source) where T : DependencyObject
    {
        for (var current = source; current is not null; current = GetParent(current))
            if (current is T match) return match;
        return null;
    }

    private static DependencyObject? GetParent(DependencyObject current)
    {
        if (current is FrameworkElement element && element.Parent is not null) return element.Parent;
        try { return VisualTreeHelper.GetParent(current); }
        catch { return null; }
    }

    private static IEnumerable<T> FindDescendants<T>(DependencyObject root) where T : DependencyObject
    {
        var visited = new HashSet<DependencyObject>();
        var stack = new Stack<DependencyObject>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            var current = stack.Pop();
            if (!visited.Add(current)) continue;
            if (current is T match) yield return match;
            try
            {
                for (var index = 0; index < VisualTreeHelper.GetChildrenCount(current); index++)
                    stack.Push(VisualTreeHelper.GetChild(current, index));
            }
            catch { }
            try
            {
                foreach (var child in LogicalTreeHelper.GetChildren(current).OfType<DependencyObject>())
                    stack.Push(child);
            }
            catch { }
        }
    }
}