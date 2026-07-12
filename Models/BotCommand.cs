namespace TiHiY.StreamControlCenter.Models;

public sealed class BotCommand
{
    public bool Enabled { get; set; } = true;
    public string Name { get; set; } = "!команда";
    public string Reply { get; set; } = "Відповідь бота";
    public string Platform { get; set; } = "Twitch + YouTube";
    public string Access { get; set; } = "Усі";
    public int CooldownSeconds { get; set; } = 10;
}
