namespace TiHiY.StreamControlCenter.Models;

public sealed class YouTubeBroadcastSettings
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string PrivacyStatus { get; set; } = "public";
    public DateTime ScheduledStartTime { get; set; } = DateTime.Now.AddMinutes(15);
    public string LifeCycleStatus { get; set; } = string.Empty;
    public string LiveChatId { get; set; } = string.Empty;
    public string Display => string.IsNullOrWhiteSpace(Id) ? "Трансляцію не вибрано" : $"{Title} • {LifeCycleStatus} • {ScheduledStartTime:g}";
}
