namespace TiHiY.StreamControlCenter.Services;

public static class UiTextLocalizer
{
    private sealed record TextEntry(string Source, string Ukrainian, string English);
    private sealed class TextState
    {
        public string Original { get; set; } = string.Empty;
        public string LastApplied { get; set; } = string.Empty;
    }

    private static readonly ConditionalWeakTable<DependencyObject, Dictionary<string, TextState>> States = new();

    private static readonly TextEntry[] Entries =
    [
        new("НАЛАШТУВАННЯ  •  SETTINGS", "НАЛАШТУВАННЯ", "SETTINGS"),
        new("Загальні / General", "Загальні", "General"),
        new("Трансляція / Broadcast", "Трансляція", "Broadcast"),
        new("Донати / Donations", "Донати", "Donations"),
        new("Музика / Music", "Музика", "Music"),
        new("Безпека / Security", "Безпека", "Security"),
        new("Журнал / Logs", "Журнал", "Logs"),
        new("Про програму / About", "Про програму", "About"),
        new("ТЕМА ТА ІНТЕРФЕЙС / THEME & UI", "ТЕМА ТА ІНТЕРФЕЙС", "THEME & UI"),
        new("ЗАГАЛЬНІ / GENERAL", "ЗАГАЛЬНІ", "GENERAL"),
        new("ТРАНСЛЯЦІЯ / BROADCAST", "ТРАНСЛЯЦІЯ", "BROADCAST"),
        new("ДОНАТИ / DONATIONS", "ДОНАТИ", "DONATIONS"),
        new("МУЗИКА / MUSIC", "МУЗИКА", "MUSIC"),
        new("БЕЗПЕКА / SECURITY", "БЕЗПЕКА", "SECURITY"),
        new("ПРО ПРОГРАМУ / ABOUT", "ПРО ПРОГРАМУ", "ABOUT"),
        new("МАКЕТ: ЗАКРІПЛЕНО", "МАКЕТ: ЗАКРІПЛЕНО", "LAYOUT: LOCKED"),
        new("МАКЕТ: РЕДАГУВАННЯ", "МАКЕТ: РЕДАГУВАННЯ", "LAYOUT: EDITING"),
        new("ВІДКРИТИ AIDA64 / МОНІТОРИНГ", "ВІДКРИТИ AIDA64 / МОНІТОРИНГ", "OPEN AIDA64 / MONITORING"),
        new("ВІДКРИТИ ДОНАТИ / ВІДЖЕТИ", "ВІДКРИТИ ДОНАТИ / ВІДЖЕТИ", "OPEN DONATIONS / WIDGETS"),
        new("ВІДКРИТИ МУЗИЧНИЙ ПЛЕЄР", "ВІДКРИТИ МУЗИЧНИЙ ПЛЕЄР", "OPEN MUSIC PLAYER"),
        new("ВІДКРИТИ DISCORD СПОВІЩЕННЯ", "ВІДКРИТИ DISCORD СПОВІЩЕННЯ", "OPEN DISCORD NOTIFICATIONS"),
        new("ВІДКРИТИ КАНАЛИ ТА TWITCH", "ВІДКРИТИ КАНАЛИ ТА TWITCH", "OPEN CHANNELS AND TWITCH"),
        new("ВИДАЛИТИ ЗБЕРЕЖЕНИЙ OBS-ПАРОЛЬ", "ВИДАЛИТИ ЗБЕРЕЖЕНИЙ OBS-ПАРОЛЬ", "DELETE SAVED OBS PASSWORD"),
        new("ВІДНОВИТИ СТАНДАРТНИЙ МАКЕТ", "ВІДНОВИТИ СТАНДАРТНИЙ МАКЕТ", "RESTORE DEFAULT LAYOUT"),
        new("ВІДКРИТИ ПАПКУ НАЛАШТУВАНЬ", "ВІДКРИТИ ПАПКУ НАЛАШТУВАНЬ", "OPEN SETTINGS FOLDER"),
        new("ШВИДКИЙ МІКШЕР • AUDIO MIXER OBS", "ШВИДКИЙ МІКШЕР • AUDIO MIXER OBS", "QUICK MIXER • OBS AUDIO MIXER"),
        new("МУЛЬТИЧАТ • TWITCH + YOUTUBE", "МУЛЬТИЧАТ • TWITCH + YOUTUBE", "MULTICHAT • TWITCH + YOUTUBE"),
        new("Об’єднаний чат ваших трансляцій у реальному часі", "Об’єднаний чат ваших трансляцій у реальному часі", "Combined chat from your live streams in real time"),
        new("Наведіть курсор або виберіть тему, щоб побачити її зразок праворуч.", "Наведіть курсор або виберіть тему, щоб побачити її зразок праворуч.", "Hover or select a theme to preview it on the right."),
        new("Підтримувані мови: Українська та English. Повне перемикання всіх вікон буде доступне через єдиний словник локалізації.", "Підтримувані мови: Українська та English. Вибрана мова застосовується до всіх вікон програми.", "Supported languages: Ukrainian and English. The selected language is applied to all application windows."),
        new("Налаштування YouTube-трансляції, каналів і overlay зібрані в цьому розділі.", "Налаштування YouTube-трансляції, каналів і overlay зібрані в цьому розділі.", "YouTube broadcast, channel and overlay settings are collected in this section."),
        new("Реальні канали OBS • гучність • Mute", "Реальні канали OBS • гучність • Mute", "Real OBS channels • volume • mute"),
        new("ВІДКРИТИ ПОВНИЙ МІКШЕР", "ВІДКРИТИ ПОВНИЙ МІКШЕР", "OPEN FULL MIXER"),
        new("ВІДКРИТИ ПОВНИЙ ЖУРНАЛ", "ВІДКРИТИ ПОВНИЙ ЖУРНАЛ", "OPEN FULL JOURNAL"),
        new("ВІДКРИТИ YOUTUBE STUDIO", "ВІДКРИТИ YOUTUBE STUDIO", "OPEN YOUTUBE STUDIO"),
        new("КАНАЛИ ТА ОБЛІКОВІ ЗАПИСИ", "КАНАЛИ ТА ОБЛІКОВІ ЗАПИСИ", "CHANNELS AND ACCOUNTS"),
        new("ЗБЕРЕГТИ Й ПІДКЛЮЧИТИ", "ЗБЕРЕГТИ Й ПІДКЛЮЧИТИ", "SAVE AND CONNECT"),
        new("Масштаб і поведінка інтерфейсу", "Масштаб і поведінка інтерфейсу", "Interface scale and behavior"),
        new("Мова програми / Application language", "Мова програми", "Application language"),
        new("Оперативне керування трансляцією", "Оперативне керування трансляцією", "Live broadcast controls"),
        new("Останніх донатів ще не отримано", "Останніх донатів ще не отримано", "No donations received yet"),
        new("Блокувати макет після запуску", "Блокувати макет після запуску", "Lock layout after startup"),
        new("Підключатися автоматично", "Підключатися автоматично", "Connect automatically"),
        new("Адреса OBS WebSocket", "Адреса OBS WebSocket", "OBS WebSocket address"),
        new("Запам’ятати пароль", "Запам’ятати пароль", "Remember password"),
        new("Автоматичний масштаб", "Автоматичний масштаб", "Automatic scale"),
        new("ВІДКЛЮЧИТИ OBS", "ВІДКЛЮЧИТИ OBS", "DISCONNECT OBS"),
        new("НАЛАШТУВАННЯ YOUTUBE", "НАЛАШТУВАННЯ YOUTUBE", "YOUTUBE SETTINGS"),
        new("ЗАСТОСУВАТИ ТЕМУ", "ЗАСТОСУВАТИ ТЕМУ", "APPLY THEME"),
        new("ВІДНОВИТИ DEFAULT", "ВІДНОВИТИ DEFAULT", "RESTORE DEFAULT"),
        new("Показувати підказки", "Показувати підказки", "Show tooltips"),
        new("Анімації інтерфейсу", "Анімації інтерфейсу", "Interface animations"),
        new("ВІДКРИТИ ПАПКУ LOGS", "ВІДКРИТИ ПАПКУ LOGS", "OPEN LOGS FOLDER"),
        new("ЖУРНАЛ ПОДІЙ", "ЖУРНАЛ ПОДІЙ", "EVENT LOG"),
        new("Останні 14 записів", "Останні 14 записів", "Last 14 entries"),
        new("Тема та інтерфейс", "Тема та інтерфейс", "Theme and interface"),
        new("Вибір теми", "Вибір теми", "Theme selection"),
        new("Ручний масштаб:", "Ручний масштаб:", "Manual scale:"),
        new("ВІДКРИТИ DONATELLO", "ВІДКРИТИ DONATELLO", "OPEN DONATELLO"),
        new("ЗАБУТИ ПАРОЛЬ", "ЗАБУТИ ПАРОЛЬ", "FORGET PASSWORD"),
        new("СЛАВА УКРАЇНІ!", "СЛАВА УКРАЇНІ!", "GLORY TO UKRAINE!"),
        new("ГЕРОЯМ СЛАВА!", "ГЕРОЯМ СЛАВА!", "GLORY TO THE HEROES!"),
        new("Напишіть повідомлення…", "Напишіть повідомлення…", "Type a message…"),
        new("Подій ще немає", "Подій ще немає", "No events yet"),
        new("МУЗИЧНИЙ ПЛЕЄР", "МУЗИЧНИЙ ПЛЕЄР", "MUSIC PLAYER"),
        new("СТАН СИСТЕМИ", "СТАН СИСТЕМИ", "SYSTEM STATUS"),
        new("СПОВІЩЕННЯ", "СПОВІЩЕННЯ", "NOTIFICATIONS"),
        new("НАЛАШТУВАННЯ", "НАЛАШТУВАННЯ", "SETTINGS"),
        new("ТРАНСЛЯЦІЯ", "ТРАНСЛЯЦІЯ", "BROADCAST"),
        new("2 / 2 ПЕРЕДАЧІ", "2 / 2 ПЕРЕДАЧІ", "2 / 2 STREAMS"),
        new("НЕ ПІДКЛЮЧЕНО", "НЕ ПІДКЛЮЧЕНО", "DISCONNECTED"),
        new("не підключено", "не підключено", "not connected"),
        new("ПІДКЛЮЧЕНО", "ПІДКЛЮЧЕНО", "CONNECTED"),
        new("підключено", "підключено", "connected"),
        new("не запущено", "не запущено", "not running"),
        new("працює", "працює", "running"),
        new("ПЕРЕВІРЕНО", "ПЕРЕВІРЕНО", "CHECKED"),
        new("В ЕФІРІ", "В ЕФІРІ", "LIVE"),
        new("повідомлень", "повідомлень", "messages"),
        new("вибраних каналів", "вибраних каналів", "selected channels"),
        new("каналів", "каналів", "channels"),
        new("МУЗИКА", "МУЗИКА", "MUSIC"),
        new("ДОНАТИ", "ДОНАТИ", "DONATIONS"),
        new("ОБИДВА", "ОБИДВА", "BOTH"),
        new("МАСШТАБ", "МАСШТАБ", "SCALE"),
        new("АВТО", "АВТО", "AUTO"),
        new("Пароль", "Пароль", "Password"),
        new("ОЧИСТИТИ", "ОЧИСТИТИ", "CLEAR"),
        new("ЗАКРИТИ", "ЗАКРИТИ", "CLOSE"),
        new("ЗБЕРЕГТИ", "ЗБЕРЕГТИ", "SAVE"),
        new("ЗАСТОСУВАТИ", "ЗАСТОСУВАТИ", "APPLY"),
        new("СКАСУВАТИ", "СКАСУВАТИ", "CANCEL"),
        new("ОНОВИТИ", "ОНОВИТИ", "REFRESH"),
        new("ПІДКЛЮЧИТИ", "ПІДКЛЮЧИТИ", "CONNECT"),
        new("ВІДКЛЮЧИТИ", "ВІДКЛЮЧИТИ", "DISCONNECT")
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
                ApplyProperty(element, "Title", window.Title, english, value => window.Title = value);
                break;
            case TextBlock textBlock:
                ApplyProperty(element, "Text", textBlock.Text, english, value => textBlock.Text = value);
                break;
            case HeaderedContentControl headeredContent:
                if (headeredContent.Header is string headerText)
                    ApplyProperty(element, "Header", headerText, english, value => headeredContent.Header = value);
                if (headeredContent.Content is string headeredContentText)
                    ApplyProperty(element, "Content", headeredContentText, english, value => headeredContent.Content = value);
                break;
            case HeaderedItemsControl headeredItems when headeredItems.Header is string itemHeaderText:
                ApplyProperty(element, "Header", itemHeaderText, english, value => headeredItems.Header = value);
                break;
            case ContentControl contentControl when contentControl.Content is string contentText:
                ApplyProperty(element, "Content", contentText, english, value => contentControl.Content = value);
                break;
        }

        if (element is FrameworkElement frameworkElement && frameworkElement.ToolTip is string toolTipText)
            ApplyProperty(element, "ToolTip", toolTipText, english, value => frameworkElement.ToolTip = value);
    }

    private static void ApplyProperty(DependencyObject element, string propertyKey, string current, bool english, Action<string> setter)
    {
        var properties = States.GetValue(element, _ => new Dictionary<string, TextState>(StringComparer.Ordinal));
        if (!properties.TryGetValue(propertyKey, out var state))
        {
            state = new TextState { Original = current, LastApplied = current };
            properties[propertyKey] = state;
        }
        else if (!string.Equals(current, state.LastApplied, StringComparison.Ordinal) &&
                 !string.Equals(current, state.Original, StringComparison.Ordinal))
        {
            // The application changed a live status label after the previous language pass.
            // Treat that new value as the new source instead of translating an old snapshot.
            state.Original = current;
        }

        var translated = Translate(state.Original, english);
        setter(translated);
        state.LastApplied = translated;
    }

    private static string Translate(string? value, bool english)
    {
        if (string.IsNullOrEmpty(value)) return value ?? string.Empty;

        var leadingCount = value.Length - value.TrimStart().Length;
        var trailingCount = value.Length - value.TrimEnd().Length;
        var core = value.Trim();

        var exact = Entries.FirstOrDefault(entry =>
            string.Equals(entry.Source, core, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(entry.Ukrainian, core, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(entry.English, core, StringComparison.OrdinalIgnoreCase));
        if (exact is not null)
        {
            var exactTranslation = english ? exact.English : exact.Ukrainian;
            return new string(' ', leadingCount) + exactTranslation + new string(' ', trailingCount);
        }

        foreach (var entry in Entries.OrderByDescending(x => Math.Max(x.Source.Length, Math.Max(x.Ukrainian.Length, x.English.Length))))
        {
            var target = english ? entry.English : entry.Ukrainian;
            foreach (var source in new[] { entry.Source, entry.Ukrainian, entry.English }.Distinct(StringComparer.OrdinalIgnoreCase).OrderByDescending(x => x.Length))
            {
                if (core.IndexOf(source, StringComparison.OrdinalIgnoreCase) < 0) continue;
                var translated = core.Replace(source, target, StringComparison.OrdinalIgnoreCase);
                return new string(' ', leadingCount) + translated + new string(' ', trailingCount);
            }
        }

        return value;
    }
}
