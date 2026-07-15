using System.Windows.Threading;

namespace TiHiY.StreamControlCenter.Services;

public static class MainWindowVisualTuner
{
    private sealed class Controller : IDisposable
    {
        private readonly MainWindow _window;
        private readonly DispatcherTimer _guardTimer;
        private bool _footerLayoutInitialized;
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
            TunePatrioticCard();
            TuneAidaCard();
            TuneSystemStatusCard();
        }
        private void GuardTimer_Tick(object? sender, EventArgs e)
        {
            EnforceAidaHeader();
            UpdateEmptyStates();
        }
        private void Window_Closed(object? sender, EventArgs e) => Dispose();

        public void ApplyNow()
        {
            if (_disposed) return;
            InitializeFooterLayoutOnce();
            TunePatrioticCard();
            TuneAidaCard();
            TuneSystemStatusCard();
            EnforceAidaHeader();
            UpdateEmptyStates();
        }

        private void InitializeFooterLayoutOnce()
        {
            if (_footerLayoutInitialized) return;
            _footerLayoutInitialized = true;

            var settings = App.Services.Settings.Value;
            if (settings.DashboardLayoutVersion >= 20) return;

            settings.FooterHeight = 165;
            settings.FooterSystemColumnWeight = 0.32;
            settings.FooterEventsColumnWeight = 0.25;
            settings.FooterMonitorColumnWeight = 0.43;
            settings.DashboardLayoutVersion = 20;
            App.Services.Save();

            var footerGrid = FindNamed<Grid>("FooterBlocksGrid");
            if (footerGrid is not null && footerGrid.ColumnDefinitions.Count >= 5)
            {
                footerGrid.ColumnDefinitions[0].Width = new GridLength(settings.FooterSystemColumnWeight, GridUnitType.Star);
                footerGrid.ColumnDefinitions[2].Width = new GridLength(settings.FooterEventsColumnWeight, GridUnitType.Star);
                footerGrid.ColumnDefinitions[4].Width = new GridLength(settings.FooterMonitorColumnWeight, GridUnitType.Star);
            }

            var designSurface = FindNamed<Grid>("DesignSurface");
            if (designSurface is not null && designSurface.RowDefinitions.Count >= 5)
            {
                designSurface.RowDefinitions[4].Height = new GridLength(settings.FooterHeight, GridUnitType.Pixel);
                designSurface.RowDefinitions[4].MinHeight = 150;
                designSurface.RowDefinitions[4].MaxHeight = 230;
            }
        }

        private void TuneSystemStatusCard()
        {
            if (FindNamed<TextBlock>("CpuLoadMonitorText")?.Parent is not StackPanel metricsStack ||
                metricsStack.Parent is not Grid systemGrid || systemGrid.ColumnDefinitions.Count < 3)
                return;

            systemGrid.ColumnDefinitions[0].Width = new GridLength(1.35, GridUnitType.Star);
            systemGrid.ColumnDefinitions[1].Width = new GridLength(0.65, GridUnitType.Star);
            systemGrid.ColumnDefinitions[2].Width = new GridLength(90, GridUnitType.Pixel);

            foreach (var text in metricsStack.Children.OfType<TextBlock>())
                text.FontSize = text.FontWeight == FontWeights.Bold ? 10 : 9;
        }

        private void TunePatrioticCard()
        {
            var gloryLine = FindDescendants<TextBlock>(_window).FirstOrDefault(x =>
                x.Text.Contains("СЛАВА УКРАЇНІ", StringComparison.OrdinalIgnoreCase) ||
                x.Text.Contains("GLORY TO UKRAINE", StringComparison.OrdinalIgnoreCase));
            if (gloryLine?.Parent is not StackPanel textStack || textStack.Parent is not Grid cardGrid)
                return;

            textStack.VerticalAlignment = VerticalAlignment.Bottom;
            textStack.HorizontalAlignment = HorizontalAlignment.Center;
            textStack.Margin = new Thickness(6, 0, 6, 5);

            foreach (var text in textStack.Children.OfType<TextBlock>())
            {
                text.FontSize = 11.5;
                text.LineHeight = 13;
                text.Margin = new Thickness(0);
                text.TextAlignment = TextAlignment.Center;
            }

            var emblem = cardGrid.Children.OfType<Image>().FirstOrDefault();
            if (emblem is not null)
            {
                emblem.Width = 66;
                emblem.Height = 66;
                emblem.VerticalAlignment = VerticalAlignment.Top;
                emblem.HorizontalAlignment = HorizontalAlignment.Center;
                emblem.Margin = new Thickness(0);
            }

            if (FindAncestor<ContentControl>(cardGrid) is { } card)
                card.Padding = new Thickness(5);
        }

        private void TuneAidaCard()
        {
            if (FindNamed<ContentControl>("SystemMonitorPanel") is { } panel)
                panel.Padding = new Thickness(10, 7, 10, 7);

            foreach (var name in new[]
                     {
                         "CpuTemperatureMonitorText",
                         "GpuTemperatureMonitorText",
                         "GpuLoadMonitorText",
                         "ObsFpsText"
                     })
            {
                if (FindNamed<TextBlock>(name) is not { } valueText) continue;
                if (FindAncestor<Border>(valueText) is not { } circle) continue;
                circle.Width = 70;
                circle.Height = 70;
                circle.CornerRadius = new CornerRadius(35);
                circle.Margin = new Thickness(3, 0, 3, 1);
                valueText.FontSize = name == "ObsFpsText" ? 9.5 : 14.5;
            }

            if (FindNamed<TextBlock>("AidaStatusText") is { } header)
            {
                header.FontSize = 15;
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
