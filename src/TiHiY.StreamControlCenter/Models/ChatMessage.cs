namespace TiHiY.StreamControlCenter.Models;

public sealed record ChatMessage(
    DateTime Timestamp,
    string Platform,
    string Author,
    string Text,
    bool IsBot = false);
