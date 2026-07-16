using System.Text.Json;
using TiHiY.StreamControlCenter.Models;

namespace TiHiY.StreamControlCenter.Services;

public sealed class DonationService
{
    private readonly string _historyFile;
    private readonly AppLogger? _logger;
    private readonly object _saveGate = new();
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    public ObservableCollection<DonationEvent> History { get; } = new();
    public event EventHandler<DonationEvent>? DonationAdded;

    public decimal ExternalTotalAmount { get; set; }
    public decimal GoalInitialAmount { get; set; }

    public decimal TotalAmount
    {
        get
        {
            var historyTotal = History.Where(CountsTowardGoal).Sum(x => x.Amount);
            var synchronizedTotal = Math.Max(historyTotal, ExternalTotalAmount);
            return Math.Max(Math.Max(0, GoalInitialAmount), synchronizedTotal);
        }
    }

    public decimal GoalAmount { get; set; } = 10000m;
    public string GoalCurrency { get; set; } = "UAH";
    public double GoalProgress => GoalAmount <= 0 ? 0 : Math.Clamp((double)(TotalAmount / GoalAmount), 0, 1);

    public DonationService(string? settingsFolder = null, AppLogger? logger = null)
    {
        var folder = string.IsNullOrWhiteSpace(settingsFolder)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TiHiY", "StreamControlCenter")
            : settingsFolder;
        _historyFile = Path.Combine(folder, "donations-history.json");
        _logger = logger;
        LoadHistory();
    }

    private bool CountsTowardGoal(DonationEvent item) =>
        item.Amount > 0 &&
        item.Currency.Equals(GoalCurrency, StringComparison.OrdinalIgnoreCase) &&
        !item.IsTest &&
        !item.IsReplay &&
        !item.Source.Equals("TEST", StringComparison.OrdinalIgnoreCase) &&
        !item.ExternalId.StartsWith("test:", StringComparison.OrdinalIgnoreCase) &&
        !item.ExternalId.StartsWith("replay:", StringComparison.OrdinalIgnoreCase) &&
        !item.Currency.Equals("SUB", StringComparison.OrdinalIgnoreCase) &&
        !item.Currency.Equals("MEMBER", StringComparison.OrdinalIgnoreCase);

    public IReadOnlyList<(string User, decimal Amount)> GetTopDonors(string period, int count)
    {
        var now = DateTime.Now;
        var cutoff = period.Trim().ToLowerInvariant() switch
        {
            "stream" => now.Date,
            "day" => now.Date,
            "month" => new DateTime(now.Year, now.Month, 1),
            _ => DateTime.MinValue
        };

        return History
            .Where(CountsTowardGoal)
            .Where(x => x.Time >= cutoff)
            .GroupBy(x => string.IsNullOrWhiteSpace(x.User) ? "Анонім" : x.User.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(x => (User: x.Key, Amount: x.Sum(y => y.Amount)))
            .OrderByDescending(x => x.Amount)
            .ThenBy(x => x.User, StringComparer.OrdinalIgnoreCase)
            .Take(Math.Clamp(count, 1, 30))
            .ToList();
    }

    public void Add(DonationEvent donation)
    {
        if (!string.IsNullOrWhiteSpace(donation.StableId) &&
            History.Any(x => string.Equals(x.StableId, donation.StableId, StringComparison.OrdinalIgnoreCase)))
            return;

        History.Add(donation);
        while (History.Count > 200) History.RemoveAt(0);
        SaveHistory();
        DonationAdded?.Invoke(this, donation);
    }

    public DonationEvent ReplayForOverlay(DonationEvent source)
    {
        var replay = new DonationEvent
        {
            Time = DateTime.Now,
            ExternalId = "replay:" + Guid.NewGuid().ToString("N"),
            Source = source.Source,
            Kind = source.Kind,
            User = source.User,
            Amount = source.Amount,
            Currency = source.Currency,
            Message = source.Message,
            Accent = source.Accent,
            ShowOnOverlay = true,
            IsHistorical = false,
            IsReplay = true
        };
        Add(replay);
        return replay;
    }

    public DonationEvent AddTestDonation()
    {
        var donation = new DonationEvent
        {
            Source = "TEST",
            User = "TEST DONOR",
            Amount = 200m,
            Currency = "UAH",
            Message = "TEST: перевірка панелі та OBS alert overlay.",
            ExternalId = "test:" + Guid.NewGuid().ToString("N"),
            Accent = "#FFD329",
            IsTest = true
        };
        Add(donation);
        return donation;
    }

    private void LoadHistory()
    {
        try
        {
            if (!File.Exists(_historyFile)) return;
            var items = JsonSerializer.Deserialize<List<DonationEvent>>(File.ReadAllText(_historyFile), _jsonOptions);
            if (items is null) return;
            foreach (var item in items.TakeLast(200)) History.Add(item);
        }
        catch (Exception ex)
        {
            _logger?.Error("Завантаження історії донатів", ex);
        }
    }

    private void SaveHistory()
    {
        try
        {
            lock (_saveGate)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_historyFile)!);
                var temp = _historyFile + ".tmp";
                File.WriteAllText(temp, JsonSerializer.Serialize(History.ToList(), _jsonOptions));
                File.Move(temp, _historyFile, true);
            }
        }
        catch (Exception ex)
        {
            _logger?.Error("Збереження історії донатів", ex);
        }
    }
}