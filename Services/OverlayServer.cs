using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using TiHiY.StreamControlCenter.Models;

namespace TiHiY.StreamControlCenter.Services;

public sealed class OverlayServer : IAsyncDisposable
{
    private readonly Func<IReadOnlyList<ChatMessage>> _chatProvider;
    private readonly Func<IReadOnlyList<DonationEvent>> _donationProvider;
    private readonly Func<object> _nowPlayingProvider;
    private readonly Func<object> _streamStatsProvider;
    private readonly Func<string> _themeProvider;
    private TcpListener? _listener;
    private CancellationTokenSource? _cts;
    private Task? _loop;

    public int Port { get; private set; }
    public bool IsRunning => _listener is not null;

    public OverlayServer(
        Func<IReadOnlyList<ChatMessage>> chatProvider,
        Func<IReadOnlyList<DonationEvent>> donationProvider,
        Func<object> nowPlayingProvider,
        Func<object> streamStatsProvider,
        Func<string> themeProvider)
    {
        _chatProvider = chatProvider;
        _donationProvider = donationProvider;
        _nowPlayingProvider = nowPlayingProvider;
        _streamStatsProvider = streamStatsProvider;
        _themeProvider = themeProvider;
    }

    public Task StartAsync(int port)
    {
        if (_listener is not null) return Task.CompletedTask;
        Port = port;
        _cts = new CancellationTokenSource();
        _listener = new TcpListener(IPAddress.Loopback, port);
        _listener.Start();
        _loop = AcceptLoopAsync(_cts.Token);
        return Task.CompletedTask;
    }

    public async Task RestartAsync(int port) { await StopAsync(); await StartAsync(port); }

    private async Task AcceptLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested && _listener is not null)
        {
            try
            {
                var client = await _listener.AcceptTcpClientAsync(token);
                _ = Task.Run(() => HandleClientAsync(client, token), token);
            }
            catch (OperationCanceledException) { break; }
            catch { try { await Task.Delay(100, token); } catch { break; } }
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken token)
    {
        using (client)
        using (var stream = client.GetStream())
        using (var reader = new StreamReader(stream, Encoding.UTF8, false, 4096, true))
        {
            var first = await reader.ReadLineAsync(token);
            if (string.IsNullOrWhiteSpace(first)) return;
            string? line;
            do { line = await reader.ReadLineAsync(token); } while (!string.IsNullOrEmpty(line));
            var parts = first.Split(' ');
            var target = parts.Length > 1 ? parts[1] : "/";
            var uri = new Uri($"http://127.0.0.1:{Port}{target}");
            var path = uri.AbsolutePath.ToLowerInvariant();

            if (path == "/api/chat")
            {
                var data = _chatProvider().TakeLast(30).Select(m => new
                {
                    id = string.IsNullOrWhiteSpace(m.ExternalId) ? $"{m.Time.Ticks}:{m.Platform}:{m.User}:{m.Text}" : m.ExternalId,
                    platform = m.Platform,
                    user = m.User,
                    text = m.Text,
                    role = m.Role,
                    foreground = m.Foreground,
                    highlighted = m.IsHighlighted
                });
                await RespondAsync(stream, "application/json; charset=utf-8", JsonSerializer.Serialize(data), token);
                return;
            }
            if (path == "/api/donations")
            {
                var data = _donationProvider().Where(x => x.ShowOnOverlay).TakeLast(30).Select(d => new
                {
                    id = d.StableId,
                    source = d.Source,
                    kind = d.Kind,
                    user = d.User,
                    amount = d.Amount,
                    currency = d.Currency,
                    message = d.Message,
                    accent = d.Accent,
                    time = d.Time.ToString("O")
                });
                await RespondAsync(stream, "application/json; charset=utf-8", JsonSerializer.Serialize(data), token);
                return;
            }
            if (path == "/api/now-playing")
            {
                await RespondAsync(stream, "application/json; charset=utf-8", JsonSerializer.Serialize(_nowPlayingProvider()), token);
                return;
            }
            if (path == "/api/stream-stats")
            {
                await RespondAsync(stream, "application/json; charset=utf-8", JsonSerializer.Serialize(_streamStatsProvider()), token);
                return;
            }
            if (path == "/overlay/chat")
            {
                await RespondAsync(stream, "text/html; charset=utf-8", BuildChatHtml(GetQuery(uri, "theme") ?? _themeProvider()), token);
                return;
            }
            if (path == "/overlay/alerts")
            {
                await RespondAsync(stream, "text/html; charset=utf-8", BuildAlertsHtml(GetQuery(uri, "theme") ?? _themeProvider()), token);
                return;
            }
            if (path == "/overlay/now-playing")
            {
                await RespondAsync(stream, "text/html; charset=utf-8", BuildNowPlayingHtml(GetQuery(uri, "theme") ?? _themeProvider()), token);
                return;
            }
            if (path == "/health")
            {
                await RespondAsync(stream, "text/plain; charset=utf-8", "TiHiY Overlay Server OK", token);
                return;
            }
            await RespondAsync(stream, "text/plain; charset=utf-8", "Not found", token, "404 Not Found");
        }
    }

    private static string? GetQuery(Uri uri, string name)
    {
        foreach (var pair in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = pair.Split('=', 2);
            if (parts.Length == 2 && string.Equals(parts[0], name, StringComparison.OrdinalIgnoreCase)) return Uri.UnescapeDataString(parts[1]);
        }
        return null;
    }

    private static async Task RespondAsync(NetworkStream stream, string contentType, string body, CancellationToken token, string status = "200 OK")
    {
        var bytes = Encoding.UTF8.GetBytes(body);
        var header = $"HTTP/1.1 {status}\r\nContent-Type: {contentType}\r\nContent-Length: {bytes.Length}\r\nCache-Control: no-store\r\nAccess-Control-Allow-Origin: *\r\nConnection: close\r\n\r\n";
        await stream.WriteAsync(Encoding.ASCII.GetBytes(header), token);
        await stream.WriteAsync(bytes, token);
        await stream.FlushAsync(token);
    }

    private static string BuildChatHtml(string theme)
    {
        var template = """
<!doctype html><html lang="uk"><head><meta charset="utf-8"><style>
html,body{margin:0;background:transparent;overflow:hidden;font-family:"Segoe UI",sans-serif}.wrap{position:absolute;left:12px;right:12px;bottom:44px;display:flex;flex-direction:column;gap:4px}.stats{position:absolute;left:12px;right:12px;bottom:8px;display:flex;justify-content:flex-end;gap:16px;padding:4px 2px;background:transparent;font-size:13px;font-weight:800;color:#dff7ff;text-shadow:0 2px 5px rgba(0,0,0,.95)}.msg{padding:5px 6px;border-radius:0;font-size:17px;line-height:1.3;background:transparent!important;box-shadow:none!important;text-shadow:0 2px 5px rgba(0,0,0,.98);transition:none;animation:none}.line{display:flex;gap:8px;align-items:flex-start}.platformIcon{width:24px;height:20px;border-radius:4px;display:inline-flex;align-items:center;justify-content:center;color:#fff;font-size:11px;font-weight:900;flex:0 0 auto}.platformIcon.tw{background:#9147ff}.platformIcon.yt{background:#ff0000}.platformIcon.dn{background:#e0a800;color:#101010}.statIcon{width:18px;height:18px;border-radius:4px;display:inline-flex;align-items:center;justify-content:center;color:#fff;font-size:10px;font-weight:900;margin-right:5px;text-shadow:none}.statIcon.tw{background:#9147ff}.statIcon.yt{background:#ff0000}.user{font-weight:800;flex:0 0 auto}.text{word-break:break-word;min-width:0}.highlight{background:rgba(255,211,41,.10)!important;border-left:4px solid #FFD329!important;padding-left:8px!important}
__THEME__
.msg{background:transparent!important;box-shadow:none!important}.stats{background:transparent!important}
</style></head><body><div id="chat" class="wrap"></div><div class="stats"><span id="tw"></span><span id="yt"></span><span id="likes">👍 0</span></div><script>
const nodes=new Map();
function icon(platform,small=false){const p=(platform||'').toUpperCase();const e=document.createElement('span');let cls='tw',text='T',title='Twitch';if(p==='YOUTUBE'){cls='yt';text='▶';title='YouTube'}else if(p==='DONATELLO'){cls='dn';text='♥';title='Donatello'}e.className=(small?'statIcon ':'platformIcon ')+cls;e.textContent=text;e.title=title;return e}
function makeMessage(m){const box=document.createElement('div');box.className='msg'+(m.highlighted?' highlight':'');box.style.color=m.foreground||'#fff';const line=document.createElement('div');line.className='line';line.append(icon(m.platform));const user=document.createElement('span');user.className='user';user.textContent=m.user+':';line.append(user);const text=document.createElement('span');text.className='text';text.textContent=m.text;line.append(text);box.append(line);return box}
async function update(){try{const data=await(await fetch('/api/chat',{cache:'no-store'})).json();const root=document.getElementById('chat');const latest=data.slice(-12);const active=new Set();for(const m of latest){const key=String(m.id||((m.platform||'')+'|'+(m.user||'')+'|'+(m.text||'')));active.add(key);let box=nodes.get(key);if(!box){box=makeMessage(m);nodes.set(key,box)}root.append(box)}for(const [key,box] of [...nodes]){if(!active.has(key)){box.remove();nodes.delete(key)}}const st=await(await fetch('/api/stream-stats',{cache:'no-store'})).json();const tw=document.getElementById('tw');tw.replaceChildren(icon('TWITCH',true),document.createTextNode('👁 '+(st.twitchViewers||0)));const yt=document.getElementById('yt');yt.replaceChildren(icon('YOUTUBE',true),document.createTextNode('👁 '+(st.youtubeViewers||0)));document.getElementById('likes').textContent='👍 '+(st.youtubeLikes||0)}catch(e){}}setInterval(update,1000);update();
</script></body></html>
""";
        return template.Replace("__THEME__", ThemeCss(theme), StringComparison.Ordinal);
    }

    private static string BuildAlertsHtml(string theme)
    {
        var template = """
<!doctype html><html lang="uk"><head><meta charset="utf-8"><style>
html,body{margin:0;background:transparent;overflow:hidden;font-family:"Segoe UI",sans-serif;color:#fff}#host{position:absolute;inset:0;display:flex;align-items:center;justify-content:center;pointer-events:none}#card{min-width:520px;max-width:1000px;padding:24px 34px;border-radius:14px;opacity:0;transform:scale(.86) translateY(30px);transition:opacity .28s,transform .28s;text-align:center;text-shadow:0 3px 8px #000;background:rgba(4,20,32,.88);border:2px solid #FFD329;box-shadow:0 0 38px rgba(255,211,41,.34)}#card.on{opacity:1;transform:scale(1) translateY(0)}.source{font-size:15px;font-weight:900;letter-spacing:2px;color:#FFD329}.amount{font-size:48px;font-weight:1000;line-height:1.05;margin:8px 0}.user{font-size:27px;font-weight:900}.message{font-size:21px;margin-top:8px;white-space:pre-wrap;word-break:break-word}.sub .source,.sub .amount{color:#7DFFB2}.sub{border-color:#2BEB82;box-shadow:0 0 38px rgba(43,235,130,.34)}
__THEME__
</style></head><body><div id="host"><div id="card"><div id="source" class="source"></div><div id="amount" class="amount"></div><div id="user" class="user"></div><div id="message" class="message"></div></div></div><script>
let initialized=false,lastId='',queue=[],showing=false;
function chime(){try{const c=new(window.AudioContext||window.webkitAudioContext)();const g=c.createGain();g.gain.setValueAtTime(.001,c.currentTime);g.gain.exponentialRampToValueAtTime(.12,c.currentTime+.03);g.gain.exponentialRampToValueAtTime(.001,c.currentTime+.55);g.connect(c.destination);[523,659,784].forEach((f,i)=>{const o=c.createOscillator();o.frequency.value=f;o.connect(g);o.start(c.currentTime+i*.08);o.stop(c.currentTime+.55+i*.08)});setTimeout(()=>c.close(),1200)}catch(e){}}
async function poll(){try{const data=await(await fetch('/api/donations',{cache:'no-store'})).json();if(!data.length)return;const latest=data[data.length-1];if(!initialized){initialized=true;lastId=String(latest.id||'');return}let fresh=[];let found=false;for(let i=data.length-1;i>=0;i--){const id=String(data[i].id||'');if(id===lastId){found=true;break}fresh.unshift(data[i])}if(!found)fresh=[latest];if(fresh.length){lastId=String(latest.id||lastId);queue.push(...fresh);showNext()}}catch(e){}}
function showNext(){if(showing||!queue.length)return;showing=true;const d=queue.shift();const card=document.getElementById('card');card.classList.toggle('sub',(d.kind||'').toUpperCase()==='SUBSCRIPTION');document.getElementById('source').textContent=(d.kind||'').toUpperCase()==='SUBSCRIPTION'?'НОВА ПЛАТНА ПІДПИСКА':(d.source||'ДОНАТ');document.getElementById('amount').textContent=(Number(d.amount)||0).toLocaleString('uk-UA',{maximumFractionDigits:2})+' '+(d.currency||'');document.getElementById('user').textContent=d.user||'Анонім';document.getElementById('message').textContent=d.message||'';requestAnimationFrame(()=>card.classList.add('on'));chime();setTimeout(()=>{card.classList.remove('on');setTimeout(()=>{showing=false;showNext()},420)},7000)}
setInterval(poll,750);poll();
</script></body></html>
""";
        return template.Replace("__THEME__", ThemeCss(theme), StringComparison.Ordinal);
    }

    private static string BuildNowPlayingHtml(string theme)
    {
        var template = """
<!doctype html><html lang="uk"><head><meta charset="utf-8"><style>
html,body{margin:0;background:transparent;overflow:hidden;font-family:"Segoe UI",sans-serif;color:#fff}#card{position:absolute;left:18px;bottom:18px;min-width:420px;max-width:760px;padding:14px 18px;border-radius:9px;opacity:0;transform:translateY(15px);transition:.3s}#card.on{opacity:1;transform:none}.k{font-size:12px;font-weight:800;letter-spacing:1.7px;opacity:.72}.title{font-size:24px;font-weight:800;white-space:nowrap;overflow:hidden;text-overflow:ellipsis}.artist{font-size:16px;opacity:.82}.bar{height:4px;background:rgba(255,255,255,.14);margin-top:10px}.fill{height:100%;width:0;background:#FFD329}
__THEME__
</style></head><body><div id="card"><div class="k">♫ ЗАРАЗ ГРАЄ</div><div id="title" class="title"></div><div id="artist" class="artist"></div><div class="bar"><div id="fill" class="fill"></div></div></div><script>
async function update(){try{const m=await(await fetch('/api/now-playing',{cache:'no-store'})).json();const c=document.getElementById('card');c.classList.toggle('on',!!m.active);document.getElementById('title').textContent=m.title||'';document.getElementById('artist').textContent=m.artist||'';const p=m.durationSeconds>0?m.positionSeconds/m.durationSeconds*100:0;document.getElementById('fill').style.width=Math.max(0,Math.min(100,p))+'%'}catch(e){}}setInterval(update,500);update();
</script></body></html>
""";
        return template.Replace("__THEME__", ThemeCss(theme), StringComparison.Ordinal);
    }

    private static string ThemeCss(string theme)
    {
        var key = theme.ToLowerInvariant();
        if (key.Contains("cobra")) return ".msg,#card{background:linear-gradient(90deg,rgba(1,15,8,.94),rgba(2,40,19,.82));border:1px solid rgba(40,255,120,.45);border-left:4px solid #28ff78}.platformIcon{box-shadow:0 0 9px rgba(40,255,120,.45)}.fill{background:#28ff78}";
        if (key.Contains("ukraine")) return ".msg,#card{background:linear-gradient(90deg,rgba(0,37,89,.94),rgba(20,45,72,.84));border-left:4px solid #FFD329}.platformIcon{box-shadow:0 0 9px rgba(72,168,255,.45)}.fill{background:#FFD329}";
        if (key.Contains("minimal")) return ".msg,#card{background:rgba(0,0,0,.18);text-shadow:0 2px 4px #000}.platformIcon{box-shadow:0 0 9px rgba(102,217,255,.45)}";
        if (key.Contains("compact")) return ".msg,#card{background:rgba(4,14,22,.88);border-left:3px solid #45b6ff}.msg{padding:5px 8px;font-size:14px}.platformIcon{box-shadow:0 0 9px rgba(69,182,255,.45)}";
        return ".msg,#card{background:linear-gradient(90deg,rgba(4,20,32,.95),rgba(7,44,61,.82));border:1px solid rgba(69,182,255,.48);border-left:4px solid #45b6ff;box-shadow:0 0 16px rgba(0,130,210,.18)}.platformIcon{box-shadow:0 0 9px rgba(69,182,255,.45)}";
    }

    public async Task StopAsync()
    {
        if (_listener is null) return;
        _cts?.Cancel();
        _listener.Stop();
        _listener = null;
        if (_loop is not null) try { await _loop.ConfigureAwait(false); } catch { }
        _cts?.Dispose();
        _cts = null;
    }

    public async ValueTask DisposeAsync() => await StopAsync().ConfigureAwait(false);
}
