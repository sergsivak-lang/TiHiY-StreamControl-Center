# Як додати проєкт у GitHub

## Варіант через браузер

1. Відкрий репозиторій `sergsivak-lang/TiHiY-StreamControl-Center`.
2. Натисни **Add file → Upload files**.
3. Розпакуй ZIP на комп’ютері.
4. Відкрий папку `TiHiY-StreamControl-Center`.
5. Виділи **весь вміст усередині папки**, включно з папкою `.github`.
6. Перетягни файли у вікно GitHub.
7. Commit message: `Initial TiHiY StreamControl Center v0.4`.
8. Натисни **Commit changes**.

Після завантаження відкрий вкладку **Actions**. Workflow `Windows Build` автоматично збере self-contained Windows x64 версію.

## Де завантажити зібрану програму

1. Відкрий **Actions → Windows Build**.
2. Відкрий останній успішний запуск із зеленою галочкою.
3. Унизу сторінки знайди **Artifacts**.
4. Завантаж `TiHiY-StreamControl-Center-win-x64`.

## Важливо

- Не завантажуй у GitHub пароль OBS, токени Twitch/YouTube або інші секрети.
- Папки `bin`, `obj`, `.vs` завантажувати не потрібно — вони виключені через `.gitignore`.
- Якщо браузер не дозволяє завантажити приховану папку `.github`, скористайся GitHub Desktop.
