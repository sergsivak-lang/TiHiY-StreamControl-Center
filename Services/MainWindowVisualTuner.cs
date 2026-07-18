using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media.Imaging;
using TiHiY.StreamControlCenter.Models;

namespace TiHiY.StreamControlCenter.Services;

public static class MainWindowVisualTuner
{
    private sealed class Controller : IDisposable
    {
        private const string CenterBlockKey = "UkraineCenterBlock";
        private readonly MainWindow _window;
        private readonly DispatcherTimer _guardTimer;
        private readonly Dictionary<string, BlockHost> _hosts = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<AudioChannel, PropertyChangedEventHandler> _channelHandlers = new();
        private Canvas? _canvas;
        private Grid? _designSurface;
        private bool _freeformBuilt;
        private bool _editMode;
        private bool _applyingBounds;
        private bool _autoMixerRepairBusy;
        private int _topZ = 20;
        private bool _disposed;

        private static readonly string[] DashboardNames =
        {
            "ChatBlockPanel",
            "DonationsBlockPanel",
            "MixerBlockPanel",
            "NotificationsBlockPanel",
            "SystemStatusBlockPanel",
            "SystemMonitorPanel"
        };

        private static readonly Dictionary<string, Rect> DefaultBounds = new(StringComparer.OrdinalIgnoreCase)
        {
            ["ChatBlockPanel"] = new Rect(0.000, 0.000, 0.497, 0.438),
            ["DonationsBlockPanel"] = new Rect(0.503, 0.000, 0.497, 0.438),
            ["MixerBlockPanel"] = new Rect(0.000, 0.447, 0.497, 0.305),
            ["NotificationsBlockPanel"] = new Rect(0.503, 0.447, 0.497, 0.305),
            ["SystemStatusBlockPanel"] = new Rect(0.000, 0.761, 0.370, 0.239),
            [CenterBlockKey] = new Rect(0.376, 0.761, 0.248, 0.239),
            ["SystemMonitorPanel"] = new Rect(0.630, 0.761, 0.370, 0.239)
        };

        public Controller(MainWindow window)
        {
            _window = window;
            _guardTimer = new DispatcherTimer(DispatcherPriority.Background, window.Dispatcher)
            {
                Interval = TimeSpan.FromMilliseconds(250)
            };
            _guardTimer.Tick += GuardTimer_Tick;
            _window.Loaded += Window_Loaded;
            _window.SizeChanged += Window_SizeChanged;
            _window.Closed += Window_Closed;

            if (_window.IsLoaded)
                _window.Dispatcher.BeginInvoke(new Action(ApplyNow), DispatcherPriority.Loaded);
            _guardTimer.Start();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e) =>
            _window.Dispatcher.BeginInvoke(new Action(ApplyNow), DispatcherPriority.Loaded);

        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (_canvas is not null && !_applyingBounds)
                _window.Dispatcher.BeginInvoke(new Action(ApplySavedBounds), DispatcherPriority.Background);
        }

        private void Window_Closed(object? sender, EventArgs e) => Dispose();

        private async void GuardTimer_Tick(object? sender, EventArgs e)
        {
            if (_disposed) return;
            ApplyVisualCorrections();
            UpdateMuteVisuals();
            UpdateObsConnectionCaption();
            await TryRepairQuickMixerAsync();
        }

        public void ApplyNow()
        {
            if (_disposed) return;
            BuildFreeformDashboard();
            ApplyVisualCorrections();
            UpdateMuteVisuals();
            UpdateObsConnectionCaption();
        }

        private void BuildFreeformDashboard()
        {
            if (_freeformBuilt || !_window.IsLoaded) return;

            _designSurface = FindNamed<Grid>("DesignSurface");
            var dashboardGrid = FindNamed<Grid>("DashboardBlocksGrid");
            var footerGrid = FindNamed<Grid>("FooterBlocksGrid");
            if (_designSurface is null || dashboardGrid is null || footerGrid is null) return;

            var blocks = new Dictionary<string, ContentControl>(StringComparer.OrdinalIgnoreCase);
            foreach (var name in DashboardNames)
                if (FindNamed<ContentControl>(name) is { } block)
                    blocks[name] = block;

            var center = footerGrid.Children.OfType<ContentControl>()
                .FirstOrDefault(x => Grid.GetColumn(x) == 2 && string.IsNullOrWhiteSpace(x.Name));
            if (center is not null)
                blocks[CenterBlockKey] = center;

            if (blocks.Count < 7) return;

            _canvas = new Canvas
            {
                Name = "FreeformDashboardCanvas",
                Background = Brushes.Transparent,
                ClipToBounds = true,
                Margin = new Thickness(0, 0, 0, 0),
                Focusable = true
            };
            Grid.SetRow(_canvas, 2);
            Grid.SetRowSpan(_canvas, 3);
            Panel.SetZIndex(_canvas, 30);
            _designSurface.Children.Add(_canvas);

            foreach (var pair in blocks)
            {
                Detach(pair.Value);
                var host = CreateBlockHost(pair.Key, pair.Value);
                _hosts[pair.Key] = host;
                _canvas.Children.Add(host.Root);
            }

            dashboardGrid.Visibility = Visibility.Collapsed;
            footerGrid.Visibility = Visibility.Collapsed;

            if (FindNamed<Button>("LayoutEditButton") is { } editButton)
            {
                editButton.Click += LayoutEditButton_Click;
                editButton.ToolTip = "Редагування як в OBS: переміщення, довільна ширина та висота кожного блока";
            }

            _canvas.SizeChanged += Canvas_SizeChanged;
            _freeformBuilt = true;
            _window.Dispatcher.BeginInvoke(new Action(ApplySavedBounds), DispatcherPriority.Render);
        }

        private BlockHost CreateBlockHost(string key, ContentControl block)
        {
            block.Margin = new Thickness(0);
            block.HorizontalAlignment = HorizontalAlignment.Stretch;
            block.VerticalAlignment = VerticalAlignment.Stretch;
            block.Visibility = Visibility.Visible;
            block.AllowDrop = false;

            var root = new Grid
            {
                MinWidth = 230,
                MinHeight = 120,
                ClipToBounds = false,
                Tag = key
            };
            root.Children.Add(block);

            var selection = new Border
            {
                BorderBrush = new SolidColorBrush(Color.FromRgb(255, 184, 0)),
                BorderThickness = new Thickness(2),
                CornerRadius = new CornerRadius(7),
                Background = Brushes.Transparent,
                IsHitTestVisible = false,
                Visibility = Visibility.Collapsed
            };
            root.Children.Add(selection);

            var move = new Thumb
            {
                Height = 36,
                VerticalAlignment = VerticalAlignment.Top,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Cursor = Cursors.SizeAll,
                Background = new SolidColorBrush(Color.FromArgb(1, 255, 255, 255)),
                BorderThickness = new Thickness(0),
                Visibility = Visibility.Collapsed,
                ToolTip = "Перемістити блок"
            };
            move.DragStarted += (_, _) => BringToFront(key);
            move.DragDelta += (_, e) => MoveBlock(root, e.HorizontalChange, e.VerticalChange);
            move.DragCompleted += (_, _) => SaveBounds();
            root.Children.Add(move);

            var resize = new Thumb
            {
                Width = 22,
                Height = 22,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Bottom,
                Cursor = Cursors.SizeNWSE,
                Background = new SolidColorBrush(Color.FromArgb(180, 245, 169, 0)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(255, 210, 74)),
                BorderThickness = new Thickness(1),
                Visibility = Visibility.Collapsed,
                ToolTip = "Змінити ширину та висоту"
            };
            resize.DragStarted += (_, _) => BringToFront(key);
            resize.DragDelta += (_, e) => ResizeBlock(root, e.HorizontalChange, e.VerticalChange);
            resize.DragCompleted += (_, _) => SaveBounds();
            root.Children.Add(resize);

            root.PreviewMouseLeftButtonDown += (_, _) => BringToFront(key);
            return new BlockHost(key, root, block, move, resize, selection);
        }

        private void LayoutEditButton_Click(object sender, RoutedEventArgs e)
        {
            _window.Dispatcher.BeginInvoke(new Action(() =>
            {
                var text = (sender as Button)?.Content?.ToString() ?? string.Empty;
                _editMode = text.Contains("РЕДАГУВАННЯ", StringComparison.OrdinalIgnoreCase);
                foreach (var host in _hosts.Values)
                {
                    host.MoveHandle.Visibility = _editMode ? Visibility.Visible : Visibility.Collapsed;
                    host.ResizeHandle.Visibility = _editMode ? Visibility.Visible : Visibility.Collapsed;
                    host.Selection.Visibility = _editMode ? Visibility.Visible : Visibility.Collapsed;
                }
            }), DispatcherPriority.Background);
        }

        private void Canvas_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (!_applyingBounds)
                ApplySavedBounds();
        }

        private void MoveBlock(FrameworkElement root, double dx, double dy)
        {
            if (!_editMode || _canvas is null) return;
            var left = SafeCanvasValue(Canvas.GetLeft(root));
            var top = SafeCanvasValue(Canvas.GetTop(root));
            Canvas.SetLeft(root, Math.Clamp(left + dx, 0, Math.Max(0, _canvas.ActualWidth - root.ActualWidth)));
            Canvas.SetTop(root, Math.Clamp(top + dy, 0, Math.Max(0, _canvas.ActualHeight - root.ActualHeight)));
        }

        private void ResizeBlock(FrameworkElement root, double dx, double dy)
        {
            if (!_editMode || _canvas is null) return;
            var left = SafeCanvasValue(Canvas.GetLeft(root));
            var top = SafeCanvasValue(Canvas.GetTop(root));
            var maxWidth = Math.Max(root.MinWidth, _canvas.ActualWidth - left);
            var maxHeight = Math.Max(root.MinHeight, _canvas.ActualHeight - top);
            root.Width = Math.Clamp(Math.Max(root.ActualWidth, root.MinWidth) + dx, root.MinWidth, maxWidth);
            root.Height = Math.Clamp(Math.Max(root.ActualHeight, root.MinHeight) + dy, root.MinHeight, maxHeight);
        }

        private void BringToFront(string key)
        {
            if (!_hosts.TryGetValue(key, out var host)) return;
            Panel.SetZIndex(host.Root, ++_topZ);
        }

        private void ApplySavedBounds()
        {
            if (_canvas is null || _canvas.ActualWidth < 100 || _canvas.ActualHeight < 100) return;
            _applyingBounds = true;
            try
            {
                var saved = App.Services.Settings.Value.DashboardFreeformBounds;
                foreach (var pair in _hosts)
                {
                    var normalized = TryParseBounds(saved.TryGetValue(pair.Key, out var text) ? text : null)
                        ?? DefaultBounds[pair.Key];
                    var rect = ClampNormalized(normalized);
                    var root = pair.Value.Root;
                    root.Width = Math.Max(root.MinWidth, rect.Width * _canvas.ActualWidth);
                    root.Height = Math.Max(root.MinHeight, rect.Height * _canvas.ActualHeight);
                    Canvas.SetLeft(root, rect.X * _canvas.ActualWidth);
                    Canvas.SetTop(root, rect.Y * _canvas.ActualHeight);
                    if (saved.TryGetValue(pair.Key, out var savedText))
                    {
                        var parts = savedText.Split(';');
                        if (parts.Length >= 5 && int.TryParse(parts[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out var z))
                        {
                            Panel.SetZIndex(root, z);
                            _topZ = Math.Max(_topZ, z);
                        }
                    }
                }
            }
            finally
            {
                _applyingBounds = false;
            }
        }

        private void SaveBounds()
        {
            if (_canvas is null || _canvas.ActualWidth <= 0 || _canvas.ActualHeight <= 0) return;
            var target = App.Services.Settings.Value.DashboardFreeformBounds;
            foreach (var pair in _hosts)
            {
                var root = pair.Value.Root;
                var x = Math.Clamp(SafeCanvasValue(Canvas.GetLeft(root)) / _canvas.ActualWidth, 0, 1);
                var y = Math.Clamp(SafeCanvasValue(Canvas.GetTop(root)) / _canvas.ActualHeight, 0, 1);
                var w = Math.Clamp(root.ActualWidth / _canvas.ActualWidth, 0.08, 1);
                var h = Math.Clamp(root.ActualHeight / _canvas.ActualHeight, 0.08, 1);
                target[pair.Key] = string.Create(CultureInfo.InvariantCulture, $"{x:0.######};{y:0.######};{w:0.######};{h:0.######};{Panel.GetZIndex(root)}");
            }
            try { App.Services.Save(); } catch { }
        }

        private static Rect? TryParseBounds(string? text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            var p = text.Split(';');
            if (p.Length < 4) return null;
            if (!double.TryParse(p[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var x) ||
                !double.TryParse(p[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var y) ||
                !double.TryParse(p[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var w) ||
                !double.TryParse(p[3], NumberStyles.Float, CultureInfo.InvariantCulture, out var h)) return null;
            return new Rect(x, y, w, h);
        }

        private static Rect ClampNormalized(Rect value)
        {
            var w = Math.Clamp(value.Width, 0.08, 1);
            var h = Math.Clamp(value.Height, 0.08, 1);
            var x = Math.Clamp(value.X, 0, Math.Max(0, 1 - w));
            var y = Math.Clamp(value.Y, 0, Math.Max(0, 1 - h));
            return new Rect(x, y, w, h);
        }

        private void ApplyVisualCorrections()
        {
            ShiftBlockContentAwayFromOrnaments();
            BuildSeparateCounters();
            ArrangeMultichatButtons();
            ConfigureMixerMeters();
            ConfigureAidaCard();
            ConfigureCenterTexture();
            UpdateEmptyStates();
        }

        private void ShiftBlockContentAwayFromOrnaments()
        {
            foreach (var name in DashboardNames)
            {
                if (FindNamed<ContentControl>(name) is not { } block) continue;
                var left = name == "SystemStatusBlockPanel" ? 46 : 38;
                block.Padding = new Thickness(Math.Max(block.Padding.Left, left), block.Padding.Top, block.Padding.Right, block.Padding.Bottom);
            }

            foreach (var title in FindDescendants<TextBlock>(_window).Where(x =>
                         x.Text is "МУЛЬТИЧАТ • TWITCH + YOUTUBE" or "ДОНАТИ" or
                             "ШВИДКИЙ МІКШЕР • AUDIO MIXER OBS" or "СПОВІЩЕННЯ" or "СТАН СИСТЕМИ"))
                title.Margin = new Thickness(Math.Max(title.Margin.Left, 8), title.Margin.Top, title.Margin.Right, title.Margin.Bottom);
        }

        private void BuildSeparateCounters()
        {
            var twitch = FindNamed<TextBlock>("TwitchViewerText");
            var youtube = FindNamed<TextBlock>("YouTubeViewerText");
            var likes = FindNamed<TextBlock>("YouTubeLikesText");
            if (twitch is null || youtube is null || likes is null) return;
            if (twitch.Tag as string == "SeparatedCounters") return;

            var rightPanel = FindAncestor<StackPanel>(FindAncestor<Border>(twitch)!)?.Parent as StackPanel;
            if (rightPanel is null) return;

            Detach(twitch);
            Detach(youtube);
            Detach(likes);
            rightPanel.Children.Clear();
            rightPanel.Orientation = Orientation.Horizontal;
            rightPanel.Children.Add(CreateCounter("/TiHiY.StreamControlCenter;component/Assets/Platforms/twitch.png", twitch, "#8F4FE2", "#2D1742"));
            rightPanel.Children.Add(CreateCounter("/TiHiY.StreamControlCenter;component/Assets/Platforms/youtube.png", youtube, "#D33942", "#351219"));
            rightPanel.Children.Add(CreateLikeCounter(likes));
            twitch.Tag = "SeparatedCounters";
        }

        private static Border CreateCounter(string icon, TextBlock value, string borderColor, string background)
        {
            value.FontSize = 17;
            value.FontWeight = FontWeights.SemiBold;
            value.Margin = new Thickness(8, 0, 4, 0);
            var panel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            panel.Children.Add(new Image { Source = new BitmapImage(new Uri(icon, UriKind.RelativeOrAbsolute)), Width = 21, Height = 21 });
            panel.Children.Add(value);
            return new Border
            {
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(background)),
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(borderColor)),
                BorderThickness = new Thickness(1.2),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(9, 4, 9, 4),
                Margin = new Thickness(3),
                Child = panel
            };
        }

        private static Border CreateLikeCounter(TextBlock value)
        {
            value.FontSize = 17;
            value.FontWeight = FontWeights.SemiBold;
            value.Margin = new Thickness(6, 0, 2, 0);
            var panel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            panel.Children.Add(new TextBlock
            {
                Text = "♥",
                Foreground = new SolidColorBrush(Color.FromRgb(255, 55, 66)),
                FontSize = 21,
                FontWeight = FontWeights.Black,
                VerticalAlignment = VerticalAlignment.Center
            });
            panel.Children.Add(value);
            return new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(30, 10, 18)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(146, 36, 48)),
                BorderThickness = new Thickness(1.2),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(9, 4, 9, 4),
                Margin = new Thickness(3),
                Child = panel
            };
        }

        private void ArrangeMultichatButtons()
        {
            var twitch = FindNamed<Button>("SendTwitchButton");
            var youtube = FindNamed<Button>("SendYouTubeButton");
            var both = FindNamed<Button>("SendBothButton");
            var input = FindNamed<TextBox>("ChatInput");
            if (twitch?.Parent is not Grid grid || youtube is null || both is null || input is null) return;

            var send = grid.Children.OfType<Button>().FirstOrDefault(x => Equals(x.Content, "➤"));
            if (send is null || grid.Tag as string == "ChatButtonsArranged") return;

            grid.ColumnDefinitions.Clear();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.62, GridUnitType.Star), MinWidth = 108 });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.62, GridUnitType.Star), MinWidth = 108 });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.52, GridUnitType.Star), MinWidth = 92 });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(48) });
            Grid.SetColumn(input, 0);
            Grid.SetColumn(twitch, 1);
            Grid.SetColumn(youtube, 2);
            Grid.SetColumn(both, 3);
            Grid.SetColumn(send, 4);
            send.FontSize = 19;
            send.Foreground = new SolidColorBrush(Color.FromRgb(245, 169, 0));
            grid.Tag = "ChatButtonsArranged";
        }

        private void ConfigureMixerMeters()
        {
            foreach (var slider in FindDescendants<Slider>(_window).Where(x => x.Tag is AudioChannel))
            {
                slider.Visibility = Visibility.Collapsed;
                slider.IsHitTestVisible = false;
            }

            foreach (var progress in FindDescendants<ProgressBar>(_window).Where(x => x.DataContext is AudioChannel))
            {
                progress.Minimum = 0;
                progress.Maximum = 1;
                progress.Height = 12;
                progress.Margin = new Thickness(5, 0, 10, 0);
                progress.VerticalAlignment = VerticalAlignment.Center;
                progress.Background = new SolidColorBrush(Color.FromRgb(1, 8, 15));
                progress.BorderBrush = new SolidColorBrush(Color.FromRgb(40, 90, 120));
                progress.BorderThickness = new Thickness(1);
                progress.Foreground = CreateMeterBrush();
            }

            foreach (var channel in _window.QuickAudioPage)
                HookChannel(channel);
        }

        private static LinearGradientBrush CreateMeterBrush()
        {
            var brush = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 0) };
            brush.GradientStops.Add(new GradientStop(Color.FromRgb(23, 215, 102), 0));
            brush.GradientStops.Add(new GradientStop(Color.FromRgb(174, 225, 57), 0.62));
            brush.GradientStops.Add(new GradientStop(Color.FromRgb(245, 176, 0), 0.82));
            brush.GradientStops.Add(new GradientStop(Color.FromRgb(255, 76, 88), 1));
            return brush;
        }

        private void HookChannel(AudioChannel channel)
        {
            if (_channelHandlers.ContainsKey(channel)) return;
            PropertyChangedEventHandler handler = (_, e) =>
            {
                if (e.PropertyName == nameof(AudioChannel.IsMuted))
                    _window.Dispatcher.BeginInvoke(new Action(UpdateMuteVisuals), DispatcherPriority.Background);
            };
            channel.PropertyChanged += handler;
            _channelHandlers[channel] = handler;
        }

        private void UpdateMuteVisuals()
        {
            foreach (var button in FindDescendants<Button>(_window).Where(x => x.Tag is AudioChannel))
            {
                if (button.Tag is not AudioChannel channel) continue;
                HookChannel(channel);
                if (channel.IsMuted)
                {
                    button.Background = new SolidColorBrush(Color.FromRgb(92, 21, 26));
                    button.BorderBrush = new SolidColorBrush(Color.FromRgb(255, 57, 70));
                    button.Foreground = Brushes.White;
                    button.ToolTip = "MUTE: ON";
                }
                else
                {
                    button.SetResourceReference(Control.BackgroundProperty, "ButtonGradient");
                    button.SetResourceReference(Control.BorderBrushProperty, "Line");
                    button.Foreground = Brushes.White;
                    button.ToolTip = "MUTE";
                }
            }
        }

        private void ConfigureAidaCard()
        {
            if (FindNamed<TextBlock>("AidaStatusText") is { } header)
            {
                header.Text = "AIDA64 LIVE";
                header.FontSize = 18;
                header.FontWeight = FontWeights.Black;
                header.Foreground = new SolidColorBrush(Color.FromRgb(255, 190, 20));
                header.Margin = new Thickness(8, 0, 0, 0);
            }

            foreach (var name in new[] { "CpuTemperatureMonitorText", "GpuTemperatureMonitorText", "GpuLoadMonitorText", "ObsFpsText" })
            {
                if (FindNamed<TextBlock>(name) is not { } value) continue;
                value.FontFamily = new FontFamily("Consolas");
                value.FontSize = name == "ObsFpsText" ? 21 : 25;
                value.FontWeight = FontWeights.Black;
                value.Foreground = new SolidColorBrush(Color.FromRgb(255, 190, 20));
                value.TextAlignment = TextAlignment.Center;
                if (FindAncestor<Border>(value) is { } card)
                {
                    card.Width = double.NaN;
                    card.Height = 70;
                    card.MinWidth = 70;
                    card.CornerRadius = new CornerRadius(4);
                    card.BorderThickness = new Thickness(1.2);
                    card.BorderBrush = new SolidColorBrush(Color.FromRgb(224, 156, 0));
                    card.Background = new SolidColorBrush(Color.FromArgb(205, 5, 20, 36));
                    card.Margin = new Thickness(4, 2, 4, 3);
                }
                if (value.Parent is StackPanel stack)
                {
                    var label = stack.Children.OfType<TextBlock>().FirstOrDefault(x => !ReferenceEquals(x, value));
                    if (label is not null)
                    {
                        label.FontSize = 13;
                        label.FontWeight = FontWeights.Black;
                        label.Foreground = new SolidColorBrush(Color.FromRgb(255, 190, 20));
                    }
                }
            }
        }

        private void ConfigureCenterTexture()
        {
            if (!_hosts.TryGetValue(CenterBlockKey, out var host)) return;
            if (host.Content.Tag as string == "ExactCenterTexture") return;

            try
            {
                var uri = new Uri("/TiHiY.StreamControlCenter;component/Assets/Themes/UkraineExact/central-glory.png", UriKind.Relative);
                var image = new Image
                {
                    Source = new BitmapImage(uri),
                    Stretch = Stretch.UniformToFill,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Stretch,
                    SnapsToDevicePixels = true
                };
                host.Content.Padding = new Thickness(0);
                host.Content.Content = image;
                host.Content.Tag = "ExactCenterTexture";
            }
            catch
            {
                // Asset is generated into the branch together with this feature. Keep the
                // original content only if a developer is running an incomplete checkout.
            }
        }

        private void UpdateObsConnectionCaption()
        {
            var connected = App.Services.Obs.IsConnected;
            foreach (var text in FindDescendants<TextBlock>(_window).Where(x =>
                         string.Equals(x.Text, "OBS підключено", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(x.Text, "OBS не підключено", StringComparison.OrdinalIgnoreCase)))
            {
                text.Text = connected ? "OBS підключено" : "OBS не підключено";
                text.Foreground = new SolidColorBrush(connected ? Color.FromRgb(23, 215, 102) : Color.FromRgb(255, 76, 88));
                if (text.Parent is StackPanel panel)
                    foreach (var ellipse in panel.Children.OfType<Ellipse>())
                        ellipse.Fill = text.Foreground;
            }
        }

        private async Task TryRepairQuickMixerAsync()
        {
            if (_autoMixerRepairBusy || !App.Services.Obs.IsConnected || _window.QuickAudioPage.Count > 0 ||
                !App.Services.Settings.Value.AudioAutoDetect) return;
            _autoMixerRepairBusy = true;
            try
            {
                var inputs = (await App.Services.Obs.GetPrimaryMixerInputsAsync()).ToList();
                if (inputs.Count == 0) inputs = (await App.Services.Obs.GetMixerInputsAsync()).ToList();
                if (inputs.Count == 0) return;

                var settings = App.Services.Settings.Value;
                settings.SelectedAudioInputs = inputs.Select(x => x.name).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                App.Services.Save();
                var refresh = typeof(MainWindow).GetMethod("RefreshQuickAudioSafeAsync", BindingFlags.Instance | BindingFlags.NonPublic);
                if (refresh?.Invoke(_window, null) is Task task) await task;
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
            if (FindNamed<TextBlock>("ChatEmptyStateText") is { } chat)
                chat.Visibility = _window.MainChatMessages.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            if (FindNamed<TextBlock>("AudioEmptyStateText") is { } audio)
                audio.Visibility = _window.QuickAudioPage.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            if (FindNamed<TextBlock>("RecentDonationsEmptyText") is { } donations)
                donations.Visibility = _window.RecentDonations.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            if (FindNamed<TextBlock>("DonationEmptyStateText") is { } notices)
                notices.Visibility = _window.DonationPage.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
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
            if (_canvas is not null) _canvas.SizeChanged -= Canvas_SizeChanged;
            foreach (var pair in _channelHandlers)
                pair.Key.PropertyChanged -= pair.Value;
            _channelHandlers.Clear();
        }

        private sealed record BlockHost(
            string Key,
            Grid Root,
            ContentControl Content,
            Thumb MoveHandle,
            Thumb ResizeHandle,
            Border Selection);
    }

    private static readonly ConditionalWeakTable<MainWindow, Controller> Controllers = new();

    public static IDisposable Attach(MainWindow window)
    {
        if (Controllers.TryGetValue(window, out var existing)) return existing;
        var controller = new Controller(window);
        Controllers.Add(window, controller);
        return controller;
    }

    public static void ApplyNow(MainWindow window)
    {
        if (Controllers.TryGetValue(window, out var controller)) controller.ApplyNow();
        else _ = Attach(window);
    }

    private static double SafeCanvasValue(double value) => double.IsNaN(value) || double.IsInfinity(value) ? 0 : value;

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
                for (var index = 0; index < count; index++) pending.Push(VisualTreeHelper.GetChild(current, index));
            }
            catch { }

            try
            {
                foreach (var child in LogicalTreeHelper.GetChildren(current).OfType<DependencyObject>()) pending.Push(child);
            }
            catch { }
        }
    }

    private static T? FindAncestor<T>(DependencyObject? source) where T : DependencyObject
    {
        for (var current = source; current is not null; current = GetParent(current))
            if (current is T match) return match;
        return null;
    }

    private static DependencyObject? GetParent(DependencyObject current)
    {
        if (current is FrameworkElement element && element.Parent is not null) return element.Parent;
        if (current is FrameworkContentElement contentElement) return contentElement.Parent;
        try { return VisualTreeHelper.GetParent(current); }
        catch { return null; }
    }
}