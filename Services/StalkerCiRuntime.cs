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
            await Task.Delay(35).ConfigureAwait(false);
            var application = Application.Current;
            if (application is null) continue;

            try
            {
                var ready = await application.Dispatcher.InvokeAsync(() =>
                    App.Services is not null && application.MainWindow is not null && application.MainWindow.IsLoaded);
                if (!ready) continue;

                await ApplyThemeAsync(application, "Україна", "CI roundtrip 1/4: Ukraine base restored.");
                await Task.Delay(110).ConfigureAwait(false);
                await ApplyThemeAsync(application, "Сталкер", "CI roundtrip 2/4: Stalker textures applied.");
                await Task.Delay(110).ConfigureAwait(false);
                await ApplyThemeAsync(application, "Україна", "CI roundtrip 3/4: Ukraine restored after Stalker.");
                await Task.Delay(110).ConfigureAwait(false);
                await ApplyThemeAsync(application, "Сталкер", "CI roundtrip 4/4: final Stalker capture state.");
                await Task.Delay(260).ConfigureAwait(false);
                return;
            }
            catch
            {
                // Startup is still constructing services or the main window.
            }
        }
    }

    private static async Task ApplyThemeAsync(Application application, string theme, string logMessage)
    {
        await application.Dispatcher.InvokeAsync(() =>
        {
            App.Services.Theme.Apply(theme, save: false);
            App.Services.Settings.Value.UiTheme = theme;
            App.Services.Logger.Info(logMessage);
        }, DispatcherPriority.Send);
    }
}
