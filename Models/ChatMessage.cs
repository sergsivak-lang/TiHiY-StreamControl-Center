namespace TiHiY.StreamControlCenter.Models;

public sealed class ChatMessage
{
    public DateTime Time { get; init; } = DateTime.Now;
    public string Platform { get; init; } = "SYSTEM";
    public string User { get; init; } = "System";
    public string Text { get; init; } = "";
    public string Accent { get; init; } = "#22D878";
}
