using TiHiY.StreamControlCenter.Windows;

namespace TiHiY.StreamControlCenter.Services;

internal static class SettingsCiSectionSelector
{
    [ModuleInitializer]
    internal static void Register() =>
        EventManager.RegisterClassHandler(typeof(SettingsWindow), FrameworkElement.LoadedEvent, new RoutedEventHandler(OnLoaded));

    private static void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is not SettingsWindow window) return;
        var argument = Environment.GetCommandLineArgs()
            .FirstOrDefault(x => x.StartsWith("--ci-settings-section=", StringComparison.OrdinalIgnoreCase));
        if (argument is null) return;
        var requested = argument[(argument.IndexOf('=') + 1)..].Trim('"');
        if (string.IsNullOrWhiteSpace(requested)) return;

        window.Dispatcher.BeginInvoke(new Action(() =>
        {
            var tabs = Descendants<TabControl>(window).FirstOrDefault(x => x.Name == "SettingsTabs");
            if (tabs is null) return;
            var target = tabs.Items.OfType<TabItem>().FirstOrDefault(tab => HeaderContains(tab, requested));
            if (target is not null) target.IsSelected = true;
        }), DispatcherPriority.ApplicationIdle);
    }

    private static bool HeaderContains(TabItem tab, string requested)
    {
        if (tab.Header is not DependencyObject root)
            return tab.Header?.ToString()?.Contains(requested, StringComparison.OrdinalIgnoreCase) == true;
        return Descendants<TextBlock>(root).Any(x =>
            x.Text.Contains(requested, StringComparison.OrdinalIgnoreCase) ||
            Normalize(x.Text).Contains(Normalize(requested), StringComparison.OrdinalIgnoreCase));
    }

    private static string Normalize(string value) => value
        .Replace("-", string.Empty, StringComparison.Ordinal)
        .Replace(" ", string.Empty, StringComparison.Ordinal)
        .ToLowerInvariant();

    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
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