using System.Globalization;
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
    private readonly Func<object> _donationSummaryProvider;
    private readonly Func<string> _themeProvider;
    private readonly Func<AppSettings> _settingsProvider;
    private TcpListener? _listener;
    private CancellationTokenSource? _cts;
    private Task? _loop;

    public int Port { get; private set; }
    public bool IsRunning => _listener is not null;
    public event EventHandler? StatusChanged;

    public OverlayServer(
        Func<IReadOnlyList<ChatMessage>> chatProvider,
        Func<IReadOnlyList<DonationEvent>> donationProvider,
        Func<object> nowPlayingProvider,
        Func<object> streamStatsProvider,
        Func<object> donationSummaryProvider,
        Func<string> themeProvider,
        Func<AppSettings> settingsProvider)
    {
        _chatProvider = chatProvider;
        _donationProvider = donationProvider;
        _nowPlayingProvider = nowPlayingProvider;
        _streamStatsProvider = streamStatsProvider;
        _donationSummaryProvider = donationSummaryProvider;
        _themeProvider = themeProvider;
        _settingsProvider = settingsProvider;
    }

    public Task StartAsync(int port)
    {
        if (_listener is not null) return Task.CompletedTask;
        Port = port;
        _cts = new CancellationTokenSource();
        _listener = new TcpListener(IPAddress.Loopback, port);
        _listener.Start();
        _loop = AcceptLoopAsync(_cts.Token);
        StatusChanged?.Invoke(this, EventArgs.Empty);
        return Task.CompletedTask;
    }

    public async Task RestartAsync(int port)
    {
        await StopAsync();
        await StartAsync(port);
    }

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
            catch
            {
                try { await Task.Delay(100, token); }
                catch { break; }
            }
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
                var data = _chatProvider().TakeLast(50).Select(m => new
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
                    isTest = d.IsTest,
                    isReplay = d.IsReplay,
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

            if (path == "/api/donation-summary")
            {
                await RespondAsync(stream, "application/json; charset=utf-8", JsonSerializer.Serialize(_donationSummaryProvider()), token);
                return;
            }

            var theme = GetQuery(uri, "theme") ?? _themeProvider();
            if (path == "/overlay/chat")
            {
                await RespondAsync(stream, "text/html; charset=utf-8", BuildChatHtml(theme, _settingsProvider()), token);
                return;
            }
            if (path == "/overlay/alerts")
            {
                await RespondAsync(stream, "text/html; charset=utf-8", BuildAlertsHtml(theme), token);
                return;
            }
            if (path == "/overlay/now-playing")
            {
                await RespondAsync(stream, "text/html; charset=utf-8", BuildNowPlayingHtml(theme), token);
                return;
            }
            if (path == "/overlay/donatello")
            {
                await RespondAsync(stream, "text/html; charset=utf-8", BuildDonatelloHtml(theme), token);
                return;
            }
            if (path == "/overlay/top-donors")
            {
                await RespondAsync(stream, "text/html; charset=utf-8", BuildTopDonorsHtml(), token);
                return;
            }
            if (path == "/overlay/goal")
            {
                await RespondAsync(stream, "text/html; charset=utf-8", BuildGoalHtml(), token);
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
            if (parts.Length == 2 && string.Equals(parts[0], name, StringComparison.OrdinalIgnoreCase))
                return Uri.UnescapeDataString(parts[1]);
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

    private static string BuildChatHtml(string theme, AppSettings settings)
    {
        var fontSize = Math.Clamp(settings.StreamChatOverlayFontSize, 11, 48).ToString("0.#", CultureInfo.InvariantCulture);
        var maxMessages = Math.Clamp(settings.StreamChatOverlayMaxMessages, 3, 30).ToString(CultureInfo.InvariantCulture);
        var textColor = CssColor(settings.StreamChatOverlayTextColor, "#F2FAFF");
        var userColor = CssColor(settings.StreamChatOverlayUserColor, "#FFD329");
        var background = CssRgba(settings.StreamChatOverlayBackgroundColor, settings.StreamChatOverlayBackgroundOpacity);

        var template = """
<!doctype html><html lang="uk"><head><meta charset="utf-8"><style>
html,body{margin:0;background:transparent;overflow:hidden;font-family:"Segoe UI",sans-serif}.wrap{position:absolute;left:12px;right:12px;bottom:44px;display:flex;flex-direction:column;gap:5px}.stats{position:absolute;left:12px;right:12px;bottom:7px;display:flex;justify-content:flex-end;gap:16px;padding:5px 3px;background:transparent;font-size:14px;font-weight:800;color:#dff7ff;text-shadow:0 2px 5px rgba(0,0,0,.95)}.msg{padding:6px 8px;border-radius:5px;font-size:__FONT__px;line-height:1.32;color:__TEXT__;background:__BACKGROUND__;text-shadow:0 2px 5px rgba(0,0,0,.98)}.line{display:flex;gap:9px;align-items:flex-start}.platformIcon,.statIcon{display:inline-flex;align-items:center;justify-content:center;flex:0 0 auto}.platformIcon{width:28px;height:23px}.statIcon{width:20px;height:18px;margin-right:5px}.platformIcon svg,.statIcon svg{display:block;width:100%;height:100%}.user{font-weight:900;flex:0 0 auto;color:__USER__}.text{word-break:break-word;min-width:0}.highlight .text,.highlight .user{color:#FFD329!important;font-weight:900}
__THEME__
</style></head><body><div id="chat" class="wrap"></div><div class="stats"><span id="tw"></span><span id="yt"></span><span id="likes">♥ 0</span></div><script>
const nodes=new Map();const maxMessages=__MAX__;
function svg(platform){const p=(platform||'').toUpperCase();if(p==='YOUTUBE')return '<svg viewBox="0 0 32 22" aria-label="YouTube"><rect width="32" height="22" rx="6" fill="#ff0033"/><path d="M13 6.5L22 11l-9 4.5z" fill="#fff"/></svg>';if(p==='DONATELLO')return '<svg viewBox="0 0 24 24"><path d="M12 21S3 15.6 3 8.8C3 5.2 7.5 3.7 12 7.5 16.5 3.7 21 5.2 21 8.8 21 15.6 12 21 12 21z" fill="#ffd329"/></svg>';return '<svg viewBox="0 0 28 28" aria-label="Twitch"><path d="M4 3h21v15l-6 6h-5l-3 3v-3H4z" fill="#9147ff"/><path d="M9 7h3v8H9zm7 0h3v8h-3z" fill="#fff"/></svg>'}
function icon(platform,small=false){const e=document.createElement('span');e.className=small?'statIcon':'platformIcon';e.innerHTML=svg(platform);return e}
function makeMessage(m){const box=document.createElement('div');box.className='msg'+(m.highlighted?' highlight':'');const line=document.createElement('div');line.className='line';line.append(icon(m.platform));const user=document.createElement('span');user.className='user';user.style.color=m.foreground||'';user.textContent=(m.user||'Глядач')+':';line.append(user);const text=document.createElement('span');text.className='text';text.textContent=m.text||'';line.append(text);box.append(line);return box}
async function update(){try{const data=await(await fetch('/api/chat',{cache:'no-store'})).json();const root=document.getElementById('chat');const latest=data.slice(-maxMessages);const active=new Set();for(const m of latest){const key=String(m.id||((m.platform||'')+'|'+(m.user||'')+'|'+(m.text||'')));active.add(key);let box=nodes.get(key);if(!box){box=makeMessage(m);nodes.set(key,box)}root.append(box)}for(const [key,box] of [...nodes])if(!active.has(key)){box.remove();nodes.delete(key)}const st=await(await fetch('/api/stream-stats',{cache:'no-store'})).json();const tw=document.getElementById('tw');tw.replaceChildren(icon('TWITCH',true),document.createTextNode(' '+(st.twitchViewers||0)));const yt=document.getElementById('yt');yt.replaceChildren(icon('YOUTUBE',true),document.createTextNode(' '+(st.youtubeViewers||0)));document.getElementById('likes').textContent='♥ '+(st.youtubeLikes||0)}catch(e){}}
setInterval(update,900);update();
</script></body></html>
""";
        return template
            .Replace("__FONT__", fontSize, StringComparison.Ordinal)
            .Replace("__MAX__", maxMessages, StringComparison.Ordinal)
            .Replace("__TEXT__", textColor, StringComparison.Ordinal)
            .Replace("__USER__", userColor, StringComparison.Ordinal)
            .Replace("__BACKGROUND__", background, StringComparison.Ordinal)
            .Replace("__THEME__", ThemeCss(theme), StringComparison.Ordinal);
    }

    private static string BuildAlertsHtml(string theme)
    {
        var template = """
<!doctype html><html lang="uk"><head><meta charset="utf-8"><style>
html,body{margin:0;background:transparent;overflow:hidden;font-family:"Segoe UI",sans-serif;color:#fff}#host{position:absolute;inset:0;display:flex;align-items:center;justify-content:center;pointer-events:none}#card{min-width:520px;max-width:1000px;padding:24px 34px;border-radius:14px;opacity:0;transform:scale(.86) translateY(30px);transition:opacity .28s,transform .28s;text-align:center;text-shadow:0 3px 8px #000;background:rgba(4,20,32,.88);border:2px solid #FFD329;box-shadow:0 0 38px rgba(255,211,41,.34)}#card.on{opacity:1;transform:scale(1) translateY(0)}.source{font-size:15px;font-weight:900;letter-spacing:2px;color:#FFD329}.amount{font-size:48px;font-weight:1000;line-height:1.05;margin:8px 0}.user{font-size:27px;font-weight:900}.message{font-size:21px;margin-top:8px;white-space:pre-wrap;word-break:break-word}.sub .source,.sub .amount{color:#7DFFB2}.sub{border-color:#2BEB82}
__THEME__
</style></head><body><div id="host"><div id="card"><div id="source" class="source"></div><div id="amount" class="amount"></div><div id="user" class="user"></div><div id="message" class="message"></div></div></div><script>
let initialized=false,lastId='',queue=[],showing=false;async function poll(){try{const data=await(await fetch('/api/donations',{cache:'no-store'})).json();if(!data.length)return;const latest=data[data.length-1];if(!initialized){initialized=true;lastId=String(latest.id||'');return}let fresh=[];for(let i=data.length-1;i>=0;i--){if(String(data[i].id||'')===lastId)break;fresh.unshift(data[i])}if(fresh.length){lastId=String(latest.id||lastId);queue.push(...fresh);showNext()}}catch(e){}}function showNext(){if(showing||!queue.length)return;showing=true;const d=queue.shift();const c=document.getElementById('card');c.classList.toggle('sub',(d.kind||'').toUpperCase()==='SUBSCRIPTION');document.getElementById('source').textContent=(d.kind||'').toUpperCase()==='SUBSCRIPTION'?'НОВА ПЛАТНА ПІДПИСКА':(d.source||'ДОНАТ');document.getElementById('amount').textContent=(Number(d.amount)||0).toLocaleString('uk-UA')+' '+(d.currency||'');document.getElementById('user').textContent=d.user||'Анонім';document.getElementById('message').textContent=d.message||'';requestAnimationFrame(()=>c.classList.add('on'));setTimeout(()=>{c.classList.remove('on');setTimeout(()=>{showing=false;showNext()},400)},7000)}setInterval(poll,800);poll();
</script></body></html>
""";
        return template.Replace("__THEME__", ThemeCss(theme), StringComparison.Ordinal);
    }

    private static string BuildDonatelloHtml(string theme)
    {
        var template = """
<!doctype html><html lang="uk"><head><meta charset="utf-8"><style>
html,body{margin:0;background:transparent;overflow:hidden;font-family:"Segoe UI",sans-serif;color:#F0FAFF}.wrap{position:absolute;left:16px;top:16px;right:16px;display:grid;grid-template-columns:minmax(320px,1.08fr) minmax(280px,.92fr);gap:16px}.card{background:rgba(5,19,31,.88);border:1px solid rgba(65,214,255,.45);border-radius:16px;padding:16px 18px}.title{font-size:14px;font-weight:900;color:#45D6FF}.goalTitle{font-size:24px;font-weight:900;margin:8px 0;color:#fff}.goalLine{display:flex;justify-content:space-between;font-weight:800;color:#FFD24A;font-size:18px}.bar{height:16px;background:rgba(255,255,255,.08);border-radius:999px;overflow:hidden;margin-top:10px}.fill{height:100%;width:0;background:#FFD329}.lastUser{font-size:26px;font-weight:900}.lastAmount{font-size:34px;font-weight:1000;color:#FFD24A}.lastMessage{margin-top:8px;font-size:19px}.list{display:flex;flex-direction:column;gap:8px;margin-top:10px}.item{display:grid;grid-template-columns:1fr auto;gap:10px;padding:8px 10px;background:rgba(2,11,18,.72);border:1px solid rgba(34,137,178,.25)}
__THEME__
</style></head><body><div class="wrap"><div class="card"><div class="title">DONATELLO • ЦІЛЬ ЗБОРУ</div><div id="goalTitle" class="goalTitle">Ціль збору</div><div class="goalLine"><span id="goalCurrent">0 UAH</span><span id="goalTarget">0 UAH</span></div><div class="bar"><div id="goalFill" class="fill"></div></div></div><div class="card"><div class="title">ОСТАННІ ДОНАТИ</div><div id="lastUser" class="lastUser">Очікування…</div><div id="lastAmount" class="lastAmount">—</div><div id="lastMessage" class="lastMessage"></div><div id="list" class="list"></div></div></div><script>
function fmt(v,c){return (Number(v)||0).toLocaleString('uk-UA',{maximumFractionDigits:2})+' '+(c||'UAH')}async function update(){try{const d=await(await fetch('/api/donation-summary',{cache:'no-store'})).json();document.getElementById('goalTitle').textContent=d.goalTitle||'Ціль збору';document.getElementById('goalCurrent').textContent=fmt(d.currentAmount,d.goalCurrency);document.getElementById('goalTarget').textContent=fmt(d.goalAmount,d.goalCurrency);document.getElementById('goalFill').style.width=Math.max(0,Math.min(100,Number(d.progressPercent)||0))+'%';if(d.lastDonation){document.getElementById('lastUser').textContent=d.lastDonation.user||'Анонім';document.getElementById('lastAmount').textContent=fmt(d.lastDonation.amount,d.lastDonation.currency);document.getElementById('lastMessage').textContent=d.lastDonation.message||''}const list=document.getElementById('list');list.replaceChildren();for(const x of (d.recent||[]).slice(0,5)){const row=document.createElement('div');row.className='item';row.innerHTML='<b></b><span></span>';row.children[0].textContent=(x.user||'Анонім')+' • '+fmt(x.amount,x.currency);row.children[1].textContent=new Date(x.time).toLocaleTimeString('uk-UA',{hour:'2-digit',minute:'2-digit'});list.append(row)}}catch(e){}}setInterval(update,1000);update();
</script></body></html>
""";
        return template.Replace("__THEME__", ThemeCss(theme), StringComparison.Ordinal);
    }

    private static string BuildTopDonorsHtml() => """
<!doctype html><html lang="uk"><head><meta charset="utf-8"><style>
html,body{margin:0;background:transparent;overflow:hidden;font-family:"Segoe UI",sans-serif}#frame{position:absolute;inset:0;display:flex;align-items:center;overflow:hidden;border-radius:8px}#track{display:flex;align-items:center;gap:42px;white-space:nowrap;will-change:transform;font-size:30px;font-weight:900;text-shadow:0 2px 6px #000}.item{display:inline-flex;gap:10px;align-items:center}.rank{opacity:.72}.amount{font-weight:1000}
</style></head><body><div id="frame"><div id="track"></div></div><script>
let signature='';function hexRgba(hex,a){const h=(hex||'#06172A').replace('#','');const v=h.length===8?h.slice(2):h;const n=parseInt(v,16)||0;return `rgba(${(n>>16)&255},${(n>>8)&255},${n&255},${Math.max(0,Math.min(1,Number(a)||0))})`}async function update(){try{const d=await(await fetch('/api/donation-summary',{cache:'no-store'})).json();const items=d.topDonors||[];const sig=JSON.stringify(items)+JSON.stringify(d.tickerStyle||{});if(sig===signature)return;signature=sig;const s=d.tickerStyle||{};const frame=document.getElementById('frame');const track=document.getElementById('track');frame.style.background=hexRgba(s.backgroundColor,s.backgroundOpacity);track.style.color=s.textColor||'#FFD329';track.replaceChildren();const source=items.length?items:[{rank:1,user:'Очікування донатів',amount:0,currency:d.goalCurrency||'UAH'}];for(let r=0;r<2;r++)for(const x of source){const el=document.createElement('span');el.className='item';el.innerHTML='<span class="rank"></span><span class="user"></span><span class="amount"></span>';el.children[0].textContent='#'+x.rank;el.children[1].textContent=x.user||'Анонім';el.children[2].textContent=(Number(x.amount)||0).toLocaleString('uk-UA')+' '+(x.currency||'UAH');track.append(el)}requestAnimationFrame(()=>{const distance=Math.max(1,track.scrollWidth/2);const speed=Math.max(20,Math.min(250,Number(s.speed)||70));track.animate([{transform:'translateX(0)'},{transform:`translateX(-${distance}px)`}],{duration:distance/speed*1000,iterations:Infinity,easing:'linear'})})}catch(e){}}setInterval(update,1500);update();
</script></body></html>
""";

    private static string BuildGoalHtml() => """
<!doctype html><html lang="uk"><head><meta charset="utf-8"><style>
html,body{margin:0;background:transparent;overflow:hidden;font-family:"Segoe UI",sans-serif}#card{position:absolute;inset:0;border-radius:12px;padding:18px 22px;box-sizing:border-box;display:flex;flex-direction:column;justify-content:center;text-shadow:0 2px 6px #000}.title{font-size:28px;font-weight:1000}.line{display:flex;justify-content:space-between;gap:20px;font-size:22px;font-weight:900;margin-top:8px}.bar{height:22px;background:rgba(255,255,255,.12);border:1px solid rgba(255,255,255,.24);border-radius:999px;overflow:hidden;margin-top:12px}.fill{height:100%;width:0;transition:width .5s}.percent{text-align:right;font-size:16px;font-weight:900;margin-top:5px}
</style></head><body><div id="card"><div id="title" class="title">Ціль збору</div><div class="line"><span id="current">0 UAH</span><span id="target">0 UAH</span></div><div class="bar"><div id="fill" class="fill"></div></div><div id="percent" class="percent">0%</div></div><script>
function fmt(v,c){return (Number(v)||0).toLocaleString('uk-UA',{maximumFractionDigits:2})+' '+(c||'UAH')}async function update(){try{const d=await(await fetch('/api/donation-summary',{cache:'no-store'})).json();const s=d.goalStyle||{};const card=document.getElementById('card');card.style.background=s.backgroundColor||'#06172A';card.style.color=s.textColor||'#F4F8FF';document.getElementById('title').textContent=d.goalTitle||'Ціль збору';document.getElementById('current').textContent=fmt(d.currentAmount,d.goalCurrency);document.getElementById('target').textContent=fmt(d.goalAmount,d.goalCurrency);const p=Math.max(0,Math.min(100,Number(d.progressPercent)||0));const fill=document.getElementById('fill');fill.style.width=p+'%';fill.style.background=s.barColor||'#FFD329';document.getElementById('percent').textContent=p.toFixed(0)+'%'}catch(e){}}setInterval(update,1000);update();
</script></body></html>
""";

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
        if (key.Contains("cobra")) return ".msg,#card{border:1px solid rgba(40,255,120,.45);border-left:4px solid #28ff78}.fill{background:#28ff78}";
        if (key.Contains("ukraine")) return ".msg,#card{border:1px solid rgba(54,158,255,.50);border-left:4px solid #FFD329}.fill{background:#FFD329}";
        if (key.Contains("neon")) return ".msg,#card{border:1px solid rgba(255,48,202,.55);border-left:4px solid #35EAFF}.fill{background:#FF35D3}";
        if (key.Contains("military")) return ".msg,#card{border:1px solid rgba(140,158,65,.48);border-left:4px solid #D4B642}.fill{background:#9A7A1C}";
        if (key.Contains("synthwave")) return ".msg,#card{border:1px solid rgba(189,58,255,.52);border-left:4px solid #FF35B5}.fill{background:#FF35B5}";
        if (key.Contains("cyberpunk")) return ".msg,#card{border:1px solid rgba(32,224,255,.52);border-left:4px solid #FF2E8A}.fill{background:#FF2E8A}";
        if (key.Contains("stalker")) return ".msg,#card{border:1px solid rgba(142,130,83,.48);border-left:4px solid #C8893B}.fill{background:#C8893B}";
        if (key.Contains("minimal")) return ".msg,#card{border:0;background:rgba(0,0,0,.12)}";
        return ".msg,#card{border:1px solid rgba(69,182,255,.48);border-left:4px solid #45b6ff}";
    }

    private static string CssColor(string value, string fallback)
    {
        var text = value?.Trim() ?? string.Empty;
        if (!text.StartsWith('#')) text = "#" + text;
        return text.Length is 7 or 9 && text.Skip(1).All(Uri.IsHexDigit) ? text.ToUpperInvariant() : fallback;
    }

    private static string CssRgba(string value, double opacity)
    {
        var hex = CssColor(value, "#000000").TrimStart('#');
        if (hex.Length == 8) hex = hex[2..];
        var red = int.Parse(hex[..2], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        var green = int.Parse(hex.Substring(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        var blue = int.Parse(hex.Substring(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        return $"rgba({red},{green},{blue},{Math.Clamp(opacity, 0, 0.95).ToString("0.###", CultureInfo.InvariantCulture)})";
    }

    public async Task StopAsync()
    {
        if (_listener is null) return;
        _cts?.Cancel();
        _listener.Stop();
        _listener = null;
        if (_loop is not null)
            try { await _loop.ConfigureAwait(false); } catch { }
        _cts?.Dispose();
        _cts = null;
        StatusChanged?.Invoke(this, EventArgs.Empty);
    }

    public async ValueTask DisposeAsync() => await StopAsync().ConfigureAwait(false);
}