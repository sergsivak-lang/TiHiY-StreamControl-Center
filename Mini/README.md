# TiHiY StreamControl MINI

Окрема полегшена WPF-програма для стріму.

## Залишено
- Мультичат Twitch + YouTube
- Донати: Donatello, YouTube Super Chat / Super Sticker, Twitch Bits / subscriptions
- Сповіщення про важливі події

## Прибрано
- OBS Audio Mixer
- керування сценами / Preview / Record / Stream
- PC Monitor / AIDA64
- Music Player
- Overlay Server
- системний dashboard і Modules

## Незалежність від повної версії
- EXE: `TiHiY.StreamControlMini.exe`
- налаштування: `%APPDATA%\TiHiY\StreamControlMini\settings.json`
- журнал: `%APPDATA%\TiHiY\StreamControlMini\Logs`
- Windows Credential Manager prefix: `TiHiY.StreamControlMini.*`

На першому запуску, якщо Mini ще не має власних налаштувань/секретів, він може одноразово імпортувати сумісні Twitch/YouTube/Donatello параметри з `TiHiY StreamControl Center` v1.0.5.2. Після цього обидві програми працюють незалежно.

## Build
```powershell
dotnet publish Mini/TiHiY.StreamControlMini.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -o publish-mini
```
