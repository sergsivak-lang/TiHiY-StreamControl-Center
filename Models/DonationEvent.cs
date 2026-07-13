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
    public string DisplayTime => Time.ToString("HH:mm:ss");
    public string DisplayAmount => $"{Amount:0.##} {Currency}";
    public string StableId => string.IsNullOrWhiteSpace(ExternalId)
        ? $"{Source}:{Kind}:{Time.Ticks}:{User}:{Amount}:{Currency}"
        : ExternalId;
}
