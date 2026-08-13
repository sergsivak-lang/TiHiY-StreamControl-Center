namespace TiHiY.StreamControlCenter;

public partial class MainWindow
{
    static MainWindow()
    {
        EventManager.RegisterClassHandler(typeof(MainWindow), FrameworkElement.LoadedEvent, new RoutedEventHandler(MainWindowLoadedForTwitchModes));
    }

    private static void MainWindowLoadedForTwitchModes(object sender, RoutedEventArgs e)
    {
        if (sender is not MainWindow window) return;
        if (FindVisualChild<WrapPanel>(window.ModulesBlockPanel) is not WrapPanel modules) return;
        if (modules.Children.OfType<Button>().Any(b => Equals(b.Tag, "TiHiY-TwitchModes"))) return;

        var button = new Button
        {
            Style = window.FindResource("ModuleButton") as Style,
            Tag = "TiHiY-TwitchModes",
            Content = "TWITCH\nSTREAM MODES",
            Width = 148,
            Height = 44,
            Margin = new Thickness(2)
        };
        button.Click += (_, _) =>
        {
            var child = new Windows.TwitchModesWindow { Owner = window };
            child.ShowDialog();
        };
        modules.Children.Add(button);
    }

    private static T? FindVisualChild<T>(DependencyObject root) where T : DependencyObject
    {
        if (root is T wanted) return wanted;
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var found = FindVisualChild<T>(VisualTreeHelper.GetChild(root, i));
            if (found is not null) return found;
        }
        return null;
    }
}
