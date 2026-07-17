namespace TiHiY.StreamControlCenter.Services;

/// <summary>
/// Applies one geometric scale to the complete design surface of every application window.
/// The automatic mode never enlarges the approved layout above 100%; spare space is consumed
/// by responsive star-sized rows and columns instead of stretching or clipping the interface.
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

        // Never auto-enlarge the approved reference layout. On Full HD and larger screens
        // the responsive Grid receives the spare logical width/height and fills the window.
        var requestedFactor = Percent / 100.0;
        var factor = Auto
            ? Math.Min(1.0, fitFactor)
            : Math.Min(fitFactor, requestedFactor);
        factor = Math.Clamp(factor, 0.32, 1.0);

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
