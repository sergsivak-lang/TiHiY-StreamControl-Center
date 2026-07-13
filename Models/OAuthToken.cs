namespace TiHiY.StreamControlCenter.Models;

public sealed class OAuthToken
{
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public DateTime ExpiresAtUtc { get; set; } = DateTime.MinValue;
    public string TokenType { get; set; } = "Bearer";
    public bool IsUsable => !string.IsNullOrWhiteSpace(AccessToken) && ExpiresAtUtc > DateTime.UtcNow.AddMinutes(2);
}
