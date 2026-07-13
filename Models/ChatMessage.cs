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
    public string PlatformIcon => Platform.Equals("TWITCH", StringComparison.OrdinalIgnoreCase)
        ? "T"
        : Platform.Equals("YOUTUBE", StringComparison.OrdinalIgnoreCase)
            ? "▶"
            : Platform.Equals("DONATELLO", StringComparison.OrdinalIgnoreCase) ? "♥" : "•";
    public string PlatformColor => Platform.Equals("TWITCH", StringComparison.OrdinalIgnoreCase)
        ? "#A970FF"
        : Platform.Equals("YOUTUBE", StringComparison.OrdinalIgnoreCase)
            ? "#FF3B3B"
            : Platform.Equals("DONATELLO", StringComparison.OrdinalIgnoreCase) ? "#FFD329" : "#46D8FF";
}
