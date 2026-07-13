namespace TiHiY.StreamControlCenter.Services;

/// <summary>
/// Shared visual metadata for the cyber UI. Using an attached property keeps
/// Button.Tag available for existing functional commands and data objects.
/// </summary>
public static class CyberUi
{
    public static readonly DependencyProperty IconGlyphProperty = DependencyProperty.RegisterAttached(
        "IconGlyph",
        typeof(string),
        typeof(CyberUi),
        new FrameworkPropertyMetadata(string.Empty));

    public static void SetIconGlyph(DependencyObject element, string value) => element.SetValue(IconGlyphProperty, value);
    public static string GetIconGlyph(DependencyObject element) => (string)element.GetValue(IconGlyphProperty);
}
