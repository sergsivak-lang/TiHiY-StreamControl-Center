namespace TiHiY.StreamControlCenter.Models;

public sealed class SystemMonitorSnapshot
{
    public DateTime Timestamp { get; init; } = DateTime.Now;
    public bool AidaAvailable { get; init; }
    public string SourceName { get; init; } = "Windows";
    public string StatusText { get; init; } = "AIDA64 не підключено";

    public double? CpuUsagePercent { get; init; }
    public double? CpuTemperatureC { get; init; }
    public double? CpuClockMhz { get; init; }
    public double? CpuPowerW { get; init; }
    public double? MemoryClockMhz { get; init; }

    public double? GpuUsagePercent { get; init; }
    public double? GpuTemperatureC { get; init; }
    public double? GpuClockMhz { get; init; }
    public double? GpuPowerW { get; init; }

    public double? RamUsagePercent { get; init; }
    public double? RamUsedGb { get; init; }
    public double? RamTotalGb { get; init; }

    public double? VramUsagePercent { get; init; }
    public double? VramUsedGb { get; init; }
    public double? VramTotalGb { get; init; }

    public double? DownloadMbps { get; init; }
    public double? UploadMbps { get; init; }
}
