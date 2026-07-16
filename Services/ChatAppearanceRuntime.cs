using TiHiY.StreamControlCenter.Models;
using TiHiY.StreamControlCenter.Windows;

namespace TiHiY.StreamControlCenter.Services;

internal static class ChatAppearanceRuntime
{
    private static readonly ConditionalWeakTable<MainWindow, Controller> Controllers = new();

    [ModuleInitializer]
    internal static void Register() =>
        EventManager.RegisterClassHandler(typeof(MainWindow), FrameworkElement.LoadedEvent, new RoutedEventHandler(OnLoaded));

    private static void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is not MainWindow window || Controllers.TryGetValue(window, out _)) return;
        Controllers.Add(window, new Controller(window));
    }

    public static void Apply(MainWindow window)
    {
        if (Controllers.TryGetValue(window, out var controller)) controller.Apply();
        else
        {
            var created = new Controller(window);
            Controllers.Add(window, created);
            created.Apply();
        }
    }

    private sealed class Controller : IDisposable
    {
        private readonly MainWindow _window;
        private readonly DispatcherTimer _timer;
        private Button? _settingsButton;

        public Controller(MainWindow window)
        {
            _window = window;
            _window.Closed += Window_Closed;
            _timer = new DispatcherTimer(DispatcherPriority.Background, window.Dispatcher)
            {
                Interval = TimeSpan.FromMilliseconds(450)
            };
            _timer.Tick += Timer_Tick;
            _timer.Start();
            Apply();
        }

        private void Timer_Tick(object? sender, EventArgs e) => Apply();

        public void Apply()
        {
            if (!_window.IsLoaded) return;
            var settings = App.Services.Settings.Value;
            var input = FindNamed<TextBox>("ChatInput");
            var list = FindNamed<ListBox>("MainChatList");
            if (input is null || list is null) return;

            list.FontSize = Math.Clamp(settings.MainChatFontSize, 11, 28);
            list.Foreground = BrushFrom(settings.MainChatTextColor, "#DCE9F3");
            input.FontSize = Math.Clamp(settings.MainChatInputFontSize, 11, 26);
            input.MinHeight = Math.Clamp(settings.MainChatInputHeight, 36, 64);
            input.Height = Math.Clamp(settings.MainChatInputHeight, 36, 64);
            input.Foreground = BrushFrom(settings.MainChatInputTextColor, "#EAF6FF");
            input.Background = BrushFrom(settings.MainChatInputBackgroundColor, "#071525");
            input.Padding = new Thickness(12, 5, 12, 5);

            foreach (var text in Descendants<TextBlock>(list).Where(x => x.DataContext is ChatMessage))
            {
                if (text.Parent is Grid row && Grid.GetColumn(text) == 3)
                {
                    text.Foreground = list.Foreground;
                    text.FontSize = list.FontSize;
                }
                else if (text.Parent is Grid userRow && Grid.GetColumn(text) == 2)
                {
                    text.FontSize = list.FontSize;
                }
            }

            if (input.Parent is Grid rowGrid)
            {
                var parentGrid = rowGrid.Parent as Grid;
                if (parentGrid is not null && parentGrid.RowDefinitions.Count >= 3)
                    parentGrid.RowDefinitions[2].Height = new GridLength(Math.Clamp(settings.MainChatInputHeight + 14, 50, 78));
                EnsureButtons(rowGrid, input);
            }

            ReplacePlatformBitmaps(_window);
        }

        private void EnsureButtons(Grid grid, TextBox input)
        {
            var twitch = FindNamed<Button>("SendTwitchButton");
            var youtube = FindNamed<Button>("SendYouTubeButton");
            var both = FindNamed<Button>("SendBothButton");
            var send = grid.Children.OfType<Button>().FirstOrDefault(x => Equals(x.Content, "➤"));
            if (twitch is null || youtube is null || both is null || send is null) return;

            if (_settingsButton is null || !grid.Children.Contains(_settingsButton))
            {
                _settingsButton = new Button
                {
                    Width = 44,
                    MinWidth = 44,
                    ToolTip = "Налаштування мультичату, OBS-оверлею та чату поверх гри",
                    Content = new TextBlock
                    {
                        Text = "\uE713",
                        FontFamily = new FontFamily("Segoe MDL2 Assets"),
                        FontSize = 18,
                        Foreground = BrushFrom("#FFD329", "#FFD329"),
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    }
                };
                _settingsButton.SetResourceReference(FrameworkElement.StyleProperty, "TinyActionButton");
                _settingsButton.Click += (_, _) => App.Services.Windows.Show(() => new ChatAppearanceSettingsWindow(), _window);
                grid.Children.Add(_settingsButton);
            }

            grid.ColumnDefinitions.Clear();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = 160 });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.62, GridUnitType.Star), MinWidth = 108 });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.62, GridUnitType.Star), MinWidth = 108 });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.52, GridUnitType.Star), MinWidth = 92 });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(48) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(48) });
            Grid.SetColumn(input, 0);
            Grid.SetColumn(twitch, 1);
            Grid.SetColumn(youtube, 2);
            Grid.SetColumn(both, 3);
            Grid.SetColumn(_settingsButton, 4);
            Grid.SetColumn(send, 5);
            grid.Margin = new Thickness(0, 7, 0, 0);
            grid.MinHeight = Math.Max(48, input.MinHeight);

            youtube.Content = BuildPlatformButtonContent("YOUTUBE", "YOUTUBE");
            twitch.Content = BuildPlatformButtonContent("TWITCH", "TWITCH");
        }

        private static StackPanel BuildPlatformButtonContent(string platform, string caption)
        {
            var panel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
            panel.Children.Add(new PlatformVectorIcon(platform) { Width = 22, Height = 20, Margin = new Thickness(0, 0, 6, 0) });
            panel.Children.Add(new TextBlock { Text = caption, VerticalAlignment = VerticalAlignment.Center });
            return panel;
        }

        private static void ReplacePlatformBitmaps(DependencyObject root)
        {
            foreach (var image in Descendants<Image>(root).ToList())
            {
                var uri = image.Source?.ToString() ?? string.Empty;
                string? platform = uri.Contains("youtube.png", StringComparison.OrdinalIgnoreCase) ? "YOUTUBE"
                    : uri.Contains("twitch.png", StringComparison.OrdinalIgnoreCase) ? "TWITCH" : null;
                if (platform is null) continue;

                var icon = new PlatformVectorIcon(platform)
                {
                    Width = double.IsNaN(image.Width) || image.Width <= 0 ? 22 : image.Width,
                    Height = double.IsNaN(image.Height) || image.Height <= 0 ? 20 : image.Height,
                    Margin = image.Margin,
                    HorizontalAlignment = image.HorizontalAlignment,
                    VerticalAlignment = image.VerticalAlignment
                };

                switch (image.Parent)
                {
                    case Panel panel:
                    {
                        var index = panel.Children.IndexOf(image);
                        panel.Children.RemoveAt(index);
                        panel.Children.Insert(index, icon);
                        break;
                    }
                    case Border border when ReferenceEquals(border.Child, image):
                        border.Child = icon;
                        break;
                    case ContentControl content when ReferenceEquals(content.Content, image):
                        content.Content = icon;
                        break;
                }
            }
        }

        private T? FindNamed<T>(string name) where T : FrameworkElement =>
            Descendants<T>(_window).FirstOrDefault(x => string.Equals(x.Name, name, StringComparison.Ordinal));

        private static Brush BrushFrom(string value, string fallback)
        {
            try
            {
                var text = value.Trim();
                if (!text.StartsWith('#')) text = "#" + text;
                if (!(text.Length is 7 or 9) || !text.Skip(1).All(Uri.IsHexDigit)) text = fallback;
                return new SolidColorBrush((Color)ColorConverter.ConvertFromString(text));
            }
            catch { return new SolidColorBrush((Color)ColorConverter.ConvertFromString(fallback)); }
        }

        private void Window_Closed(object? sender, EventArgs e) => Dispose();

        public void Dispose()
        {
            _timer.Stop();
            _timer.Tick -= Timer_Tick;
            _window.Closed -= Window_Closed;
        }
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        var visited = new HashSet<DependencyObject>();
        var pending = new Stack<DependencyObject>();
        pending.Push(root);
        while (pending.Count > 0)
        {
            var current = pending.Pop();
            if (!visited.Add(current)) continue;
            if (current is T match) yield return match;
            try
            {
                for (var index = 0; index < VisualTreeHelper.GetChildrenCount(current); index++)
                    pending.Push(VisualTreeHelper.GetChild(current, index));
            }
            catch { }
            try
            {
                foreach (var child in LogicalTreeHelper.GetChildren(current).OfType<DependencyObject>())
                    pending.Push(child);
            }
            catch { }
        }
    }
}