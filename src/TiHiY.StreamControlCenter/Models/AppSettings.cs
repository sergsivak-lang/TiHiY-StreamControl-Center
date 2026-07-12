namespace TiHiY.StreamControlCenter.Models;

public sealed class AppSettings
{
    public int OverlayBackgroundOpacity { get; set; } = 145;
    public bool OverlayClickThrough { get; set; }
    public bool OverlayTopMost { get; set; } = true;
    public bool OverlayAutoStart { get; set; }
    public int OverlayX { get; set; } = 40;
    public int OverlayY { get; set; } = 80;
    public int OverlayWidth { get; set; } = 560;
    public int OverlayHeight { get; set; } = 760;
    public int ViewerCount { get; set; }
    public int LikeCount { get; set; }
    public List<BotCommand> BotCommands { get; set; } =
    [
        new BotCommand { Trigger = "!discord", Response = "Discord-спільнота TiHiY-DED: додайте посилання у налаштуваннях команди." },
        new BotCommand { Trigger = "!youtube", Response = "YouTube-канал: TiHiY-DED" },
        new BotCommand { Trigger = "!twitch", Response = "Twitch: tihiy_ded" }
    ];
}
