using System.Collections.Concurrent;
using System.Collections.Specialized;
using System.Globalization;
using System.Windows.Media.Imaging;
using TiHiY.StreamControlCenter.Models;
using TiHiY.StreamControlCenter.Windows;

namespace TiHiY.StreamControlCenter.Services;

/// <summary>
/// Final runtime corrections for controls that are created by ItemsControl templates.
/// Static dashboard composition is applied once; only real audio levels are refreshed.
/// </summary>
internal static class RuntimeUiCorrections
{
    private static readonly ConditionalWeakTable<MainWindow, MainController> MainControllers = new();
    private static readonly ConditionalWeakTable<ChatBotWindow, ChatController> ChatControllers = new();
    private static readonly ConditionalWeakTable<AudioMixerWindow, AudioController> AudioControllers = new();

    [ModuleInitializer]
    internal static void Register()
    {
        EventManager.RegisterClassHandler(typeof(MainWindow), FrameworkElement.LoadedEvent, new RoutedEventHandler(OnMainLoaded));
        EventManager.RegisterClassHandler(typeof(ChatBotWindow), FrameworkElement.LoadedEvent, new RoutedEventHandler(OnChatLoaded));
        EventManager.RegisterClassHandler(typeof(AudioMixerWindow), FrameworkElement.LoadedEvent, new RoutedEventHandler(OnAudioLoaded));
    }

    private static void OnMainLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is not MainWindow window || MainControllers.TryGetValue(window, out _)) return;
        MainControllers.Add(window, new MainController(window));
    }

    private static void OnChatLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is not ChatBotWindow window || ChatControllers.TryGetValue(window, out _)) return;
        ChatControllers.Add(window, new ChatController(window));
    }

    private static void OnAudioLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is not AudioMixerWindow window || AudioControllers.TryGetValue(window, out _)) return;
        AudioControllers.Add(window, new AudioController(window));
    }

    private sealed class MainController : IDisposable
    {
        private readonly MainWindow _window;
        private readonly DispatcherTimer _meterTimer;
        private readonly ConcurrentDictionary<string, MeterSnapshot> _meters = new(StringComparer.OrdinalIgnoreCase);
        private bool _staticApplied;
        private bool _disposed;

        public MainController(MainWindow window)
        {
            _window = window;
            _window.QuickAudioPage.CollectionChanged += QuickAudioPage_CollectionChanged;
            _window.Closed += Window_Closed;
            App.Services.Obs.InputMeterChanged += Obs_InputMeterChanged;

            _meterTimer = new DispatcherTimer(DispatcherPriority.Render, window.Dispatcher)
            {
                Interval = TimeSpan.FromMilliseconds(90)
            };
            _meterTimer.Tick += MeterTimer_Tick;
            _meterTimer.Start();

            ScheduleStaticApply();
        }

        private void ScheduleStaticApply()
        {
            _window.Dispatcher.BeginInvoke(new Action(ApplyStaticOnce), DispatcherPriority.Loaded);
            _window.Dispatcher.BeginInvoke(new Action(ApplyStaticOnce), DispatcherPriority.Render);
            _window.Dispatcher.BeginInvoke(new Action(ApplyStaticOnce), DispatcherPriority.ContextIdle);
        }

        private void ApplyStaticOnce()
        {
            if (_disposed || !_window.IsLoaded) return;
            ApplyTopStatusIcons();
            var aida = BuildAdaptiveAida();
            var system = BuildAdaptiveSystemStatus();
            PatchQuickMixerRows();
            _staticApplied = aida && system;
        }

        private void MeterTimer_Tick(object? sender, EventArgs e)
        {
            if (_disposed) return;
            if (!_staticApplied) ApplyStaticOnce();

            foreach (var channel in _window.QuickAudioPage)
            {
                if (!_meters.TryGetValue(channel.Name, out var snapshot)) continue;
                channel.Meter = snapshot.Meter;
                channel.Db = snapshot.Db;
            }
            PatchQuickMixerRows();
        }

        private void QuickAudioPage_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
            _window.Dispatcher.BeginInvoke(new Action(PatchQuickMixerRows), DispatcherPriority.Render);

        private void Obs_InputMeterChanged(object? sender, (string inputName, double meter, double db) data)
        {
            _meters[data.inputName] = new MeterSnapshot(data.meter, data.db);
            _window.Dispatcher.BeginInvoke(new Action(() =>
            {
                var channel = _window.QuickAudioPage.FirstOrDefault(x =>
                    string.Equals(x.Name, data.inputName, StringComparison.OrdinalIgnoreCase));
                if (channel is null) return;
                channel.Meter = data.meter;
                channel.Db = data.db;
            }), DispatcherPriority.Render);
        }

        private void PatchQuickMixerRows()
        {
            var mixer = FindNamed<ContentControl>(_window, "MixerBlockPanel");
            if (mixer is null) return;

            foreach (var card in Descendants<Border>(mixer).Where(x => x.DataContext is AudioChannel).ToList())
            {
                var channel = (AudioChannel)card.DataContext;
                var row = Descendants<Grid>(card).FirstOrDefault(x => x.ColumnDefinitions.Count >= 6 && x.Height > 0);
                if (row is null || Equals(row.Tag, "TiHiYLiveMeterRow")) continue;

                var oldMeterHost = row.Children.OfType<Grid>().FirstOrDefault(x => Grid.GetColumn(x) == 2);
                if (oldMeterHost is not null)
                {
                    oldMeterHost.Visibility = Visibility.Collapsed;
                    oldMeterHost.IsHitTestVisible = false;
                }

                var meter = new LiveDbMeter(channel)
                {
                    Margin = new Thickness(7, 4, 12, 4),
                    VerticalAlignment = VerticalAlignment.Stretch,
                    HorizontalAlignment = HorizontalAlignment.Stretch
                };
                Grid.SetColumn(meter, 2);
                row.Children.Add(meter);

                var iconBorder = row.Children.OfType<Border>().FirstOrDefault(x => Grid.GetColumn(x) == 0);
                if (iconBorder is not null)
                    iconBorder.Child = new VectorChannelIcon(channel) { Margin = new Thickness(4) };

                row.Tag = "TiHiYLiveMeterRow";
            }
        }

        private bool BuildAdaptiveAida()
        {
            var panel = FindNamed<ContentControl>(_window, "SystemMonitorPanel");
            if (panel is null) return false;
            if (Equals(panel.Tag, "TiHiYAdaptiveAida")) return true;

            var cpu = FindNamed<TextBlock>(_window, "CpuTemperatureMonitorText");
            var gpu = FindNamed<TextBlock>(_window, "GpuTemperatureMonitorText");
            var ram = FindNamed<TextBlock>(_window, "GpuLoadMonitorText");
            var fps = FindNamed<TextBlock>(_window, "ObsFpsText");
            if (cpu is null || gpu is null || ram is null || fps is null) return false;

            var openButton = Descendants<Button>(panel).FirstOrDefault(x => TextOf(x.Content).Contains("AIDA64", StringComparison.OrdinalIgnoreCase));
            if (openButton is not null) Detach(openButton);

            var root = new Grid { ClipToBounds = true };
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(34) });
            root.Children.Add(new AdaptiveAidaSurface(cpu, gpu, ram, fps));
            if (openButton is not null)
            {
                openButton.Margin = new Thickness(2, 2, 2, 0);
                openButton.HorizontalAlignment = HorizontalAlignment.Stretch;
                Grid.SetRow(openButton, 1);
                root.Children.Add(openButton);
            }

            panel.Padding = new Thickness(9, 7, 9, 7);
            panel.Content = root;
            panel.Tag = "TiHiYAdaptiveAida";
            return true;
        }

        private bool BuildAdaptiveSystemStatus()
        {
            var panel = FindNamed<ContentControl>(_window, "SystemStatusBlockPanel");
            if (panel is null) return false;
            if (Equals(panel.Tag, "TiHiYAdaptiveSystem")) return true;

            var sources = new[]
            {
                FindNamed<TextBlock>(_window, "SystemObsText"),
                FindNamed<TextBlock>(_window, "SystemTwitchText"),
                FindNamed<TextBlock>(_window, "SystemYouTubeText"),
                FindNamed<TextBlock>(_window, "SystemOverlayText"),
                FindNamed<TextBlock>(_window, "SystemDonatelloText")
            };
            var cpu = FindNamed<TextBlock>(_window, "CpuLoadMonitorText");
            var ram = FindNamed<TextBlock>(_window, "RamLoadMonitorText");
            var state = FindNamed<TextBlock>(_window, "SystemStateText");
            if (sources.Any(x => x is null) || cpu is null || ram is null || state is null) return false;

            panel.Padding = new Thickness(9, 7, 9, 7);
            panel.Content = new AdaptiveSystemSurface(sources.Select(x => x!).ToArray(), cpu, ram, state);
            panel.Tag = "TiHiYAdaptiveSystem";
            return true;
        }

        private void ApplyTopStatusIcons()
        {
            AddStatusIcon("OBS AUDIO", "obs");
            AddStatusIcon("MULTISTREAM", "network");
            EnsurePlatformStatusIcon("TWITCH", "/TiHiY.StreamControlCenter;component/Assets/Platforms/twitch.png");
            EnsurePlatformStatusIcon("YOUTUBE", "/TiHiY.StreamControlCenter;component/Assets/Platforms/youtube.png");
        }

        private void AddStatusIcon(string title, string kind)
        {
            var text = Descendants<TextBlock>(_window).FirstOrDefault(x => string.Equals(x.Text, title, StringComparison.OrdinalIgnoreCase));
            if (text?.Parent is not StackPanel vertical || vertical.Parent is not StackPanel horizontal) return;
            if (horizontal.Children.OfType<VectorStatusIcon>().Any()) return;
            foreach (var dot in horizontal.Children.OfType<Ellipse>()) dot.Visibility = Visibility.Collapsed;
            horizontal.Children.Insert(0, new VectorStatusIcon(kind)
            {
                Width = 23,
                Height = 23,
                Margin = new Thickness(0, 0, 8, 0),
                VerticalAlignment = VerticalAlignment.Center
            });
        }

        private void EnsurePlatformStatusIcon(string title, string uri)
        {
            var text = Descendants<TextBlock>(_window).FirstOrDefault(x => string.Equals(x.Text, title, StringComparison.OrdinalIgnoreCase));
            if (text?.Parent is not StackPanel vertical || vertical.Parent is not StackPanel horizontal) return;
            var image = horizontal.Children.OfType<Image>().FirstOrDefault();
            if (image is null)
            {
                image = new Image();
                horizontal.Children.Insert(0, image);
            }
            image.Source = LoadImage(uri);
            image.Width = 23;
            image.Height = 23;
            image.Stretch = Stretch.Uniform;
            image.Margin = new Thickness(0, 0, 8, 0);
        }

        private void Window_Closed(object? sender, EventArgs e) => Dispose();

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _meterTimer.Stop();
            _meterTimer.Tick -= MeterTimer_Tick;
            _window.QuickAudioPage.CollectionChanged -= QuickAudioPage_CollectionChanged;
            _window.Closed -= Window_Closed;
            App.Services.Obs.InputMeterChanged -= Obs_InputMeterChanged;
        }
    }

    private sealed class ChatController : IDisposable
    {
        private readonly ChatBotWindow _window;
        private readonly DispatcherTimer _timer;

        public ChatController(ChatBotWindow window)
        {
            _window = window;
            _window.Closed += Window_Closed;
            _timer = new DispatcherTimer(DispatcherPriority.Background, window.Dispatcher)
            {
                Interval = TimeSpan.FromMilliseconds(300)
            };
            _timer.Tick += Timer_Tick;
            _timer.Start();
            Patch();
        }

        private void Timer_Tick(object? sender, EventArgs e) => Patch();

        private void Patch()
        {
            foreach (var rowBorder in Descendants<Border>(_window).Where(x => x.DataContext is ChatMessage).ToList())
            {
                var message = (ChatMessage)rowBorder.DataContext;
                var row = Descendants<Grid>(rowBorder).FirstOrDefault(x => x.ColumnDefinitions.Count == 4);
                var platformBorder = row?.Children.OfType<Border>().FirstOrDefault(x => Grid.GetColumn(x) == 1);
                if (platformBorder is null || Equals(platformBorder.Tag, "TiHiYPlatformIcon")) continue;
                platformBorder.Child = CreatePlatformVisual(message.Platform);
                platformBorder.ToolTip = message.Platform;
                platformBorder.Tag = "TiHiYPlatformIcon";
            }
        }

        private void Window_Closed(object? sender, EventArgs e) => Dispose();
        public void Dispose()
        {
            _timer.Stop();
            _timer.Tick -= Timer_Tick;
            _window.Closed -= Window_Closed;
        }
    }

    private sealed class AudioController : IDisposable
    {
        private readonly AudioMixerWindow _window;
        private readonly DispatcherTimer _timer;

        public AudioController(AudioMixerWindow window)
        {
            _window = window;
            _window.Closed += Window_Closed;
            _timer = new DispatcherTimer(DispatcherPriority.Background, window.Dispatcher)
            {
                Interval = TimeSpan.FromMilliseconds(300)
            };
            _timer.Tick += Timer_Tick;
            _timer.Start();
            Patch();
        }

        private void Timer_Tick(object? sender, EventArgs e) => Patch();

        private void Patch()
        {
            foreach (var card in Descendants<Border>(_window).Where(x => x.DataContext is AudioChannel).ToList())
            {
                var channel = (AudioChannel)card.DataContext;
                var layout = Descendants<Grid>(card).FirstOrDefault(x => x.RowDefinitions.Count >= 5);
                var header = layout?.Children.OfType<Grid>().FirstOrDefault(x => Grid.GetRow(x) == 0);
                if (header is null || Equals(header.Tag, "TiHiYChannelIcon")) continue;

                var name = header.Children.OfType<TextBlock>().FirstOrDefault(x => x.HorizontalAlignment != HorizontalAlignment.Right);
                if (name is not null) name.Margin = new Thickness(31, 0, 92, 0);
                header.Children.Add(new VectorChannelIcon(channel)
                {
                    Width = 23,
                    Height = 23,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Center
                });
                header.Tag = "TiHiYChannelIcon";
            }
        }

        private void Window_Closed(object? sender, EventArgs e) => Dispose();
        public void Dispose()
        {
            _timer.Stop();
            _timer.Tick -= Timer_Tick;
            _window.Closed -= Window_Closed;
        }
    }

    private sealed class LiveDbMeter : FrameworkElement, IDisposable
    {
        private readonly AudioChannel _channel;
        private readonly Pen _borderPen = new(new SolidColorBrush(Color.FromRgb(40, 90, 120)), 1);
        private bool _disposed;

        public LiveDbMeter(AudioChannel channel)
        {
            _channel = channel;
            MinHeight = 10;
            SnapsToDevicePixels = true;
            _channel.PropertyChanged += Channel_PropertyChanged;
            Unloaded += (_, _) => Dispose();
        }

        private void Channel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(AudioChannel.Db) or nameof(AudioChannel.Meter)) InvalidateVisual();
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);
            var rect = new Rect(0.5, 0.5, Math.Max(0, ActualWidth - 1), Math.Max(0, ActualHeight - 1));
            dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromRgb(1, 8, 15)), _borderPen, rect, 3, 3);
            if (rect.Width <= 2 || rect.Height <= 2) return;

            var level = _channel.Db > -59.95
                ? Math.Clamp((_channel.Db + 60) / 60, 0, 1)
                : Math.Clamp(_channel.Meter, 0, 1);
            var fillWidth = Math.Max(0, (rect.Width - 2) * level);
            if (fillWidth > 0.5)
            {
                var brush = new LinearGradientBrush
                {
                    StartPoint = new Point(0, 0),
                    EndPoint = new Point(rect.Width, 0),
                    MappingMode = BrushMappingMode.Absolute
                };
                brush.GradientStops.Add(new GradientStop(Color.FromRgb(23, 215, 102), 0));
                brush.GradientStops.Add(new GradientStop(Color.FromRgb(174, 225, 57), rect.Width * 0.62));
                brush.GradientStops.Add(new GradientStop(Color.FromRgb(245, 176, 0), rect.Width * 0.82));
                brush.GradientStops.Add(new GradientStop(Color.FromRgb(255, 76, 88), rect.Width));
                dc.DrawRoundedRectangle(brush, null, new Rect(1.5, 1.5, fillWidth, Math.Max(1, rect.Height - 2)), 2, 2);
            }

            var tickPen = new Pen(new SolidColorBrush(Color.FromArgb(70, 230, 245, 255)), 0.6);
            foreach (var db in new[] { -48d, -36d, -24d, -12d, -6d })
            {
                var x = 1.5 + (db + 60) / 60 * Math.Max(0, rect.Width - 2);
                dc.DrawLine(tickPen, new Point(x, rect.Height * 0.25), new Point(x, rect.Height * 0.75));
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _channel.PropertyChanged -= Channel_PropertyChanged;
        }
    }

    private sealed class VectorChannelIcon : FrameworkElement
    {
        private readonly AudioChannel _channel;
        public VectorChannelIcon(AudioChannel channel) => _channel = channel;
        protected override void OnRender(DrawingContext dc) => DrawIcon(dc, new Rect(0, 0, ActualWidth, ActualHeight), ChannelKind(_channel), Color.FromRgb(245, 169, 0));
    }

    private sealed class VectorStatusIcon : FrameworkElement
    {
        private readonly string _kind;
        public VectorStatusIcon(string kind) => _kind = kind;
        protected override void OnRender(DrawingContext dc) => DrawIcon(dc, new Rect(0, 0, ActualWidth, ActualHeight), _kind, Color.FromRgb(245, 169, 0));
    }

    private abstract class AdaptiveSurfaceBase : FrameworkElement, IDisposable
    {
        private readonly List<(TextBlock Source, DependencyPropertyDescriptor Descriptor, EventHandler Handler)> _subscriptions = new();
        private bool _disposed;

        protected void Watch(params TextBlock[] sources)
        {
            foreach (var source in sources)
            {
                var descriptor = DependencyPropertyDescriptor.FromProperty(TextBlock.TextProperty, typeof(TextBlock));
                if (descriptor is null) continue;
                EventHandler handler = (_, _) => InvalidateVisual();
                descriptor.AddValueChanged(source, handler);
                _subscriptions.Add((source, descriptor, handler));
            }
        }

        protected static FormattedText TextLayout(string text, double size, Brush brush, FontWeight weight, double maxWidth)
        {
            var formatted = new FormattedText(
                text ?? string.Empty,
                CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight,
                new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, weight, FontStretches.Normal),
                Math.Max(7, size),
                brush,
                1.0)
            {
                MaxTextWidth = Math.Max(1, maxWidth),
                Trimming = TextTrimming.CharacterEllipsis
            };
            return formatted;
        }

        protected static void DrawCentered(DrawingContext dc, string text, Rect rect, double size, Brush brush, FontWeight weight)
        {
            var ft = TextLayout(text, size, brush, weight, rect.Width);
            dc.DrawText(ft, new Point(rect.Left + Math.Max(0, (rect.Width - ft.Width) / 2), rect.Top + Math.Max(0, (rect.Height - ft.Height) / 2)));
        }

        protected static void DrawLeft(DrawingContext dc, string text, Rect rect, double size, Brush brush, FontWeight weight)
        {
            var ft = TextLayout(text, size, brush, weight, rect.Width);
            dc.DrawText(ft, new Point(rect.Left, rect.Top + Math.Max(0, (rect.Height - ft.Height) / 2)));
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            foreach (var item in _subscriptions) item.Descriptor.RemoveValueChanged(item.Source, item.Handler);
            _subscriptions.Clear();
        }
    }

    private sealed class AdaptiveAidaSurface : AdaptiveSurfaceBase
    {
        private readonly TextBlock[] _values;
        private readonly ImageSource? _wheat;

        public AdaptiveAidaSurface(params TextBlock[] values)
        {
            _values = values;
            Watch(values);
            _wheat = LoadImage("/TiHiY.StreamControlCenter;component/Assets/Themes/UkraineExact/footer-wheat.png");
            Unloaded += (_, _) => Dispose();
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);
            var w = ActualWidth;
            var h = ActualHeight;
            if (w < 20 || h < 20) return;
            var scale = Math.Clamp(Math.Min(w / 520, h / 122), 0.55, 1.75);
            var gold = new SolidColorBrush(Color.FromRgb(255, 190, 20));
            var panel = new SolidColorBrush(Color.FromArgb(215, 5, 20, 36));
            var line = new Pen(new SolidColorBrush(Color.FromRgb(224, 156, 0)), Math.Max(1, 1.2 * scale));

            if (_wheat is not null)
            {
                dc.PushOpacity(0.24);
                var imageWidth = Math.Min(w * 0.25, h * 0.9);
                dc.DrawImage(_wheat, new Rect(w - imageWidth, 0, imageWidth, h));
                dc.Pop();
            }

            var titleHeight = Math.Clamp(27 * scale, 20, h * 0.28);
            dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(34, 218, 114)), null, new Point(8 * scale, titleHeight / 2), 4.5 * scale, 4.5 * scale);
            DrawLeft(dc, "AIDA64 LIVE", new Rect(18 * scale, 0, w - 18 * scale, titleHeight), 18 * scale, gold, FontWeights.Black);

            var labels = new[] { "CPU", "GPU", "RAM", "FPS" };
            var gap = Math.Max(4, 7 * scale);
            var top = titleHeight + gap;
            var cardHeight = Math.Max(18, h - top - 2);
            var cardWidth = Math.Max(30, (w - gap * 5) / 4);
            for (var i = 0; i < 4; i++)
            {
                var rect = new Rect(gap + i * (cardWidth + gap), top, cardWidth, cardHeight);
                dc.DrawRoundedRectangle(panel, line, rect, 4 * scale, 4 * scale);
                DrawCentered(dc, labels[i], new Rect(rect.Left, rect.Top + 2, rect.Width, rect.Height * 0.34), 13 * scale, gold, FontWeights.Black);
                DrawCentered(dc, _values[i].Text, new Rect(rect.Left + 2, rect.Top + rect.Height * 0.30, rect.Width - 4, rect.Height * 0.66), 23 * scale, gold, FontWeights.Black);
            }
        }
    }

    private sealed class AdaptiveSystemSurface : AdaptiveSurfaceBase
    {
        private readonly TextBlock[] _statuses;
        private readonly TextBlock _cpu;
        private readonly TextBlock _ram;
        private readonly TextBlock _state;

        public AdaptiveSystemSurface(TextBlock[] statuses, TextBlock cpu, TextBlock ram, TextBlock state)
        {
            _statuses = statuses;
            _cpu = cpu;
            _ram = ram;
            _state = state;
            Watch(statuses.Concat(new[] { cpu, ram, state }).ToArray());
            Unloaded += (_, _) => Dispose();
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);
            var w = ActualWidth;
            var h = ActualHeight;
            if (w < 20 || h < 20) return;
            var scale = Math.Clamp(Math.Min(w / 520, h / 150), 0.52, 1.7);
            var gold = new SolidColorBrush(Color.FromRgb(255, 190, 20));
            var muted = new SolidColorBrush(Color.FromRgb(143, 168, 186));
            var green = new SolidColorBrush(Color.FromRgb(34, 218, 114));
            var cyan = new SolidColorBrush(Color.FromRgb(25, 183, 255));
            var titleHeight = Math.Clamp(28 * scale, 20, h * 0.25);
            DrawLeft(dc, "СТАН СИСТЕМИ", new Rect(2, 0, w - 4, titleHeight), 17 * scale, gold, FontWeights.Black);

            var bodyTop = titleHeight + 2;
            var bodyHeight = Math.Max(1, h - bodyTop);
            var leftWidth = w * 0.54;
            var middleWidth = w * 0.29;
            var rowHeight = bodyHeight / Math.Max(5, _statuses.Length);
            for (var i = 0; i < _statuses.Length; i++)
            {
                var sourceBrush = _statuses[i].Foreground as Brush ?? green;
                DrawLeft(dc, _statuses[i].Text, new Rect(2, bodyTop + i * rowHeight, leftWidth - 6, rowHeight), 11.5 * scale, sourceBrush, FontWeights.SemiBold);
            }

            var metricX = leftWidth + 4;
            DrawLeft(dc, "CPU", new Rect(metricX, bodyTop, middleWidth, bodyHeight * 0.18), 11 * scale, muted, FontWeights.Bold);
            DrawLeft(dc, _cpu.Text, new Rect(metricX, bodyTop + bodyHeight * 0.15, middleWidth, bodyHeight * 0.25), 15 * scale, _cpu.Foreground as Brush ?? green, FontWeights.Black);
            DrawLeft(dc, "RAM", new Rect(metricX, bodyTop + bodyHeight * 0.43, middleWidth, bodyHeight * 0.18), 11 * scale, muted, FontWeights.Bold);
            DrawLeft(dc, _ram.Text, new Rect(metricX, bodyTop + bodyHeight * 0.58, middleWidth, bodyHeight * 0.24), 13 * scale, _ram.Foreground as Brush ?? green, FontWeights.Black);
            DrawLeft(dc, _state.Text, new Rect(metricX, bodyTop + bodyHeight * 0.82, middleWidth, bodyHeight * 0.18), 8.5 * scale, muted, FontWeights.Normal);

            var x0 = leftWidth + middleWidth + 5;
            var heartW = Math.Max(10, w - x0 - 4);
            var mid = bodyTop + bodyHeight * 0.54;
            var points = new[]
            {
                new Point(x0, mid), new Point(x0 + heartW * .18, mid), new Point(x0 + heartW * .26, mid - bodyHeight * .10),
                new Point(x0 + heartW * .36, mid + bodyHeight * .13), new Point(x0 + heartW * .49, bodyTop + bodyHeight * .13),
                new Point(x0 + heartW * .62, bodyTop + bodyHeight * .88), new Point(x0 + heartW * .74, mid - bodyHeight * .12),
                new Point(x0 + heartW * .84, mid), new Point(x0 + heartW, mid)
            };
            var pen = new Pen(cyan, Math.Max(1.2, 2.3 * scale));
            for (var i = 1; i < points.Length; i++) dc.DrawLine(pen, points[i - 1], points[i]);
        }
    }

    private static FrameworkElement CreatePlatformVisual(string platform)
    {
        if (platform.Equals("TWITCH", StringComparison.OrdinalIgnoreCase))
            return new Image { Source = LoadImage("/TiHiY.StreamControlCenter;component/Assets/Platforms/twitch.png"), Stretch = Stretch.Uniform, Margin = new Thickness(2) };
        if (platform.Equals("YOUTUBE", StringComparison.OrdinalIgnoreCase))
            return new Image { Source = LoadImage("/TiHiY.StreamControlCenter;component/Assets/Platforms/youtube.png"), Stretch = Stretch.Uniform, Margin = new Thickness(2) };
        if (platform.Equals("DONATELLO", StringComparison.OrdinalIgnoreCase))
            return new TextBlock { Text = "♥", Foreground = new SolidColorBrush(Color.FromRgb(255, 210, 41)), FontSize = 16, FontWeight = FontWeights.Black, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        if (platform.Equals("DISCORD", StringComparison.OrdinalIgnoreCase))
            return new VectorStatusIcon("chat") { Margin = new Thickness(3) };
        return new Image { Source = LoadImage("/TiHiY.StreamControlCenter;component/Assets/AppIcon.png"), Stretch = Stretch.Uniform, Margin = new Thickness(2) };
    }

    private static ImageSource? LoadImage(string uri)
    {
        try
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.UriSource = new Uri(uri, UriKind.RelativeOrAbsolute);
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch { return null; }
    }

    private static string ChannelKind(AudioChannel channel)
    {
        var value = $"{channel.Name} {channel.Kind}".ToLowerInvariant();
        if (value.Contains("mic") || value.Contains("мікроф") || value.Contains("aux")) return "mic";
        if (value.Contains("browser") || value.Contains("брауз")) return "globe";
        if (value.Contains("media") || value.Contains("music") || value.Contains("музик") || value.Contains("vlc") || value.Contains("ffmpeg")) return "music";
        if (value.Contains("discord") || value.Contains("chat") || value.Contains("чат")) return "chat";
        if (value.Contains("desktop") || value.Contains("систем") || value.Contains("wasapi_output")) return "desktop";
        if (value.Contains("obs")) return "obs";
        return "speaker";
    }

    private static void DrawIcon(DrawingContext dc, Rect bounds, string kind, Color color)
    {
        var s = Math.Max(1, Math.Min(bounds.Width, bounds.Height));
        var ox = bounds.Left + (bounds.Width - s) / 2;
        var oy = bounds.Top + (bounds.Height - s) / 2;
        Point P(double x, double y) => new(ox + x * s, oy + y * s);
        var brush = new SolidColorBrush(color);
        var pen = new Pen(brush, Math.Max(1, s * 0.075)) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };

        switch (kind)
        {
            case "mic":
                dc.DrawRoundedRectangle(null, pen, new Rect(P(.35, .12), P(.65, .58)), s * .15, s * .15);
                dc.DrawLine(pen, P(.22, .45), P(.22, .52));
                dc.DrawArcSafe(pen, P(.22, .48), P(.78, .48), P(.50, .76));
                dc.DrawLine(pen, P(.50, .76), P(.50, .90));
                dc.DrawLine(pen, P(.34, .90), P(.66, .90));
                break;
            case "desktop":
            case "obs":
                dc.DrawRoundedRectangle(null, pen, new Rect(P(.12, .18), P(.88, .68)), s * .05, s * .05);
                dc.DrawLine(pen, P(.50, .68), P(.50, .84));
                dc.DrawLine(pen, P(.32, .84), P(.68, .84));
                if (kind == "obs") dc.DrawEllipse(brush, null, P(.50, .43), s * .10, s * .10);
                break;
            case "music":
                dc.DrawLine(pen, P(.58, .18), P(.58, .70));
                dc.DrawLine(pen, P(.58, .18), P(.82, .12));
                dc.DrawLine(pen, P(.82, .12), P(.82, .62));
                dc.DrawEllipse(null, pen, P(.43, .73), s * .15, s * .11);
                dc.DrawEllipse(null, pen, P(.67, .65), s * .15, s * .11);
                break;
            case "globe":
            case "network":
                dc.DrawEllipse(null, pen, P(.50, .50), s * .36, s * .36);
                dc.DrawEllipse(null, pen, P(.50, .50), s * .16, s * .36);
                dc.DrawLine(pen, P(.16, .50), P(.84, .50));
                if (kind == "network")
                {
                    dc.DrawLine(pen, P(.18, .25), P(.82, .75));
                    dc.DrawEllipse(brush, null, P(.18, .25), s * .07, s * .07);
                    dc.DrawEllipse(brush, null, P(.82, .75), s * .07, s * .07);
                }
                break;
            case "chat":
                dc.DrawRoundedRectangle(null, pen, new Rect(P(.12, .20), P(.88, .70)), s * .09, s * .09);
                dc.DrawLine(pen, P(.35, .70), P(.26, .88));
                dc.DrawLine(pen, P(.26, .88), P(.52, .70));
                break;
            default:
                var geometry = new StreamGeometry();
                using (var ctx = geometry.Open())
                {
                    ctx.BeginFigure(P(.14, .40), true, true);
                    ctx.LineTo(P(.34, .40), true, false);
                    ctx.LineTo(P(.58, .20), true, false);
                    ctx.LineTo(P(.58, .80), true, false);
                    ctx.LineTo(P(.34, .60), true, false);
                    ctx.LineTo(P(.14, .60), true, false);
                }
                geometry.Freeze();
                dc.DrawGeometry(null, pen, geometry);
                dc.DrawArcSafe(pen, P(.62, .35), P(.84, .65), P(.78, .50));
                break;
        }
    }

    private static void DrawArcSafe(this DrawingContext dc, Pen pen, Point start, Point end, Point through)
    {
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(start, false, false);
            var radius = new Size(Math.Abs(end.X - start.X) / 2 + Math.Abs(through.X - (start.X + end.X) / 2), Math.Abs(through.Y - start.Y) + 1);
            ctx.ArcTo(end, radius, 0, false, SweepDirection.Clockwise, true, false);
        }
        geometry.Freeze();
        dc.DrawGeometry(null, pen, geometry);
    }

    private static T? FindNamed<T>(DependencyObject root, string name) where T : FrameworkElement =>
        Descendants<T>(root).FirstOrDefault(x => string.Equals(x.Name, name, StringComparison.Ordinal));

    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
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
                for (var i = 0; i < VisualTreeHelper.GetChildrenCount(current); i++)
                    pending.Push(VisualTreeHelper.GetChild(current, i));
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

    private static string TextOf(object? content)
    {
        if (content is null) return string.Empty;
        if (content is string text) return text;
        if (content is TextBlock block) return block.Text ?? string.Empty;
        if (content is DependencyObject dependency)
            return string.Join(" ", Descendants<TextBlock>(dependency).Select(x => x.Text).Where(x => !string.IsNullOrWhiteSpace(x)));
        return content.ToString() ?? string.Empty;
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

    private readonly record struct MeterSnapshot(double Meter, double Db);
}