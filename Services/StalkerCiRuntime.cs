using System.Runtime.CompilerServices;

namespace TiHiY.StreamControlCenter.Services;

internal static class StalkerCiRuntime
{
    [ModuleInitializer]
    internal static void InitializeModule()
    {
        if (!Environment.GetCommandLineArgs().Any(x =>
                string.Equals(x, "--ci-apply-stalker-theme", StringComparison.OrdinalIgnoreCase)))
            return;

        _ = ApplyWhenReadyAsync();
    }

    private static async Task ApplyWhenReadyAsync()
    {
        for (var attempt = 0; attempt < 300; attempt++)
        {
            await Task.Delay(50).ConfigureAwait(false);
            var application = Application.Current;
            if (application is null) continue;

            try
            {
                var applied = await application.Dispatcher.InvokeAsync(() =>
                {
                    if (App.Services is null || application.MainWindow is null) return false;
                    App.Services.Theme.Apply("Сталкер", save: false);
                    App.Services.Settings.Value.UiTheme = "Сталкер";
                    App.Services.Logger.Info("CI: Stalker theme applied.");
                    return true;
                });
                if (applied) return;
            }
            catch
            {
                // Startup is still constructing services or the main window.
            }
        }
    }
}
