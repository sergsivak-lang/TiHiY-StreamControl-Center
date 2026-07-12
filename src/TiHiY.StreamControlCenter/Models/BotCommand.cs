namespace TiHiY.StreamControlCenter.Models;

public sealed class BotCommand
{
    public string Trigger { get; set; } = string.Empty;
    public string Response { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
}
