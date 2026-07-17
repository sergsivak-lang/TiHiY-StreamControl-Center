namespace TiHiY.StreamControlCenter.Models;

public sealed class AudioChannel : INotifyPropertyChanged
{
    private double _volume = 1;
    private double _meter;
    private double _db = -60;
    private bool _isMuted;
    private bool _isPinned;

    public string Name { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public double Volume { get => _volume; set => Set(ref _volume, Math.Clamp(value, 0, 1)); }
    public double Meter { get => _meter; set => Set(ref _meter, Math.Clamp(value, 0, 1)); }
    public double Db { get => _db; set { if (Set(ref _db, value)) OnPropertyChanged(nameof(DbText)); } }
    public bool IsMuted { get => _isMuted; set { if (Set(ref _isMuted, value)) OnPropertyChanged(nameof(MuteText)); } }
    public bool IsPinned { get => _isPinned; set { if (Set(ref _isPinned, value)) OnPropertyChanged(nameof(PinText)); } }
    public string DbText => $"{Db:0.0} dB";

    public string IconGlyph
    {
        get
        {
            var value = $"{Name} {Kind}".ToLowerInvariant();
            if (value.Contains("mic") || value.Contains("мікроф") || value.Contains("voice")) return "\uE720";
            if (value.Contains("music") || value.Contains("музик") || value.Contains("media") || value.Contains("spotify") || value.Contains("vlc")) return "\uE189";
            if (value.Contains("discord")) return "\uE716";
            if (value.Contains("chat") || value.Contains("чат")) return "\uE716";
            if (value.Contains("game") || value.Contains("гра") || value.Contains("gaming")) return "\uE7FC";
            if (value.Contains("browser") || value.Contains("брауз")) return "\uE774";
            if (value.Contains("obs")) return "\uE95D";
            if (value.Contains("desktop") || value.Contains("system") || value.Contains("систем") || value.Contains("wasapi")) return "\uE7F5";
            if (value.Contains("capture") || value.Contains("захват")) return "\uE7F4";
            return "\uE767";
        }
    }

    public string MuteText => IsMuted ? "MUTE: ON" : "MUTE";
    public string PinText => IsPinned ? "ПРИБРАТИ З ГОЛОВНОЇ" : "ДОДАТИ НА ГОЛОВНУ";

    public event PropertyChangedEventHandler? PropertyChanged;
    private bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(name);
        return true;
    }
    private void OnPropertyChanged(string? name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}