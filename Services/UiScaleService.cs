namespace TiHiY.StreamControlCenter.Services;

/// <summary>
/// Applies one geometric scale to the complete design surface of every application window.
/// Fonts, icons, buttons, lists, sliders and all child controls therefore scale together.
/// The logical design surface also expands in the spare dimension, allowing responsive
/// star-sized rows and columns to fill the complete client area without top/bottom bars.
/// </summary>
public sealed class UiScaleService
{
    private readonly AppSettingsAccessor _settings;
    public event EventHandler? ScaleChanged;

    public UiScaleService(AppSettingsAccessor settings) => _settings = settings;

    public bool Auto
    {
        get => _settings.Value.UiScaleAuto;
        set
        {
            _settings.Value.UiScaleAuto = value;
            Raise();
        }
    }

    public int Percent
    {
        get => _settings.Value.UiScalePercent;
        set
        {
            _settings.Value.UiScalePercent = Math.Clamp(value, 60, 150);
            Raise();
        }
    }

    public void Increase()
    {
        Auto = false;
        Percent += 5;
    }

    public void Decrease()
    {
        Auto = false;
        Percent -= 5;
    }

    public void Reset()
    {
        Auto = true;
        Percent = 100;
        Raise();
    }

    public int Apply(FrameworkElement designSurface, Window host, double baseWidth, double baseHeight)
    {
        if (host.ActualWidth < 40 || host.ActualHeight < 40)
            return 100;

        var availableWidth = Math.Max(1, host.ActualWidth - 22);
        var availableHeight = Math.Max(1, host.ActualHeight - 22);
        var fitFactor = Math.Min(availableWidth / Math.Max(1, baseWidth),
                                 availableHeight / Math.Max(1, baseHeight));

        // Manual zoom may make the interface smaller, but never larger than the
        // current fit. This guarantees that resizing a window cannot clip controls.
        var requestedFactor = Percent / 100.0;
        var factor = Auto ? fitFactor : Math.Min(fitFactor, requestedFactor);
        factor = Math.Clamp(factor, 0.32, 1.55);

        // Preserve a uniform scale for text and icons, while giving the responsive
        // Grid extra logical width/height in the dimension that otherwise produced
        // letterboxing. Star-sized rows and columns consume that space naturally.
        designSurface.Width = Math.Max(baseWidth, availableWidth / factor);
        designSurface.Height = Math.Max(baseHeight, availableHeight / factor);
        designSurface.HorizontalAlignment = HorizontalAlignment.Center;
        designSurface.VerticalAlignment = VerticalAlignment.Center;
        designSurface.RenderTransform = Transform.Identity;
        designSurface.LayoutTransform = new ScaleTransform(factor, factor);

        return (int)Math.Round(factor * 100);
    }

    private void Raise() => ScaleChanged?.Invoke(this, EventArgs.Empty);
}
