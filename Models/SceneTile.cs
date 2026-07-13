namespace TiHiY.StreamControlCenter.Models;

public sealed class SceneTile : INotifyPropertyChanged
{
    private bool _isProgram;
    private bool _isPreview;
    public string Name { get; set; } = string.Empty;
    public bool IsProgram { get => _isProgram; set { if (Set(ref _isProgram, value)) OnPropertyChanged(nameof(StateText)); } }
    public bool IsPreview { get => _isPreview; set { if (Set(ref _isPreview, value)) OnPropertyChanged(nameof(StateText)); } }
    public string StateText => IsProgram ? "PROGRAM" : IsPreview ? "PREVIEW" : string.Empty;
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
