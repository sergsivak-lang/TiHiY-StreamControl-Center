using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Windows.Markup;
using System.Windows.Media;
using TiHiY.StreamControlCenter.Models;

namespace TiHiY.StreamControlCenter.Services;

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
                // AppServices can still be under construction.
            }
        }
    }

    private static void Twitch_MessageReceived(object? sender, ChatMessage message)
    {
        if (!message.Platform.Equals("TWITCH", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(message.Text)) return;
        var found = new List<ChatEmote>();
        foreach (Match match in Regex.Matches(message.Text, @"\S+", RegexOptions.CultureInvariant))
        {
            EmoteDefinition? definition;
            lock (TwitchEmotes) TwitchEmotes.TryGetValue(match.Value, out definition);
            if (definition is null) continue;
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
        if (found.Count > 0) message.Emotes = found;
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
        var root = JsonNode.Parse(await response.Content.ReadAsStringAsync().ConfigureAwait(false))?.AsObject();
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
        try
        {
            const string xaml = """
                <DataTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                              xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                              xmlns:models="clr-namespace:TiHiY.StreamControlCenter.Models;assembly=TiHiY.StreamControlCenter"
                              xmlns:controls="clr-namespace:TiHiY.StreamControlCenter.Controls;assembly=TiHiY.StreamControlCenter"
                              DataType="{x:Type models:ChatMessage}">
                    <Border Background="{Binding Background}" Padding="3,5">
                        <Grid>
                            <Grid.ColumnDefinitions>
                                <ColumnDefinition Width="76"/>
                                <ColumnDefinition Width="32"/>
                                <ColumnDefinition Width="145"/>
                                <ColumnDefinition Width="*"/>
                            </Grid.ColumnDefinitions>
                            <TextBlock Text="{Binding DisplayTime}" Foreground="#71899C" FontFamily="Consolas" FontSize="11"/>
                            <Border Grid.Column="1" Width="21" Height="21" CornerRadius="3" Background="{Binding PlatformColor}" HorizontalAlignment="Left">
                                <Image Source="{Binding PlatformIconPath}" Margin="2"/>
                            </Border>
                            <TextBlock Grid.Column="2" Text="{Binding User}" Foreground="{Binding Foreground}" FontWeight="Bold" TextTrimming="CharacterEllipsis"/>
                            <controls:RichChatTextBlock Grid.Column="3" Message="{Binding}" MessageBrush="#DCE9F3" HighlightBrush="#FFD329" EmoteSize="21"/>
                        </Grid>
                    </Border>
                </DataTemplate>
                """;
            list.ItemTemplate = (DataTemplate)XamlReader.Parse(xaml);
            list.Tag = "RichChatInstalled";
        }
        catch (Exception ex)
        {
            try { App.Services.Logger.Error("Rich-chat template", ex); } catch { }
        }
    }

    private static void ApplyWindowTheme(Window window, bool stalker)
    {
        if (!window.IsLoaded) return;
        var windowBrush = window.TryFindResource("StalkerWindowBrush") as Brush;
        if (stalker)
        {
            Remember(window);
            if (windowBrush is not null) window.Background = windowBrush;
            if (windowBrush is not null && FindVisualChild<Border>(window) is { } rootBorder)
            {
                Remember(rootBorder);
                rootBorder.Background = windowBrush;
            }
        }
        else
        {
            Restore(window);
            if (FindVisualChild<Border>(window) is { } rootBorder) Restore(rootBorder);
        }
        ApplyElementTheme(window, stalker);
    }

    private static void ApplyElementTheme(DependencyObject root, bool stalker)
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
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
    }

    private static bool IsDashboardPanel(ContentControl control) =>
        control.Name.EndsWith("BlockPanel", StringComparison.Ordinal) || control.Name is "SystemStatusBlockPanel" or "SystemMonitorPanel";

    private static bool IsDangerButton(Button button) =>
        button.Name.Contains("Close", StringComparison.OrdinalIgnoreCase) || button.Content?.ToString() == "×";

    private static T? FindVisualChild<T>(DependencyObject root) where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            if (child is T match) return match;
            if (FindVisualChild<T>(child) is { } descendant) return descendant;
        }
        return null;
    }

    private static void Remember(FrameworkElement element)
    {
        if (OriginalStates.TryGetValue(element, out _)) return;
        var background = element switch
        {
            Control control => control.ReadLocalValue(Control.BackgroundProperty),
            Panel panel => panel.ReadLocalValue(Panel.BackgroundProperty),
            Border border => border.ReadLocalValue(Border.BackgroundProperty),
            _ => DependencyProperty.UnsetValue
        };
        OriginalStates.Add(element, new OriginalState(element.ReadLocalValue(FrameworkElement.StyleProperty), background));
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