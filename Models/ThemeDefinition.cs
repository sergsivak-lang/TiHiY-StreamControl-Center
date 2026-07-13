namespace TiHiY.StreamControlCenter.Models;

public sealed class ThemeDefinition
{
    public required string Id { get; init; }
    public required string DisplayName { get; init; }
    public required string Description { get; init; }
    public required string PrimaryHex { get; init; }
    public required string AccentHex { get; init; }
    public required IReadOnlyDictionary<string, string> Colors { get; init; }
}
