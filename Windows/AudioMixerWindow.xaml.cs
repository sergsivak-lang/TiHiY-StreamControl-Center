using TiHiY.StreamControlCenter.Models;
using TiHiY.StreamControlCenter.Services;

namespace TiHiY.StreamControlCenter.Windows;

public partial class AudioMixerWindow : ModuleWindowBase
{
    private readonly AppServices _services = App.Services;
    private readonly DispatcherTimer _refreshTimer;
    private readonly List<AudioChannel> _allChannels = new();
    private int _pageIndex;
    private bool _loading;
    private bool _syncingVolumeFromObs;
    private bool _showAllForSelection;

    public ObservableCollection<AudioChannel> ChannelPage { get; } = new();

    public AudioMixerWindow()
    {
        InitializeComponent();
        DataContext = this;
        ConfigureModule(DesignSurface, 1200, 760, "AudioMixer");

        _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        _refreshTimer.Tick += async (_, _) => await RefreshChannelsSafeAsync();

        _services.Obs.ConnectionChanged += Obs_ConnectionChanged;
        _services.Obs.InputMeterChanged += Obs_InputMeterChanged;
        _services.Obs.InputMuteChanged += Obs_InputMuteChanged;
        _services.Obs.InputVolumeChanged += Obs_InputVolumeChanged;
        Loaded += async (_, _) =>
        {
            _refreshTimer.Start();
            await RefreshChannelsSafeAsync();
        };
        Closed += (_, _) =>
        {
            _refreshTimer.Stop();
            _services.Obs.ConnectionChanged -= Obs_ConnectionChanged;
            _services.Obs.InputMeterChanged -= Obs_InputMeterChanged;
            _services.Obs.InputMuteChanged -= Obs_InputMuteChanged;
            _services.Obs.InputVolumeChanged -= Obs_InputVolumeChanged;
        };
    }

    private void Obs_ConnectionChanged(object? sender, bool connected) => Dispatcher.BeginInvoke(new Action(async () =>
    {
        if (connected) await RefreshChannelsSafeAsync();
        else
        {
            _allChannels.Clear();
            ChannelPage.Clear();
            PageText.Text = "1 / 1";
            StatusText.Text = "OBS не підключено";
        }
    }));

    private void Obs_InputMeterChanged(object? sender, (string inputName, double meter, double db) data) => Dispatcher.BeginInvoke(new Action(() =>
    {
        var channel = _allChannels.FirstOrDefault(x => string.Equals(x.Name, data.inputName, StringComparison.OrdinalIgnoreCase));
        if (channel is not null)
        {
            channel.Meter = data.meter;
            channel.Db = data.db;
        }
    }));

    private void Obs_InputVolumeChanged(object? sender, (string inputName, double volume) data) => Dispatcher.BeginInvoke(new Action(() =>
    {
        var channel = _allChannels.FirstOrDefault(x => string.Equals(x.Name, data.inputName, StringComparison.OrdinalIgnoreCase));
        if (channel is null) return;
        _syncingVolumeFromObs = true;
        try { channel.Volume = Math.Clamp(data.volume, 0, 1); }
        finally { _syncingVolumeFromObs = false; }
    }));

    private void Obs_InputMuteChanged(object? sender, (string inputName, bool muted) data) => Dispatcher.BeginInvoke(new Action(() =>
    {
        var channel = _allChannels.FirstOrDefault(x => string.Equals(x.Name, data.inputName, StringComparison.OrdinalIgnoreCase));
        if (channel is not null) channel.IsMuted = data.muted;
    }));

    private async Task RefreshChannelsAsync()
    {
        if (!_services.Obs.IsConnected)
        {
            StatusText.Text = "OBS не підключено. Відкрийте налаштування головного вікна.";
            return;
        }

        _loading = true;
        try
        {
            var inputs = await _services.Obs.GetMixerInputsAsync();
            var selected = _services.Settings.Value.SelectedAudioInputs;
            var preferred = new[] { "Гра звук", "Медіа", "Мікро", "Чат" };
            var detected = preferred.Where(name => inputs.Any(x => string.Equals(x.name, name, StringComparison.OrdinalIgnoreCase))).ToList();
            if (selected.Count == 0 || (selected.Count > 6 && detected.Count >= 3))
            {
                selected.Clear();
                selected.AddRange(detected);
                if (selected.Count > 0) _services.Save();
            }
            if (!_showAllForSelection && selected.Count > 0)
                inputs = inputs.Where(x => selected.Contains(x.name, StringComparer.OrdinalIgnoreCase))
                    .OrderBy(x => selected.FindIndex(n => string.Equals(n, x.name, StringComparison.OrdinalIgnoreCase))).ToList();
            var oldByName = _allChannels.ToDictionary(x => x.Name, StringComparer.OrdinalIgnoreCase);
            var refreshed = new List<AudioChannel>();
            foreach (var input in inputs)
            {
                var channel = oldByName.TryGetValue(input.name, out var existing)
                    ? existing
                    : new AudioChannel { Name = input.name, Kind = input.kind };
                channel.IsPinned = _services.Settings.Value.SelectedAudioInputs.Contains(input.name, StringComparer.OrdinalIgnoreCase);
                try { channel.IsMuted = await _services.Obs.GetInputMuteAsync(input.name); } catch { }
                try { channel.Volume = Math.Clamp(await _services.Obs.GetInputVolumeAsync(input.name), 0, 1); } catch { }
                refreshed.Add(channel);
            }

            _allChannels.Clear();
            _allChannels.AddRange(refreshed);
            _pageIndex = Math.Clamp(_pageIndex, 0, PageCount - 1);
            ShowPage();
            StatusText.Text = _allChannels.Count == 0
                ? "Вибрані аудіоканали не знайдено. Увімкніть показ усіх і виберіть потрібні."
                : _showAllForSelection ? $"Режим вибору: {_allChannels.Count} доступних джерел." : $"Відображено {_allChannels.Count} вибраних каналів, як у вашому OBS Mixer.";
        }
        finally
        {
            _loading = false;
        }
    }

    private int PageCount => Math.Max(1, (int)Math.Ceiling(_allChannels.Count / 6.0));
    private void ShowPage()
    {
        ChannelPage.Clear();
        foreach (var channel in _allChannels.Skip(_pageIndex * 6).Take(6)) ChannelPage.Add(channel);
        PageText.Text = $"{_pageIndex + 1} / {PageCount}";
    }


    private async void ToggleShowAll_Click(object sender, RoutedEventArgs e)
    {
        _showAllForSelection = !_showAllForSelection;
        ShowAllButton.Content = _showAllForSelection ? "ПОКАЗАТИ ТІЛЬКИ ВИБРАНІ" : "ПОКАЗАТИ ВСІ ДЛЯ ВИБОРУ";
        _pageIndex = 0;
        await RefreshChannelsSafeAsync();
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e) => await RefreshChannelsSafeAsync();
    private async Task RefreshChannelsSafeAsync()
    {
        if (_loading) return;
        try { await RefreshChannelsAsync(); }
        catch (Exception ex)
        {
            _services.Logger.Error("Оновлення Audio Mixer OBS", ex);
            StatusText.Text = "Не вдалося оновити Audio Mixer OBS.";
        }
    }

    private async void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_loading || _syncingVolumeFromObs || !IsLoaded || sender is not Slider { Tag: AudioChannel channel }) return;
        try
        {
            await _services.Obs.SetInputVolumeAsync(channel.Name, e.NewValue);
            channel.Volume = e.NewValue;
        }
        catch (Exception ex)
        {
            _services.Logger.Error($"Гучність {channel.Name}", ex);
        }
    }

    private async void Mute_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: AudioChannel channel }) return;
        try
        {
            await _services.Obs.SetInputMuteAsync(channel.Name, !channel.IsMuted);
            channel.IsMuted = !channel.IsMuted;
        }
        catch (Exception ex)
        {
            _services.Logger.Error($"MUTE {channel.Name}", ex);
            MessageBox.Show(this, ex.GetBaseException().Message, "OBS Audio", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }


    private void Pin_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: AudioChannel channel }) return;
        var selected = _services.Settings.Value.SelectedAudioInputs;
        var existing = selected.FirstOrDefault(x => string.Equals(x, channel.Name, StringComparison.OrdinalIgnoreCase));
        if (existing is null)
        {
            selected.Add(channel.Name);
            channel.IsPinned = true;
        }
        else
        {
            selected.Remove(existing);
            channel.IsPinned = false;
        }
        _services.Save();
        StatusText.Text = channel.IsPinned ? $"{channel.Name}: показується у швидкому мікшері." : $"{channel.Name}: приховано зі швидкого мікшера.";
    }

    private async void MuteAll_Click(object sender, RoutedEventArgs e) => await SetAllMuteAsync(true);
    private async void UnmuteAll_Click(object sender, RoutedEventArgs e) => await SetAllMuteAsync(false);
    private async Task SetAllMuteAsync(bool muted)
    {
        foreach (var channel in _allChannels)
        {
            try
            {
                await _services.Obs.SetInputMuteAsync(channel.Name, muted);
                channel.IsMuted = muted;
            }
            catch (Exception ex)
            {
                _services.Logger.Error($"MUTE {channel.Name}", ex);
            }
        }
    }

    private void PreviousPage_Click(object sender, RoutedEventArgs e)
    {
        if (_pageIndex <= 0) return;
        _pageIndex--;
        ShowPage();
    }

    private void NextPage_Click(object sender, RoutedEventArgs e)
    {
        if (_pageIndex + 1 >= PageCount) return;
        _pageIndex++;
        ShowPage();
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => DragTitle(sender, e);
    private void Minimize_Click(object sender, RoutedEventArgs e) => MinimizeWindow(sender, e);
    private void Maximize_Click(object sender, RoutedEventArgs e) => MaximizeWindow(sender, e);
    private void Close_Click(object sender, RoutedEventArgs e) => CloseWindow(sender, e);
}
