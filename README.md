# TiHiY StreamControl Center

Тестова робоча основа окремої програми керування стрімом для каналу **TiHiY-DED**.

## Уже реалізовано

- головне вікно українською мовою;
- вкладки **Чат**, **Overlay**, **Бот і команди**, **Налаштування**;
- локальний журнал подій усередині програми;
- тестові повідомлення Twitch та YouTube;
- локальні команди бота з редагуванням;
- overlay-чат як окреме Windows-вікно;
- автоматичне закриття overlay разом із головною програмою;
- прозорість застосовується тільки до фону overlay;
- текст чату та статистика залишаються непрозорими;
- перемикач **Пропускати кліки** керується тільки з головної програми;
- внизу overlay відображаються глядачі та лайки;
- локальне збереження налаштувань у `%AppData%\TiHiY\StreamControlCenter\settings.json`;
- GitHub Actions для автоматичної перевірки та створення Windows-збірки.

## Як запустити локально

1. Встановити **.NET 8 Desktop Runtime** або **.NET 8 SDK**.
2. Відкрити `TiHiY.StreamControlCenter.sln` у Visual Studio 2022.
3. Запустити проєкт `TiHiY.StreamControlCenter`.

Або в терміналі:

```powershell
dotnet run --project .\src\TiHiY.StreamControlCenter\TiHiY.StreamControlCenter.csproj
```

## Як отримати готову збірку з GitHub

Після успішного запуску workflow **Windows Build** відкрийте його та завантажте artifact:

`TiHiY-StreamControl-Center-win-x64`

## Наступні модулі

- OAuth-підключення Twitch;
- YouTube Live Chat API;
- автоматичне отримання реальних глядачів і лайків;
- алерти;
- OBS WebSocket;
- імпорт доступних налаштувань RutonyChat;
- інсталятор Windows.
