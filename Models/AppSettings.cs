namespace TiHiY.StreamControlCenter.Models;

public sealed class AppSettings
{
    public string ObsUrl { get; set; } = "ws://127.0.0.1:4455";
    public bool RememberObsPassword { get; set; } = true;
    public bool AutoConnectObs { get; set; } = true;
    public string MultiStreamVendorName { get; set; } = "tihiy.multistream";
    public int OverlayPort { get; set; } = 17845;

    public string TwitchChannelName { get; set; } = "tihiy_ded";
    public string TwitchClientId { get; set; } = string.Empty;
    public bool TwitchAutoConnect { get; set; } = true;
    public string TwitchUserLogin { get; set; } = string.Empty;
    public string TwitchBroadcasterId { get; set; } = string.Empty;
    public string TwitchUserId { get; set; } = string.Empty;
    public string TwitchLastStreamId { get; set; } = string.Empty;
    public string TwitchCurrentStreamId { get; set; } = string.Empty;

    public string YouTubeChannelName { get; set; } = "TiHiY-DED";
    public string YouTubeClientId { get; set; } = string.Empty;
    public bool YouTubeAutoConnect { get; set; } = true;
    public string YouTubeActiveBroadcastId { get; set; } = string.Empty;
    public string YouTubeLastNotifiedBroadcastId { get; set; } = string.Empty;

    public string DiscordApplicationId { get; set; } = string.Empty;
    public bool DiscordNotificationsEnabled { get; set; }
    public bool NotificationBotAutoStart { get; set; }
    public string DiscordMention { get; set; } = "@everyone";
    public string DiscordChannelIds { get; set; } = string.Empty;
    public bool DiscordNotifyTwitch { get; set; } = true;
    public bool DiscordNotifyYouTube { get; set; } = true;
    public string DiscordMessageTemplate { get; set; } = "🔴 {platform}: трансляція почалася!\n{title}\n{url}";

    public bool DiscordMonetizationEnabled { get; set; }
    public string DiscordMonetizationChannelIds { get; set; } = string.Empty;
    public string DiscordMonetizationMention { get; set; } = string.Empty;
    public bool DiscordNotifyDonatelloMonetization { get; set; } = true;
    public bool DiscordNotifyTwitchMonetization { get; set; } = true;
    public bool DiscordNotifyYouTubeMonetization { get; set; } = true;

    public bool DonatelloEnabled { get; set; }
    public bool DonatelloAutoStart { get; set; }
    public string DonatelloPageUrl { get; set; } = "https://donatello.to/TiHiY-DED";
    public int DonatelloPollSeconds { get; set; } = 8;
    public decimal DonatelloMinimumOverlayAmount { get; set; }
    public bool DonatelloShowInChat { get; set; } = true;
    public bool DonatelloSendToPlatformChats { get; set; } = true;
    public bool DonatelloShowOnOverlay { get; set; } = true;
    public bool DonatelloNotifyDiscord { get; set; } = true;
    public List<string> DonatelloRecentEventIds { get; set; } = new();
    public Dictionary<string, int> DonatelloSubscriberPayments { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public decimal DonationGoalAmount { get; set; } = 5000m;

    // Застарілі поля лишені тільки для безпечного читання старого settings.json.
    public string DonatelloDiscordChannelId { get; set; } = string.Empty;
    public string DonatelloLastMessageId { get; set; } = string.Empty;

    public int TwitchViewers { get; set; }
    public int YouTubeViewers { get; set; }
    public int YouTubeLikes { get; set; }
    public bool TwitchLive { get; set; }
    public bool YouTubeLive { get; set; }
    public string TwitchStreamTitle { get; set; } = string.Empty;
    public string YouTubeStreamTitle { get; set; } = string.Empty;

    public string OverlayTheme { get; set; } = "Star Citizen MFD";
    public string HighlightWords { get; set; } = "TiHiY-DED,tihiy_ded,@TiHiY-DED,@tihiy_ded,Тихий Дід,дід";
    public string OwnerColor { get; set; } = "#FFD329";
    public string ModeratorColor { get; set; } = "#45B6FF";
    public string SubscriberColor { get; set; } = "#22D878";
    public string VipColor { get; set; } = "#C77DFF";
    public string ViewerColor { get; set; } = "#EDF7FF";
    public string BotColor { get; set; } = "#95A4AE";
    public string HighlightTextColor { get; set; } = "#07131E";
    public string HighlightBackgroundColor { get; set; } = "#FFD329";

    public bool LocalChatOverlayAutoStart { get; set; }
    public bool LocalChatOverlayClickThrough { get; set; } = true;
    public double LocalChatOverlayBackgroundOpacity { get; set; } = 0.12;
    public double LocalChatOverlayFontSize { get; set; } = 18;
    public int LocalChatOverlayMaxMessages { get; set; } = 12;

    public bool AutoNoticesEnabled { get; set; } = true;
    public bool UiScaleAuto { get; set; } = true;
    public int UiScalePercent { get; set; } = 100;
    public string UiTheme { get; set; } = "CyberAmber";
    public bool UiAnimationsEnabled { get; set; } = true;
    public bool UseThemeForOverlay { get; set; }
    public bool AudioAutoDetect { get; set; } = true;
    public List<string> SelectedAudioInputs { get; set; } = new();
    public List<string> PinnedAudioInputs { get; set; } = new();
    public List<ScheduledNotice> ScheduledNotices { get; set; } = new();
    public List<BotCommand> BotCommands { get; set; } = new();
    public List<string> MusicPlaylistPaths { get; set; } = new();
    public int UiLayoutVersion { get; set; } = 2;
    public double MainLeftColumnWidth { get; set; } = 1.04;
    public double MainTopRowHeight { get; set; } = 1.13;
    public Dictionary<string, WindowPlacement> WindowPlacements { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
