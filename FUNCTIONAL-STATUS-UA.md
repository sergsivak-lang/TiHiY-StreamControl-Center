# TiHiY StreamControl Center v1.0.3 — Functional status

## Real data paths

| Block | Source | Normal disconnected behavior | Connected behavior |
|---|---|---|---|
| Quick OBS mixer | OBS WebSocket v5 | Empty-state message | Actual OBS input list, meter, dB, volume and mute |
| Multichat | Twitch IRC + YouTube Live Chat API | Connect-channel prompt | Combined timestamped live messages and moderation |
| Donations | Donatello + Twitch + YouTube | No-events state | Persisted real donations/subscriptions/Super Chat/Bits |
| Donation goal | Real monetary history | 0 / configured goal | Actual sum; TEST/replay excluded |
| System health | local process + service states + OBS GetStats | Per-service state | CPU, RAM, OBS FPS, network, uptime and 6 integration states |
| Overlay | local HTTP server | Error/status in journal if port unavailable | Chat, alert and now-playing Browser Source URLs |
| Discord | Discord REST API v10 | Token/channel guidance | Live and monetization notifications |
| Music | Windows media player | Empty playlist | Local playback controls and now-playing API |

## Validation included in this package

- усі XAML-файли пройшли XML-перевірку;
- усі XAML event handlers знайдені у відповідних code-behind файлах;
- усі ресурси та pack URI іконок перевірені на наявність;
- структура адаптивних Grid/GridSplitter узгоджена з налаштуваннями збереження макета;
- звичайний режим запуску не додає демонстраційні повідомлення, аудіоканали або донати.

Фактичний Windows WPF build виконується `START-HERE.cmd`. Повний тест зовнішніх сервісів потребує власних облікових даних і запущених OBS/AIDA64.

## v1.0.3 hardware/layout additions

- AIDA64 rootless XML fragment parsing;
- CPU/RAM frequencies, CPU/GPU load and temperatures, RAM/VRAM usage;
- Windows fallback without fake sensor values;
- draggable dashboard and footer splitters;
- persisted block proportions and footer height;
- automatic scaling of the entire design surface, including fonts and buttons.
