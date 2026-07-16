using System.Globalization;
using System.Reflection;
using TiHiY.StreamControlCenter.Windows;

namespace TiHiY.StreamControlCenter.Services;

public static class SettingsWindowVisualTuner
{
    private sealed class ThemeItemNameConverter : IValueConverter
    {
        public static ThemeItemNameConverter Instance { get; } = new();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is null) return string.Empty;
            var internalName = ReadString(value, "Name") ?? ReadNestedThemeString(value, "Name") ?? value.ToString() ?? string.Empty;
            return string.Equals(App.Services.Language.CurrentLanguage, "en-US", StringComparison.OrdinalIgnoreCase)
                ? EnglishThemeName(internalName)
                : internalName;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
    }

    private sealed class Controller : IDisposable
    {
        private readonly SettingsWindow _window;
        private ComboBox? _themeCombo;
        private Image? _previewImage;
        private TextBlock? _previewName;
        private TextBlock? _previewDescription;
        private bool _disposed;

        public Controller(SettingsWindow window)
        {
            _window = window;
            _window.Loaded += Window_Loaded;
            _window.SizeChanged += Window_SizeChanged;
            _window.Closed += Window_Closed;
            App.Services.Language.LanguageChanged += Language_Changed;
            if (_window.IsLoaded)
                Apply();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e) => Apply();
        private void Window_SizeChanged(object sender, SizeChangedEventArgs e) => TuneLayout();
        private void Language_Changed(object? sender, EventArgs e) =>
            _window.Dispatcher.BeginInvoke(new Action(Apply), DispatcherPriority.Render);
        private void Window_Closed(object? sender, EventArgs e) => Dispose();

        public void Apply()
        {
            if (_disposed) return;
            _themeCombo ??= FindNamed<ComboBox>("ThemeCombo");
            _previewImage ??= FindNamed<Image>("ThemePreviewImage");
            _previewName ??= FindNamed<TextBlock>("ThemePreviewName");
            _previewDescription ??= FindNamed<TextBlock>("ThemePreviewDescription");

            TuneWindowGeometry();
            TuneLayout();

            if (_previewImage is not null)
            {
                _previewImage.Stretch = Stretch.Uniform;
                _previewImage.HorizontalAlignment = HorizontalAlignment.Center;
                _previewImage.VerticalAlignment = VerticalAlignment.Center;
                _previewImage.Margin = new Thickness(6);
            }

            if (_themeCombo is not null)
            {
                _themeCombo.DisplayMemberPath = string.Empty;
                _themeCombo.ItemTemplate = CreateThemeItemTemplate();
                _themeCombo.MinHeight = 38;
                _themeCombo.SelectionChanged -= ThemeCombo_SelectionChanged;
                _themeCombo.SelectionChanged += ThemeCombo_SelectionChanged;
            }

            UpdatePreviewText();
        }

        private void TuneWindowGeometry()
        {
            var work = SystemParameters.WorkArea;
            _window.SizeToContent = SizeToContent.Manual;
            _window.MinWidth = Math.Min(1120, work.Width);
            _window.MinHeight = Math.Min(700, work.Height);
            _window.MaxWidth = work.Width;
            _window.MaxHeight = work.Height;

            if (_window.Width < 1120 || _window.Width > work.Width)
                _window.Width = Math.Min(1360, work.Width);
            if (_window.Height < 700 || _window.Height > work.Height)
                _window.Height = Math.Min(840, work.Height);
        }

        private void TuneLayout()
        {
            var designSurface = FindNamed<Grid>("DesignSurface");
            if (designSurface is not null)
            {
                designSurface.Margin = new Thickness(10);
                if (designSurface.RowDefinitions.Count >= 3)
                {
                    designSurface.RowDefinitions[0].Height = new GridLength(78);
                    designSurface.RowDefinitions[2].Height = new GridLength(52);
                }
            }

            var tabs = FindDescendants<TabControl>(_window).FirstOrDefault();
            if (tabs is not null)
            {
                tabs.Margin = new Thickness(0);
                tabs.HorizontalContentAlignment = HorizontalAlignment.Stretch;
                tabs.VerticalContentAlignment = VerticalAlignment.Stretch;
                tabs.Background = Brushes.Transparent;
                foreach (var tab in tabs.Items.OfType<TabItem>())
                {
                    tab.MinWidth = 225;
                    tab.MaxWidth = 225;
                    tab.FontSize = 12.5;
                    tab.HorizontalContentAlignment = HorizontalAlignment.Left;
                }
            }

            var previewBorder = FindNamed<Border>("ThemePreviewBorder");
            if (previewBorder is not null)
            {
                previewBorder.MinWidth = 560;
                previewBorder.MinHeight = 470;
                previewBorder.Padding = new Thickness(10);
                previewBorder.Margin = new Thickness(4, 0, 0, 0);

                if (previewBorder.Parent is Grid themeGrid && themeGrid.ColumnDefinitions.Count >= 2)
                {
                    themeGrid.ColumnDefinitions[0].Width = new GridLength(350, GridUnitType.Pixel);
                    themeGrid.ColumnDefinitions[1].Width = new GridLength(1, GridUnitType.Star);
                }
            }

            foreach (var scroll in FindDescendants<ScrollViewer>(_window))
            {
                scroll.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
                scroll.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            }
        }

        private void ThemeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e) => UpdatePreviewText();

        private void UpdatePreviewText()
        {
            if (_themeCombo?.SelectedItem is null) return;
            var internalName = ReadString(_themeCombo.SelectedItem, "Name") ??
                               ReadNestedThemeString(_themeCombo.SelectedItem, "Name") ??
                               _themeCombo.SelectedItem.ToString() ?? string.Empty;
            var originalDescription = ReadString(_themeCombo.SelectedItem, "Description") ??
                                      ReadNestedThemeString(_themeCombo.SelectedItem, "Description") ?? string.Empty;
            var english = string.Equals(App.Services.Language.CurrentLanguage, "en-US", StringComparison.OrdinalIgnoreCase);
            var displayName = english ? EnglishThemeName(internalName) : internalName;
            var displayDescription = english ? EnglishThemeDescription(internalName, originalDescription) : originalDescription;

            if (_previewName is not null) _previewName.Text = displayName;
            if (_previewDescription is not null) _previewDescription.Text = displayDescription;
            _themeCombo.ToolTip = displayDescription;
        }

        private static DataTemplate CreateThemeItemTemplate()
        {
            var text = new FrameworkElementFactory(typeof(TextBlock));
            text.SetBinding(TextBlock.TextProperty, new Binding { Converter = ThemeItemNameConverter.Instance });
            text.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis);
            text.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
            return new DataTemplate { VisualTree = text };
        }

        private T? FindNamed<T>(string name) where T : FrameworkElement =>
            FindDescendants<T>(_window).FirstOrDefault(x => string.Equals(x.Name, name, StringComparison.Ordinal));

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            if (_themeCombo is not null)
                _themeCombo.SelectionChanged -= ThemeCombo_SelectionChanged;
            _window.Loaded -= Window_Loaded;
            _window.SizeChanged -= Window_SizeChanged;
            _window.Closed -= Window_Closed;
            App.Services.Language.LanguageChanged -= Language_Changed;
        }
    }

    private static readonly ConditionalWeakTable<SettingsWindow, Controller> Controllers = new();

    public static IDisposable Attach(SettingsWindow window)
    {
        if (Controllers.TryGetValue(window, out var existing)) return existing;
        var controller = new Controller(window);
        Controllers.Add(window, controller);
        return controller;
    }

    private static string? ReadString(object source, string propertyName) =>
        source.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(source) as string;

    private static string? ReadNestedThemeString(object source, string propertyName)
    {
        var theme = source.GetType().GetProperty("Theme", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(source);
        return theme is null ? null : ReadString(theme, propertyName);
    }

    private static string EnglishThemeName(string name) => name switch
    {
        "Україна" => "Ukraine",
        "Космічна" => "Space",
        "Драйв" => "Drive",
        "Неон" => "Neon",
        "Військова" => "Military",
        "Синтвейв" => "Synthwave",
        "Кіберпанк" => "Cyberpunk",
        "Сталкер" => "Stalker",
        _ => name
    };

    private static string EnglishThemeDescription(string name, string fallback) => name switch
    {
        "Україна" => "Premium dark-blue Ukraine theme with a trident, golden frames, ornaments and Ukrainian accents.",
        "Космічна" => "Deep-space interface with cool blue highlights and a futuristic control-center atmosphere.",
        "Драйв" => "High-energy driving theme with warm accents and dashboard-inspired surfaces.",
        "Неон" => "Bright neon interface with saturated cyber colors and glowing outlines.",
        "Військова" => "Tactical military interface with restrained colors and rugged visual details.",
        "Синтвейв" => "Retro-futuristic synthwave palette with purple, pink and electric-blue accents.",
        "Кіберпанк" => "Dark cyberpunk theme with high-contrast neon controls and industrial details.",
        "Сталкер" => "Atmospheric Zone-inspired theme with worn tactical surfaces and warning accents.",
        _ => string.IsNullOrWhiteSpace(fallback) ? "Application interface theme preview." : fallback
    };

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
