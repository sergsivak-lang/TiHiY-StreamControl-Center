using System.Runtime.InteropServices;

namespace TiHiY.StreamControlCenter.Services;

public static class ShortcutService
{
    private const string ShortcutFileName = "TiHiY StreamControl Center.lnk";

    public static bool EnsureDesktopShortcut(AppLogger? logger = null)
    {
        if (!OperatingSystem.IsWindows()) return false;

        object? shell = null;
        object? shortcut = null;
        try
        {
            var executablePath = Environment.ProcessPath;
            if (string.IsNullOrWhiteSpace(executablePath) || !File.Exists(executablePath))
                return false;

            var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            if (string.IsNullOrWhiteSpace(desktop)) return false;
            Directory.CreateDirectory(desktop);

            var shortcutPath = Path.Combine(desktop, ShortcutFileName);
            var shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType is null) return false;

            shell = Activator.CreateInstance(shellType);
            if (shell is null) return false;

            shortcut = shellType.InvokeMember(
                "CreateShortcut",
                System.Reflection.BindingFlags.InvokeMethod,
                binder: null,
                target: shell,
                args: [shortcutPath]);
            if (shortcut is null) return false;

            var shortcutType = shortcut.GetType();
            shortcutType.InvokeMember("TargetPath", System.Reflection.BindingFlags.SetProperty, null, shortcut, [executablePath]);
            shortcutType.InvokeMember("WorkingDirectory", System.Reflection.BindingFlags.SetProperty, null, shortcut, [Path.GetDirectoryName(executablePath) ?? AppContext.BaseDirectory]);
            shortcutType.InvokeMember("Description", System.Reflection.BindingFlags.SetProperty, null, shortcut, ["TiHiY StreamControl Center"]);
            shortcutType.InvokeMember("IconLocation", System.Reflection.BindingFlags.SetProperty, null, shortcut, [$"{executablePath},0"]);
            shortcutType.InvokeMember("WindowStyle", System.Reflection.BindingFlags.SetProperty, null, shortcut, [1]);
            shortcutType.InvokeMember("Save", System.Reflection.BindingFlags.InvokeMethod, null, shortcut, null);

            logger?.Info($"Ярлик програми готовий: {shortcutPath}");
            return true;
        }
        catch (Exception ex)
        {
            logger?.Error("Створення ярлика програми", ex);
            return false;
        }
        finally
        {
            try { if (shortcut is not null && Marshal.IsComObject(shortcut)) Marshal.FinalReleaseComObject(shortcut); } catch { }
            try { if (shell is not null && Marshal.IsComObject(shell)) Marshal.FinalReleaseComObject(shell); } catch { }
        }
    }
}
