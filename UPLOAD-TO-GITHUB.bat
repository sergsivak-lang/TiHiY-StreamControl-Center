@echo off
setlocal
cd /d "%~dp0"

echo ==================================================
echo  TiHiY StreamControl Center - Upload to GitHub
echo ==================================================
echo.

where git >nul 2>nul
if errorlevel 1 (
    echo [ПОМИЛКА] Git не знайдено.
    echo Встановіть Git for Windows і запустіть файл знову.
    pause
    exit /b 1
)

if not exist ".git" (
    git init
    if errorlevel 1 goto :failed
)

git config user.name "sergsivak-lang"
git config user.email "sergsivak-lang@users.noreply.github.com"
git branch -M main

git add .
git diff --cached --quiet
if not errorlevel 1 (
    echo Нових змін для завантаження немає.
) else (
    git commit -m "Build TiHiY StreamControl Center core"
    if errorlevel 1 goto :failed
)

git remote get-url origin >nul 2>nul
if errorlevel 1 (
    git remote add origin https://github.com/sergsivak-lang/TiHiY-StreamControl-Center.git
) else (
    git remote set-url origin https://github.com/sergsivak-lang/TiHiY-StreamControl-Center.git
)

echo.
echo Відкриється авторизація GitHub, якщо вона ще не виконана.
git push -u origin main
if errorlevel 1 goto :failed

echo.
echo ==================================================
echo ГОТОВО: проєкт завантажено в GitHub.
echo ==================================================
pause
exit /b 0

:failed
echo.
echo [ПОМИЛКА] GitHub не прийняв завантаження.
echo Перевірте авторизацію Git for Windows і доступ до репозиторію.
pause
exit /b 1
