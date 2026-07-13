namespace TiHiY.StreamControlCenter.Models;

public sealed class ScheduledNotice : INotifyPropertyChanged
{
    private string _name = "Нове сповіщення";
    private string _text = string.Empty;
    private string _target = "Twitch + YouTube";
    private int _intervalMinutes = 20;
    private int _minimumChatMessages;
    private bool _enabled = true;
    private DateTime _nextRun = DateTime.Now.AddMinutes(20);

    public string Name { get => _name; set => Set(ref _name, value); }
    public string Text { get => _text; set => Set(ref _text, value); }
    public string Target { get => _target; set => Set(ref _target, value); }
    public int IntervalMinutes { get => _intervalMinutes; set => Set(ref _intervalMinutes, Math.Max(1, value)); }
    public int MinimumChatMessages { get => _minimumChatMessages; set => Set(ref _minimumChatMessages, Math.Max(0, value)); }
    public bool Enabled { get => _enabled; set => Set(ref _enabled, value); }
    public DateTime NextRun { get => _nextRun; set { if (Set(ref _nextRun, value)) OnPropertyChanged(nameof(NextRunText)); } }
    public DateTime? LastSent { get; set; }
    public string NextRunText => NextRun.ToString("HH:mm:ss");

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
