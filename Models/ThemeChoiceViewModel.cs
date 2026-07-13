namespace TiHiY.StreamControlCenter.Models;

public sealed class ThemeChoiceViewModel : INotifyPropertyChanged
{
    private bool _isSelected;

    public ThemeChoiceViewModel(ThemeDefinition definition)
    {
        Definition = definition;
        PrimaryBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(definition.PrimaryHex)!);
        AccentBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(definition.AccentHex)!);
    }

    public ThemeDefinition Definition { get; }
    public string Id => Definition.Id;
    public string DisplayName => Definition.DisplayName;
    public string Description => Definition.Description;
    public Brush PrimaryBrush { get; }
    public Brush AccentBrush { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value) return;
            _isSelected = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
