using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using TiHiY.StreamControlCenter.Models;

namespace TiHiY.StreamControlCenter.Services;

public sealed class SystemMonitorService
{
    private DateTime _lastNetworkSample = DateTime.UtcNow;
    private long _lastReceivedBytes;
    private long _lastSentBytes;

    public SystemMonitorSnapshot ReadSnapshot(bool aida64Enabled)
    {
        var nowUtc = DateTime.UtcNow;
        var (receivedBytes, sentBytes) = ReadNetworkTotals();
        var elapsedSeconds = Math.Max(0.001, (nowUtc - _lastNetworkSample).TotalSeconds);

        double? downloadMbps = null;
        double? uploadMbps = null;
        if (_lastReceivedBytes > 0 || _lastSentBytes > 0)
        {
            downloadMbps = Math.Max(0, receivedBytes - _lastReceivedBytes) * 8d / elapsedSeconds / 1_000_000d;
            uploadMbps = Math.Max(0, sentBytes - _lastSentBytes) * 8d / elapsedSeconds / 1_000_000d;
        }

        _lastNetworkSample = nowUtc;
        _lastReceivedBytes = receivedBytes;
        _lastSentBytes = sentBytes;

        var memory = ReadMemory();
        double? ramUsedGb = memory.totalGb.HasValue && memory.availableGb.HasValue
            ? Math.Max(0, memory.totalGb.Value - memory.availableGb.Value)
            : null;
        double? ramUsage = memory.totalGb > 0 && ramUsedGb.HasValue
            ? ramUsedGb.Value / memory.totalGb.Value * 100d
            : null;

        return new SystemMonitorSnapshot
        {
            Timestamp = DateTime.Now,
            AidaAvailable = false,
            StatusText = aida64Enabled
                ? "AIDA64 Shared Memory недоступна; показано доступні метрики Windows."
                : "Показано доступні метрики Windows.",
            RamUsagePercent = ramUsage,
            RamUsedGb = ramUsedGb,
            RamTotalGb = memory.totalGb,
            DownloadMbps = downloadMbps,
            UploadMbps = uploadMbps
        };
    }

    private static (long received, long sent) ReadNetworkTotals()
    {
        long received = 0;
        long sent = 0;
        foreach (var networkInterface in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (networkInterface.OperationalStatus != OperationalStatus.Up ||
                networkInterface.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                continue;

            try
            {
                var stats = networkInterface.GetIPv4Statistics();
                received += stats.BytesReceived;
                sent += stats.BytesSent;
            }
            catch
            {
                // A transient adapter failure must not break the dashboard refresh.
            }
        }
        return (received, sent);
    }

    private static (double? totalGb, double? availableGb) ReadMemory()
    {
        var status = new MemoryStatusEx();
        if (!GlobalMemoryStatusEx(status)) return (null, null);
        const double bytesPerGb = 1024d * 1024d * 1024d;
        return (status.TotalPhysical / bytesPerGb, status.AvailablePhysical / bytesPerGb);
    }

    [return: MarshalAs(UnmanagedType.Bool)]
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GlobalMemoryStatusEx([In, Out] MemoryStatusEx buffer);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private sealed class MemoryStatusEx
    {
        public uint Length = (uint)Marshal.SizeOf<MemoryStatusEx>();
        public uint MemoryLoad;
        public ulong TotalPhysical;
        public ulong AvailablePhysical;
        public ulong TotalPageFile;
        public ulong AvailablePageFile;
        public ulong TotalVirtual;
        public ulong AvailableVirtual;
        public ulong AvailableExtendedVirtual;
    }
}
