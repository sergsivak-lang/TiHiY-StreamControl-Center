namespace TiHiY.StreamControlCenter.Models;

public sealed class ChatEmote
{
    public string Platform { get; set; } = string.Empty;
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int Start { get; set; }
    public int End { get; set; }
    public string ImageUrl { get; set; } = string.Empty;

    public int Length => Math.Max(0, End - Start + 1);
}
