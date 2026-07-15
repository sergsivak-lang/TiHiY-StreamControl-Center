using System.Windows.Shapes;

namespace TiHiY.StreamControlCenter.Services;

/// <summary>
/// Keeps the inside of a dashboard block responsive while GridSplitter changes
/// the block width and/or height. Horizontal measurements follow width changes,
/// vertical measurements follow height changes, while fonts and icons keep their
/// aspect ratio through a separate uniform scale.
/// </summary>
public static class ResponsiveBlockService
{
    public static IDisposable Attach(FrameworkElement root, double minScale = 0.55, double maxScale = 1.20)
        => new Controller(root, minScale, maxScale);

    private sealed class Controller : IDisposable
    {
        private readonly FrameworkElement _root;
        private readonly double _minScale;
        private readonly double _maxScale;
        private readonly Dictionary<FrameworkElement, ElementBaseline> _elements = new();
        private readonly Dictionary<ColumnDefinition, GridLength> _columns = new();
        private readonly Dictionary<RowDefinition, GridLength> _rows = new();
        private bool _initialized;
        private bool _refreshQueued;
        private bool _disposed;
        private double _baseWidth;
        private double _baseHeight;
        private double _widthFactor = 1.0;
        private double _heightFactor = 1.0;
        private double _uniformFactor = 1.0;

        public Controller(FrameworkElement root, double minScale, double maxScale)
        {
            _root = root;
            _minScale = Math.Clamp(minScale, 0.30, 1.0);
            _maxScale = Math.Clamp(maxScale, 1.0, 1.8);

            _root.Loaded += Root_Loaded;
            _root.SizeChanged += Root_SizeChanged;
            _root.LayoutUpdated += Root_LayoutUpdated;

            if (_root.IsLoaded)
                _root.Dispatcher.BeginInvoke(new Action(Initialize), DispatcherPriority.Loaded);
        }

        private void Root_Loaded(object sender, RoutedEventArgs e) =>
            _root.Dispatcher.BeginInvoke(new Action(Initialize), DispatcherPriority.Loaded);

        private void Initialize()
        {
            if (_disposed || _initialized || _root.ActualWidth < 20 || _root.ActualHeight < 20)
                return;

            _baseWidth = _root.ActualWidth;
            _baseHeight = _root.ActualHeight;
            _initialized = true;
            CaptureNewVisuals(_root, includeRoot: false);
            ApplyScale();
        }

        private void Root_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (!_initialized)
            {
                Initialize();
                return;
            }

            ApplyScale();
        }

        private void Root_LayoutUpdated(object? sender, EventArgs e)
        {
            if (!_initialized || _disposed || _refreshQueued) return;
            _refreshQueued = true;
            _root.Dispatcher.BeginInvoke(new Action(() =>
            {
                _refreshQueued = false;
                if (_disposed) return;
                if (CaptureNewVisuals(_root, includeRoot: false))
                    ApplyScale();
            }), DispatcherPriority.ContextIdle);
        }

        private void ApplyScale()
        {
            if (!_initialized || _root.ActualWidth < 1 || _root.ActualHeight < 1) return;

            _widthFactor = Math.Clamp(_root.ActualWidth / Math.Max(1, _baseWidth), _minScale, _maxScale);
            _heightFactor = Math.Clamp(_root.ActualHeight / Math.Max(1, _baseHeight), _minScale, _maxScale);

            // Fonts and icons must not be distorted when the block changes only in
            // one direction. The geometric mean responds to both dimensions but
            // preserves a visually balanced, uniform scale.
            _uniformFactor = Math.Clamp(Math.Sqrt(_widthFactor * _heightFactor), _minScale, _maxScale);

            foreach (var pair in _elements)
                pair.Value.Apply(pair.Key, _widthFactor, _heightFactor, _uniformFactor);

            foreach (var pair in _columns)
            {
                if (pair.Value.IsAbsolute)
                    pair.Key.Width = new GridLength(Math.Max(1, pair.Value.Value * _widthFactor), GridUnitType.Pixel);
            }

            foreach (var pair in _rows)
            {
                if (pair.Value.IsAbsolute)
                    pair.Key.Height = new GridLength(Math.Max(1, pair.Value.Value * _heightFactor), GridUnitType.Pixel);
            }
        }

        private bool CaptureNewVisuals(DependencyObject parent, bool includeRoot)
        {
            var discovered = false;
            if (parent is FrameworkElement element && (includeRoot || !ReferenceEquals(element, _root)))
            {
                // Skip template chrome (scrollbar internals, button chrome, etc.) but keep
                // traversing so user-provided content inside templates is still discovered.
                if (element.TemplatedParent is null && !_elements.ContainsKey(element))
                {
                    _elements[element] = ElementBaseline.Capture(
                        element,
                        _widthFactor,
                        _heightFactor,
                        _uniformFactor);
                    discovered = true;
                }

                if (element is Grid grid && grid.TemplatedParent is null)
                {
                    foreach (var column in grid.ColumnDefinitions)
                    {
                        if (!_columns.ContainsKey(column))
                        {
                            _columns[column] = Unscale(column.Width, _widthFactor);
                            discovered = true;
                        }
                    }

                    foreach (var row in grid.RowDefinitions)
                    {
                        if (!_rows.ContainsKey(row))
                        {
                            _rows[row] = Unscale(row.Height, _heightFactor);
                            discovered = true;
                        }
                    }
                }
            }

            var count = VisualTreeHelper.GetChildrenCount(parent);
            for (var i = 0; i < count; i++)
                discovered |= CaptureNewVisuals(VisualTreeHelper.GetChild(parent, i), includeRoot: true);

            return discovered;
        }

        private static GridLength Unscale(GridLength value, double factor)
        {
            if (!value.IsAbsolute || factor <= 0.001) return value;
            return new GridLength(value.Value / factor, GridUnitType.Pixel);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _root.Loaded -= Root_Loaded;
            _root.SizeChanged -= Root_SizeChanged;
            _root.LayoutUpdated -= Root_LayoutUpdated;
            _elements.Clear();
            _columns.Clear();
            _rows.Clear();
        }
    }

    private sealed class ElementBaseline
    {
        public double FontSize { get; init; }
        public bool ScaleFont { get; init; }
        public double Width { get; init; }
        public bool ScaleWidth { get; init; }
        public double Height { get; init; }
        public bool ScaleHeight { get; init; }
        public bool PreserveAspectRatio { get; init; }
        public double MinWidth { get; init; }
        public double MinHeight { get; init; }
        public Thickness Margin { get; init; }
        public Thickness? Padding { get; init; }
        public CornerRadius? CornerRadius { get; init; }
        public double? StrokeThickness { get; init; }
        public double? LineHeight { get; init; }

        public static ElementBaseline Capture(
            FrameworkElement element,
            double currentWidthFactor,
            double currentHeightFactor,
            double currentUniformFactor)
        {
            currentWidthFactor = Math.Max(0.001, currentWidthFactor);
            currentHeightFactor = Math.Max(0.001, currentHeightFactor);
            currentUniformFactor = Math.Max(0.001, currentUniformFactor);

            var width = element.Width;
            var height = element.Height;
            var fontSize = ReadFontSize(element);
            var preserveAspect = element is Image || element is Shape ||
                                 (!double.IsNaN(width) && !double.IsNaN(height) &&
                                  width > 0 && height > 0 && Math.Abs(width - height) <= 2.0);

            Thickness? padding = element switch
            {
                Control control => control.Padding,
                Border border => border.Padding,
                _ => null
            };

            return new ElementBaseline
            {
                FontSize = fontSize / currentUniformFactor,
                ScaleFont = fontSize > 0 && !double.IsNaN(fontSize) && !double.IsInfinity(fontSize),
                Width = width / (preserveAspect ? currentUniformFactor : currentWidthFactor),
                ScaleWidth = !double.IsNaN(width) && !double.IsInfinity(width) && width > 0,
                Height = height / (preserveAspect ? currentUniformFactor : currentHeightFactor),
                ScaleHeight = !double.IsNaN(height) && !double.IsInfinity(height) && height > 0,
                PreserveAspectRatio = preserveAspect,
                MinWidth = element.MinWidth / currentWidthFactor,
                MinHeight = element.MinHeight / currentHeightFactor,
                Margin = Divide(element.Margin, currentWidthFactor, currentHeightFactor),
                Padding = padding is null ? null : Divide(padding.Value, currentWidthFactor, currentHeightFactor),
                CornerRadius = element is Border b ? Divide(b.CornerRadius, currentUniformFactor) : null,
                StrokeThickness = element is Shape shape ? shape.StrokeThickness / currentUniformFactor : null,
                LineHeight = element is TextBlock text && !double.IsNaN(text.LineHeight)
                    ? text.LineHeight / currentUniformFactor
                    : null
            };
        }

        public void Apply(FrameworkElement element, double widthFactor, double heightFactor, double uniformFactor)
        {
            var widthScale = PreserveAspectRatio ? uniformFactor : widthFactor;
            var heightScale = PreserveAspectRatio ? uniformFactor : heightFactor;

            if (ScaleWidth) element.Width = Math.Max(1, Width * widthScale);
            if (ScaleHeight) element.Height = Math.Max(1, Height * heightScale);

            element.MinWidth = Math.Max(0, MinWidth * widthFactor);
            element.MinHeight = Math.Max(0, MinHeight * heightFactor);
            element.Margin = Multiply(Margin, widthFactor, heightFactor);

            if (ScaleFont)
                element.SetValue(System.Windows.Documents.TextElement.FontSizeProperty, Math.Max(7, FontSize * uniformFactor));

            if (Padding is not null)
            {
                var scaled = Multiply(Padding.Value, widthFactor, heightFactor);
                if (element is Control control) control.Padding = scaled;
                else if (element is Border border) border.Padding = scaled;
            }

            if (CornerRadius is not null && element is Border rounded)
                rounded.CornerRadius = Multiply(CornerRadius.Value, uniformFactor);

            if (StrokeThickness is not null && element is Shape shape)
                shape.StrokeThickness = Math.Max(0.5, StrokeThickness.Value * uniformFactor);

            if (LineHeight is not null && element is TextBlock text)
                text.LineHeight = Math.Max(1, LineHeight.Value * uniformFactor);
        }

        private static double ReadFontSize(FrameworkElement element) => element switch
        {
            TextBlock text => text.FontSize,
            Control control => control.FontSize,
            _ => 0
        };

        private static Thickness Divide(Thickness value, double widthFactor, double heightFactor) =>
            new(value.Left / widthFactor, value.Top / heightFactor,
                value.Right / widthFactor, value.Bottom / heightFactor);

        private static Thickness Multiply(Thickness value, double widthFactor, double heightFactor) =>
            new(value.Left * widthFactor, value.Top * heightFactor,
                value.Right * widthFactor, value.Bottom * heightFactor);

        private static CornerRadius Divide(CornerRadius value, double factor) =>
            new(value.TopLeft / factor, value.TopRight / factor,
                value.BottomRight / factor, value.BottomLeft / factor);

        private static CornerRadius Multiply(CornerRadius value, double factor) =>
            new(value.TopLeft * factor, value.TopRight * factor,
                value.BottomRight * factor, value.BottomLeft * factor);
    }
}
