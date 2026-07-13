namespace TiHiY.StreamControlCenter.Models;

public sealed class StreamLiveInfo
{
    public string Platform { get; set; } = string.Empty;
    public bool IsLive { get; set; }
    public string BroadcastId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public int Viewers { get; set; }
    public int Likes { get; set; }
    public string ThumbnailUrl { get; set; } = string.Empty;
    public DateTimeOffset? StartedAtUtc { get; set; }
}
