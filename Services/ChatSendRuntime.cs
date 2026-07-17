namespace TiHiY.StreamControlCenter.Services;

internal static class ChatSendRuntime
{
    private static readonly ConditionalWeakTable<MainWindow, object> AttachedWindows = new();

    [ModuleInitializer]
    internal static void Register()
    {
        EventManager.RegisterClassHandler(typeof(Button), Button.ClickEvent, new RoutedEventHandler(OnButtonClick));
        EventManager.RegisterClassHandler(typeof(MainWindow), FrameworkElement.LoadedEvent, new RoutedEventHandler(OnMainLoaded));
    }

    private static void OnMainLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is not MainWindow window || AttachedWindows.TryGetValue(window, out _)) return;
        AttachedWindows.Add(window, new object());

        if (window.FindName("ChatInput") is TextBox input)
            input.PreviewKeyDown += ChatInput_PreviewKeyDown;
    }

    private static async void OnButtonClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || Window.GetWindow(button) is not MainWindow window) return;

        var target = button.Name switch
        {
            "SendTwitchButton" => "Twitch",
            "SendYouTubeButton" => "YouTube",
            "SendBothButton" => "Twitch + YouTube",
            _ when Equals(button.Content, "➤") => "Twitch + YouTube",
            _ => string.Empty
        };

        if (string.IsNullOrWhiteSpace(target)) return;
        e.Handled = true;
        await SendAsync(window, target, button);
    }

    private static async void ChatInput_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) return;
        if (sender is not TextBox input || Window.GetWindow(input) is not MainWindow window) return;

        e.Handled = true;
        await SendAsync(window, "Twitch + YouTube", null);
    }

    private static async Task SendAsync(MainWindow window, string target, Button? sourceButton)
    {
        if (window.FindName("ChatInput") is not TextBox input) return;
        var text = input.Text.Trim();
        if (string.IsNullOrWhiteSpace(text)) return;

        var status = window.FindName("ChatStatusText") as TextBlock;
        var originalStatus = status?.Text ?? string.Empty;
        if (status is not null) status.Text = $"Надсилання в {target}…";
        if (sourceButton is not null) sourceButton.IsEnabled = false;

        try
        {
            await App.Services.Chat.SendManualAsync(text, target);
            input.Clear();
            if (status is not null) status.Text = $"Повідомлення успішно надіслано: {target}.";
        }
        catch (Exception ex)
        {
            var message = FriendlyMessage(ex);
            if (status is not null) status.Text = message;
            MessageBox.Show(window, message, "Мультичат", MessageBoxButton.OK, MessageBoxImage.Warning);
            input.SelectAll();
            input.Focus();
        }
        finally
        {
            if (sourceButton is not null) sourceButton.IsEnabled = true;
            if (status is not null && string.IsNullOrWhiteSpace(status.Text)) status.Text = originalStatus;
        }
    }

    private static string FriendlyMessage(Exception exception)
    {
        var raw = exception.GetBaseException().Message;
        if (raw.Contains("quotaExceeded", StringComparison.OrdinalIgnoreCase) ||
            raw.Contains("exceeded your", StringComparison.OrdinalIgnoreCase) && raw.Contains("quota", StringComparison.OrdinalIgnoreCase))
            return "Добову квоту YouTube API вичерпано. Текст не видалено — після відновлення квоти його можна надіслати повторно.";

        if (raw.Contains("active YouTube live chat", StringComparison.OrdinalIgnoreCase) ||
            raw.Contains("live chat не знайдено", StringComparison.OrdinalIgnoreCase) ||
            raw.Contains("liveChat", StringComparison.OrdinalIgnoreCase) && raw.Contains("not", StringComparison.OrdinalIgnoreCase))
            return "Активний чат YouTube не знайдено. Перевірте, що трансляція вже запущена, чат увімкнений і YouTube підключено заново.";

        if (raw.Contains("OAuth", StringComparison.OrdinalIgnoreCase) || raw.Contains("Unauthorized", StringComparison.OrdinalIgnoreCase))
            return "YouTube потребує повторної OAuth-авторизації у розділі «Канали / YouTube».";

        if (raw.Contains("403", StringComparison.OrdinalIgnoreCase))
            return "YouTube відхилив запит (403). Перевірте квоту API, права OAuth і доступність live chat.";

        return raw;
    }
}
