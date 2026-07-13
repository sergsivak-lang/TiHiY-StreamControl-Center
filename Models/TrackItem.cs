namespace TiHiY.StreamControlCenter.Models;

public sealed class TrackItem
{
    public string FilePath { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Artist { get; set; } = string.Empty;
    public string FileName => Path.GetFileName(FilePath);
    public string Display => string.IsNullOrWhiteSpace(Artist) ? Title : $"{Artist} — {Title}";

    public static TrackItem FromPath(string path)
    {
        var raw = Path.GetFileNameWithoutExtension(path);
        var parts = raw.Split(new[] { " - " }, 2, StringSplitOptions.TrimEntries);
        return new TrackItem
        {
            FilePath = path,
            Artist = parts.Length == 2 ? parts[0] : string.Empty,
            Title = parts.Length == 2 ? parts[1] : raw
        };
    }
}
