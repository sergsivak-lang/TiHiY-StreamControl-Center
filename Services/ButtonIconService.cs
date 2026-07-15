namespace TiHiY.StreamControlCenter.Services;

public static class ButtonIconService
{
    private sealed record IconRule(string[] Keywords, string Glyph);

    private static readonly IconRule[] Rules =
    [
        new(["НАЛАШТУВ", "SETTING"], "\uE713"),
        new(["ТРАНСЛЯЦ", "BROADCAST", "LIVE"], "\uE789"),
        new(["МУЗИК", "MUSIC", "PLAYLIST"], "\uE189"),
        new(["ЗБЕРЕГ", "SAVE"], "\uE74E"),
        new(["ЗАСТОС", "APPLY"], "\uE73E"),
        new(["СКАСУВ", "CANCEL"], "\uE711"),
        new(["ЗАКРИТ", "CLOSE"], "\uE8BB"),
        new(["ПІДКЛЮЧ", "CONNECT"], "\uE8A7"),
        new(["ВІДКЛЮЧ", "DISCONNECT"], "\uE8A8"),
        new(["ОНОВ", "REFRESH"], "\uE72C"),
        new(["ОЧИСТ", "CLEAR"], "\uE74D"),
        new(["ВИДАЛ", "DELETE", "REMOVE"], "\uE74D"),
        new(["ДОДАТ", "ADD"], "\uE710"),
        new(["ВІДКРИТИ ПАПК", "OPEN FOLDER", "FOLDER"], "\uE838"),
        new(["ЖУРНАЛ", "LOG"], "\uE81C"),
        new(["КАНАЛ", "CHANNEL"], "\uE716"),
        new(["DISCORD"], "\uE716"),
        new(["TWITCH"], "\uE7FC"),
        new(["YOUTUBE"], "\uE714"),
        new(["DONATELLO", "ДОНАТ", "DONATION"], "\uE8C7"),
        new(["OBS"], "\uE7F4"),
        new(["OVERLAY"], "\uE7F4"),
        new(["МІКШЕР", "MIXER", "AUDIO"], "\uE9D9"),
        new(["AIDA", "МОНІТОР", "MONITOR"], "\uE950"),
        new(["ТЕСТ", "TEST"], "\uE9D5"),
        new(["СТАРТ", "START", "PLAY"], "\uE768"),
        new(["ПАУЗ", "PAUSE"], "\uE769"),
        new(["СТОП", "STOP"], "\uE71A"),
        new(["НАЗАД", "BACK", "PREVIOUS"], "\uE72B"),
        new(["ДАЛІ", "NEXT"], "\uE72A"),
        new(["КОПІЮВ", "COPY"], "\uE8C8"),
        new(["ЕКСПОРТ", "EXPORT"], "\uEDE1"),
        new(["ІМПОРТ", "IMPORT"], "\uE8B5"),
        new(["ПЕРЕВІР", "CHECK", "VERIFY"], "\uE73E")
    ];

    private static readonly HashSet<string> DynamicButtonNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "LayoutEditButton",
        "ZoomTextButton",
        "ConnectObsButton",
        "SendTwitchButton",
        "SendYouTubeButton",
        "SendBothButton"
    };

    public static void Apply(DependencyObject root)
    {
        foreach (var button in FindDescendants<Button>(root))
            ApplyToButton(button);
    }

    private static void ApplyToButton(Button button)
    {
        if (button.Content is not string label || string.IsNullOrWhiteSpace(label)) return;
        if (button.ContentTemplate is not null || DynamicButtonNames.Contains(button.Name)) return;
        if (label.Length <= 2 || label.All(c => char.IsDigit(c) || char.IsPunctuation(c) || char.IsSymbol(c) || char.IsWhiteSpace(c))) return;

        var normalized = label.Trim().ToUpperInvariant();
        var rule = Rules.FirstOrDefault(x => x.Keywords.Any(normalized.Contains));
        if (rule is null) return;

        var glyph = new TextBlock
        {
            Text = rule.Glyph,
            FontFamily = new FontFamily("Segoe MDL2 Assets"),
            FontSize = Math.Max(13, button.FontSize + 2),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 7, 0)
        };
        glyph.SetResourceReference(TextBlock.ForegroundProperty, "Amber");

        var text = new TextBlock
        {
            Text = label,
            FontFamily = button.FontFamily,
            FontSize = button.FontSize,
            FontWeight = button.FontWeight,
            Foreground = button.Foreground,
            VerticalAlignment = VerticalAlignment.Center,
            TextWrapping = TextWrapping.Wrap
        };

        var content = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        content.Children.Add(glyph);
        content.Children.Add(text);
        button.Content = content;
    }

    private static IEnumerable<T> FindDescendants<T>(DependencyObject root) where T : DependencyObject
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
                var count = VisualTreeHelper.GetChildrenCount(current);
                for (var index = 0; index < count; index++)
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
