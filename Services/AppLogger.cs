namespace TiHiY.StreamControlCenter.Services;

public sealed class AppLogger
{
    private readonly string _folder;
    private readonly string _file;
    private readonly object _gate = new();
    public ObservableCollection<string> Entries { get; } = new();

    public AppLogger()
    {
        _folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TiHiY", "StreamControlCenter", "Logs");
        Directory.CreateDirectory(_folder);
        _file = Path.Combine(_folder, $"TiHiY-{DateTime.Now:yyyy-MM-dd}.log");
    }

    public string Folder => _folder;

    public void Info(string message) => Write("INFO", message);
    public void Error(string message, Exception? exception = null) => Write("ERROR", exception is null ? message : $"{message} | {exception}");

    private void Write(string level, string message)
    {
        var line = $"[{DateTime.Now:HH:mm:ss}] [{level}] {message}";
        lock (_gate)
        {
            try { File.AppendAllText(_file, line + Environment.NewLine); } catch { }
        }
        Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
        {
            Entries.Add(line);
            while (Entries.Count > 500) Entries.RemoveAt(0);
        }));
    }
}
