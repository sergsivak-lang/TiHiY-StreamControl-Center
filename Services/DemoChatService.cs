using System.Collections.ObjectModel;
using TiHiY.StreamControlCenter.Models;

namespace TiHiY.StreamControlCenter.Services;

public sealed class DemoChatService
{
    public ObservableCollection<ChatMessage> Messages { get; } = new();
    public ObservableCollection<string> Events { get; } = new();

    public DemoChatService()
    {
        Add("Twitch", "StarGazer", "Привіт усім! Гарного стріму! 🚀", "#A970FF");
        Add("YouTube", "CosmoPilot", "Лайк та підписка! Удачі! 💙💛", "#FF4350");
        Add("Twitch", "ViperUA", "Я з України! Слава ЗСУ!", "#A970FF");
        Add("Donatello", "ViperUA", "Донат — 200 UAH. Дякую за контент!", "#22D878");
        Events.Insert(0, $"{DateTime.Now:HH:mm:ss}  SYSTEM  Демонстраційний режим запущено");
    }

    public void Add(string platform, string user, string text, string accent = "#2B8CFF")
    {
        Messages.Add(new ChatMessage { Platform = platform, User = user, Text = text, Accent = accent });
        Events.Insert(0, $"{DateTime.Now:HH:mm:ss}  {platform.ToUpperInvariant()}  {user}: {text}");
    }
}
