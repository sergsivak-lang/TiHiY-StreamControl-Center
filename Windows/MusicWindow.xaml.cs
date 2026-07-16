using System.Diagnostics;
using Microsoft.Win32;
using TiHiY.StreamControlCenter.Models;
using TiHiY.StreamControlCenter.Services;

namespace TiHiY.StreamControlCenter.Windows;

public partial class MusicWindow : ModuleWindowBase
{
    private readonly AppServices _services = App.Services;
    private int _pageIndex;
    private TrackItem? _selectedTrack;
    public ObservableCollection<TrackItem> PlaylistPage { get; } = new();

    public MusicWindow()
    {
        InitializeComponent();
        DataContext = this;
        ConfigureModule(DesignSurface, 1160, 730, "Music");
        VolumeSlider.Value = _services.Music.Volume;
        _services.Music.RepeatMode = MusicPlayerService.ParseRepeatMode(_services.Settings.Value.MusicRepeatMode);
        RepeatModeCombo.SelectedIndex = _services.Music.RepeatMode switch
        {
            MusicRepeatMode.Off => 0,
            MusicRepeatMode.Track => 1,
            _ => 2
        };
        _services.Music.TrackChanged += Music_Changed;
        _services.Music.PositionChanged += Music_Changed;
        Closed += (_, _) =>
        {
            _services.Music.TrackChanged -= Music_Changed;
            _services.Music.PositionChanged -= Music_Changed;
            _services.Save();
        };
        ShowPage();
        UpdateNowPlaying();
    }

    private void Music_Changed(object? sender, EventArgs e) => Dispatcher.BeginInvoke(new Action(UpdateNowPlaying));
    private int PageCount => Math.Max(1, (int)Math.Ceiling(_services.Music.Playlist.Count / 9.0));
    private void ShowPage()
    {
        _pageIndex = Math.Clamp(_pageIndex, 0, PageCount - 1);
        PlaylistPage.Clear();
        foreach (var track in _services.Music.Playlist.Skip(_pageIndex * 9).Take(9)) PlaylistPage.Add(track);
        PageText.Text = $"{_pageIndex + 1} / {PageCount}";
    }
    private void PreviousPage_Click(object sender, RoutedEventArgs e) { if (_pageIndex > 0) { _pageIndex--; ShowPage(); } }
    private void NextPage_Click(object sender, RoutedEventArgs e) { if (_pageIndex + 1 < PageCount) { _pageIndex++; ShowPage(); } }
    private void AddFiles_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Multiselect = true, Title = "Додати музику", Filter = "Аудіофайли|*.mp3;*.flac;*.wav;*.aac;*.m4a;*.wma;*.ogg|Усі файли|*.*" };
        if (dialog.ShowDialog(this) != true) return;
        _services.Music.AddFiles(dialog.FileNames);
        _services.Save();
        ShowPage();
    }
    private void Track_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: TrackItem track }) return;
        _selectedTrack = track;
        var index = _services.Music.Playlist.IndexOf(track);
        _services.Music.Play(index);
    }
    private void Remove_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedTrack is not null) _services.Music.Remove(_selectedTrack);
        _selectedTrack = null;
        _services.Save(); ShowPage();
    }
    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        _services.Music.Stop();
        _services.Music.Playlist.Clear();
        _services.Save(); ShowPage(); UpdateNowPlaying();
    }
    private void Previous_Click(object sender, RoutedEventArgs e) => _services.Music.Previous();
    private void Next_Click(object sender, RoutedEventArgs e) => _services.Music.Next();
    private void PlayPause_Click(object sender, RoutedEventArgs e) => _services.Music.PlayOrPause();
    private void Mute_Click(object sender, RoutedEventArgs e)
    {
        _services.Music.IsMuted = !_services.Music.IsMuted;
        MuteButton.Content = _services.Music.IsMuted ? "MUTE: ON" : "MUTE";
    }
    private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) { if (IsLoaded) _services.Music.Volume = e.NewValue; }

    private void RepeatModeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (RepeatModeCombo.SelectedItem is not ComboBoxItem item) return;
        var mode = MusicPlayerService.ParseRepeatMode(item.Tag?.ToString());
        _services.Music.RepeatMode = mode;
        _services.Settings.Value.MusicRepeatMode = mode.ToString();
        if (IsLoaded)
        {
            _services.Save();
            StatusText.Text = mode switch
            {
                MusicRepeatMode.Off => "Повтор вимкнено: плейлист зупиниться після останнього треку.",
                MusicRepeatMode.Track => "Увімкнено повтор поточного треку.",
                _ => "Увімкнено повтор усього плейлиста."
            };
        }
    }

    private void UpdateNowPlaying()
    {
        var track = _services.Music.CurrentTrack;
        TitleText.Text = track?.Title ?? "Трек не вибрано";
        ArtistText.Text = track?.Artist ?? string.Empty;
        PlayPauseButton.Content = _services.Music.IsPlaying ? "⏸" : "▶";
        var duration = _services.Music.Duration;
        var position = _services.Music.Position;
        Progress.Value = duration.TotalSeconds > 0 ? Math.Clamp(position.TotalSeconds / duration.TotalSeconds, 0, 1) : 0;
        TimeText.Text = $"{Format(position)} / {Format(duration)}";
    }
    private string OverlayUrl => $"http://127.0.0.1:{_services.Settings.Value.OverlayPort}/overlay/now-playing?theme={Uri.EscapeDataString(_services.Settings.Value.OverlayTheme)}";
    private void OpenOverlay_Click(object sender, RoutedEventArgs e) { try { Process.Start(new ProcessStartInfo(OverlayUrl) { UseShellExecute = true }); } catch (Exception ex) { _services.Logger.Error("Now Playing", ex); } }
    private void CopyOverlay_Click(object sender, RoutedEventArgs e) { try { Clipboard.SetText(OverlayUrl); StatusText.Text = "Now Playing URL скопійовано."; } catch (Exception ex) { _services.Logger.Error("Буфер обміну", ex); } }
    private static string Format(TimeSpan value) => value.TotalHours >= 1 ? value.ToString(@"hh\:mm\:ss") : value.ToString(@"mm\:ss");
    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => DragTitle(sender, e);
    private void Minimize_Click(object sender, RoutedEventArgs e) => MinimizeWindow(sender, e);
    private void Maximize_Click(object sender, RoutedEventArgs e) => MaximizeWindow(sender, e);
    private void Close_Click(object sender, RoutedEventArgs e) => CloseWindow(sender, e);
}