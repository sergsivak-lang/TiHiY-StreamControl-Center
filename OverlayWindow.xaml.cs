using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using TiHiY.StreamControlCenter.Models;

namespace TiHiY.StreamControlCenter;

public partial class OverlayWindow : Window
{
    private const int GwlExstyle = -20, WsExTransparent = 0x20, WsExLayered = 0x80000;
    [DllImport("user32.dll")] private static extern int GetWindowLong(IntPtr hwnd, int index);
    [DllImport("user32.dll")] private static extern int SetWindowLong(IntPtr hwnd, int index, int value);

    public double OverlayOpacity => OpacitySlider.Value;
    public bool ClickThroughEnabled => ClickThroughCheck.IsChecked == true;

    public OverlayWindow(ObservableCollection<ChatMessage> messages, double opacity)
    {
        InitializeComponent();
        OpacitySlider.Value = Math.Clamp(opacity, 0.15, 0.95);
        DataContext = messages;
        messages.CollectionChanged += (_, _) => Dispatcher.Invoke(() => { if (messages.Count > 0) OverlayChat.ScrollIntoView(messages[^1]); });
    }
    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) { if (e.LeftButton == MouseButtonState.Pressed) DragMove(); }
    private void Close_Click(object sender, RoutedEventArgs e) => Hide();
    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void OpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) { if (OverlayPanel != null) OverlayPanel.Opacity = e.NewValue; }
    private void ClickThroughCheck_Click(object sender, RoutedEventArgs e) => ApplyClickThrough(ClickThroughCheck.IsChecked == true);
    public void ToggleClickThrough()
    {
        ClickThroughCheck.IsChecked = ClickThroughCheck.IsChecked != true;
        ApplyClickThrough(ClickThroughCheck.IsChecked == true);
    }
    public void SetOpacity(double value) => OpacitySlider.Value = Math.Clamp(value, 0.15, 0.95);

    private void ApplyClickThrough(bool enabled)
    {
        var hwnd = new WindowInteropHelper(this).Handle; if (hwnd == IntPtr.Zero) return;
        var style = GetWindowLong(hwnd, GwlExstyle);
        SetWindowLong(hwnd, GwlExstyle, enabled ? style | WsExTransparent | WsExLayered : style & ~WsExTransparent);
    }
}
