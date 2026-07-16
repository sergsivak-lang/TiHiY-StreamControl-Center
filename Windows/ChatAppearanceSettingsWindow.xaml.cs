using System.Diagnostics;
using TiHiY.StreamControlCenter.Services;

namespace TiHiY.StreamControlCenter.Windows;

public partial class ChatAppearanceSettingsWindow : ModuleWindowBase
{
    private readonly AppServices _services = App.Services;
    private bool _loading;

    public ChatAppearanceSettingsWindow()
    {
        InitializeComponent();
        ConfigureModule(DesignSurface, 1156, 766, "ChatAppearanceSettings");
        LoadSettings();
        UpdateValues();
        UpdatePreview();
        UpdateUrl();
    }

    private void LoadSettings()
    {
        _loading = true;
        try
        {
            var s = _services.Settings.Value;
            MainFontSlider.Value = s.MainChatFontSize;
            MainTextColorBox.Text = s.MainChatTextColor;
            InputFontSlider.Value = s.MainChatInputFontSize;
            InputHeightSlider.Value = s.MainChatInputHeight;
            InputTextColorBox.Text = s.MainChatInputTextColor;
            InputBackgroundColorBox.Text = s.MainChatInputBackgroundColor;

            StreamFontSlider.Value = s.StreamChatOverlayFontSize;
            StreamMaxSlider.Value = s.StreamChatOverlayMaxMessages;
            StreamOpacitySlider.Value = s.StreamChatOverlayBackgroundOpacity;
            StreamTextColorBox.Text = s.StreamChatOverlayTextColor;
            StreamUserColorBox.Text = s.StreamChatOverlayUserColor;
            StreamBackgroundColorBox.Text = s.StreamChatOverlayBackgroundColor;

            GameAutoStartCheck.IsChecked = s.LocalChatOverlayAutoStart;
            GameClickThroughCheck.IsChecked = s.LocalChatOverlayClickThrough;
            GameFontSlider.Value = s.LocalChatOverlayFontSize;
            GameMaxSlider.Value = s.LocalChatOverlayMaxMessages;
            GameOpacitySlider.Value = s.LocalChatOverlayBackgroundOpacity;
            GameTextColorBox.Text = s.LocalChatOverlayTextColor;
            GameUserColorBox.Text = s.LocalChatOverlayUserColor;
        }
        finally { _loading = false; }
    }

    private void SaveToSettings()
    {
        var s = _services.Settings.Value;
        s.MainChatFontSize = Math.Clamp(MainFontSlider.Value, 11, 28);
        s.MainChatTextColor = NormalizeColor(MainTextColorBox.Text, "#DCE9F3");
        s.MainChatInputFontSize = Math.Clamp(InputFontSlider.Value, 11, 26);
        s.MainChatInputHeight = Math.Clamp(InputHeightSlider.Value, 36, 64);
        s.MainChatInputTextColor = NormalizeColor(InputTextColorBox.Text, "#EAF6FF");
        s.MainChatInputBackgroundColor = NormalizeColor(InputBackgroundColorBox.Text, "#071525");

        s.StreamChatOverlayFontSize = Math.Clamp(StreamFontSlider.Value, 11, 48);
        s.StreamChatOverlayMaxMessages = (int)Math.Round(Math.Clamp(StreamMaxSlider.Value, 3, 30));
        s.StreamChatOverlayBackgroundOpacity = Math.Clamp(StreamOpacitySlider.Value, 0, 0.9);
        s.StreamChatOverlayTextColor = NormalizeColor(StreamTextColorBox.Text, "#F2FAFF");
        s.StreamChatOverlayUserColor = NormalizeColor(StreamUserColorBox.Text, "#FFD329");
        s.StreamChatOverlayBackgroundColor = NormalizeColor(StreamBackgroundColorBox.Text, "#000000");

        s.LocalChatOverlayAutoStart = GameAutoStartCheck.IsChecked == true;
        s.LocalChatOverlayClickThrough = GameClickThroughCheck.IsChecked == true;
        s.LocalChatOverlayFontSize = Math.Clamp(GameFontSlider.Value, 11, 42);
        s.LocalChatOverlayMaxMessages = (int)Math.Round(Math.Clamp(GameMaxSlider.Value, 3, 30));
        s.LocalChatOverlayBackgroundOpacity = Math.Clamp(GameOpacitySlider.Value, 0, 0.85);
        s.LocalChatOverlayTextColor = NormalizeColor(GameTextColorBox.Text, "#F2FAFF");
        s.LocalChatOverlayUserColor = NormalizeColor(GameUserColorBox.Text, "#FFD329");
        _services.Save();
    }

    private void ApplyLive()
    {
        SaveToSettings();
        if (Application.Current.MainWindow is MainWindow main)
            ChatAppearanceRuntime.Apply(main);
        _services.Windows.Get<LocalChatOverlayWindow>()?.ApplySettings();
        UpdatePreview();
        UpdateUrl();
        StatusText.Text = "Оформлення мультичату застосовано.";
    }

    private void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_loading || MainFontValue is null) return;
        UpdateValues();
        UpdatePreview();
    }

    private void UpdateValues()
    {
        if (MainFontValue is null) return;
        MainFontValue.Text = $"{MainFontSlider.Value:0} px";
        InputFontValue.Text = $"{InputFontSlider.Value:0} px";
        InputHeightValue.Text = $"{InputHeightSlider.Value:0} px";
        StreamFontValue.Text = $"{StreamFontSlider.Value:0} px";
        StreamMaxValue.Text = $"{StreamMaxSlider.Value:0}";
        StreamOpacityValue.Text = $"{StreamOpacitySlider.Value * 100:0}%";
        GameFontValue.Text = $"{GameFontSlider.Value:0} px";
        GameMaxValue.Text = $"{GameMaxSlider.Value:0}";
        GameOpacityValue.Text = $"{GameOpacitySlider.Value * 100:0}%";
    }

    private void UpdatePreview()
    {
        if (MainPreviewMessage is null) return;
        MainPreviewMessage.FontSize = MainFontSlider.Value;
        MainPreviewMessage.Foreground = BrushFrom(MainTextColorBox.Text, "#DCE9F3");
        InputPreviewBox.FontSize = InputFontSlider.Value;
        InputPreviewBox.Height = InputHeightSlider.Value;
        InputPreviewBox.Foreground = BrushFrom(InputTextColorBox.Text, "#EAF6FF");
        InputPreviewBox.Background = BrushFrom(InputBackgroundColorBox.Text, "#071525");
        MainPreviewBorder.Background = BrushFrom(InputBackgroundColorBox.Text, "#071525");
    }

    private void UpdateUrl()
    {
        if (StreamOverlayUrlBox is null) return;
        var port = _services.Overlay.IsRunning ? _services.Overlay.Port : _services.Settings.Value.OverlayPort;
        var theme = Uri.EscapeDataString(_services.Settings.Value.OverlayTheme);
        StreamOverlayUrlBox.Text = $"http://127.0.0.1:{port}/overlay/chat?theme={theme}";
    }

    private void Defaults_Click(object sender, RoutedEventArgs e)
    {
        MainFontSlider.Value = 15;
        MainTextColorBox.Text = "#DCE9F3";
        InputFontSlider.Value = 15;
        InputHeightSlider.Value = 42;
        InputTextColorBox.Text = "#EAF6FF";
        InputBackgroundColorBox.Text = "#071525";
        StreamFontSlider.Value = 20;
        StreamMaxSlider.Value = 12;
        StreamOpacitySlider.Value = 0;
        StreamTextColorBox.Text = "#F2FAFF";
        StreamUserColorBox.Text = "#FFD329";
        StreamBackgroundColorBox.Text = "#000000";
        GameFontSlider.Value = 20;
        GameMaxSlider.Value = 12;
        GameOpacitySlider.Value = 0.12;
        GameTextColorBox.Text = "#F2FAFF";
        GameUserColorBox.Text = "#FFD329";
        UpdateValues();
        UpdatePreview();
        StatusText.Text = "Встановлено стандартні значення. Натисніть «Застосувати».";
    }

    private void Apply_Click(object sender, RoutedEventArgs e) => ApplyLive();
    private void Save_Click(object sender, RoutedEventArgs e) { ApplyLive(); Close(); }

    private void OpenStreamOverlay_Click(object sender, RoutedEventArgs e)
    {
        ApplyLive();
        OpenUrl(StreamOverlayUrlBox.Text);
    }

    private void CopyStreamOverlay_Click(object sender, RoutedEventArgs e)
    {
        ApplyLive();
        try { Clipboard.SetText(StreamOverlayUrlBox.Text); StatusText.Text = "URL стрім-чату скопійовано."; }
        catch (Exception ex) { ShowError("Буфер обміну", ex); }
    }

    private void OpenGameOverlay_Click(object sender, RoutedEventArgs e)
    {
        ApplyLive();
        _services.Windows.Show(() => new LocalChatOverlayWindow());
        _services.Windows.Get<LocalChatOverlayWindow>()?.ApplySettings();
    }

    private void CloseGameOverlay_Click(object sender, RoutedEventArgs e) => _services.Windows.Close<LocalChatOverlayWindow>();

    private void OpenUrl(string target)
    {
        try { Process.Start(new ProcessStartInfo(target) { UseShellExecute = true }); }
        catch (Exception ex) { ShowError("Відкриття overlay", ex); }
    }

    private void ShowError(string title, Exception ex)
    {
        _services.Logger.Error(title, ex);
        MessageBox.Show(this, ex.GetBaseException().Message, title, MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    private static string NormalizeColor(string value, string fallback)
    {
        var text = value.Trim();
        if (!text.StartsWith('#')) text = "#" + text;
        return text.Length is 7 or 9 && text.Skip(1).All(Uri.IsHexDigit) ? text.ToUpperInvariant() : fallback;
    }

    private static Brush BrushFrom(string value, string fallback)
    {
        try { return new SolidColorBrush((Color)ColorConverter.ConvertFromString(NormalizeColor(value, fallback))); }
        catch { return new SolidColorBrush((Color)ColorConverter.ConvertFromString(fallback)); }
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => DragTitle(sender, e);
    private void Minimize_Click(object sender, RoutedEventArgs e) => MinimizeWindow(sender, e);
    private void Maximize_Click(object sender, RoutedEventArgs e) => MaximizeWindow(sender, e);
    private void Close_Click(object sender, RoutedEventArgs e) => CloseWindow(sender, e);
}