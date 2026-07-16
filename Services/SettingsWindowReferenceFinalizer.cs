using System.Windows.Media.Imaging;
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
        private const int LayoutVersion = 2;
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
                EnsureReferenceTitleStrip();
                StretchThemePage();
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
            var settings = App.Services.Settings.Value;
            var work = SystemParameters.WorkArea;
            var ciCapture = Environment.GetCommandLineArgs()
                .Any(x => x.StartsWith("--ci-screenshot=", StringComparison.OrdinalIgnoreCase));

            if (ciCapture)
            {
                _window.MaxWidth = 4096;
                _window.MaxHeight = 2160;
                _window.Width = 1648;
                _window.Height = 928;
                _window.Left = 0;
                _window.Top = 0;
            }
            else if (settings.SettingsWindowLayoutVersion < LayoutVersion)
            {
                var targetWidth = Math.Min(1648, work.Width);
                var targetHeight = Math.Min(928, work.Height);
                _window.Width = targetWidth;
                _window.Height = targetHeight;
                _window.Left = work.Left + Math.Max(0, (work.Width - targetWidth) / 2);
                _window.Top = work.Top + Math.Max(0, (work.Height - targetHeight) / 2);
                settings.SettingsWindowLayoutVersion = LayoutVersion;
                App.Services.Save();
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

        private void EnsureReferenceTitleStrip()
        {
            if (FindNamed<Grid>("DesignSurface") is not { } design ||
                design.Children.OfType<FrameworkElement>().Any(x => x.Name == "SettingsReferenceTitleStrip"))
                return;

            var mainHeader = design.Children.OfType<Grid>().FirstOrDefault(x => Grid.GetRow(x) == 0);
            if (mainHeader is null || design.RowDefinitions.Count < 3) return;

            design.RowDefinitions[0].Height = new GridLength(132);
            mainHeader.Margin = new Thickness(
                mainHeader.Margin.Left,
                mainHeader.Margin.Top + 28,
                mainHeader.Margin.Right,
                mainHeader.Margin.Bottom);

            foreach (var image in FindDescendants<Image>(mainHeader))
            {
                var source = image.Source?.ToString() ?? string.Empty;
                if (!source.Contains("header-emblem", StringComparison.OrdinalIgnoreCase)) continue;
                image.Height = 112;
                image.Margin = new Thickness(-8, 0, 0, 0);
                break;
            }

            var strip = new Border
            {
                Name = "SettingsReferenceTitleStrip",
                Height = 28,
                VerticalAlignment = VerticalAlignment.Top,
                Background = new SolidColorBrush(Color.FromRgb(3, 14, 25)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(22, 73, 105)),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(8, 0, 0, 0)
            };
            Grid.SetRow(strip, 0);
            Panel.SetZIndex(strip, 500);

            var titleGrid = new Grid();
            titleGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            titleGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            titleGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var icon = new Image
            {
                Width = 18,
                Height = 18,
                Margin = new Thickness(0, 0, 8, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Source = new BitmapImage(new Uri(
                    "pack://application:,,,/TiHiY.StreamControlCenter;component/Assets/AppIcon.png",
                    UriKind.Absolute))
            };
            titleGrid.Children.Add(icon);

            var title = new TextBlock
            {
                Text = "TiHiY StreamControl Center — Налаштування",
                FontSize = 12.5,
                Foreground = Brushes.White,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(title, 1);
            titleGrid.Children.Add(title);

            var controls = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            controls.Children.Add(CreateTitleButton("—", () => _window.WindowState = WindowState.Minimized));
            controls.Children.Add(CreateTitleButton("□", () =>
                _window.WindowState = _window.WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized));
            controls.Children.Add(CreateTitleButton("×", _window.Close, closeButton: true));
            Grid.SetColumn(controls, 2);
            titleGrid.Children.Add(controls);

            strip.Child = titleGrid;
            strip.MouseLeftButtonDown += (_, e) =>
            {
                if (e.LeftButton != MouseButtonState.Pressed) return;
                if (e.ClickCount == 2)
                    _window.WindowState = _window.WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
                else
                {
                    try { _window.DragMove(); }
                    catch (InvalidOperationException) { }
                }
            };
            design.Children.Add(strip);
        }

        private void StretchThemePage()
        {
            if (FindNamed<ListBox>("ThemeList") is not { } list) return;

            var pageGrid = FindAncestorGridWithRows(list, 4);
            if (pageGrid is null) return;

            var scroll = FindAncestor<ScrollViewer>(pageGrid);
            var tab = FindAncestor<TabItem>(scroll ?? pageGrid);
            if (scroll is not null && tab is not null && ReferenceEquals(scroll.Content, pageGrid))
            {
                scroll.Content = null;
                tab.Content = pageGrid;
            }

            pageGrid.VerticalAlignment = VerticalAlignment.Stretch;
            pageGrid.HorizontalAlignment = HorizontalAlignment.Stretch;
            pageGrid.RowDefinitions[0].Height = GridLength.Auto;
            pageGrid.RowDefinitions[1].Height = new GridLength(1.22, GridUnitType.Star);
            pageGrid.RowDefinitions[2].Height = new GridLength(0.75, GridUnitType.Star);
            pageGrid.RowDefinitions[3].Height = new GridLength(0.95, GridUnitType.Star);
        }

        private static Button CreateTitleButton(string text, Action action, bool closeButton = false)
        {
            var button = new Button
            {
                Content = text,
                Width = 45,
                Height = 27,
                Padding = new Thickness(0),
                Margin = new Thickness(0),
                FontSize = 13,
                Foreground = Brushes.White,
                Background = closeButton
                    ? new SolidColorBrush(Color.FromRgb(70, 17, 22))
                    : Brushes.Transparent,
                BorderThickness = new Thickness(0),
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center
            };
            button.Click += (_, _) => action();
            return button;
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

    private static Grid? FindAncestorGridWithRows(DependencyObject source, int rowCount)
    {
        for (var current = source; current is not null; current = GetParent(current))
            if (current is Grid grid && grid.RowDefinitions.Count == rowCount)
                return grid;
        return null;
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