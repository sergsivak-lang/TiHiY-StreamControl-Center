namespace TiHiY.StreamControlCenter.Services;

public sealed partial class AppServices
{
    public SystemMonitorService SystemMonitor { get; } = new();
}
