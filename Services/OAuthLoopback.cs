using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Collections.Concurrent;

namespace TiHiY.StreamControlCenter.Services;

public static class OAuthLoopback
{
    private static readonly ConcurrentDictionary<int, SemaphoreSlim> PortLocks = new();
    public static async Task<Dictionary<string, string>> AuthorizeAsync(string authorizeUrl, string redirectUri, CancellationToken token)
    {
        var redirect = new Uri(redirectUri);
        var portLock = PortLocks.GetOrAdd(redirect.Port, _ => new SemaphoreSlim(1, 1));
        if (!await portLock.WaitAsync(0, token))
            throw new InvalidOperationException("Авторизація для цього сервісу вже відкрита у браузері. Завершіть її або зачекайте до скасування.");

        var listener = new TcpListener(IPAddress.Loopback, redirect.Port);
        try
        {
            listener.Start();
            Process.Start(new ProcessStartInfo(authorizeUrl) { UseShellExecute = true });
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
            timeout.CancelAfter(TimeSpan.FromMinutes(5));
            TcpClient client;
            try
            {
                client = await listener.AcceptTcpClientAsync(timeout.Token);
            }
            catch (OperationCanceledException) when (!token.IsCancellationRequested)
            {
                throw new TimeoutException("Час очікування відповіді OAuth минув. Натисніть «Авторизувати» ще раз.");
            }
            using (client)
            {
            using var stream = client.GetStream();
            using var reader = new StreamReader(stream, Encoding.UTF8, false, 4096, true);
            var firstLine = await reader.ReadLineAsync(token);
            if (string.IsNullOrWhiteSpace(firstLine)) throw new InvalidOperationException("Браузер не повернув OAuth-запит.");
            string? header;
            do { header = await reader.ReadLineAsync(token); } while (!string.IsNullOrEmpty(header));
            var parts = firstLine.Split(' ');
            if (parts.Length < 2) throw new InvalidOperationException("Невірна відповідь OAuth.");
            var requestUri = new Uri($"http://localhost:{redirect.Port}{parts[1]}");
            if (!requestUri.AbsolutePath.Equals(redirect.AbsolutePath, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("OAuth повернувся на невірний шлях.");

            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var pair in requestUri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                var item = pair.Split('=', 2);
                var key = Uri.UnescapeDataString(item[0].Replace('+', ' '));
                var value = item.Length > 1 ? Uri.UnescapeDataString(item[1].Replace('+', ' ')) : string.Empty;
                values[key] = value;
            }

            var ok = values.ContainsKey("code") && !values.ContainsKey("error");
            var html = ok
                ? "<!doctype html><meta charset='utf-8'><title>TiHiY</title><body style='background:#07131e;color:#edf7ff;font:24px Segoe UI;padding:40px'>Авторизацію завершено. Це вікно можна закрити.</body>"
                : "<!doctype html><meta charset='utf-8'><title>TiHiY</title><body style='background:#07131e;color:#ff5860;font:24px Segoe UI;padding:40px'>Авторизацію не завершено. Поверніться до TiHiY StreamControl Center.</body>";
            var body = Encoding.UTF8.GetBytes(html);
            var response = Encoding.ASCII.GetBytes($"HTTP/1.1 200 OK\r\nContent-Type: text/html; charset=utf-8\r\nContent-Length: {body.Length}\r\nConnection: close\r\n\r\n");
            await stream.WriteAsync(response, token);
            await stream.WriteAsync(body, token);
            await stream.FlushAsync(token);
            return values;
            }
        }
        catch (SocketException ex) when (ex.SocketErrorCode == SocketError.AddressAlreadyInUse)
        {
            throw new InvalidOperationException($"Порт OAuth {redirect.Port} уже зайнятий. Закрийте стару копію програми та повторіть авторизацію.", ex);
        }
        finally
        {
            listener.Stop();
            portLock.Release();
        }
    }
}
