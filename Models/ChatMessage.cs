namespace TiHiY.StreamControlCenter.Models;

public sealed class ChatMessage
{
    public DateTime Time { get; set; } = DateTime.Now;
    public string Platform { get; set; } = "LOCAL";
    public string User { get; set; } = "TiHiY-DED";
    public string Text { get; set; } = string.Empty;
    public string Role { get; set; } = "Viewer";
    public string ExternalId { get; set; } = string.Empty;
    public string AuthorId { get; set; } = string.Empty;
    public string Foreground { get; set; } = "#EDF7FF";
    public string Background { get; set; } = "Transparent";
    public bool IsHighlighted { get; set; }
    public string DisplayTime => Time.ToString("HH:mm:ss");
    public string PlatformIconPath => Platform.Equals("TWITCH", StringComparison.OrdinalIgnoreCase)
        ? "/TiHiY.StreamControlCenter;component/Assets/Platforms/twitch.png"
        : Platform.Equals("YOUTUBE", StringComparison.OrdinalIgnoreCase)
            ? "/TiHiY.StreamControlCenter;component/Assets/Platforms/youtube.png"
            : Platform.Equals("DONATELLO", StringComparison.OrdinalIgnoreCase)
                ? "/TiHiY.StreamControlCenter;component/Assets/Platforms/donatello.png"
                : Platform.Equals("DISCORD", StringComparison.OrdinalIgnoreCase)
                    ? "/TiHiY.StreamControlCenter;component/Assets/Platforms/discord.png"
                    : "Assets/AppIcon.png";
    public string PlatformColor => Platform.Equals("TWITCH", StringComparison.OrdinalIgnoreCase)
        ? "#A970FF"
        : Platform.Equals("YOUTUBE", StringComparison.OrdinalIgnoreCase)
            ? "#FF3B3B"
            : Platform.Equals("DONATELLO", StringComparison.OrdinalIgnoreCase) ? "#FFD329" : "#46D8FF";
}
