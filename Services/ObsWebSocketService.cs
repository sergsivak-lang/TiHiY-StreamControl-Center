using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using TiHiY.StreamControlCenter.Models;

namespace TiHiY.StreamControlCenter.Services;

public sealed class ObsWebSocketService : IAsyncDisposable
{
    private ClientWebSocket? _socket;
    private CancellationTokenSource? _cts;
    private Task? _receiveLoop;
    private readonly ConcurrentDictionary<string, TaskCompletionSource<JsonObject>> _requests = new();
    private readonly SemaphoreSlim _sendGate = new(1, 1);
    private readonly ConcurrentDictionary<string, byte> _meterInputNames = new(StringComparer.OrdinalIgnoreCase);

    public bool IsConnected => _socket?.State == WebSocketState.Open;
    public bool IsStreaming { get; private set; }
    public bool IsRecording { get; private set; }
    public string CurrentProgramScene { get; private set; } = string.Empty;
    public string CurrentPreviewScene { get; private set; } = string.Empty;
    public bool StudioModeEnabled { get; private set; }

    public event EventHandler<bool>? ConnectionChanged;
    public event EventHandler<string>? Log;
    public event EventHandler<string>? ProgramSceneChanged;
    public event EventHandler<string>? PreviewSceneChanged;
    public event EventHandler<bool>? StudioModeChanged;
    public event EventHandler<bool>? StreamingChanged;
    public event EventHandler<bool>? RecordingChanged;
    public event EventHandler<(string inputName, bool muted)>? InputMuteChanged;
    public event EventHandler<(string inputName, double volume)>? InputVolumeChanged;
    public event EventHandler<(string inputName, double meter, double db)>? InputMeterChanged;
    public event EventHandler<JsonObject>? VendorEventReceived;

    public async Task ConnectAsync(string uri, string password, CancellationToken cancellationToken = default)
    {
        await DisconnectAsync();
        _socket = new ClientWebSocket();
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        Log?.Invoke(this, $"Підключення до OBS: {uri}");
        await _socket.ConnectAsync(new Uri(uri), cancellationToken);

        var hello = await ReceiveJsonAsync(_socket, cancellationToken);
        if (hello["op"]?.GetValue<int>() != 0) throw new InvalidOperationException("OBS не надіслав Hello.");
        var identifyData = new JsonObject
        {
            ["rpcVersion"] = 1,
            ["eventSubscriptions"] = 1 | 4 | 8 | 64 | 128 | 512 | 65536
        };
        if (hello["d"]?["authentication"] is JsonObject auth)
        {
            if (string.IsNullOrEmpty(password)) throw new InvalidOperationException("OBS вимагає пароль WebSocket.");
            identifyData["authentication"] = CreateAuthentication(
                password,
                auth["salt"]?.GetValue<string>() ?? string.Empty,
                auth["challenge"]?.GetValue<string>() ?? string.Empty);
        }
        await SendJsonAsync(new JsonObject { ["op"] = 1, ["d"] = identifyData }, cancellationToken);
        var identified = await ReceiveJsonAsync(_socket, cancellationToken);
        if (identified["op"]?.GetValue<int>() != 2) throw new InvalidOperationException("OBS не підтвердив авторизацію.");

        _receiveLoop = Task.Run(() => ReceiveLoopAsync(_cts.Token), _cts.Token);
        ConnectionChanged?.Invoke(this, true);
        Log?.Invoke(this, "OBS WebSocket підключено.");
        await RefreshStateAsync();
    }

    public async Task DisconnectAsync()
    {
        var socket = _socket;
        if (socket is null) return;
        try
        {
            _cts?.Cancel();
            if (socket.State == WebSocketState.Open)
            {
                using var closeCts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
                await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "TiHiY shutdown", closeCts.Token).ConfigureAwait(false);
            }
        }
        catch { }
        finally
        {
            socket.Dispose();
            _socket = null;
            _cts?.Dispose();
            _cts = null;
            foreach (var pending in _requests.Values) pending.TrySetCanceled();
            _requests.Clear();
            _meterInputNames.Clear();
            IsStreaming = false;
            IsRecording = false;
            ConnectionChanged?.Invoke(this, false);
        }
    }

    public async Task RefreshStateAsync()
    {
        var scene = await RequestAsync("GetCurrentProgramScene");
        CurrentProgramScene = scene["currentProgramSceneName"]?.GetValue<string>() ?? string.Empty;
        ProgramSceneChanged?.Invoke(this, CurrentProgramScene);
        var studio = await RequestAsync("GetStudioModeEnabled");
        StudioModeEnabled = studio["studioModeEnabled"]?.GetValue<bool>() ?? false;
        StudioModeChanged?.Invoke(this, StudioModeEnabled);
        if (StudioModeEnabled)
        {
            var preview = await RequestAsync("GetCurrentPreviewScene");
            CurrentPreviewScene = preview["currentPreviewSceneName"]?.GetValue<string>() ?? string.Empty;
            PreviewSceneChanged?.Invoke(this, CurrentPreviewScene);
        }
    }

    public async Task<IReadOnlyList<string>> GetScenesAsync()
    {
        var data = await RequestAsync("GetSceneList");
        CurrentProgramScene = data["currentProgramSceneName"]?.GetValue<string>() ?? CurrentProgramScene;
        var result = new List<string>();
        if (data["scenes"] is JsonArray scenes)
        {
            foreach (var item in scenes)
            {
                var name = item?["sceneName"]?.GetValue<string>();
                if (!string.IsNullOrWhiteSpace(name)) result.Add(name);
            }
        }
        return result;
    }

    public Task SetCurrentProgramSceneAsync(string sceneName) => RequestAsync("SetCurrentProgramScene", new JsonObject { ["sceneName"] = sceneName });
    public Task SetCurrentPreviewSceneAsync(string sceneName) => RequestAsync("SetCurrentPreviewScene", new JsonObject { ["sceneName"] = sceneName });
    public Task TriggerStudioTransitionAsync() => RequestAsync("TriggerStudioModeTransition");

    public async Task<IReadOnlyList<(string name, string kind)>> GetInputsAsync()
    {
        var data = await RequestAsync("GetInputList");
        var result = new List<(string, string)>();
        if (data["inputs"] is JsonArray inputs)
        {
            foreach (var item in inputs)
            {
                var name = item?["inputName"]?.GetValue<string>();
                var kind = item?["inputKind"]?.GetValue<string>() ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(name)) result.Add((name, kind));
            }
        }
        return result;
    }

    private async Task<HashSet<string>> GetSpecialInputNamesAsync()
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var special = await RequestAsync("GetSpecialInputs");
            foreach (var key in new[] { "desktop1", "desktop2", "mic1", "mic2", "mic3", "mic4" })
            {
                var name = special[key]?.GetValue<string>();
                if (!string.IsNullOrWhiteSpace(name)) result.Add(name);
            }
        }
        catch { }
        return result;
    }

    public async Task<IReadOnlyList<(string name, string kind)>> GetMixerInputsAsync()
    {
        var inputs = await GetInputsAsync();
        var specialInputs = await GetSpecialInputNamesAsync();
        var meterInputs = _meterInputNames.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var result = new List<(string name, string kind)>();
        foreach (var input in inputs)
        {
            var include = specialInputs.Contains(input.name) || meterInputs.Contains(input.name) || IsNativeAudioKind(input.kind);
            if (!include) continue;
            try
            {
                await GetInputVolumeAsync(input.name);
                await GetInputMuteAsync(input.name);
                result.Add(input);
            }
            catch
            {
                // Джерело без аудіовиходу не належить до Audio Mixer OBS.
            }
        }
        return result;
    }

    public async Task<IReadOnlyList<(string name, string kind)>> GetPrimaryMixerInputsAsync(
        IEnumerable<string>? selectedInputs = null)
    {
        var selected = selectedInputs?
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList() ?? new List<string>();

        if (selected.Count > 0)
        {
            var all = await GetMixerInputsAsync();
            return all
                .Where(x => selected.Contains(x.name, StringComparer.OrdinalIgnoreCase))
                .OrderBy(x => selected.FindIndex(n => string.Equals(n, x.name, StringComparison.OrdinalIgnoreCase)))
                .ToList();
        }

        if (string.IsNullOrWhiteSpace(CurrentProgramScene))
        {
            try { await RefreshStateAsync(); } catch { }
        }

        if (!string.IsNullOrWhiteSpace(CurrentProgramScene))
        {
            return await GetSceneAudioInputsAsync(
                CurrentProgramScene,
                includeGroups: true,
                visibleOnly: true,
                includeGlobalDevices: true);
        }

        return Array.Empty<(string name, string kind)>();
    }

    private static bool IsNativeAudioKind(string kind)
    {
        var value = kind.ToLowerInvariant();
        return value.Contains("wasapi") || value.Contains("audio_input") || value.Contains("audio_output") ||
               value.Contains("pulse") || value.Contains("coreaudio") || value.Contains("alsa") ||
               value.Contains("browser") || value.Contains("media_source") || value.Contains("ffmpeg_source") || value.Contains("vlc");
    }

    public async Task<IReadOnlyList<(string name, string kind)>> GetSceneAudioInputsAsync(
        string sceneName,
        bool includeGroups,
        bool visibleOnly,
        bool includeGlobalDevices,
        IEnumerable<string>? pinnedInputs = null)
    {
        var allInputs = (await GetInputsAsync()).ToDictionary(x => x.name, x => x.kind, StringComparer.OrdinalIgnoreCase);
        var found = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await CollectSceneSourcesAsync(sceneName, includeGroups, visibleOnly, found, visited);
        if (includeGlobalDevices)
        {
            var specialInputs = await GetSpecialInputNamesAsync();
            foreach (var name in specialInputs)
                if (allInputs.ContainsKey(name)) found.Add(name);
        }
        if (pinnedInputs is not null)
            foreach (var pinned in pinnedInputs) if (allInputs.ContainsKey(pinned)) found.Add(pinned);
        var result = new List<(string name, string kind)>();
        foreach (var name in found.Where(allInputs.ContainsKey))
        {
            try
            {
                await GetInputVolumeAsync(name);
                await GetInputMuteAsync(name);
                result.Add((name, allInputs[name]));
            }
            catch
            {
                // OBS не показує це джерело як аудіоканал мікшера.
            }
        }
        return result.OrderBy(x => x.name, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    private async Task CollectSceneSourcesAsync(
        string sceneName,
        bool includeGroups,
        bool visibleOnly,
        HashSet<string> found,
        HashSet<string> visited)
    {
        if (!visited.Add(sceneName)) return;
        JsonObject data;
        try { data = await RequestAsync("GetSceneItemList", new JsonObject { ["sceneName"] = sceneName }); }
        catch { return; }
        if (data["sceneItems"] is not JsonArray items) return;
        foreach (var item in items)
        {
            var enabled = item?["sceneItemEnabled"]?.GetValue<bool>() ?? true;
            if (visibleOnly && !enabled) continue;
            var sourceName = item?["sourceName"]?.GetValue<string>() ?? string.Empty;
            var inputKind = item?["inputKind"]?.GetValue<string>() ?? string.Empty;
            var sourceType = item?["sourceType"]?.GetValue<string>() ?? string.Empty;
            var isGroup = item?["isGroup"]?.GetValue<bool>() ?? false;
            if (!string.IsNullOrWhiteSpace(inputKind) && IsLikelyAudioKind(inputKind)) found.Add(sourceName);
            if (includeGroups && isGroup)
            {
                try
                {
                    var group = await RequestAsync("GetGroupSceneItemList", new JsonObject { ["sceneName"] = sourceName });
                    if (group["sceneItems"] is JsonArray groupItems)
                        CollectItems(groupItems, visibleOnly, found);
                }
                catch { }
            }
            if (includeGroups && string.Equals(sourceType, "OBS_SOURCE_TYPE_SCENE", StringComparison.OrdinalIgnoreCase))
                await CollectSceneSourcesAsync(sourceName, true, visibleOnly, found, visited);
        }
    }

    private static void CollectItems(JsonArray items, bool visibleOnly, HashSet<string> found)
    {
        foreach (var item in items)
        {
            if (visibleOnly && !(item?["sceneItemEnabled"]?.GetValue<bool>() ?? true)) continue;
            var sourceName = item?["sourceName"]?.GetValue<string>() ?? string.Empty;
            var inputKind = item?["inputKind"]?.GetValue<string>() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(inputKind) && IsLikelyAudioKind(inputKind)) found.Add(sourceName);
        }
    }

    private static bool IsLikelyAudioKind(string kind)
    {
        var value = kind.ToLowerInvariant();
        return value.Contains("audio") || value.Contains("wasapi") || value.Contains("pulse") || value.Contains("coreaudio") ||
               value.Contains("browser") || value.Contains("media_source") || value.Contains("ffmpeg_source") || value.Contains("vlc");
    }

    public async Task<bool> GetInputMuteAsync(string inputName)
    {
        var data = await RequestAsync("GetInputMute", new JsonObject { ["inputName"] = inputName });
        return data["inputMuted"]?.GetValue<bool>() ?? false;
    }

    public async Task<double> GetInputVolumeAsync(string inputName)
    {
        var data = await RequestAsync("GetInputVolume", new JsonObject { ["inputName"] = inputName });
        return data["inputVolumeMul"]?.GetValue<double>() ?? 1;
    }

    public Task SetInputMuteAsync(string inputName, bool muted) => RequestAsync("SetInputMute", new JsonObject { ["inputName"] = inputName, ["inputMuted"] = muted });
    public Task SetInputVolumeAsync(string inputName, double volume) => RequestAsync("SetInputVolume", new JsonObject { ["inputName"] = inputName, ["inputVolumeMul"] = Math.Clamp(volume, 0, 20) });
    public Task StartStreamAsync() => RequestAsync("StartStream");
    public Task StopStreamAsync() => RequestAsync("StopStream");
    public Task StartRecordAsync() => RequestAsync("StartRecord");
    public Task StopRecordAsync() => RequestAsync("StopRecord");
    public Task<JsonObject> GetStatsAsync() => RequestAsync("GetStats");

    public async Task<string?> GetCurrentProgramScreenshotAsync(int width = 960, int height = 540)
    {
        if (string.IsNullOrWhiteSpace(CurrentProgramScene)) await RefreshStateAsync();
        if (string.IsNullOrWhiteSpace(CurrentProgramScene)) return null;
        var data = await RequestAsync("GetSourceScreenshot", new JsonObject
        {
            ["sourceName"] = CurrentProgramScene,
            ["imageFormat"] = "png",
            ["imageWidth"] = width,
            ["imageHeight"] = height,
            ["imageCompressionQuality"] = -1
        });
        return data["imageData"]?.GetValue<string>();
    }

    public Task<JsonObject> CallVendorRequestAsync(string vendorName, string requestType, JsonObject? requestData = null) =>
        RequestAsync("CallVendorRequest", new JsonObject
        {
            ["vendorName"] = vendorName,
            ["requestType"] = requestType,
            ["requestData"] = requestData ?? new JsonObject()
        });

    private async Task<JsonObject> RequestAsync(string requestType, JsonObject? requestData = null)
    {
        if (!IsConnected) throw new InvalidOperationException("OBS не підключено.");
        var id = Guid.NewGuid().ToString("N");
        var tcs = new TaskCompletionSource<JsonObject>(TaskCreationOptions.RunContinuationsAsynchronously);
        _requests[id] = tcs;
        var data = new JsonObject { ["requestType"] = requestType, ["requestId"] = id };
        if (requestData is not null) data["requestData"] = requestData;
        try
        {
            await SendJsonAsync(new JsonObject { ["op"] = 6, ["d"] = data }, _cts?.Token ?? CancellationToken.None);
            return await tcs.Task.WaitAsync(TimeSpan.FromSeconds(8));
        }
        finally
        {
            _requests.TryRemove(id, out _);
        }
    }

    private async Task ReceiveLoopAsync(CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested && _socket?.State == WebSocketState.Open)
            {
                var message = await ReceiveJsonAsync(_socket, token);
                var op = message["op"]?.GetValue<int>() ?? -1;
                var data = message["d"] as JsonObject ?? new JsonObject();
                if (op == 7)
                {
                    var id = data["requestId"]?.GetValue<string>() ?? string.Empty;
                    if (_requests.TryGetValue(id, out var tcs))
                    {
                        var status = data["requestStatus"] as JsonObject;
                        if (status?["result"]?.GetValue<bool>() ?? false)
                            tcs.TrySetResult(data["responseData"] as JsonObject ?? new JsonObject());
                        else
                            tcs.TrySetException(new InvalidOperationException(status?["comment"]?.GetValue<string>() ?? "OBS відхилив команду."));
                    }
                }
                else if (op == 5)
                {
                    HandleEvent(data);
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { Log?.Invoke(this, $"Зв’язок з OBS втрачено: {ex.Message}"); }
        finally { ConnectionChanged?.Invoke(this, false); }
    }

    private void HandleEvent(JsonObject data)
    {
        var type = data["eventType"]?.GetValue<string>() ?? string.Empty;
        var payload = data["eventData"] as JsonObject ?? new JsonObject();
        switch (type)
        {
            case "CurrentProgramSceneChanged":
                CurrentProgramScene = payload["sceneName"]?.GetValue<string>() ?? string.Empty;
                ProgramSceneChanged?.Invoke(this, CurrentProgramScene);
                break;
            case "CurrentPreviewSceneChanged":
                CurrentPreviewScene = payload["sceneName"]?.GetValue<string>() ?? string.Empty;
                PreviewSceneChanged?.Invoke(this, CurrentPreviewScene);
                break;
            case "StudioModeStateChanged":
                StudioModeEnabled = payload["studioModeEnabled"]?.GetValue<bool>() ?? false;
                StudioModeChanged?.Invoke(this, StudioModeEnabled);
                break;
            case "StreamStateChanged":
                IsStreaming = payload["outputActive"]?.GetValue<bool>() ?? false;
                StreamingChanged?.Invoke(this, IsStreaming);
                break;
            case "RecordStateChanged":
                IsRecording = payload["outputActive"]?.GetValue<bool>() ?? false;
                RecordingChanged?.Invoke(this, IsRecording);
                break;
            case "InputMuteStateChanged":
                var inputName = payload["inputName"]?.GetValue<string>() ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(inputName)) InputMuteChanged?.Invoke(this, (inputName, payload["inputMuted"]?.GetValue<bool>() ?? false));
                break;
            case "InputVolumeChanged":
                var volumeInputName = payload["inputName"]?.GetValue<string>() ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(volumeInputName))
                    InputVolumeChanged?.Invoke(this, (volumeInputName, Math.Clamp(payload["inputVolumeMul"]?.GetValue<double>() ?? 1, 0, 1)));
                break;
            case "InputVolumeMeters":
                if (payload["inputs"] is JsonArray inputs)
                {
                    foreach (var input in inputs)
                    {
                        var name = input?["inputName"]?.GetValue<string>() ?? string.Empty;
                        if (string.IsNullOrWhiteSpace(name)) continue;
                        _meterInputNames[name] = 0;
                        var peak = ExtractPeak(input?["inputLevelsMul"] as JsonArray);
                        var db = peak <= 0.000001 ? -60 : Math.Max(-60, 20 * Math.Log10(peak));
                        InputMeterChanged?.Invoke(this, (name, Math.Clamp((db + 60) / 60, 0, 1), db));
                    }
                }
                break;
            case "VendorEvent":
                VendorEventReceived?.Invoke(this, payload);
                break;
        }
    }

    private static double ExtractPeak(JsonArray? levels)
    {
        if (levels is null) return 0;
        double peak = 0;
        foreach (var channel in levels)
            if (channel is JsonArray values)
                foreach (var value in values)
                    if (value is not null)
                        try { peak = Math.Max(peak, value.GetValue<double>()); } catch { }
        return peak;
    }

    private async Task SendJsonAsync(JsonObject json, CancellationToken token)
    {
        if (_socket is null) throw new InvalidOperationException("OBS socket відсутній.");
        var bytes = Encoding.UTF8.GetBytes(json.ToJsonString());
        await _sendGate.WaitAsync(token);
        try
        {
            await _socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, token);
        }
        finally
        {
            _sendGate.Release();
        }
    }

    private static async Task<JsonObject> ReceiveJsonAsync(ClientWebSocket socket, CancellationToken token)
    {
        var buffer = new byte[64 * 1024];
        using var stream = new MemoryStream();
        WebSocketReceiveResult result;
        do
        {
            result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), token);
            if (result.MessageType == WebSocketMessageType.Close) throw new WebSocketException("OBS закрив WebSocket.");
            stream.Write(buffer, 0, result.Count);
        } while (!result.EndOfMessage);
        return JsonNode.Parse(Encoding.UTF8.GetString(stream.ToArray())) as JsonObject ?? throw new InvalidOperationException("OBS надіслав некоректний JSON.");
    }

    private static string CreateAuthentication(string password, string salt, string challenge)
    {
        var secret = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(password + salt)));
        return Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(secret + challenge)));
    }

    public async ValueTask DisposeAsync() => await DisconnectAsync().ConfigureAwait(false);
}
