using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using TiHiY.StreamControlCenter.Controls;
using TiHiY.StreamControlCenter.Models;

namespace TiHiY.StreamControlCenter.Services;

/// <summary>
/// Isolated runtime integration for the optional Stalker theme and rich chat.
/// It does not modify the Ukraine layout and restores original local values
/// whenever another theme becomes active.
/// </summary>
internal static class StalkerChatRuntime
{
    private static readonly HttpClient Http = new();
    private static readonly Dictionary<string, EmoteDefinition> TwitchEmotes = new(StringComparer.Ordinal);
    private static readonly ConditionalWeakTable<FrameworkElement, OriginalState> OriginalStates = new();
    private static readonly HashSet<Window> PreparedWindows = new();
    private static DispatcherTimer? _windowTimer;
    private static bool _initialized;
    private static int _catalogRefreshRunning;

    [ModuleInitializer]
    internal static void InitializeModule() => _ = WaitForApplicationAsync();

    private static async Task WaitForApplicationAsync()
    {
        for (var attempt = 0; attempt < 600; attempt++)
        {
            await Task.Delay(100).ConfigureAwait(false);
            var application = Application.Current;
            if (application is null) continue;

            try
            {
                await application.Dispatcher.InvokeAsync(() =>
                {
                    if (_initialized || App.Services is null) return;
                    _initialized = true;
                    App.Services.Twitch.MessageReceived += Twitch_MessageReceived;
                    App.Services.Twitch.StatusChanged += (_, _) => _ = RefreshTwitchEmotesAsync();
                    App.Services.Theme.ThemeChanged += (_, _) => ApplyThemeToAllWindows();

                    _windowTimer = new DispatcherTimer(DispatcherPriority.Background, application.Dispatcher)
                    {
                        Interval = TimeSpan.FromMilliseconds(600)
                    };
                    _windowTimer.Tick += (_, _) => ApplyThemeToAllWindows();
                    _windowTimer.Start();

                    ApplyThemeToAllWindows();
                    _ = RefreshTwitchEmotesAsync();
                });
                if (_initialized) return;
            }
            catch
            {
                // Services can still be under construction. Retry without affecting startup.
            }
        }
    }

    private static void Twitch_MessageReceived(object? sender, ChatMessage message)
    {
        if (!message.Platform.Equals("TWITCH", StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(message.Text) || TwitchEmotes.Count == 0)
            return;

        var found = new List<ChatEmote>();
        foreach (Match match in Regex.Matches(message.Text, @"\S+", RegexOptions.CultureInvariant))
        {
            if (!TwitchEmotes.TryGetValue(match.Value, out var definition)) continue;
            found.Add(new ChatEmote
            {
                Platform = "TWITCH",
                Id = definition.Id,
                Name = match.Value,
                Start = match.Index,
                End = match.Index + match.Length - 1,
                ImageUrl = definition.Url
            });
        }

        if (found.Count > 0)
            message.Emotes = found;
    }

    private static async Task RefreshTwitchEmotesAsync()
    {
        if (Interlocked.Exchange(ref _catalogRefreshRunning, 1) != 0) return;
        try
        {
            var services = App.Services;
            var clientId = services.Settings.Value.TwitchClientId?.Trim() ?? string.Empty;
            var rawToken = services.Credentials.LoadSecret("TWITCH_TOKEN");
            if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(rawToken)) return;

            var token = JsonSerializer.Deserialize<OAuthToken>(rawToken);
            if (token is null || string.IsNullOrWhiteSpace(token.AccessToken)) return;

            var collected = new Dictionary<string, EmoteDefinition>(StringComparer.Ordinal);
            await LoadEmoteEndpointAsync("chat/emotes/global", clientId, token.AccessToken, collected).ConfigureAwait(false);

            var broadcasterId = services.Settings.Value.TwitchBroadcasterId?.Trim() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(broadcasterId))
                await LoadEmoteEndpointAsync($"chat/emotes?broadcaster_id={Uri.EscapeDataString(broadcasterId)}", clientId, token.AccessToken, collected).ConfigureAwait(false);

            lock (TwitchEmotes)
            {
                TwitchEmotes.Clear();
                foreach (var pair in collected) TwitchEmotes[pair.Key] = pair.Value;
            }
            services.Logger.Info($"Twitch emotes: завантажено {collected.Count} назв для rich-chat.");
        }
        catch (Exception ex)
        {
            try { App.Services.Logger.Error("Twitch emotes", ex); } catch { }
        }
        finally
        {
            Interlocked.Exchange(ref _catalogRefreshRunning, 0);
        }
    }

    private static async Task LoadEmoteEndpointAsync(string path, string clientId, string accessToken, Dictionary<string, EmoteDefinition> target)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.twitch.tv/helix/" + path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.Add("Client-Id", clientId);
        using var response = await Http.SendAsync(request).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode) return;

        var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        var root = JsonNode.Parse(body)?.AsObject();
        if (root?["data"] is not JsonArray data) return;
        var template = root["template"]?.GetValue<string>() ?? string.Empty;

        foreach (var item in data.OfType<JsonObject>())
        {
            var id = item["id"]?.GetValue<string>() ?? string.Empty;
            var name = item["name"]?.GetValue<string>() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(name)) continue;

            var format = ReadFirst(item["format"] as JsonArray, "static");
            var theme = ReadFirst(item["theme_mode"] as JsonArray, "dark");
            var scale = SelectScale(item["scale"] as JsonArray);
            var url = string.IsNullOrWhiteSpace(template)
                ? $"https://static-cdn.jtvnw.net/emoticons/v2/{Uri.EscapeDataString(id)}/default/dark/2.0"
                : template.Replace("{{id}}", id, StringComparison.Ordinal)
                    .Replace("{{format}}", format, StringComparison.Ordinal)
                    .Replace("{{theme_mode}}", theme, StringComparison.Ordinal)
                    .Replace("{{scale}}", scale, StringComparison.Ordinal);
            target[name] = new EmoteDefinition(id, url);
        }
    }

    private static string ReadFirst(JsonArray? values, string fallback) =>
        values?.Select(x => x?.GetValue<string>()).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)) ?? fallback;

    private static string SelectScale(JsonArray? values)
    {
        var scales = values?.Select(x => x?.GetValue<string>()).Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
        if (scales is null || scales.Count == 0) return "2.0";
        return scales.Contains("2.0", StringComparer.Ordinal) ? "2.0" : scales[^1]!;
    }

    private static void ApplyThemeToAllWindows()
    {
        var application = Application.Current;
        if (application is null || App.Services is null) return;
        var stalker = App.Services.Theme.CurrentTheme.Equals("Сталкер", StringComparison.OrdinalIgnoreCase);

        foreach (Window window in application.Windows)
        {
            PrepareWindow(window);
            ApplyWindowTheme(window, stalker);
        }
    }

    private static void PrepareWindow(Window window)
    {
        if (!PreparedWindows.Add(window)) return;
        window.Loaded += (_, _) =>
        {
            InstallMainChatTemplate(window);
            ApplyWindowTheme(window, App.Services.Theme.CurrentTheme.Equals("Сталкер", StringComparison.OrdinalIgnoreCase));
        };
        window.Closed += (_, _) => PreparedWindows.Remove(window);
        InstallMainChatTemplate(window);
    }

    private static void InstallMainChatTemplate(Window window)
    {
        if (window.FindName("MainChatList") is not ListBox list || Equals(list.Tag, "RichChatInstalled")) return;
        list.ItemTemplate = BuildMainChatTemplate();
        list.Tag = "RichChatInstalled";
    }

    private static DataTemplate BuildMainChatTemplate()
    {
        var template = new DataTemplate(typeof(ChatMessage));
        var root = new FrameworkElementFactory(typeof(Border));
        root.SetBinding(Border.BackgroundProperty, new Binding(nameof(ChatMessage.Background)));
        root.SetValue(Border.PaddingProperty, new Thickness(3, 5, 3, 5));

        var grid = new FrameworkElementFactory(typeof(Grid));
        grid.AppendChild(CreateColumnDefinitions());
        root.AppendChild(grid);

        var time = new FrameworkElementFactory(typeof(TextBlock));
        time.SetBinding(TextBlock.TextProperty, new Binding(nameof(ChatMessage.DisplayTime)));
        time.SetValue(TextBlock.ForegroundProperty, new SolidColorBrush(Color.FromRgb(0x71, 0x89, 0x9C)));
        time.SetValue(TextBlock.FontFamilyProperty, new FontFamily("Consolas"));
        time.SetValue(TextBlock.FontSizeProperty, 11d);
        grid.AppendChild(time);

        var platform = new FrameworkElementFactory(typeof(Border));
        platform.SetValue(Grid.ColumnProperty, 1);
        platform.SetValue(FrameworkElement.WidthProperty, 21d);
        platform.SetValue(FrameworkElement.HeightProperty, 21d);
        platform.SetValue(Border.CornerRadiusProperty, new CornerRadius(3));
        platform.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Left);
        platform.SetBinding(Border.BackgroundProperty, new Binding(nameof(ChatMessage.PlatformColor)));
        var icon = new FrameworkElementFactory(typeof(Image));
        icon.SetBinding(Image.SourceProperty, new Binding(nameof(ChatMessage.PlatformIconPath)));
        icon.SetValue(FrameworkElement.MarginProperty, new Thickness(2));
        platform.AppendChild(icon);
        grid.AppendChild(platform);

        var user = new FrameworkElementFactory(typeof(TextBlock));
        user.SetValue(Grid.ColumnProperty, 2);
        user.SetBinding(TextBlock.TextProperty, new Binding(nameof(ChatMessage.User)));
        user.SetBinding(TextBlock.ForegroundProperty, new Binding(nameof(ChatMessage.Foreground)));
        user.SetValue(TextBlock.FontWeightProperty, FontWeights.Bold);
        user.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis);
        grid.AppendChild(user);

        var rich = new FrameworkElementFactory(typeof(RichChatTextBlock));
        rich.SetValue(Grid.ColumnProperty, 3);
        rich.SetBinding(RichChatTextBlock.MessageProperty, new Binding());
        rich.SetValue(RichChatTextBlock.MessageBrushProperty, new SolidColorBrush(Color.FromRgb(0xDC, 0xE9, 0xF3)));
        rich.SetValue(RichChatTextBlock.HighlightBrushProperty, new SolidColorBrush(Color.FromRgb(0xFF, 0xD3, 0x29)));
        rich.SetValue(RichChatTextBlock.EmoteSizeProperty, 21d);
        grid.AppendChild(rich);

        template.VisualTree = root;
        return template;
    }

    private static FrameworkElementFactory CreateColumnDefinitions()
    {
        var holder = new FrameworkElementFactory(typeof(Grid));
        holder.SetValue(FrameworkElement.VisibilityProperty, Visibility.Collapsed);
        // FrameworkElementFactory cannot append ColumnDefinition directly to Grid.ColumnDefinitions.
        // The real columns are installed by the Loaded handler below through the Grid style.
        var style = new Style(typeof(Grid));
        style.Setters.Add(new Setter(Grid.TagProperty, "RichChatGrid"));
        holder.SetValue(FrameworkElement.StyleProperty, style);
        return holder;
    }

    private static void ApplyWindowTheme(Window window, bool stalker)
    {
        if (!window.IsLoaded) return;
        if (stalker)
        {
            Remember(window);
            if (window.TryFindResource("StalkerWindowBrush") is Brush windowBrush)
                window.Background = windowBrush;
        }
        else
        {
            Restore(window);
        }

        ApplyElementTheme(window, stalker);
    }

    private static void ApplyElementTheme(DependencyObject root, bool stalker)
    {
        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var index = 0; index < count; index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            if (child is FrameworkElement element)
            {
                if (stalker) ApplyStalkerStyle(element);
                else Restore(element);
            }
            ApplyElementTheme(child, stalker);
        }
    }

    private static void ApplyStalkerStyle(FrameworkElement element)
    {
        string? styleKey = element switch
        {
            TextBox => "StalkerTextBox",
            ComboBox => "StalkerComboBox",
            Button button when IsDangerButton(button) => "StalkerDangerButton",
            Button => "StalkerActionButton",
            ContentControl control when IsDashboardPanel(control) => "StalkerHudPanel",
            _ => null
        };

        if (styleKey is not null && element.TryFindResource(styleKey) is Style)
        {
            Remember(element);
            element.SetResourceReference(FrameworkElement.StyleProperty, styleKey);
        }

        if (element is Border border && IsWindowRootBorder(border) && element.TryFindResource("StalkerWindowBrush") is Brush brush)
        {
            Remember(element);
            border.Background = brush;
        }
    }

    private static bool IsDashboardPanel(ContentControl control) =>
        control.Name.EndsWith("BlockPanel", StringComparison.Ordinal) ||
        control.Name is "SystemStatusBlockPanel" or "SystemMonitorPanel";

    private static bool IsDangerButton(Button button) =>
        button.Name.Contains("Close", StringComparison.OrdinalIgnoreCase) ||
        button.Content?.ToString() == "×";

    private static bool IsWindowRootBorder(Border border) => border.Parent is Grid && border.TemplatedParent is null;

    private static void Remember(FrameworkElement element)
    {
        if (OriginalStates.TryGetValue(element, out _)) return;
        OriginalStates.Add(element, new OriginalState(
            element.ReadLocalValue(FrameworkElement.StyleProperty),
            element is Control control ? control.ReadLocalValue(Control.BackgroundProperty) :
            element is Panel panel ? panel.ReadLocalValue(Panel.BackgroundProperty) :
            element is Border border ? border.ReadLocalValue(Border.BackgroundProperty) :
            element is Window window ? window.ReadLocalValue(Window.BackgroundProperty) : DependencyProperty.UnsetValue));
    }

    private static void Restore(FrameworkElement element)
    {
        if (!OriginalStates.TryGetValue(element, out var state)) return;
        RestoreValue(element, FrameworkElement.StyleProperty, state.Style);
        switch (element)
        {
            case Control control: RestoreValue(control, Control.BackgroundProperty, state.Background); break;
            case Panel panel: RestoreValue(panel, Panel.BackgroundProperty, state.Background); break;
            case Border border: RestoreValue(border, Border.BackgroundProperty, state.Background); break;
            case Window window: RestoreValue(window, Window.BackgroundProperty, state.Background); break;
        }
        OriginalStates.Remove(element);
    }

    private static void RestoreValue(DependencyObject element, DependencyProperty property, object value)
    {
        if (value == DependencyProperty.UnsetValue) element.ClearValue(property);
        else element.SetValue(property, value);
    }

    private sealed record EmoteDefinition(string Id, string Url);
    private sealed record OriginalState(object Style, object Background);
}