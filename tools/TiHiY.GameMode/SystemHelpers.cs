using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace TiHiY.GameMode;

public sealed class SavedState
{
    public string Mode { get; set; } = string.Empty;
    public DateTime StartedUtc { get; set; }
    public List<SavedProcess> Processes { get; set; } = new();
    public List<string> Services { get; set; } = new();
}

public sealed class SavedProcess
{
    public string Name { get; set; } = string.Empty;
    public string? Path { get; set; }
}

internal static class ProcessUtils
{
    public static bool IsWindowsCorePath(string path)
    {
        var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var system32 = Path.Combine(windows, "System32") + Path.DirectorySeparatorChar;
        var syswow64 = Path.Combine(windows, "SysWOW64") + Path.DirectorySeparatorChar;

        return path.StartsWith(system32, StringComparison.OrdinalIgnoreCase) ||
               path.StartsWith(syswow64, StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsRunning(string processName)
    {
        Process[]? processes = null;
        try
        {
            processes = Process.GetProcessesByName(processName);
            return processes.Length > 0;
        }
        finally
        {
            if (processes is not null)
                foreach (var process in processes)
                    process.Dispose();
        }
    }

    public static string RunHidden(string file, string args)
    {
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = file,
            Arguments = args,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        process.Start();
        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit(5000);
        return output + error;
    }

    public static bool ServiceQueryShowsRunning(string output)
    {
        if (output.Contains("RUNNING", StringComparison.OrdinalIgnoreCase)) return true;
        return Regex.IsMatch(output, @"STATE\s*:\s*4\b", RegexOptions.IgnoreCase) ||
               Regex.IsMatch(output, @"\b4\s+RUNNING\b", RegexOptions.IgnoreCase);
    }
}

internal static class MemoryInfo
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private sealed class MEMORYSTATUSEX
    {
        public uint dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern bool GlobalMemoryStatusEx([In, Out] MEMORYSTATUSEX lpBuffer);

    public static (double TotalGb, double AvailableGb, double UsedGb, double PercentUsed) Get()
    {
        var status = new MEMORYSTATUSEX();
        if (!GlobalMemoryStatusEx(status)) return (0, 0, 0, 0);

        var total = status.ullTotalPhys / 1073741824.0;
        var available = status.ullAvailPhys / 1073741824.0;
        var used = total - available;
        var percent = total <= 0 ? 0 : used / total * 100.0;
        return (total, available, used, percent);
    }

    public static double GetAvailableGb() => Get().AvailableGb;
}
