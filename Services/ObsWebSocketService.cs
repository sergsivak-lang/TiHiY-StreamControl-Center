using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace TiHiY.StreamControlCenter.Services;

public sealed class ObsWebSocketService : IAsyncDisposable
{
    private ClientWebSocket? _socket;
    private int _requestNumber;
    public bool IsConnected => _socket?.State == WebSocketState.Open;

    public async Task ConnectAsync(string url, string password, CancellationToken cancellationToken = default)
    {
        await DisconnectAsync();
        _socket = new ClientWebSocket();
        await _socket.ConnectAsync(new Uri(url), cancellationToken);
        using var hello = await ReceiveAsync(cancellationToken);
        if (hello.RootElement.GetProperty("op").GetInt32() != 0) throw new InvalidOperationException("OBS не надіслав Hello.");

        string? authentication = null;
        var data = hello.RootElement.GetProperty("d");
        if (data.TryGetProperty("authentication", out var auth))
        {
            var challenge = auth.GetProperty("challenge").GetString() ?? "";
            var salt = auth.GetProperty("salt").GetString() ?? "";
            var secret = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(password + salt)));
            authentication = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(secret + challenge)));
        }

        var identify = authentication is null
            ? new { op = 1, d = new { rpcVersion = 1 } }
            : new { op = 1, d = new { rpcVersion = 1, authentication } };
        await SendAsync(identify, cancellationToken);
        using var identified = await ReceiveAsync(cancellationToken);
        if (identified.RootElement.GetProperty("op").GetInt32() != 2) throw new UnauthorizedAccessException("OBS відхилив пароль WebSocket.");
    }

    public async Task DisconnectAsync()
    {
        if (_socket?.State == WebSocketState.Open)
            await _socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "TiHiY disconnect", CancellationToken.None);
        _socket?.Dispose(); _socket = null;
    }

    public Task StartStreamAsync() => RequestAsync("StartStream");
    public Task StopStreamAsync() => RequestAsync("StopStream");

    public async Task<ObsStats> GetStatsAsync()
    {
        var stats = await RequestForDataAsync("GetStats");
        var stream = await RequestForDataAsync("GetStreamStatus");
        return new ObsStats(
            stats.TryGetProperty("activeFps", out var fps) ? fps.GetDouble() : 0,
            stats.TryGetProperty("outputSkippedFrames", out var skipped) ? skipped.GetInt64() : 0,
            stats.TryGetProperty("outputTotalFrames", out var total) ? total.GetInt64() : 0,
            stream.TryGetProperty("outputBytes", out var bytes) ? bytes.GetInt64() : 0,
            stream.TryGetProperty("outputDuration", out var duration) ? duration.GetInt64() : 0,
            stream.TryGetProperty("outputActive", out var active) && active.GetBoolean());
    }

    private async Task RequestAsync(string requestType) => _ = await RequestForDataAsync(requestType);
    private async Task<JsonElement> RequestForDataAsync(string requestType)
    {
        if (!IsConnected) throw new InvalidOperationException("OBS не підключено.");
        var id = Interlocked.Increment(ref _requestNumber).ToString();
        await SendAsync(new { op = 6, d = new { requestType, requestId = id } }, CancellationToken.None);
        while (true)
        {
            using var response = await ReceiveAsync(CancellationToken.None);
            if (response.RootElement.GetProperty("op").GetInt32() != 7) continue;
            var d = response.RootElement.GetProperty("d");
            if (d.GetProperty("requestId").GetString() != id) continue;
            var status = d.GetProperty("requestStatus");
            if (!status.GetProperty("result").GetBoolean())
                throw new InvalidOperationException(status.TryGetProperty("comment", out var c) ? c.GetString() : "OBS request failed");
            return d.TryGetProperty("responseData", out var result) ? result.Clone() : JsonDocument.Parse("{}").RootElement.Clone();
        }
    }

    private async Task SendAsync(object payload, CancellationToken token)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(payload);
        await _socket!.SendAsync(bytes, WebSocketMessageType.Text, true, token);
    }

    private async Task<JsonDocument> ReceiveAsync(CancellationToken token)
    {
        var buffer = new byte[64 * 1024]; var offset = 0;
        ValueWebSocketReceiveResult result;
        do
        {
            result = await _socket!.ReceiveAsync(buffer.AsMemory(offset), token);
            offset += result.Count;
            if (offset == buffer.Length && !result.EndOfMessage) throw new InvalidOperationException("Завелика відповідь OBS.");
        } while (!result.EndOfMessage);
        if (result.MessageType == WebSocketMessageType.Close) throw new WebSocketException("OBS закрив з’єднання.");
        return JsonDocument.Parse(buffer.AsMemory(0, offset));
    }

    public async ValueTask DisposeAsync() => await DisconnectAsync();
}

public sealed record ObsStats(double Fps, long SkippedFrames, long TotalFrames, long OutputBytes, long OutputDurationMs, bool Active);
