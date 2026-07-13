namespace TiHiY.StreamControlCenter.Services;

public sealed class UiScaleService
{
    private readonly AppSettingsAccessor _settings;
    public event EventHandler? ScaleChanged;

    public UiScaleService(AppSettingsAccessor settings) => _settings = settings;
    public bool Auto { get => _settings.Value.UiScaleAuto; set { _settings.Value.UiScaleAuto = value; Raise(); } }
    public int Percent { get => _settings.Value.UiScalePercent; set { _settings.Value.UiScalePercent = Math.Clamp(value, 60, 150); Raise(); } }

    public void Increase() { Auto = false; Percent += 5; }
    public void Decrease() { Auto = false; Percent -= 5; }
    public void Reset() { Auto = true; Percent = 100; Raise(); }

    public int Apply(FrameworkElement designSurface, Window host, double baseWidth, double baseHeight)
    {
        var availableWidth = Math.Max(1, host.ActualWidth - 24);
        var availableHeight = Math.Max(1, host.ActualHeight - 24);
        var autoFactor = Math.Min(availableWidth / Math.Max(1, baseWidth), availableHeight / Math.Max(1, baseHeight));
        var factor = Auto ? Math.Clamp(autoFactor, 0.60, 1.30) : Percent / 100.0;
        var appliedPercent = (int)Math.Round(factor * 100);

        designSurface.Width = double.NaN;
        designSurface.Height = double.NaN;
        designSurface.HorizontalAlignment = HorizontalAlignment.Stretch;
        designSurface.VerticalAlignment = VerticalAlignment.Stretch;
        designSurface.LayoutTransform = Math.Abs(factor - 1.0) < 0.005
            ? Transform.Identity
            : new ScaleTransform(factor, factor);
        return appliedPercent;
    }

    private void Raise() => ScaleChanged?.Invoke(this, EventArgs.Empty);
}
