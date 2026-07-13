using TiHiY.StreamControlCenter.Models;

namespace TiHiY.StreamControlCenter.Services;

public sealed class DonationService
{
    public ObservableCollection<DonationEvent> History { get; } = new();
    public event EventHandler<DonationEvent>? DonationAdded;

    public decimal TotalAmount => History.Sum(x => x.Amount);
    public decimal GoalAmount { get; set; } = 5000m;
    public double GoalProgress => GoalAmount <= 0 ? 0 : Math.Clamp((double)(TotalAmount / GoalAmount), 0, 1);

    public void Add(DonationEvent donation)
    {
        History.Add(donation);
        while (History.Count > 200) History.RemoveAt(0);
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
            IsHistorical = false
        };
        Add(replay);
        return replay;
    }

    public DonationEvent AddTestDonation()
    {
        var donation = new DonationEvent
        {
            Source = "TEST",
            User = "ViperUA",
            Amount = 200m,
            Currency = "UAH",
            Message = "Тестове сповіщення донату для панелі та OBS alert overlay.",
            ExternalId = "test:" + Guid.NewGuid().ToString("N"),
            Accent = "#FFD329"
        };
        Add(donation);
        return donation;
    }
}
