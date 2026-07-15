using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using TiHiY.StreamControlCenter.Models;
using TiHiY.StreamControlCenter.Services;

namespace TiHiY.StreamControlCenter.Windows;

public partial class LocalChatOverlayWindow : Window
{
    private const int GwlExStyle = -20;
    private const int WsExTransparent = 0x00000020;
    private const int WsExToolWindow = 0x00000080;
    private const int WsExNoActivate = 0x08000000;

    private readonly AppServices _services = App.Services;
    private readonly DispatcherTimer _statsTimer;
    private IDisposable? _responsiveController;
    private IntPtr _hwnd;
    private bool _clickThrough;

    public ObservableCollection<ChatMessage> Messages { get; } = new();

    public LocalChatOverlayWindow()
    {
        InitializeComponent();
        DataContext = this;
        _services.Placement.Attach(this, "LocalChatOverlay");
        _services.Chat.MessageAdded += Chat_MessageAdded;
        _services.ChannelStatusChanged += Services_ChannelStatusChanged;

        _statsTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _statsTimer.Tick += (_, _) => UpdateViewerStats();

        LoadCurrentMessages();
        ApplySettings();
        _responsiveController = ResponsiveBlockService.Attach(OverlayFrame, 0.55, 1.60);
        UpdateViewerStats();
    }

    public void ApplySettings()
    {
        var settings = _services.Settings.Value;
        Topmost = true;
        SetBackgroundOpacity(settings.LocalChatOverlayBackgroundOpacity);
        SetClickThrough(settings.LocalChatOverlayClickThrough);
        FontSize = Math.Clamp(settings.LocalChatOverlayFontSize, 11, 32);
        var statsFont = Math.Clamp(FontSize * 0.65, 9, 20);
        ViewerStatsBar.SetValue(System.Windows.Documents.TextElement.FontSizeProperty, statsFont);
        var iconSize = Math.Clamp(statsFont + 7, 16, 28);
        TwitchOverlayIcon.Width = iconSize;
        TwitchOverlayIcon.Height = iconSize;
        YouTubeOverlayIcon.Width = iconSize + 2;
        YouTubeOverlayIcon.Height = iconSize;

        if (IsLoaded)
        {
            _responsiveController?.Dispose();
            _responsiveController = ResponsiveBlockService.Attach(OverlayFrame, 0.55, 1.60);
        }

        UpdateViewerStats();
    }

    public void SetBackgroundOpacity(double value)
    {
        value = Math.Clamp(value, 0, 0.85);
        var alpha = (byte)Math.Round(value * 255);
        OverlayFrame.Background = new SolidColorBrush(Color.FromArgb(alpha, 0, 0, 0));
    }

    public void SetClickThrough(bool enabled)
    {
        _clickThrough = enabled;
        ControlBar.Visibility = enabled ? Visibility.Collapsed : Visibility.Visible;
        ModeText.Text = enabled ? "КРІЗЬ КЛІКИ" : "КЕРУВАННЯ";
        ApplyExtendedStyle();
    }

    private void LoadCurrentMessages()
    {
        Messages.Clear();
        foreach (var message in _services.Chat.Messages.TakeLast(Math.Max(3, _services.Settings.Value.LocalChatOverlayMaxMessages)))
            Messages.Add(message);
    }

    private void Chat_MessageAdded(object? sender, ChatMessage message) => Dispatcher.BeginInvoke(new Action(() =>
    {
        Messages.Add(message);
        var max = Math.Max(3, _services.Settings.Value.LocalChatOverlayMaxMessages);
        while (Messages.Count > max) Messages.RemoveAt(0);
        OverlayChatList.ScrollIntoView(message);
    }));

    private void Services_ChannelStatusChanged(object? sender, EventArgs e) =>
        Dispatcher.BeginInvoke(new Action(UpdateViewerStats));

    private void UpdateViewerStats()
    {
        if (!IsInitialized) return;

        var settings = _services.Settings.Value;
        TwitchOverlayViewersText.Text = settings.TwitchViewers.ToString("N0");
        TwitchOverlayStatusText.Text = settings.TwitchLive ? "LIVE" : "OFF";
        TwitchOverlayStatusText.Foreground = settings.TwitchLive ? Brushes.LimeGreen : Brushes.Gray;
        TwitchOverlayLiveDot.Fill = settings.TwitchLive ? Brushes.LimeGreen : Brushes.Gray;

        YouTubeOverlayViewersText.Text = settings.YouTubeViewers.ToString("N0");
        YouTubeOverlayLikesText.Text = settings.YouTubeLikes.ToString("N0");
        YouTubeOverlayStatusText.Text = settings.YouTubeLive ? "LIVE" : "OFF";
        YouTubeOverlayStatusText.Foreground = settings.YouTubeLive ? Brushes.LimeGreen : Brushes.Gray;
        YouTubeOverlayLiveDot.Fill = settings.YouTubeLive ? Brushes.LimeGreen : Brushes.Gray;
    }

    private void Window_SourceInitialized(object? sender, EventArgs e)
    {
        _hwnd = new WindowInteropHelper(this).Handle;
        ApplyExtendedStyle();
    }

    private void ApplyExtendedStyle()
    {
        if (_hwnd == IntPtr.Zero) return;
        var style = GetWindowLongPtr(_hwnd, GwlExStyle).ToInt64();
        style |= WsExToolWindow;
        if (_clickThrough) style |= WsExTransparent | WsExNoActivate;
        else style &= ~(WsExTransparent | WsExNoActivate);
        SetWindowLongPtr(_hwnd, GwlExStyle, new IntPtr(style));
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        _statsTimer.Start();
        UpdateViewerStats();
        if (Messages.Count > 0) OverlayChatList.ScrollIntoView(Messages[^1]);
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_clickThrough || e.LeftButton != MouseButtonState.Pressed) return;
        try { DragMove(); } catch { }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        _statsTimer.Stop();
        _responsiveController?.Dispose();
        _services.Chat.MessageAdded -= Chat_MessageAdded;
        _services.ChannelStatusChanged -= Services_ChannelStatusChanged;
    }

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
    private static extern int GetWindowLong32(IntPtr hWnd, int nIndex);

    private static IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex) => IntPtr.Size == 8
        ? GetWindowLongPtr64(hWnd, nIndex)
        : new IntPtr(GetWindowLong32(hWnd, nIndex));

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongW")]
    private static extern int SetWindowLong32(IntPtr hWnd, int nIndex, int dwNewLong);

    private static IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr value) => IntPtr.Size == 8
        ? SetWindowLongPtr64(hWnd, nIndex, value)
        : new IntPtr(SetWindowLong32(hWnd, nIndex, value.ToInt32()));
}
