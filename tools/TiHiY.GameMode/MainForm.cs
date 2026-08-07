using System.Diagnostics;
using System.Text.Json;

namespace TiHiY.GameMode;

public sealed class MainForm : Form
{
    private readonly Label _ramLabel = new();
    private readonly Label _statusLabel = new();
    private readonly TextBox _log = new();
    private readonly Button _streamButton = new();
    private readonly Button _gameButton = new();
    private readonly Button _restoreButton = new();
    private readonly CheckBox _autoRestore = new();
    private readonly System.Windows.Forms.Timer _monitorTimer = new();

    private bool _modeActive;
    private bool _gameSeen;
    private int _missTicks;
    private string _activeMode = string.Empty;

    private static readonly string DataDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "TiHiY", "GameMode");

    private static readonly string StateFile = Path.Combine(DataDir, "state.json");
    private static readonly string LogFile = Path.Combine(DataDir, "TiHiY_GameMode.log");

    private static readonly string[] StreamProcesses =
    {
        "OneDrive", "OneDriveStandaloneUpdater", "ms-teams", "Teams",
        "Widgets", "WidgetService", "PhoneExperienceHost", "YourPhone",
        "Copilot", "GameBar", "GameBarFTServer", "XboxPcApp",
        "CCXProcess", "AdobeIPCBroker", "AdobeCollabSync", "Creative Cloud",
        "Dropbox", "GoogleDriveFS"
    };

    private static readonly string[] GameOnlyProcesses =
    {
        "obs64", "chrome", "msedge", "firefox", "brave", "opera",
        "EpicGamesLauncher", "GalaxyClient", "EADesktop", "Battle.net"
    };

    private static readonly string[] ServicesToPause =
    {
        "DiagTrack", "WSearch", "MapsBroker"
    };

    public MainForm()
    {
        Directory.CreateDirectory(DataDir);

        Text = "TiHiY Game Mode — Star Citizen";
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(780, 610);
        MinimumSize = new Size(780, 610);
        BackColor = Color.FromArgb(18, 20, 24);
        ForeColor = Color.WhiteSmoke;
        Font = new Font("Segoe UI", 10F);

        BuildUi();
        UpdateRam();

        _monitorTimer.Interval = 3000;
        _monitorTimer.Tick += MonitorTimer_Tick;

        if (File.Exists(StateFile))
        {
            AppendLog("Знайдено незавершений попередній режим. Натисни «ВІДНОВИТИ ВСЕ».");
            _statusLabel.Text = "Є збережений стан для відновлення";
            _statusLabel.ForeColor = Color.Gold;
        }
    }

    private void BuildUi()
    {
        var title = new Label
        {
            Text = "TiHiY GAME MODE",
            Font = new Font("Segoe UI Semibold", 24F, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(28, 22),
            ForeColor = Color.White
        };

        var subtitle = new Label
        {
            Text = "Оптимізація Windows під Star Citizen",
            AutoSize = true,
            Location = new Point(32, 70),
            ForeColor = Color.Silver
        };

        _ramLabel.Location = new Point(32, 108);
        _ramLabel.Size = new Size(710, 30);
        _ramLabel.Font = new Font("Segoe UI Semibold", 12F, FontStyle.Bold);
        _ramLabel.ForeColor = Color.LightGreen;

        _streamButton.Text = "STREAM MODE\r\nStar Citizen + OBS + Discord + Sonar";
        _streamButton.Location = new Point(32, 154);
        _streamButton.Size = new Size(340, 82);
        StylePrimaryButton(_streamButton);
        _streamButton.Click += async (_, _) => await StartModeAsync("STREAM");

        _gameButton.Text = "GAME ONLY\r\nМаксимум вільних ресурсів";
        _gameButton.Location = new Point(398, 154);
        _gameButton.Size = new Size(340, 82);
        StylePrimaryButton(_gameButton);
        _gameButton.Click += async (_, _) => await StartModeAsync("GAME ONLY");

        _restoreButton.Text = "ВІДНОВИТИ ВСЕ";
        _restoreButton.Location = new Point(32, 252);
        _restoreButton.Size = new Size(706, 50);
        _restoreButton.FlatStyle = FlatStyle.Flat;
        _restoreButton.FlatAppearance.BorderSize = 1;
        _restoreButton.FlatAppearance.BorderColor = Color.FromArgb(180, 80, 80);
        _restoreButton.BackColor = Color.FromArgb(65, 30, 32);
        _restoreButton.ForeColor = Color.White;
        _restoreButton.Font = new Font("Segoe UI Semibold", 11F, FontStyle.Bold);
        _restoreButton.Click += async (_, _) => await RestoreAsync(true);

        _autoRestore.Text = "Автоматично відновити все після виходу зі Star Citizen";
        _autoRestore.Location = new Point(34, 317);
        _autoRestore.Size = new Size(620, 30);
        _autoRestore.Checked = true;
        _autoRestore.ForeColor = Color.Gainsboro;

        _statusLabel.Location = new Point(34, 356);
        _statusLabel.Size = new Size(700, 28);
        _statusLabel.Text = "Готово";
        _statusLabel.ForeColor = Color.LightGreen;
        _statusLabel.Font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold);

        _log.Location = new Point(32, 392);
        _log.Size = new Size(706, 175);
        _log.Multiline = true;
        _log.ReadOnly = true;
        _log.ScrollBars = ScrollBars.Vertical;
        _log.BackColor = Color.FromArgb(10, 12, 15);
        _log.ForeColor = Color.Gainsboro;
        _log.BorderStyle = BorderStyle.FixedSingle;
        _log.Font = new Font("Consolas", 9F);

        var note = new Label
        {
            Text = "STREAM MODE не закриває OBS, Discord, SteelSeries/Sonar, NVIDIA та Stream Control.",
            AutoSize = true,
            Location = new Point(34, 578),
            ForeColor = Color.DarkGray,
            Font = new Font("Segoe UI", 8.5F)
        };

        Controls.AddRange(new Control[]
        {
            title, subtitle, _ramLabel, _streamButton, _gameButton,
            _restoreButton, _autoRestore, _statusLabel, _log, note
        });
    }

    private static void StylePrimaryButton(Button button)
    {
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 1;
        button.FlatAppearance.BorderColor = Color.FromArgb(60, 140, 110);
        button.BackColor = Color.FromArgb(25, 55, 48);
        button.ForeColor = Color.White;
        button.Font = new Font("Segoe UI Semibold", 11F, FontStyle.Bold);
    }

    private async Task StartModeAsync(string mode)
    {
        if (_modeActive)
        {
            MessageBox.Show("Режим уже активний. Спочатку натисни «ВІДНОВИТИ ВСЕ».",
                "TiHiY Game Mode", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (mode == "GAME ONLY")
        {
            var confirm = MessageBox.Show(
                "GAME ONLY закриє OBS і поширені браузери/лаунчери.\r\n\r\nПродовжити?",
                "TiHiY Game Mode", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (confirm != DialogResult.Yes) return;
        }

        if (File.Exists(StateFile))
        {
            var answer = MessageBox.Show(
                "Є збережений стан від попереднього запуску. Спочатку відновити його?",
                "TiHiY Game Mode", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (answer == DialogResult.Yes)
                await RestoreAsync(false);
            else
                return;
        }

        SetButtons(false);
        _activeMode = mode;
        _statusLabel.Text = $"Запуск {mode}...";
        _statusLabel.ForeColor = Color.Gold;

        var before = MemoryInfo.GetAvailableGb();
        AppendLog($"=== {mode} START ===");
        AppendLog($"Вільно RAM до: {before:0.00} ГБ");

        var state = new SavedState
        {
            Mode = mode,
            StartedUtc = DateTime.UtcNow,
            Processes = new List<SavedProcess>(),
            Services = new List<string>()
        };

        var targets = new List<string>(StreamProcesses);
        if (mode == "GAME ONLY") targets.AddRange(GameOnlyProcesses);

        await Task.Run(() =>
        {
            foreach (var name in targets.Distinct(StringComparer.OrdinalIgnoreCase))
                StopProcessByName(name, state);

            foreach (var service in ServicesToPause)
                StopServiceIfRunning(service, state);

            SaveState(state);
        });

        await Task.Delay(800);
        var after = MemoryInfo.GetAvailableGb();
        AppendLog($"Вільно RAM після: {after:0.00} ГБ");
        AppendLog($"Звільнено: {(after - before):0.00} ГБ");

        _modeActive = true;
        _gameSeen = false;
        _missTicks = 0;
        _statusLabel.Text = $"{mode} активний — очікую Star Citizen";
        _statusLabel.ForeColor = Color.LightGreen;
        _monitorTimer.Start();
        SetButtons(true);
        UpdateRam();
    }

    private void StopProcessByName(string name, SavedState state)
    {
        try
        {
            foreach (var process in Process.GetProcessesByName(name))
            {
                try
                {
                    string? path = null;
                    try { path = process.MainModule?.FileName; } catch { }

                    if (!string.IsNullOrWhiteSpace(path) && ProcessUtils.IsWindowsCorePath(path))
                    {
                        AppendLogSafe($"SKIP Windows: {process.ProcessName}");
                        continue;
                    }

                    state.Processes.Add(new SavedProcess { Name = process.ProcessName, Path = path });
                    process.Kill(true);
                    process.WaitForExit(2000);
                    AppendLogSafe($"STOP process: {process.ProcessName}");
                }
                catch (Exception ex)
                {
                    AppendLogSafe($"SKIP process {name}: {ex.Message}");
                }
                finally
                {
                    process.Dispose();
                }
            }
        }
        catch (Exception ex)
        {
            AppendLogSafe($"Process error {name}: {ex.Message}");
        }
    }

    private void StopServiceIfRunning(string service, SavedState state)
    {
        try
        {
            var query = ProcessUtils.RunHidden("sc.exe", $"query \"{service}\"");
            if (!ProcessUtils.ServiceQueryShowsRunning(query)) return;

            state.Services.Add(service);
            ProcessUtils.RunHidden("sc.exe", $"stop \"{service}\"");
            AppendLogSafe($"STOP service: {service}");
        }
        catch (Exception ex)
        {
            AppendLogSafe($"Service error {service}: {ex.Message}");
        }
    }

    private async Task RestoreAsync(bool userInitiated)
    {
        _monitorTimer.Stop();

        if (!File.Exists(StateFile))
        {
            _modeActive = false;
            _statusLabel.Text = "Немає збереженого стану — все вже відновлено";
            _statusLabel.ForeColor = Color.LightGreen;
            SetButtons(true);
            if (userInitiated)
                MessageBox.Show("Немає збережених змін для відновлення.", "TiHiY Game Mode");
            return;
        }

        SetButtons(false);
        _statusLabel.Text = "Відновлення...";
        _statusLabel.ForeColor = Color.Gold;
        AppendLog("=== RESTORE START ===");

        SavedState? state = null;
        try
        {
            var json = await File.ReadAllTextAsync(StateFile);
            state = JsonSerializer.Deserialize<SavedState>(json);
        }
        catch (Exception ex)
        {
            AppendLog($"Не вдалося прочитати state.json: {ex.Message}");
        }

        if (state is not null)
        {
            await Task.Run(() =>
            {
                foreach (var service in state.Services.Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    try
                    {
                        ProcessUtils.RunHidden("sc.exe", $"start \"{service}\"");
                        AppendLogSafe($"RESTORE service: {service}");
                    }
                    catch (Exception ex)
                    {
                        AppendLogSafe($"Restore service {service}: {ex.Message}");
                    }
                }

                foreach (var saved in state.Processes
                    .Where(p => !string.IsNullOrWhiteSpace(p.Path))
                    .GroupBy(p => p.Path!, StringComparer.OrdinalIgnoreCase)
                    .Select(g => g.First()))
                {
                    try
                    {
                        if (!File.Exists(saved.Path!)) continue;
                        if (ProcessUtils.IsRunning(saved.Name)) continue;

                        Process.Start(new ProcessStartInfo(saved.Path!) { UseShellExecute = true });
                        AppendLogSafe($"RESTORE process: {saved.Name}");
                    }
                    catch (Exception ex)
                    {
                        AppendLogSafe($"Restore process {saved.Name}: {ex.Message}");
                    }
                }
            });
        }

        try { File.Delete(StateFile); } catch { }
        _modeActive = false;
        _gameSeen = false;
        _activeMode = string.Empty;
        AppendLog("=== RESTORE DONE ===");
        _statusLabel.Text = "Все відновлено";
        _statusLabel.ForeColor = Color.LightGreen;
        SetButtons(true);
        UpdateRam();
    }

    private async void MonitorTimer_Tick(object? sender, EventArgs e)
    {
        UpdateRam();
        var gameRunning = ProcessUtils.IsRunning("StarCitizen");

        if (gameRunning)
        {
            if (!_gameSeen)
            {
                _gameSeen = true;
                _missTicks = 0;
                _statusLabel.Text = $"{_activeMode} активний — Star Citizen працює";
                _statusLabel.ForeColor = Color.LightGreen;
                AppendLog("StarCitizen.exe знайдено.");
            }
            else
            {
                _missTicks = 0;
            }
            return;
        }

        if (!_gameSeen) return;

        _missTicks++;
        if (_missTicks < 5) return;

        _monitorTimer.Stop();
        AppendLog("Star Citizen закрито.");

        if (_autoRestore.Checked)
            await RestoreAsync(false);
        else
        {
            _statusLabel.Text = "Star Citizen закрито — натисни «ВІДНОВИТИ ВСЕ»";
            _statusLabel.ForeColor = Color.Gold;
        }
    }

    private void SaveState(SavedState state)
    {
        var json = JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(StateFile, json);
    }

    private void SetButtons(bool enabled)
    {
        if (InvokeRequired)
        {
            BeginInvoke(() => SetButtons(enabled));
            return;
        }

        _streamButton.Enabled = enabled && !_modeActive;
        _gameButton.Enabled = enabled && !_modeActive;
        _restoreButton.Enabled = enabled;
    }

    private void UpdateRam()
    {
        var m = MemoryInfo.Get();
        _ramLabel.Text = $"RAM: {m.UsedGb:0.0} / {m.TotalGb:0.0} ГБ  •  вільно {m.AvailableGb:0.0} ГБ  •  {m.PercentUsed:0}%";
        _ramLabel.ForeColor = m.PercentUsed >= 90 ? Color.OrangeRed :
            m.PercentUsed >= 80 ? Color.Gold : Color.LightGreen;
    }

    private void AppendLog(string text)
    {
        if (InvokeRequired)
        {
            BeginInvoke(() => AppendLog(text));
            return;
        }

        var line = $"[{DateTime.Now:HH:mm:ss}] {text}";
        _log.AppendText(line + Environment.NewLine);
        try
        {
            File.AppendAllText(LogFile,
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {text}{Environment.NewLine}");
        }
        catch { }
    }

    private void AppendLogSafe(string text) => AppendLog(text);

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (_modeActive && File.Exists(StateFile))
        {
            var answer = MessageBox.Show(
                "Game Mode ще активний. Відновити тимчасово зупинені процеси та служби перед виходом?",
                "TiHiY Game Mode", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Warning);

            if (answer == DialogResult.Cancel)
            {
                e.Cancel = true;
                return;
            }

            if (answer == DialogResult.Yes)
            {
                e.Cancel = true;
                _ = RestoreThenCloseAsync();
                return;
            }
        }

        base.OnFormClosing(e);
    }

    private async Task RestoreThenCloseAsync()
    {
        await RestoreAsync(false);
        _modeActive = false;
        Close();
    }
}
