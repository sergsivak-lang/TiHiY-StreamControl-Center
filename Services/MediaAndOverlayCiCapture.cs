using System.Net.Http;
using TiHiY.StreamControlCenter.Windows;

namespace TiHiY.StreamControlCenter.Services;

internal static class MediaAndOverlayCiCapture
{
    private static bool _started;

    [ModuleInitializer]
    internal static void Register() =>
        EventManager.RegisterClassHandler(typeof(MainWindow), FrameworkElement.LoadedEvent, new RoutedEventHandler(OnMainLoaded));

    private static void OnMainLoaded(object sender, RoutedEventArgs e)
    {
        if (_started || sender is not MainWindow main) return;
        var args = Environment.GetCommandLineArgs();
        var musicArg = args.FirstOrDefault(x => x.StartsWith("--ci-music-screenshot=", StringComparison.OrdinalIgnoreCase));
        var localArg = args.FirstOrDefault(x => x.StartsWith("--ci-local-overlay-screenshot=", StringComparison.OrdinalIgnoreCase));
        var htmlArg = args.FirstOrDefault(x => x.StartsWith("--ci-overlay-chat-html=", StringComparison.OrdinalIgnoreCase));
        if (musicArg is null && localArg is null && htmlArg is null) return;
        _started = true;

        main.Dispatcher.BeginInvoke(new Action(async () =>
        {
            try
            {
                if (musicArg is not null)
                {
                    var path = ExtractPath(musicArg);
                    var window = new MusicWindow
                    {
                        Owner = main,
                        Width = 1180,
                        Height = 760,
                        WindowState = WindowState.Normal,
                        WindowStartupLocation = WindowStartupLocation.Manual,
                        Left = 0,
                        Top = 0
                    };
                    window.Show();
                    await RenderWindowAsync(window, path);
                    window.Close();
                }
                else if (localArg is not null)
                {
                    var path = ExtractPath(localArg);
                    App.Services.Settings.Value.LocalChatOverlayClickThrough = false;
                    App.Services.Settings.Value.LocalChatOverlayFontSize = 20;
                    App.Services.Settings.Value.LocalChatOverlayUserColor = "#55C8FF";
                    var window = new LocalChatOverlayWindow
                    {
                        Owner = main,
                        Width = 620,
                        Height = 500,
                        WindowState = WindowState.Normal,
                        WindowStartupLocation = WindowStartupLocation.Manual,
                        Left = 0,
                        Top = 0
                    };
                    window.Show();
                    window.ApplySettings();
                    await RenderWindowAsync(window, path);
                    window.Close();
                }
                else if (htmlArg is not null)
                {
                    var path = ExtractPath(htmlArg);
                    await Task.Delay(450);
                    using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
                    var url = $"http://127.0.0.1:{App.Services.Settings.Value.OverlayPort}/overlay/chat";
                    var html = await client.GetStringAsync(url);
                    if (!html.Contains("grid-template-columns:30px minmax(0,1fr)", StringComparison.Ordinal) ||
                        !html.Contains("content.className='content'", StringComparison.Ordinal))
                        throw new InvalidOperationException("OBS chat overlay does not render message text below usernames.");
                    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
                    await File.WriteAllTextAsync(path, html);
                }

                Application.Current.Shutdown(0);
            }
            catch (Exception ex)
            {
                var requested = musicArg ?? localArg ?? htmlArg ?? "ci-capture";
                var path = ExtractPath(requested);
                try { await File.WriteAllTextAsync(Path.ChangeExtension(path, ".error.txt"), ex.ToString()); } catch { }
                Environment.ExitCode = 72;
                Application.Current.Shutdown(72);
            }
        }), DispatcherPriority.ApplicationIdle);
    }

    private static string ExtractPath(string argument) => argument[(argument.IndexOf('=') + 1)..].Trim('"');

    private static async Task RenderWindowAsync(Window window, string path)
    {
        await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
        await Task.Delay(300);
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
}
