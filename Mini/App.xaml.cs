using System.Threading;

namespace TiHiY.StreamControlMini;

public partial class App : Application
{
    private static Mutex? _mutex;
    private static bool _ownsMutex;
    public static MiniServices Services { get; private set; } = null!;

    protected override async void OnStartup(StartupEventArgs e)
    {
        _mutex = new Mutex(true, "Local\\TiHiY.StreamControlMini.SingleInstance", out _ownsMutex);
        if (!_ownsMutex)
        {
            MessageBox.Show("TiHiY StreamControl MINI вже запущено.", "TiHiY StreamControl MINI", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown(0);
            return;
        }

        base.OnStartup(e);
        try
        {
            Services = new MiniServices();
            await Services.InitializeAsync();
            MainWindow = new MainWindow();
            MainWindow.Show();
        }
        catch (Exception ex)
        {
            try { Services?.Logger.Error("Запуск MINI", ex); } catch { }
            MessageBox.Show(ex.GetBaseException().Message, "TiHiY StreamControl MINI — помилка запуску", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            if (Services is not null)
            {
                var cleanup = Task.Run(async () => await Services.DisposeAsync().ConfigureAwait(false));
                cleanup.Wait(TimeSpan.FromSeconds(5));
            }
        }
        catch { }
        finally
        {
            try { if (_ownsMutex) _mutex?.ReleaseMutex(); } catch { }
            _mutex?.Dispose();
        }
        base.OnExit(e);
    }
}
