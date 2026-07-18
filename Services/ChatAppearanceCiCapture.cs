using TiHiY.StreamControlCenter.Windows;

namespace TiHiY.StreamControlCenter.Services;

internal static class ChatAppearanceCiCapture
{
    private static bool _started;

    [ModuleInitializer]
    internal static void Register() =>
        EventManager.RegisterClassHandler(typeof(MainWindow), FrameworkElement.LoadedEvent, new RoutedEventHandler(OnMainLoaded));

    private static void OnMainLoaded(object sender, RoutedEventArgs e)
    {
        if (_started || sender is not MainWindow main) return;
        var arguments = Environment.GetCommandLineArgs();
        var explicitArgument = arguments.FirstOrDefault(x =>
            x.StartsWith("--ci-chat-appearance-screenshot=", StringComparison.OrdinalIgnoreCase));
        var normalScreenshot = arguments.FirstOrDefault(x =>
            x.StartsWith("--ci-screenshot=", StringComparison.OrdinalIgnoreCase));
        if (explicitArgument is null && normalScreenshot is null) return;

        string path;
        if (explicitArgument is not null)
            path = explicitArgument[(explicitArgument.IndexOf('=') + 1)..].Trim('"');
        else
        {
            var normalPath = normalScreenshot![(normalScreenshot.IndexOf('=') + 1)..].Trim('"');
            path = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(normalPath))!, "TiHiY-Chat-Appearance-Settings.png");
        }
        if (string.IsNullOrWhiteSpace(path)) return;
        _started = true;

        main.Dispatcher.BeginInvoke(new Action(async () =>
        {
            ChatAppearanceSettingsWindow? window = null;
            try
            {
                window = new ChatAppearanceSettingsWindow
                {
                    Owner = main,
                    Width = 1180,
                    Height = 790,
                    WindowState = WindowState.Normal,
                    WindowStartupLocation = WindowStartupLocation.Manual,
                    Left = 0,
                    Top = 0
                };
                window.Show();
                await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
                await Task.Delay(260);
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
                if (new FileInfo(path).Length < 25000)
                    throw new InvalidOperationException("Multichat settings screenshot is unexpectedly small.");
            }
            catch (Exception ex)
            {
                try { File.WriteAllText(Path.ChangeExtension(path, ".error.txt"), ex.ToString()); } catch { }
                Environment.ExitCode = 71;
                Application.Current.Shutdown(71);
            }
            finally
            {
                try { window?.Close(); } catch { }
            }
        }), DispatcherPriority.ApplicationIdle);
    }
}