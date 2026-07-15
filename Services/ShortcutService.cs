using System.Runtime.InteropServices;

namespace TiHiY.StreamControlCenter.Services;

public static class ShortcutService
{
    public const string ShortcutFileName = "TiHiY StreamControl Center.lnk";

    public static bool EnsureDesktopShortcut(AppLogger? logger = null)
    {
        if (!OperatingSystem.IsWindows()) return false;
        var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        if (string.IsNullOrWhiteSpace(desktop)) return false;
        return EnsureShortcut(Path.Combine(desktop, ShortcutFileName), logger);
    }

    public static bool EnsureShortcut(string shortcutPath, AppLogger? logger = null)
    {
        if (!OperatingSystem.IsWindows() || string.IsNullOrWhiteSpace(shortcutPath)) return false;

        object? shell = null;
        object? shortcut = null;
        try
        {
            var executablePath = Environment.ProcessPath;
            if (string.IsNullOrWhiteSpace(executablePath) || !File.Exists(executablePath))
                return false;

            shortcutPath = Path.GetFullPath(shortcutPath);
            var shortcutFolder = Path.GetDirectoryName(shortcutPath);
            if (string.IsNullOrWhiteSpace(shortcutFolder)) return false;
            Directory.CreateDirectory(shortcutFolder);

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

            var created = File.Exists(shortcutPath) && new FileInfo(shortcutPath).Length > 0;
            if (created)
                logger?.Info($"Ярлик програми готовий: {shortcutPath}");
            else
                logger?.Info($"Ярлик програми не підтверджено після збереження: {shortcutPath}");
            return created;
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
