using System.Threading;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using TiHiY.StreamControlCenter.Services;

namespace TiHiY.StreamControlCenter;

public partial class App : Application
{
    private static Mutex? _singleInstanceMutex;
    private static bool _ownsMutex;
    public static AppServices Services { get; private set; } = null!;

    protected override async void OnStartup(StartupEventArgs e)
    {
        var screenshotArg = e.Args.FirstOrDefault(x => x.StartsWith("--ci-screenshot=", StringComparison.OrdinalIgnoreCase));
        var screenshotPath = screenshotArg is null ? null : screenshotArg[(screenshotArg.IndexOf('=') + 1)..].Trim('"');
        var ciMode = !string.IsNullOrWhiteSpace(screenshotPath);

        _singleInstanceMutex = new Mutex(true, "Local\\TiHiY.StreamControlCenter.SingleInstance", out _ownsMutex);
        if (!_ownsMutex)
        {
            if (!ciMode)
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
            await Services.InitializeAsync();
            var main = new MainWindow();
            MainWindow = main;
            main.Show();

            if (ciMode)
            {
                main.Width = 1680;
                main.Height = 940;
                main.WindowState = WindowState.Normal;
                main.Left = 0;
                main.Top = 0;
                main.ApplyCiDemoState();
                await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
                await Task.Delay(900);
                SaveWindowScreenshot(main, screenshotPath!);
                Shutdown(0);
            }
        }
        catch (Exception ex)
        {
            Services.Logger.Error("Запуск програми", ex);
            if (!ciMode)
                MessageBox.Show($"Не вдалося запустити програму.\n\n{ex.Message}", "TiHiY StreamControl Center", MessageBoxButton.OK, MessageBoxImage.Error);
            else
                try { File.WriteAllText(Path.ChangeExtension(screenshotPath, ".error.txt"), ex.ToString()); } catch { }
            Shutdown(1);
        }
    }

    private static void SaveWindowScreenshot(Window window, string path)
    {
        window.UpdateLayout();
        var width = Math.Max(1, (int)Math.Ceiling(window.ActualWidth));
        var height = Math.Max(1, (int)Math.Ceiling(window.ActualHeight));
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(window);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        using var stream = File.Create(path);
        encoder.Save(stream);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            if (Services is not null)
            {
                var cleanupTask = Task.Run(async () => await Services.DisposeAsync().ConfigureAwait(false));
                if (!cleanupTask.Wait(TimeSpan.FromSeconds(5)))
                    Services.Logger.Info("Завершення: фонове очищення перевищило 5 секунд.");
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
        if (!Environment.GetCommandLineArgs().Any(x => x.StartsWith("--ci-screenshot=", StringComparison.OrdinalIgnoreCase)))
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
