using System.Threading;
using System.Windows.Threading;
using TiHiY.StreamControlCenter.Services;

namespace TiHiY.StreamControlCenter;

public partial class App : Application
{
    private static Mutex? _singleInstanceMutex;
    private static bool _ownsMutex;
    public static AppServices Services { get; private set; } = null!;
    public static bool IsRenderPreview { get; private set; }
    public static string PreviewOutputPath { get; private set; } = Path.Combine(AppContext.BaseDirectory, "Cyber-Amber-Preview.png");

    protected override async void OnStartup(StartupEventArgs e)
    {
        IsRenderPreview = e.Args.Any(arg => string.Equals(arg, "--render-preview", StringComparison.OrdinalIgnoreCase));
        var outputArg = e.Args.FirstOrDefault(arg => arg.StartsWith("--output=", StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(outputArg)) PreviewOutputPath = Path.GetFullPath(outputArg[9..].Trim().Trim('"'));

        var mutexName = IsRenderPreview ? "Local\\TiHiY.StreamControlCenter.PreviewRenderer" : "Local\\TiHiY.StreamControlCenter.SingleInstance";
        _singleInstanceMutex = new Mutex(true, mutexName, out _ownsMutex);
        if (!_ownsMutex)
        {
            MessageBox.Show("TiHiY StreamControl Center вже запущено. Закрийте або відкрийте існуюче вікно програми.", "TiHiY StreamControl Center", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown(0);
            return;
        }

        base.OnStartup(e);
        Services = new AppServices();
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
        try
        {
            if (!IsRenderPreview)
                await Services.InitializeAsync();

            var main = new MainWindow();
            MainWindow = main;
            main.Show();

            if (IsRenderPreview)
            {
                var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(900) };
                timer.Tick += (_, _) =>
                {
                    timer.Stop();
                    try
                    {
                        main.RenderPreview(PreviewOutputPath);
                        Shutdown(0);
                    }
                    catch (Exception ex)
                    {
                        try { File.WriteAllText(Path.ChangeExtension(PreviewOutputPath, ".error.txt"), ex.ToString()); } catch { }
                        Shutdown(2);
                    }
                };
                timer.Start();
            }
        }
        catch (Exception ex)
        {
            Services.Logger.Error("Запуск програми", ex);
            MessageBox.Show($"Не вдалося запустити програму.\n\n{ex.Message}", "TiHiY StreamControl Center", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (IsRenderPreview)
        {
            try { if (_ownsMutex) _singleInstanceMutex?.ReleaseMutex(); } catch { }
            _singleInstanceMutex?.Dispose();
            base.OnExit(e);
            return;
        }
        // OnExit runs on the WPF dispatcher thread. Waiting directly on async cleanup here
        // can deadlock when a continuation tries to return to that dispatcher. Run cleanup
        // on a worker thread and cap the wait so the process can never remain in Task Manager.
        try
        {
            if (Services is not null)
            {
                var cleanupTask = Task.Run(async () =>
                    await Services.DisposeAsync().ConfigureAwait(false));

                if (!cleanupTask.Wait(TimeSpan.FromSeconds(5)))
                    Services.Logger.Info("Завершення: фонове очищення перевищило 5 секунд. Процес буде закрито примусово Windows після виходу WPF.");
            }
        }
        catch (Exception ex)
        {
            try { Services?.Logger.Error("Завершення програми", ex); } catch { }
        }
        finally
        {
            try { if (_ownsMutex) _singleInstanceMutex?.ReleaseMutex(); } catch { }
            _singleInstanceMutex?.Dispose();
        }

        base.OnExit(e);
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Services?.Logger.Error("Необроблена помилка інтерфейсу", e.Exception);
        e.Handled = true;
        MessageBox.Show($"Модуль повідомив про помилку, але програма продовжує роботу.\n\n{e.Exception.Message}\n\nПодробиці записані в журнал.", "TiHiY StreamControl Center", MessageBoxButton.OK, MessageBoxImage.Error);
    }

    private void OnDomainUnhandledException(object? sender, UnhandledExceptionEventArgs e) =>
        Services?.Logger.Error("Критична помилка", e.ExceptionObject as Exception);

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        Services?.Logger.Error("Помилка фонової операції", e.Exception);
        e.SetObserved();
    }
}
