using TiHiY.StreamControlCenter.Services;
using TiHiY.StreamControlCenter.UI;

namespace TiHiY.StreamControlCenter;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();

        using var settingsService = new SettingsService();
        using var overlayController = new OverlayController();
        using var mainForm = new MainForm(settingsService, overlayController);

        Application.ApplicationExit += (_, _) => overlayController.Dispose();
        Application.Run(mainForm);
    }
}
