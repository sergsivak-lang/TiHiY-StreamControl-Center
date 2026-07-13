using System.Windows.Media;
using TiHiY.StreamControlCenter.Models;

namespace TiHiY.StreamControlCenter.Services;

public sealed class MusicPlayerService : IDisposable
{
    private readonly MediaPlayer _player = new();
    private readonly DispatcherTimer _timer;
    private int _currentIndex = -1;
    private bool _isPaused;

    public ObservableCollection<TrackItem> Playlist { get; } = new();
    public TrackItem? CurrentTrack => _currentIndex >= 0 && _currentIndex < Playlist.Count ? Playlist[_currentIndex] : null;
    public bool IsPlaying => _player.Source is not null && !_isPaused;
    public TimeSpan Position => _player.Position;
    public TimeSpan Duration => _player.NaturalDuration.HasTimeSpan ? _player.NaturalDuration.TimeSpan : TimeSpan.Zero;
    public double Volume { get => _player.Volume; set => _player.Volume = Math.Clamp(value, 0, 1); }
    public bool IsMuted { get => _player.IsMuted; set => _player.IsMuted = value; }

    public event EventHandler? TrackChanged;
    public event EventHandler? PositionChanged;
    public event EventHandler<string>? PlaybackError;

    public MusicPlayerService()
    {
        _player.MediaEnded += (_, _) => Next();
        _player.MediaFailed += (_, e) => PlaybackError?.Invoke(this, e.ErrorException?.Message ?? "Помилка відтворення");
        _player.MediaOpened += (_, _) => TrackChanged?.Invoke(this, EventArgs.Empty);
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _timer.Tick += (_, _) => PositionChanged?.Invoke(this, EventArgs.Empty);
        _timer.Start();
        Volume = 0.65;
    }

    public void Restore(IEnumerable<string> paths) => AddFiles(paths);
    public void AddFiles(IEnumerable<string> files)
    {
        foreach (var file in files)
        {
            if (!File.Exists(file) || Playlist.Any(t => string.Equals(t.FilePath, file, StringComparison.OrdinalIgnoreCase))) continue;
            Playlist.Add(TrackItem.FromPath(file));
        }
    }
    public void Play(int index)
    {
        if (index < 0 || index >= Playlist.Count) return;
        _currentIndex = index;
        _player.Open(new Uri(Playlist[index].FilePath, UriKind.Absolute));
        _player.Play();
        _isPaused = false;
        TrackChanged?.Invoke(this, EventArgs.Empty);
    }
    public void PlayOrPause()
    {
        if (_player.Source is null) { if (Playlist.Count > 0) Play(_currentIndex >= 0 ? _currentIndex : 0); return; }
        if (_isPaused) { _player.Play(); _isPaused = false; } else { _player.Pause(); _isPaused = true; }
        TrackChanged?.Invoke(this, EventArgs.Empty);
    }
    public void Next() { if (Playlist.Count > 0) Play((_currentIndex + 1 + Playlist.Count) % Playlist.Count); }
    public void Previous() { if (Playlist.Count > 0) Play((_currentIndex - 1 + Playlist.Count) % Playlist.Count); }
    public void Stop() { _player.Stop(); _isPaused = false; TrackChanged?.Invoke(this, EventArgs.Empty); }
    public void Remove(TrackItem? item)
    {
        if (item is null) return;
        var index = Playlist.IndexOf(item);
        if (index < 0) return;
        if (index == _currentIndex) Stop();
        Playlist.RemoveAt(index);
        if (_currentIndex >= Playlist.Count) _currentIndex = Playlist.Count - 1;
    }
    public void Dispose() { _timer.Stop(); _player.Close(); }
}
