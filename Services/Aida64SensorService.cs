using System.Globalization;
using System.IO.MemoryMappedFiles;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using TiHiY.StreamControlCenter.Models;

namespace TiHiY.StreamControlCenter.Services;

/// <summary>
/// Reads live AIDA64 sensor values from the AIDA64_SensorValues shared-memory block.
/// When AIDA64 is unavailable, CPU/RAM/network values fall back to native Windows counters.
/// </summary>
public sealed class Aida64SensorService
{
    private const string MapName = "AIDA64_SensorValues";
    private const int MaxMapBytes = 1024 * 1024;
    private readonly AppLogger _logger;
    private readonly object _sync = new();
    private ulong _lastIdle;
    private ulong _lastKernel;
    private ulong _lastUser;
    private bool _hasCpuSample;
    private long _lastReceivedBytes;
    private long _lastSentBytes;
    private DateTime _lastNetworkSample = DateTime.UtcNow;
    private DateTime _lastAidaErrorLog = DateTime.MinValue;
    private bool _aidaWasAvailable;

    public Aida64SensorService(AppLogger logger) => _logger = logger;

    public SystemMonitorSnapshot ReadSnapshot(bool useAida)
    {
        lock (_sync)
        {
            var fallback = ReadWindowsFallback();
            if (!useAida)
                return WithStatus(fallback, "Windows", "AIDA64 вимкнено", false);

            try
            {
                var sensors = ReadAidaSensors();
                if (sensors.Count == 0)
                {
                    SetAidaAvailability(false, "AIDA64 Shared Memory порожня");
                    return WithStatus(fallback, "Windows", "AIDA64 Shared Memory порожня", false);
                }

                SetAidaAvailability(true, $"отримано {sensors.Count} датчиків");
                return BuildAidaSnapshot(sensors, fallback);
            }
            catch (FileNotFoundException)
            {
                SetAidaAvailability(false, "Shared Memory не знайдено");
                return WithStatus(fallback, "Windows", "AIDA64 Shared Memory не знайдено", false);
            }
            catch (Exception ex)
            {
                if (DateTime.UtcNow - _lastAidaErrorLog > TimeSpan.FromMinutes(1))
                {
                    _lastAidaErrorLog = DateTime.UtcNow;
                    _logger.Error("AIDA64 Shared Memory", ex);
                }
                return WithStatus(fallback, "Windows", "AIDA64: " + ex.Message, false);
            }
        }
    }


    private void SetAidaAvailability(bool available, string details)
    {
        if (_aidaWasAvailable == available) return;
        _aidaWasAvailable = available;
        if (available) _logger.Info($"AIDA64 Shared Memory підключено: {details}");
        else _logger.Info($"AIDA64 Shared Memory відключено: {details}");
    }

    private static SystemMonitorSnapshot WithStatus(SystemMonitorSnapshot snapshot, string source, string status, bool available) =>
        new()
        {
            Timestamp = snapshot.Timestamp,
            AidaAvailable = available,
            SourceName = source,
            StatusText = status,
            CpuUsagePercent = snapshot.CpuUsagePercent,
            CpuTemperatureC = snapshot.CpuTemperatureC,
            CpuClockMhz = snapshot.CpuClockMhz,
            CpuPowerW = snapshot.CpuPowerW,
            MemoryClockMhz = snapshot.MemoryClockMhz,
            GpuUsagePercent = snapshot.GpuUsagePercent,
            GpuTemperatureC = snapshot.GpuTemperatureC,
            GpuClockMhz = snapshot.GpuClockMhz,
            GpuPowerW = snapshot.GpuPowerW,
            RamUsagePercent = snapshot.RamUsagePercent,
            RamUsedGb = snapshot.RamUsedGb,
            RamTotalGb = snapshot.RamTotalGb,
            VramUsagePercent = snapshot.VramUsagePercent,
            VramUsedGb = snapshot.VramUsedGb,
            VramTotalGb = snapshot.VramTotalGb,
            DownloadMbps = snapshot.DownloadMbps,
            UploadMbps = snapshot.UploadMbps
        };

    private SystemMonitorSnapshot BuildAidaSnapshot(IReadOnlyList<SensorValue> sensors, SystemMonitorSnapshot fallback)
    {
        var cpuUsage = Pick(sensors, "util", "scpuuti", "cpu utilization", "total cpu", "cpu usage", "cpu");
        var cpuTemp = Pick(sensors, "temp", "tcpupkg", "tcpu", "cpu package", "cpu diode", "cpu");
        var cpuClock = Pick(sensors, "clk", "ccpu", "cpu clock", "cpu core #1", "cpu core");
        var cpuPower = Pick(sensors, "pwr", "pcpupkg", "pcpu", "cpu package", "cpu");
        var memoryClock = Pick(sensors, "clk", "cdram", "cmem", "memory clock", "dram clock", "memory bus", "частота пам’яті", "частота пам'яті", "частота памяти");

        var gpuUsage = Pick(sensors, "util", "sgpu1uti", "gpu utilization", "gpu core", "gpu usage", "gpu");
        var gpuTemp = Pick(sensors, "temp", "tgpu1hot", "tgpu1diod", "gpu hotspot", "gpu diode", "gpu");
        var gpuClock = Pick(sensors, "clk", "cgpu1core", "gpu clock", "gpu core");
        var gpuPower = Pick(sensors, "pwr", "pgpu1", "gpu power", "gpu");

        var ramUsage = Pick(sensors, "util", "smemuti", "memory utilization", "physical memory", "system memory");
        var ramUsed = PickAny(sensors, "used physical memory", "physical memory used", "used memory");
        var ramTotal = PickAny(sensors, "total physical memory", "physical memory total", "total memory");

        var vramUsage = Pick(sensors, "util", "sgpu1memuti", "sgpu1muti", "gpu memory utilization", "video memory utilization", "vram utilization");
        var vramUsed = PickAny(sensors, "used gpu memory", "gpu memory used", "used video memory", "vram used");
        var vramTotal = PickAny(sensors, "total gpu memory", "gpu memory total", "total video memory", "vram total");

        var down = PickAny(sensors, "download rate", "nic download rate", "network download");
        var up = PickAny(sensors, "upload rate", "nic upload rate", "network upload");

        var ramUsedGb = ToGigabytes(ramUsed);
        var ramTotalGb = ToGigabytes(ramTotal);
        var vramUsedGb = ToGigabytes(vramUsed);
        var vramTotalGb = ToGigabytes(vramTotal);

        return new SystemMonitorSnapshot
        {
            AidaAvailable = true,
            SourceName = "AIDA64",
            StatusText = $"AIDA64 LIVE • {sensors.Count} датчиків",
            CpuUsagePercent = cpuUsage?.NumericValue ?? fallback.CpuUsagePercent,
            CpuTemperatureC = cpuTemp?.NumericValue,
            CpuClockMhz = NormalizeClockMhz(cpuClock),
            CpuPowerW = cpuPower?.NumericValue,
            MemoryClockMhz = NormalizeClockMhz(memoryClock),
            GpuUsagePercent = gpuUsage?.NumericValue,
            GpuTemperatureC = gpuTemp?.NumericValue,
            GpuClockMhz = NormalizeClockMhz(gpuClock),
            GpuPowerW = gpuPower?.NumericValue,
            RamUsagePercent = ramUsage?.NumericValue ?? fallback.RamUsagePercent,
            RamUsedGb = ramUsedGb ?? fallback.RamUsedGb,
            RamTotalGb = ramTotalGb ?? fallback.RamTotalGb,
            VramUsagePercent = vramUsage?.NumericValue ?? Percent(vramUsedGb, vramTotalGb),
            VramUsedGb = vramUsedGb,
            VramTotalGb = vramTotalGb,
            DownloadMbps = ToMegabitsPerSecond(down) ?? fallback.DownloadMbps,
            UploadMbps = ToMegabitsPerSecond(up) ?? fallback.UploadMbps,
            Timestamp = DateTime.Now
        };
    }

    private List<SensorValue> ReadAidaSensors()
    {
        using var map = MemoryMappedFile.OpenExisting(MapName, MemoryMappedFileRights.Read);
        using var accessor = map.CreateViewAccessor(0, 0, MemoryMappedFileAccess.Read);
        var length = (int)Math.Min(accessor.Capacity, MaxMapBytes);
        if (length <= 0) return new();
        var buffer = new byte[length];
        accessor.ReadArray(0, buffer, 0, buffer.Length);
        var xml = DecodeSharedMemory(buffer);
        if (string.IsNullOrWhiteSpace(xml)) return new();

        var document = ParseSensorDocument(xml);
        var values = new List<SensorValue>();
        foreach (var element in document.Descendants())
        {
            var labelElement = element.Elements().FirstOrDefault(x => x.Name.LocalName.Equals("label", StringComparison.OrdinalIgnoreCase));
            var valueElement = element.Elements().FirstOrDefault(x => x.Name.LocalName.Equals("value", StringComparison.OrdinalIgnoreCase));
            var label = (labelElement?.Value ?? element.Attribute("label")?.Value ?? string.Empty).Trim();
            var rawValue = (valueElement?.Value ?? element.Attribute("value")?.Value ?? string.Empty).Trim();
            if (label.Length == 0 || rawValue.Length == 0) continue;

            var idElement = element.Elements().FirstOrDefault(x => x.Name.LocalName.Equals("id", StringComparison.OrdinalIgnoreCase));
            var unitElement = element.Elements().FirstOrDefault(x => x.Name.LocalName.Equals("unit", StringComparison.OrdinalIgnoreCase));
            var detectedUnit = DetectUnit(rawValue);
            var explicitUnit = (unitElement?.Value ?? element.Attribute("unit")?.Value ?? string.Empty).Trim();
            values.Add(new SensorValue(
                element.Name.LocalName.ToLowerInvariant(),
                (idElement?.Value ?? element.Attribute("id")?.Value ?? string.Empty).Trim(),
                label,
                rawValue,
                ParseNumber(rawValue),
                detectedUnit.Length > 0 ? detectedUnit : explicitUnit));
        }
        return values;
    }

    private static string DecodeSharedMemory(byte[] buffer)
    {
        Encoding encoding;
        if (buffer.Length >= 2 && buffer[0] == 0xFF && buffer[1] == 0xFE)
            encoding = Encoding.Unicode;
        else if (buffer.Length >= 3 && buffer[0] == 0xEF && buffer[1] == 0xBB && buffer[2] == 0xBF)
            encoding = Encoding.UTF8;
        else if (buffer.Length >= 8 && buffer[1] == 0 && buffer[3] == 0 && buffer[5] == 0)
            encoding = Encoding.Unicode;
        else
            encoding = new UTF8Encoding(false, false);

        var text = encoding.GetString(buffer).TrimStart('\uFEFF');
        var end = text.IndexOf('\0');
        if (end >= 0) text = text[..end];
        var start = text.IndexOf('<');
        var close = text.LastIndexOf('>');
        if (start < 0 || close <= start) return string.Empty;

        var fragment = text.Substring(start, close - start + 1);
        var cleaned = new StringBuilder(fragment.Length);
        foreach (var ch in fragment)
        {
            if (System.Xml.XmlConvert.IsXmlChar(ch)) cleaned.Append(ch);
        }
        return cleaned.ToString().Trim();
    }

    private static XDocument ParseSensorDocument(string xml)
    {
        // AIDA64 Shared Memory normally exposes a sequence of XML sensor nodes
        // without one common root element. XDocument requires a root, so first
        // try a regular document and then parse the sensor stream as a fragment.
        var withoutDeclaration = Regex.Replace(
            xml,
            @"<\?xml[^>]*\?>",
            string.Empty,
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant).Trim();

        try
        {
            return XDocument.Parse(withoutDeclaration, LoadOptions.None);
        }
        catch (System.Xml.XmlException)
        {
            return XDocument.Parse($"<AIDA64SensorValues>{withoutDeclaration}</AIDA64SensorValues>", LoadOptions.None);
        }
    }

    private SystemMonitorSnapshot ReadWindowsFallback()
    {
        var cpu = ReadSystemCpuUsage();
        var memory = ReadMemory();
        var network = ReadNetworkRates();
        return new SystemMonitorSnapshot
        {
            SourceName = "Windows",
            StatusText = "Windows counters • увімкніть AIDA64 Shared Memory для температур",
            CpuUsagePercent = cpu,
            RamUsagePercent = memory.Percent,
            RamUsedGb = memory.UsedGb,
            RamTotalGb = memory.TotalGb,
            DownloadMbps = network.DownloadMbps,
            UploadMbps = network.UploadMbps,
            Timestamp = DateTime.Now
        };
    }

    private double? ReadSystemCpuUsage()
    {
        if (!GetSystemTimes(out var idleFt, out var kernelFt, out var userFt)) return null;
        var idle = ToUInt64(idleFt);
        var kernel = ToUInt64(kernelFt);
        var user = ToUInt64(userFt);
        if (!_hasCpuSample)
        {
            _lastIdle = idle; _lastKernel = kernel; _lastUser = user; _hasCpuSample = true;
            return null;
        }
        var idleDelta = idle - _lastIdle;
        var kernelDelta = kernel - _lastKernel;
        var userDelta = user - _lastUser;
        _lastIdle = idle; _lastKernel = kernel; _lastUser = user;
        var total = kernelDelta + userDelta;
        return total == 0 ? null : Math.Clamp((total - idleDelta) * 100d / total, 0, 100);
    }

    private static (double? Percent, double? UsedGb, double? TotalGb) ReadMemory()
    {
        var status = new MemoryStatusEx();
        if (!GlobalMemoryStatusEx(status)) return (null, null, null);
        var total = status.TotalPhys / 1024d / 1024d / 1024d;
        var available = status.AvailPhys / 1024d / 1024d / 1024d;
        return (status.MemoryLoad, Math.Max(0, total - available), total);
    }

    private (double? DownloadMbps, double? UploadMbps) ReadNetworkRates()
    {
        long received = 0, sent = 0;
        foreach (var adapter in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (adapter.OperationalStatus != OperationalStatus.Up || adapter.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
            try
            {
                var stats = adapter.GetIPStatistics();
                received += stats.BytesReceived;
                sent += stats.BytesSent;
            }
            catch { }
        }
        var now = DateTime.UtcNow;
        var seconds = Math.Max(0.001, (now - _lastNetworkSample).TotalSeconds);
        double? down = null, up = null;
        if (_lastReceivedBytes > 0 || _lastSentBytes > 0)
        {
            down = Math.Max(0, received - _lastReceivedBytes) * 8d / seconds / 1_000_000d;
            up = Math.Max(0, sent - _lastSentBytes) * 8d / seconds / 1_000_000d;
        }
        _lastReceivedBytes = received;
        _lastSentBytes = sent;
        _lastNetworkSample = now;
        return (down, up);
    }

    private static SensorValue? Pick(IReadOnlyList<SensorValue> sensors, string type, params string[] priorities)
    {
        var typed = sensors.Where(x => x.Type.Equals(type, StringComparison.OrdinalIgnoreCase)).ToList();
        foreach (var priority in priorities)
        {
            var match = typed.FirstOrDefault(x => Match(x, priority));
            if (match is not null) return match;
        }
        return null;
    }

    private static SensorValue? PickAny(IReadOnlyList<SensorValue> sensors, params string[] priorities)
    {
        foreach (var priority in priorities)
        {
            var match = sensors.FirstOrDefault(x => Match(x, priority));
            if (match is not null) return match;
        }
        return null;
    }

    private static bool Match(SensorValue sensor, string phrase) =>
        sensor.Label.Contains(phrase, StringComparison.OrdinalIgnoreCase) ||
        sensor.Id.Contains(phrase.Replace(" ", string.Empty), StringComparison.OrdinalIgnoreCase);

    private static double? NormalizeClockMhz(SensorValue? value)
    {
        if (value?.NumericValue is not double number) return null;
        if (value.Unit.Contains("GHz", StringComparison.OrdinalIgnoreCase)) return number * 1000d;
        if (value.Unit.Contains("kHz", StringComparison.OrdinalIgnoreCase)) return number / 1000d;
        return number;
    }

    private static double? ToGigabytes(SensorValue? value)
    {
        if (value?.NumericValue is not double number) return null;
        if (value.Unit.Contains("TB", StringComparison.OrdinalIgnoreCase)) return number * 1024d;
        if (value.Unit.Contains("GB", StringComparison.OrdinalIgnoreCase)) return number;
        if (value.Unit.Contains("MB", StringComparison.OrdinalIgnoreCase)) return number / 1024d;
        if (value.Unit.Contains("KB", StringComparison.OrdinalIgnoreCase)) return number / 1024d / 1024d;
        if (value.Unit.Equals("B", StringComparison.OrdinalIgnoreCase)) return number / 1024d / 1024d / 1024d;
        return number > 1024 * 1024 ? number / 1024d / 1024d / 1024d : null;
    }

    private static double? ToMegabitsPerSecond(SensorValue? value)
    {
        if (value?.NumericValue is not double number) return null;
        var unit = value.Unit;
        if (unit.Contains("Gbps", StringComparison.OrdinalIgnoreCase)) return number * 1000d;
        if (unit.Contains("Mbps", StringComparison.OrdinalIgnoreCase)) return number;
        if (unit.Contains("Kbps", StringComparison.OrdinalIgnoreCase)) return number / 1000d;
        if (unit.Contains("GB/s", StringComparison.OrdinalIgnoreCase)) return number * 8000d;
        if (unit.Contains("MB/s", StringComparison.OrdinalIgnoreCase)) return number * 8d;
        if (unit.Contains("KB/s", StringComparison.OrdinalIgnoreCase)) return number * 8d / 1000d;
        if (unit.Contains("B/s", StringComparison.OrdinalIgnoreCase)) return number * 8d / 1_000_000d;
        return null;
    }

    private static double? Percent(double? used, double? total) => used.HasValue && total > 0 ? Math.Clamp(used.Value * 100d / total.Value, 0, 100) : null;

    private static double? ParseNumber(string value)
    {
        var match = Regex.Match(value.Replace(',', '.'), @"[-+]?\d+(?:\.\d+)?", RegexOptions.CultureInvariant);
        return match.Success && double.TryParse(match.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var result) ? result : null;
    }

    private static string DetectUnit(string value)
    {
        var match = Regex.Match(value, @"[-+]?\d+(?:[\.,]\d+)?\s*(.*)$");
        return match.Success ? match.Groups[1].Value.Trim() : string.Empty;
    }

    private static ulong ToUInt64(FileTime time) => ((ulong)time.HighDateTime << 32) | time.LowDateTime;

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetSystemTimes(out FileTime idleTime, out FileTime kernelTime, out FileTime userTime);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx([In, Out] MemoryStatusEx buffer);

    [StructLayout(LayoutKind.Sequential)]
    private struct FileTime
    {
        public uint LowDateTime;
        public uint HighDateTime;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private sealed class MemoryStatusEx
    {
        public uint Length = (uint)Marshal.SizeOf<MemoryStatusEx>();
        public uint MemoryLoad;
        public ulong TotalPhys;
        public ulong AvailPhys;
        public ulong TotalPageFile;
        public ulong AvailPageFile;
        public ulong TotalVirtual;
        public ulong AvailVirtual;
        public ulong AvailExtendedVirtual;
    }

    private sealed record SensorValue(string Type, string Id, string Label, string RawValue, double? NumericValue, string Unit);
}
