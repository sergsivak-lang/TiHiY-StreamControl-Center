using System.Reflection;
using TiHiY.StreamControlCenter.Models;

namespace TiHiY.StreamControlCenter.Services;

public static class MainWindowVisualTuner
{
    private sealed class Controller : IDisposable
    {
        private readonly MainWindow _window;
        private readonly DispatcherTimer _guardTimer;
        private readonly HashSet<Slider> _hookedMixerSliders = new();
        private readonly HashSet<Button> _hookedMuteButtons = new();
        private bool _footerLayoutInitialized;
        private bool _autoMixerRepairBusy;
        private bool _disposed;

        public Controller(MainWindow window)
        {
            _window = window;
            _guardTimer = new DispatcherTimer(DispatcherPriority.Background, window.Dispatcher)
            {
                Interval = TimeSpan.FromMilliseconds(500)
            };
            _guardTimer.Tick += GuardTimer_Tick;
            _window.Loaded += Window_Loaded;
            _window.SizeChanged += Window_SizeChanged;
            _window.Closed += Window_Closed;

            if (_window.IsLoaded)
                ApplyNow();
            _guardTimer.Start();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e) => ApplyNow();

        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            TuneAidaCard();
            HookQuickMixerControls();
        }

        private async void GuardTimer_Tick(object? sender, EventArgs e)
        {
            if (_disposed) return;
            EnforceAidaHeader();
            EnforceDigitalAidaValues();
            UpdateEmptyStates();
            HookQuickMixerControls();
            await TryRepairQuickMixerAsync();
        }

        private void Window_Closed(object? sender, EventArgs e) => Dispose();

        public void ApplyNow()
        {
            if (_disposed) return;
            RestoreApprovedDashboardOnce();
            InitializeFooterLayoutOnce();
            TuneAidaCard();
            EnforceAidaHeader();
            EnforceDigitalAidaValues();
            UpdateEmptyStates();
            HookQuickMixerControls();
        }

        private void RestoreApprovedDashboardOnce()
        {
            var settings = App.Services.Settings.Value;
            if (settings.UkraineReferenceLayoutVersion >= 1) return;

            var top = FindNamed<Grid>("TopBlocksGrid");
            var bottom = FindNamed<Grid>("BottomBlocksGrid");
            var footer = FindNamed<Grid>("FooterBlocksGrid");
            var chat = FindNamed<ContentControl>("ChatBlockPanel");
            var donations = FindNamed<ContentControl>("DonationsBlockPanel");
            var mixer = FindNamed<ContentControl>("MixerBlockPanel");
            var notifications = FindNamed<ContentControl>("NotificationsBlockPanel");
            var system = FindNamed<ContentControl>("SystemStatusBlockPanel");
            var aida = FindNamed<ContentControl>("SystemMonitorPanel");

            if (top is null || bottom is null || footer is null || chat is null || donations is null ||
                mixer is null || notifications is null || system is null || aida is null)
                return;

            PlaceBlock(chat, top, 0, 0, new Thickness(0, 0, 3, 0));
            PlaceBlock(donations, top, 0, 2, new Thickness(3, 0, 0, 0));
            PlaceBlock(mixer, bottom, 0, 0, new Thickness(0, 0, 3, 0));
            PlaceBlock(notifications, bottom, 0, 2, new Thickness(3, 0, 0, 0));
            PlaceBlock(system, footer, 0, 0, new Thickness(0, 0, 3, 0));
            PlaceBlock(aida, footer, 0, 4, new Thickness(3, 0, 0, 0));

            settings.DashboardBlockSlots.Clear();
            settings.MainLeftColumnWidth = 1.0;
            settings.MainBottomLeftColumnWidth = 1.0;
            settings.MainTopRowHeight = 1.12;
            settings.FooterHeight = 178;
            settings.FooterSystemColumnWeight = 0.37;
            settings.FooterEventsColumnWeight = 0.26;
            settings.FooterMonitorColumnWeight = 0.37;
            settings.UiScaleAuto = true;
            settings.UiScalePercent = 100;
            settings.UkraineReferenceLayoutVersion = 1;
            App.Services.Save();
        }

        private static void PlaceBlock(ContentControl block, Grid target, int row, int column, Thickness margin)
        {
            if (!ReferenceEquals(block.Parent, target))
            {
                Detach(block);
                target.Children.Add(block);
            }

            Grid.SetRow(block, row);
            Grid.SetColumn(block, column);
            Grid.SetRowSpan(block, 1);
            Grid.SetColumnSpan(block, 1);
            block.Margin = margin;
            block.Visibility = Visibility.Visible;
        }

        private static void Detach(UIElement element)
        {
            switch (element)
            {
                case FrameworkElement { Parent: Panel panel }:
                    panel.Children.Remove(element);
                    break;
                case FrameworkElement { Parent: ContentControl content } when ReferenceEquals(content.Content, element):
                    content.Content = null;
                    break;
                case FrameworkElement { Parent: Decorator decorator } when ReferenceEquals(decorator.Child, element):
                    decorator.Child = null;
                    break;
            }
        }

        private void InitializeFooterLayoutOnce()
        {
            if (_footerLayoutInitialized) return;
            _footerLayoutInitialized = true;

            var settings = App.Services.Settings.Value;
            var footerGrid = FindNamed<Grid>("FooterBlocksGrid");
            if (footerGrid is not null && footerGrid.ColumnDefinitions.Count >= 5)
            {
                footerGrid.ColumnDefinitions[0].Width = new GridLength(Math.Max(0.1, settings.FooterSystemColumnWeight), GridUnitType.Star);
                footerGrid.ColumnDefinitions[2].Width = new GridLength(Math.Max(0.1, settings.FooterEventsColumnWeight), GridUnitType.Star);
                footerGrid.ColumnDefinitions[4].Width = new GridLength(Math.Max(0.1, settings.FooterMonitorColumnWeight), GridUnitType.Star);
            }

            var designSurface = FindNamed<Grid>("DesignSurface");
            if (designSurface is not null && designSurface.RowDefinitions.Count >= 5)
            {
                designSurface.RowDefinitions[4].Height = new GridLength(Math.Clamp(settings.FooterHeight, 150, 230), GridUnitType.Pixel);
                designSurface.RowDefinitions[4].MinHeight = 150;
                designSurface.RowDefinitions[4].MaxHeight = 230;
            }
        }

        private void TuneAidaCard()
        {
            if (FindNamed<ContentControl>("SystemMonitorPanel") is { } panel)
                panel.Padding = new Thickness(14, 9, 14, 9);

            var valueNames = new[]
            {
                "CpuTemperatureMonitorText",
                "GpuTemperatureMonitorText",
                "GpuLoadMonitorText",
                "ObsFpsText"
            };

            UniformGrid? metricsGrid = null;
            foreach (var name in valueNames)
            {
                if (FindNamed<TextBlock>(name) is not { } valueText) continue;
                if (FindAncestor<Border>(valueText) is not { } metricCard) continue;

                metricsGrid ??= metricCard.Parent as UniformGrid;
                metricCard.Width = double.NaN;
                metricCard.MinWidth = 72;
                metricCard.Height = 62;
                metricCard.CornerRadius = new CornerRadius(4);
                metricCard.BorderThickness = new Thickness(1.2);
                metricCard.Margin = new Thickness(4, 2, 4, 3);

                valueText.FontFamily = new FontFamily("Consolas");
                valueText.FontSize = name == "ObsFpsText" ? 18 : 21;
                valueText.FontWeight = FontWeights.Black;
                valueText.TextAlignment = TextAlignment.Center;

                if (valueText.Parent is StackPanel stack)
                {
                    stack.VerticalAlignment = VerticalAlignment.Center;
                    var label = stack.Children.OfType<TextBlock>().FirstOrDefault(x => !ReferenceEquals(x, valueText));
                    if (label is not null)
                    {
                        label.FontSize = 11.5;
                        label.FontWeight = FontWeights.Bold;
                        if (_window.TryFindResource("Amber") is Brush amber)
                            label.Foreground = amber;
                    }
                }
            }

            if (metricsGrid is not null)
            {
                metricsGrid.Columns = 4;
                metricsGrid.Rows = 1;
                metricsGrid.Margin = new Thickness(0, 2, 0, 1);
            }

            if (FindNamed<TextBlock>("AidaStatusText") is { } header)
            {
                header.FontSize = 16;
                header.FontWeight = FontWeights.Bold;
            }
        }

        private void EnforceAidaHeader()
        {
            if (_disposed || !_window.IsVisible) return;
            var header = FindNamed<TextBlock>("AidaStatusText") ??
                         FindDescendants<TextBlock>(_window).FirstOrDefault(x =>
                             string.Equals(x.Text, "WINDOWS", StringComparison.OrdinalIgnoreCase) ||
                             string.Equals(x.Text, "AIDA64 LIVE", StringComparison.OrdinalIgnoreCase));
            if (header is null) return;
            header.Text = "AIDA64 LIVE";
            if (_window.TryFindResource("Amber") is Brush amber)
                header.Foreground = amber;
        }

        private void EnforceDigitalAidaValues()
        {
            if (FindNamed<TextBlock>("RamLoadMonitorText") is { } ramSource &&
                FindNamed<TextBlock>("GpuLoadMonitorText") is { } ramTarget)
            {
                var ram = ramSource.Text.Split('•', StringSplitOptions.TrimEntries)[0].Trim();
                if (!string.IsNullOrWhiteSpace(ram)) ramTarget.Text = ram;
            }

            if (FindNamed<TextBlock>("ObsFpsText") is { } fps)
            {
                var value = fps.Text
                    .Replace("OBS", string.Empty, StringComparison.OrdinalIgnoreCase)
                    .Replace("FPS", string.Empty, StringComparison.OrdinalIgnoreCase)
                    .Trim();
                fps.Text = string.IsNullOrWhiteSpace(value) ? "—" : value;
            }
        }

        private void HookQuickMixerControls()
        {
            foreach (var slider in FindDescendants<Slider>(_window).Where(x => x.Tag is AudioChannel))
            {
                if (!_hookedMixerSliders.Add(slider)) continue;
                slider.PreviewMouseLeftButtonUp += MixerSlider_PreviewMouseLeftButtonUp;
            }

            foreach (var button in FindDescendants<Button>(_window).Where(x => x.Tag is AudioChannel))
            {
                if (!_hookedMuteButtons.Add(button)) continue;
                button.PreviewMouseLeftButtonDown += MixerMute_PreviewMouseLeftButtonDown;
            }
        }

        private async void MixerSlider_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (sender is not Slider { Tag: AudioChannel channel } slider || !App.Services.Obs.IsConnected) return;
            try
            {
                await App.Services.Obs.SetInputVolumeAsync(channel.Name, slider.Value);
                channel.Volume = slider.Value;
            }
            catch (Exception ex)
            {
                App.Services.Logger.Error($"Гучність {channel.Name}", ex);
            }
        }

        private async void MixerMute_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is not Button { Tag: AudioChannel channel } || !App.Services.Obs.IsConnected) return;
            e.Handled = true;
            try
            {
                var current = await App.Services.Obs.GetInputMuteAsync(channel.Name);
                var target = !current;
                await App.Services.Obs.SetInputMuteAsync(channel.Name, target);
                channel.IsMuted = target;
            }
            catch (Exception ex)
            {
                App.Services.Logger.Error($"MUTE {channel.Name}", ex);
            }
        }

        private async Task TryRepairQuickMixerAsync()
        {
            if (_autoMixerRepairBusy || !App.Services.Obs.IsConnected ||
                _window.QuickAudioPage.Count > 0 || !App.Services.Settings.Value.AudioAutoDetect)
                return;

            _autoMixerRepairBusy = true;
            try
            {
                var inputs = (await App.Services.Obs.GetPrimaryMixerInputsAsync()).ToList();
                if (inputs.Count == 0)
                    inputs = (await App.Services.Obs.GetMixerInputsAsync()).ToList();
                if (inputs.Count == 0) return;

                var settings = App.Services.Settings.Value;
                settings.SelectedAudioInputs = inputs.Select(x => x.name).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                App.Services.Save();

                var refresh = typeof(MainWindow).GetMethod(
                    "RefreshQuickAudioSafeAsync",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                if (refresh?.Invoke(_window, null) is Task task)
                    await task;
            }
            catch (Exception ex)
            {
                App.Services.Logger.Error("Автовідновлення швидкого мікшера", ex);
            }
            finally
            {
                _autoMixerRepairBusy = false;
            }
        }

        private void UpdateEmptyStates()
        {
            if (FindNamed<TextBlock>("ChatEmptyStateText") is { } chatEmpty)
                chatEmpty.Visibility = _window.MainChatMessages.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            if (FindNamed<TextBlock>("AudioEmptyStateText") is { } audioEmpty)
                audioEmpty.Visibility = _window.QuickAudioPage.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            if (FindNamed<TextBlock>("RecentDonationsEmptyText") is { } donationsEmpty)
                donationsEmpty.Visibility = _window.RecentDonations.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            if (FindNamed<TextBlock>("DonationEmptyStateText") is { } notificationsEmpty)
                notificationsEmpty.Visibility = _window.DonationPage.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private T? FindNamed<T>(string name) where T : FrameworkElement =>
            FindDescendants<T>(_window).FirstOrDefault(x => string.Equals(x.Name, name, StringComparison.Ordinal));

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _guardTimer.Stop();
            _guardTimer.Tick -= GuardTimer_Tick;
            _window.Loaded -= Window_Loaded;
            _window.SizeChanged -= Window_SizeChanged;
            _window.Closed -= Window_Closed;

            foreach (var slider in _hookedMixerSliders)
                slider.PreviewMouseLeftButtonUp -= MixerSlider_PreviewMouseLeftButtonUp;
            foreach (var button in _hookedMuteButtons)
                button.PreviewMouseLeftButtonDown -= MixerMute_PreviewMouseLeftButtonDown;
            _hookedMixerSliders.Clear();
            _hookedMuteButtons.Clear();
        }
    }

    private static readonly ConditionalWeakTable<MainWindow, Controller> Controllers = new();

    public static IDisposable Attach(MainWindow window)
    {
        if (Controllers.TryGetValue(window, out var existing))
            return existing;
        var controller = new Controller(window);
        Controllers.Add(window, controller);
        return controller;
    }

    public static void ApplyNow(MainWindow window)
    {
        if (Controllers.TryGetValue(window, out var controller))
            controller.ApplyNow();
        else
            _ = Attach(window);
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

    private static T? FindAncestor<T>(DependencyObject source) where T : DependencyObject
    {
        for (var current = source; current is not null; current = GetParent(current))
            if (current is T match) return match;
        return null;
    }

    private static DependencyObject? GetParent(DependencyObject current)
    {
        if (current is FrameworkElement element && element.Parent is not null)
            return element.Parent;
        if (current is FrameworkContentElement contentElement)
            return contentElement.Parent;
        try { return VisualTreeHelper.GetParent(current); }
        catch { return null; }
    }
}
