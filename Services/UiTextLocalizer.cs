namespace TiHiY.StreamControlCenter.Services;

public static class UiTextLocalizer
{
    private sealed record TextPair(string Ukrainian, string English);

    private static readonly TextPair[] Pairs =
    [
        new("МАКЕТ: ЗАКРІПЛЕНО", "LAYOUT: LOCKED"),
        new("МАКЕТ: РЕДАГУВАННЯ", "LAYOUT: EDITING"),
        new("ВІДКРИТИ AIDA64 / МОНІТОРИНГ", "OPEN AIDA64 / MONITORING"),
        new("ВІДКРИТИ ДОНАТИ / ВІДЖЕТИ", "OPEN DONATIONS / WIDGETS"),
        new("ВІДКРИТИ МУЗИЧНИЙ ПЛЕЄР", "OPEN MUSIC PLAYER"),
        new("ВІДКРИТИ DISCORD СПОВІЩЕННЯ", "OPEN DISCORD NOTIFICATIONS"),
        new("ВІДКРИТИ КАНАЛИ ТА TWITCH", "OPEN CHANNELS AND TWITCH"),
        new("ВИДАЛИТИ ЗБЕРЕЖЕНИЙ OBS-ПАРОЛЬ", "DELETE SAVED OBS PASSWORD"),
        new("ВІДНОВИТИ СТАНДАРТНИЙ МАКЕТ", "RESTORE DEFAULT LAYOUT"),
        new("ВІДКРИТИ ПАПКУ НАЛАШТУВАНЬ", "OPEN SETTINGS FOLDER"),
        new("ШВИДКИЙ МІКШЕР • AUDIO MIXER OBS", "QUICK MIXER • OBS AUDIO MIXER"),
        new("МУЛЬТИЧАТ • TWITCH + YOUTUBE", "MULTICHAT • TWITCH + YOUTUBE"),
        new("Об’єднаний чат ваших трансляцій у реальному часі", "Combined chat from your live streams in real time"),
        new("Наведіть курсор або виберіть тему, щоб побачити її зразок праворуч.", "Hover or select a theme to preview it on the right."),
        new("Підтримувані мови: Українська та English. Повне перемикання всіх вікон буде доступне через єдиний словник локалізації.", "Supported languages: Ukrainian and English. The selected language is applied to all application windows."),
        new("Налаштування YouTube-трансляції, каналів і overlay зібрані в цьому розділі.", "YouTube broadcast, channel and overlay settings are collected in this section."),
        new("Реальні канали OBS • гучність • Mute", "Real OBS channels • volume • mute"),
        new("ВІДКРИТИ ПОВНИЙ МІКШЕР", "OPEN FULL MIXER"),
        new("ВІДКРИТИ ПОВНИЙ ЖУРНАЛ", "OPEN FULL JOURNAL"),
        new("ВІДКРИТИ YOUTUBE STUDIO", "OPEN YOUTUBE STUDIO"),
        new("КАНАЛИ ТА ОБЛІКОВІ ЗАПИСИ", "CHANNELS AND ACCOUNTS"),
        new("ЗБЕРЕГТИ Й ПІДКЛЮЧИТИ", "SAVE AND CONNECT"),
        new("ТЕМА ТА ІНТЕРФЕЙС / THEME & UI", "THEME & UI"),
        new("Масштаб і поведінка інтерфейсу", "Interface scale and behavior"),
        new("Мова програми / Application language", "Application language"),
        new("Оперативне керування трансляцією", "Live broadcast controls"),
        new("Останніх донатів ще не отримано", "No donations received yet"),
        new("Блокувати макет після запуску", "Lock layout after startup"),
        new("Підключатися автоматично", "Connect automatically"),
        new("Адреса OBS WebSocket", "OBS WebSocket address"),
        new("Запам’ятати пароль", "Remember password"),
        new("Автоматичний масштаб", "Automatic scale"),
        new("ВІДКЛЮЧИТИ OBS", "DISCONNECT OBS"),
        new("НАЛАШТУВАННЯ YOUTUBE", "YOUTUBE SETTINGS"),
        new("ЗАСТОСУВАТИ ТЕМУ", "APPLY THEME"),
        new("ВІДНОВИТИ DEFAULT", "RESTORE DEFAULT"),
        new("Показувати підказки", "Show tooltips"),
        new("Анімації інтерфейсу", "Interface animations"),
        new("ВІДКРИТИ ПАПКУ LOGS", "OPEN LOGS FOLDER"),
        new("Трансляція / Broadcast", "Broadcast"),
        new("Загальні / General", "General"),
        new("Донати / Donations", "Donations"),
        new("Музика / Music", "Music"),
        new("Безпека / Security", "Security"),
        new("Журнал / Logs", "Logs"),
        new("Про програму / About", "About"),
        new("ЗАГАЛЬНІ / GENERAL", "GENERAL"),
        new("ТРАНСЛЯЦІЯ / BROADCAST", "BROADCAST"),
        new("ДОНАТИ / DONATIONS", "DONATIONS"),
        new("МУЗИКА / MUSIC", "MUSIC"),
        new("БЕЗПЕКА / SECURITY", "SECURITY"),
        new("ПРО ПРОГРАМУ / ABOUT", "ABOUT"),
        new("ЖУРНАЛ ПОДІЙ", "EVENT LOG"),
        new("Останні 14 записів", "Last 14 entries"),
        new("Тема та інтерфейс", "Theme and interface"),
        new("Вибір теми", "Theme selection"),
        new("Ручний масштаб:", "Manual scale:"),
        new("ВІДКРИТИ DONATELLO", "OPEN DONATELLO"),
        new("ЗАБУТИ ПАРОЛЬ", "FORGET PASSWORD"),
        new("СЛАВА УКРАЇНІ!", "GLORY TO UKRAINE!"),
        new("ГЕРОЯМ СЛАВА!", "GLORY TO THE HEROES!"),
        new("Напишіть повідомлення…", "Type a message…"),
        new("Подій ще немає", "No events yet"),
        new("МУЗИЧНИЙ ПЛЕЄР", "MUSIC PLAYER"),
        new("СТАН СИСТЕМИ", "SYSTEM STATUS"),
        new("СПОВІЩЕННЯ", "NOTIFICATIONS"),
        new("НАЛАШТУВАННЯ", "SETTINGS"),
        new("ТРАНСЛЯЦІЯ", "BROADCAST"),
        new("2 / 2 ПЕРЕДАЧІ", "2 / 2 STREAMS"),
        new("НЕ ПІДКЛЮЧЕНО", "DISCONNECTED"),
        new("не підключено", "not connected"),
        new("ПІДКЛЮЧЕНО", "CONNECTED"),
        new("підключено", "connected"),
        new("не запущено", "not running"),
        new("працює", "running"),
        new("ПЕРЕВІРЕНО", "CHECKED"),
        new("В ЕФІРІ", "LIVE"),
        new("повідомлень", "messages"),
        new("вибраних каналів", "selected channels"),
        new("каналів", "channels"),
        new("МУЗИКА", "MUSIC"),
        new("ДОНАТИ", "DONATIONS"),
        new("ОБИДВА", "BOTH"),
        new("МАСШТАБ", "SCALE"),
        new("АВТО", "AUTO"),
        new("Пароль", "Password"),
        new("ОЧИСТИТИ", "CLEAR"),
        new("ЗАКРИТИ", "CLOSE"),
        new("ЗБЕРЕГТИ", "SAVE"),
        new("ЗАСТОСУВАТИ", "APPLY"),
        new("СКАСУВАТИ", "CANCEL"),
        new("ОНОВИТИ", "REFRESH"),
        new("ПІДКЛЮЧИТИ", "CONNECT"),
        new("ВІДКЛЮЧИТИ", "DISCONNECT")
    ];

    public static void ApplyToOpenWindows(string languageCode)
    {
        var application = Application.Current;
        if (application is null) return;
        foreach (Window window in application.Windows)
            Apply(window, languageCode);
    }

    public static void Apply(DependencyObject root, string languageCode)
    {
        var english = string.Equals(languageCode, "en-US", StringComparison.OrdinalIgnoreCase);
        var visited = new HashSet<DependencyObject>();
        var pending = new Stack<DependencyObject>();
        pending.Push(root);

        while (pending.Count > 0)
        {
            var current = pending.Pop();
            if (!visited.Add(current)) continue;
            TranslateElement(current, english);

            try
            {
                var visualChildren = VisualTreeHelper.GetChildrenCount(current);
                for (var index = 0; index < visualChildren; index++)
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

    private static void TranslateElement(DependencyObject element, bool english)
    {
        switch (element)
        {
            case Window window:
                window.Title = Translate(window.Title, english);
                break;
            case TextBlock textBlock:
                textBlock.Text = Translate(textBlock.Text, english);
                break;
            case HeaderedContentControl headeredContent:
                if (headeredContent.Header is string headerText)
                    headeredContent.Header = Translate(headerText, english);
                if (headeredContent.Content is string headeredContentText)
                    headeredContent.Content = Translate(headeredContentText, english);
                break;
            case HeaderedItemsControl headeredItems when headeredItems.Header is string itemHeaderText:
                headeredItems.Header = Translate(itemHeaderText, english);
                break;
            case ContentControl contentControl when contentControl.Content is string contentText:
                contentControl.Content = Translate(contentText, english);
                break;
        }

        if (element is FrameworkElement frameworkElement && frameworkElement.ToolTip is string toolTipText)
            frameworkElement.ToolTip = Translate(toolTipText, english);
    }

    private static string Translate(string? value, bool english)
    {
        if (string.IsNullOrEmpty(value)) return value ?? string.Empty;

        var leadingCount = value.Length - value.TrimStart().Length;
        var trailingCount = value.Length - value.TrimEnd().Length;
        var core = value.Trim();

        var exactPair = Pairs.FirstOrDefault(x =>
            string.Equals(x.Ukrainian, core, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(x.English, core, StringComparison.OrdinalIgnoreCase));
        if (exactPair is not null)
        {
            var exactTranslation = english ? exactPair.English : exactPair.Ukrainian;
            return new string(' ', leadingCount) + exactTranslation + new string(' ', trailingCount);
        }

        var orderedPairs = english
            ? Pairs.OrderByDescending(x => x.Ukrainian.Length)
            : Pairs.OrderByDescending(x => x.English.Length);
        foreach (var pair in orderedPairs)
        {
            var source = english ? pair.Ukrainian : pair.English;
            var target = english ? pair.English : pair.Ukrainian;
            if (core.IndexOf(source, StringComparison.OrdinalIgnoreCase) < 0) continue;
            var translated = core.Replace(source, target, StringComparison.OrdinalIgnoreCase);
            return new string(' ', leadingCount) + translated + new string(' ', trailingCount);
        }

        return value;
    }
}
