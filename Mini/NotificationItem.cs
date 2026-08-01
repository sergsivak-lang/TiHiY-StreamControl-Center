namespace TiHiY.StreamControlMini;

public sealed class NotificationItem
{
    public DateTime Time { get; set; } = DateTime.Now;
    public string Source { get; set; } = "SYSTEM";
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string DisplayTime => Time.ToString("HH:mm:ss");
}
