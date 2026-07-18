namespace TiHiY.StreamControlCenter.Services;

/// <summary>
/// Compatibility TextBlock used by the code-built STALKER poster.
/// WPF TextBlock has no CharacterSpacing property, so this property is retained
/// as harmless metadata while the visual spacing is provided by the poster font.
/// </summary>
internal class TextBlock : System.Windows.Controls.TextBlock
{
    public int CharacterSpacing { get; set; }
}
