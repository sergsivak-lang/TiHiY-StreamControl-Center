namespace TiHiY.StreamControlCenter.Models;

public sealed class DonationEvent
{
    public DateTime Time { get; set; } = DateTime.Now;
    public string ExternalId { get; set; } = string.Empty;
    public string Source { get; set; } = "DONATELLO";
    public string Kind { get; set; } = "DONATION";
    public string User { get; set; } = "Глядач";
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "UAH";
    public string Message { get; set; } = string.Empty;
    public string Accent { get; set; } = "#FFD329";
    public bool ShowOnOverlay { get; set; } = true;
    public bool IsHistorical { get; set; }
    public bool IsTest { get; set; }
    public bool IsReplay { get; set; }
    public string DisplayTime => Time.ToString("HH:mm:ss");
    public string DisplayAmount => $"{Amount:0.##} {Currency}";
    public string PlatformIconPath => Source.Contains("YOUTUBE", StringComparison.OrdinalIgnoreCase) || Source.Contains("SUPER", StringComparison.OrdinalIgnoreCase)
        ? "/TiHiY.StreamControlCenter;component/Assets/Platforms/youtube.png"
        : Source.Contains("TWITCH", StringComparison.OrdinalIgnoreCase) || Source.Contains("BITS", StringComparison.OrdinalIgnoreCase)
            ? "/TiHiY.StreamControlCenter;component/Assets/Platforms/twitch.png"
            : Source.Contains("DISCORD", StringComparison.OrdinalIgnoreCase)
                ? "/TiHiY.StreamControlCenter;component/Assets/Platforms/discord.png"
                : "/TiHiY.StreamControlCenter;component/Assets/Platforms/donatello.png";
    public string EventIcon => IsTest
        ? "T"
        : IsReplay ? "↻"
        : Kind.Equals("SUBSCRIPTION", StringComparison.OrdinalIgnoreCase)
            ? "★"
            : Source.Contains("SUPER", StringComparison.OrdinalIgnoreCase) ? "◆"
            : Source.Contains("BITS", StringComparison.OrdinalIgnoreCase) ? "◆" : "♥";
    public string KindLabel => IsTest
        ? "TEST"
        : IsReplay ? "ПОВТОР ALERT"
        : Kind.Equals("SUBSCRIPTION", StringComparison.OrdinalIgnoreCase)
            ? "ПІДПИСКА"
            : Source.Contains("SUPER CHAT", StringComparison.OrdinalIgnoreCase) ? "SUPER CHAT"
            : Source.Contains("SUPER STICKER", StringComparison.OrdinalIgnoreCase) ? "SUPER STICKER"
            : Source.Contains("BITS", StringComparison.OrdinalIgnoreCase) ? "BITS" : "ДОНАТ";
    public string EventSummary => Kind.Equals("SUBSCRIPTION", StringComparison.OrdinalIgnoreCase)
        ? Message
        : $"{DisplayAmount} • {Message}";
    public string StableId => string.IsNullOrWhiteSpace(ExternalId)
        ? $"{Source}:{Kind}:{Time.Ticks}:{User}:{Amount}:{Currency}"
        : ExternalId;
}
