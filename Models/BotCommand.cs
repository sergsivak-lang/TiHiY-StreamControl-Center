namespace TiHiY.StreamControlCenter.Models;

public sealed class BotCommand : INotifyPropertyChanged
{
    private string _name = "!команда";
    private string _reply = "Відповідь бота";
    private string _target = "Twitch + YouTube";
    private int _cooldownSeconds = 10;
    private bool _enabled = true;
    public string Name { get => _name; set => Set(ref _name, value); }
    public string Reply { get => _reply; set => Set(ref _reply, value); }
    public string Target { get => _target; set => Set(ref _target, value); }
    public int CooldownSeconds { get => _cooldownSeconds; set { Set(ref _cooldownSeconds, Math.Max(0, value)); PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CooldownText))); } }
    public string CooldownText => $"{CooldownSeconds} сек";
    public bool Enabled { get => _enabled; set => Set(ref _enabled, value); }
    public event PropertyChangedEventHandler? PropertyChanged;
    private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null) { if (EqualityComparer<T>.Default.Equals(field, value)) return; field = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name)); }
}
