namespace TiHiY.StreamControlCenter.Models;

public sealed class AppSettings
{
    public string ObsUrl { get; set; } = "ws://127.0.0.1:4455";
    public bool RememberObsPassword { get; set; } = true;
    public bool AutoConnectObs { get; set; } = true;
    public string MultiStreamVendorName { get; set; } = "tihiy.multistream";
    public int OverlayPort { get; set; } = 17845;

    public bool Aida64MonitoringEnabled { get; set; } = true;
    public int SystemMonitorRefreshMilliseconds { get; set; } = 1000;

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
    public int DonatelloPollSeconds { get; set; } = 60;
    public decimal DonatelloMinimumOverlayAmount { get; set; }
    public bool DonatelloShowInChat { get; set; } = true;
    public bool DonatelloSendToPlatformChats { get; set; } = true;
    public bool DonatelloShowOnOverlay { get; set; } = true;
    public bool DonatelloNotifyDiscord { get; set; } = true;
    public List<string> DonatelloRecentEventIds { get; set; } = new();
    public Dictionary<string, int> DonatelloSubscriberPayments { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public string DonationGoalTitle { get; set; } = "Новий ПК для стрімів";
    public decimal DonationGoalAmount { get; set; } = 10000m;
    public decimal DonationGoalInitialAmount { get; set; }
    public string DonationGoalCurrency { get; set; } = "UAH";
    public string DonationGoalBarColor { get; set; } = "#FFD329";
    public string DonationGoalTextColor { get; set; } = "#F4F8FF";
    public string DonationGoalBackgroundColor { get; set; } = "#06172A";
    public int DonationTopDonorCount { get; set; } = 8;
    public string DonationTopDonorPeriod { get; set; } = "All";
    public double DonationTickerSpeed { get; set; } = 70;
    public string DonationTickerTextColor { get; set; } = "#FFD329";
    public string DonationTickerBackgroundColor { get; set; } = "#06172A";
    public double DonationTickerBackgroundOpacity { get; set; } = 0.35;

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
    public string UiTheme { get; set; } = "TiHiY Default / Cyber Amber";
    public string UiLanguage { get; set; } = "uk-UA";
    public string UiDensity { get; set; } = "Standard";
    public bool InterfaceAnimationsEnabled { get; set; } = true;
    public bool ShowTooltips { get; set; } = true;
    public bool LockLayoutAfterStartup { get; set; } = true;
    public bool StartWithWindows { get; set; }
    public bool MinimizeToTray { get; set; } = true;
    public bool TransitionEffectsEnabled { get; set; } = true;
    public string UpdateCheckFrequency { get; set; } = "Daily";
    public string UpdateChannel { get; set; } = "Stable";
    public int SecurityAutoLockMinutes { get; set; } = 15;
    public bool ConfirmOnExit { get; set; } = true;
    public Dictionary<string, string> DashboardBlockSlots { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> DashboardFreeformBounds { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public int UkraineReferenceLayoutVersion { get; set; }
    public int SettingsWindowLayoutVersion { get; set; }
    public int ChatColorMigrationVersion { get; set; }
    public string HighlightWords { get; set; } = "TiHiY-DED,tihiy_ded,@TiHiY-DED,@tihiy_ded,Тихий Дід,дід";
    public string OwnerColor { get; set; } = "#FFD329";
    public string ModeratorColor { get; set; } = "#45B6FF";
    public string SubscriberColor { get; set; } = "#22D878";
    public string VipColor { get; set; } = "#C77DFF";
    public string ViewerColor { get; set; } = "#55C8FF";
    public string BotColor { get; set; } = "#95A4AE";
    public string HighlightTextColor { get; set; } = "#07131E";
    public string HighlightBackgroundColor { get; set; } = "#FFD329";

    public double MainChatFontSize { get; set; } = 15;
    public string MainChatTextColor { get; set; } = "#DCE9F3";
    public double MainChatInputFontSize { get; set; } = 15;
    public string MainChatInputTextColor { get; set; } = "#EAF6FF";
    public string MainChatInputBackgroundColor { get; set; } = "#071525";
    public double MainChatInputHeight { get; set; } = 42;

    public double StreamChatOverlayFontSize { get; set; } = 20;
    public string StreamChatOverlayTextColor { get; set; } = "#F2FAFF";
    public string StreamChatOverlayUserColor { get; set; } = "#55C8FF";
    public string StreamChatOverlayBackgroundColor { get; set; } = "#000000";
    public double StreamChatOverlayBackgroundOpacity { get; set; }
    public int StreamChatOverlayMaxMessages { get; set; } = 12;

    public bool LocalChatOverlayAutoStart { get; set; }
    public bool LocalChatOverlayClickThrough { get; set; } = true;
    public double LocalChatOverlayBackgroundOpacity { get; set; } = 0.12;
    public double LocalChatOverlayFontSize { get; set; } = 20;
    public int LocalChatOverlayMaxMessages { get; set; } = 12;
    public string LocalChatOverlayTextColor { get; set; } = "#F2FAFF";
    public string LocalChatOverlayUserColor { get; set; } = "#55C8FF";

    public bool ChatBotEnabled { get; set; } = true;
    public bool ChatBotAutoStart { get; set; } = true;
    public string ChatBotDefaultTarget { get; set; } = "Twitch + YouTube";
    public bool ChatBotSpamProtectionEnabled { get; set; } = true;
    public bool ChatBotBlockLinks { get; set; }
    public bool ChatBotBlockCaps { get; set; }
    public bool ChatBotBlockRepeats { get; set; } = true;
    public string ChatBotBlockedWords { get; set; } = string.Empty;
    public int ChatBotResponseDelayMilliseconds { get; set; }

    public bool AutoNoticesEnabled { get; set; } = true;
    public bool UiScaleAuto { get; set; } = true;
    public int UiScalePercent { get; set; } = 100;
    public bool AudioAutoDetect { get; set; } = true;
    public List<string> SelectedAudioInputs { get; set; } = new();
    public List<string> PinnedAudioInputs { get; set; } = new();
    public List<ScheduledNotice> ScheduledNotices { get; set; } = new();
    public List<BotCommand> BotCommands { get; set; } = new();
    public List<string> MusicPlaylistPaths { get; set; } = new();
    public string MusicRepeatMode { get; set; } = "Playlist";
    public double MusicVolume { get; set; } = 0.65;
    public bool MusicMuted { get; set; }
    public int DashboardLayoutVersion { get; set; } = 0;
    public double MainLeftColumnWidth { get; set; } = 1.035;
    public double MainBottomLeftColumnWidth { get; set; } = 1.02;
    public double MainTopRowHeight { get; set; } = 1.51;
    public double FooterHeight { get; set; } = 180;
    public double FooterSystemColumnWeight { get; set; } = 0.22;
    public double FooterEventsColumnWeight { get; set; } = 0.38;
    public double FooterMonitorColumnWeight { get; set; } = 0.40;
    public Dictionary<string, WindowPlacement> WindowPlacements { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
