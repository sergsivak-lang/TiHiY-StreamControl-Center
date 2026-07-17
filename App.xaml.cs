using System.Threading;
using System.Diagnostics;
using System.Windows.Markup;
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
        var openSettingsInCi = e.Args.Any(x => string.Equals(x, "--ci-open-settings", StringComparison.OrdinalIgnoreCase));
        var themeArg = e.Args.FirstOrDefault(x => x.StartsWith("--ci-theme=", StringComparison.OrdinalIgnoreCase));
        var ciTheme = themeArg is null ? null : themeArg[(themeArg.IndexOf('=') + 1)..].Trim('"');

        _singleInstanceMutex = new Mutex(true, "Local\\TiHiY.StreamControlCenter.SingleInstance", out _ownsMutex);
        if (!_ownsMutex)
        {
            if (!ciMode)
                MessageBox.Show("TiHiY Stream Control Center вже запущено. Закрийте або відкрийте існуюче вікно програми.", "TiHiY Stream Control Center", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown(0);
            return;
        }

        base.OnStartup(e);
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
        try
        {
            WriteStartupStage("01 Services construction");
            Services = new AppServices();
            Services.Theme.ThemeChanged += (_, _) =>
                StalkerTextureThemeManager.ApplyFor(Services.Theme.CurrentTheme);
            StalkerTextureThemeManager.ApplyFor(Services.Theme.CurrentTheme);
            WriteStartupStage("02 Services initialized in memory");
            await Services.InitializeAsync();
            WriteStartupStage("03 Background services initialized");

            if (ciMode && !string.IsNullOrWhiteSpace(ciTheme))
            {
                Services.Theme.Apply(ciTheme, save: false);
                StalkerTextureThemeManager.ApplyFor(ciTheme);
                WriteStartupStage($"03.1 CI theme applied: {ciTheme}");
            }

            var main = new MainWindow();
            WriteStartupStage("04 MainWindow constructed");
            MainWindow = main;
            main.Show();
            WriteStartupStage("05 MainWindow shown");

            if (ciMode)
            {
                main.Width = 1672;
                main.Height = 941;
                main.WindowState = WindowState.Normal;
                main.Left = 0;
                main.Top = 0;
                main.ApplyCiDemoState();

                Window captureWindow = main;
                if (openSettingsInCi)
                {
                    Services.Settings.Value.UiTheme = string.IsNullOrWhiteSpace(ciTheme) ? "Україна" : ciTheme;
                    var settings = new TiHiY.StreamControlCenter.Windows.SettingsWindow
                    {
                        Owner = main,
                        Width = 1140,
                        Height = 730,
                        WindowState = WindowState.Normal,
                        Left = 0,
                        Top = 0
                    };
                    settings.Show();
                    captureWindow = settings;
                    WriteStartupStage("06 SettingsWindow shown in CI");
                }

                await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
                await Task.Delay(900);

                // Background connection/status callbacks can refresh the collections after
                // the first demo-state pass. Reapply immediately before rendering so the
                // screenshot always represents the approved populated dashboard.
                if (ReferenceEquals(captureWindow, main))
                {
                    main.ApplyCiDemoState();
                    await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
                    main.UpdateLayout();
                }

                SaveWindowScreenshot(captureWindow, screenshotPath!);
                if (!ReferenceEquals(captureWindow, main)) captureWindow.Close();
                Shutdown(0);
            }
        }
        catch (Exception ex)
        {
            try { Services?.Logger.Error("Запуск програми", ex); } catch { }
            var crashFile = WriteStartupCrashFile(ex);
            if (!ciMode)
                MessageBox.Show(
                    BuildStartupErrorMessage(ex, crashFile),
                    "TiHiY Stream Control Center",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            else if (!string.IsNullOrWhiteSpace(screenshotPath))
                try
                {
                    var errorPath = Path.ChangeExtension(screenshotPath, ".error.txt");
                    if (!string.IsNullOrWhiteSpace(errorPath))
                        File.WriteAllText(errorPath, ex.ToString());
                }
                catch { }
            Shutdown(1);
        }
    }

    private static string BuildStartupErrorMessage(Exception ex, string crashFile)
    {
        var root = ex.GetBaseException();
        var location = ex is XamlParseException xaml
            ? $"\nXAML line: {xaml.LineNumber}, position: {xaml.LinePosition}"
            : string.Empty;
        return $"Не вдалося запустити програму.\n\n{ex.GetType().Name}: {ex.Message}{location}\n\nRoot cause: {root.GetType().Name}: {root.Message}\n\nЖурнал помилки:\n{crashFile}";
    }

    private static void WriteStartupStage(string stage)
    {
        try
        {
            var folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "TiHiY", "StreamControlCenter", "Logs");
            Directory.CreateDirectory(folder);
            File.AppendAllText(Path.Combine(folder, "startup-stage-latest.txt"),
                $"{DateTime.Now:O} {stage}{Environment.NewLine}");
        }
        catch { }
    }

    private static string WriteStartupCrashFile(Exception ex)
    {
        try
        {
            var folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "TiHiY", "StreamControlCenter", "Logs");
            Directory.CreateDirectory(folder);
            var file = Path.Combine(folder, "startup-crash-latest.txt");
            File.WriteAllText(file, $"{DateTime.Now:O}{Environment.NewLine}{ex}");
            return file;
        }
        catch
        {
            try
            {
                var file = Path.Combine(Path.GetTempPath(), "TiHiY-StreamControlCenter-startup-crash.txt");
                File.WriteAllText(file, ex.ToString());
                return file;
            }
            catch { return "журнал створити не вдалося"; }
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
            MessageBox.Show($"Модуль повідомив про помилку, але програма продовжує роботу.\n\n{e.Exception.Message}\n\nПодробиці записані в журнал.", "TiHiY Stream Control Center", MessageBoxButton.OK, MessageBoxImage.Error);
    }

    private void OnDomainUnhandledException(object? sender, UnhandledExceptionEventArgs e) =>
        Services?.Logger.Error("Критична помилка", e.ExceptionObject as Exception);

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        Services?.Logger.Error("Помилка фонової операції", e.Exception);
        e.SetObserved();
    }
}