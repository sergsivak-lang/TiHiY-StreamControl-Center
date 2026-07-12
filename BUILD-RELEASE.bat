@echo off
setlocal
cd /d "%~dp0"

echo ==============================================
echo   TiHiY StreamControl Center - Release Build
echo ==============================================
echo.

where dotnet >nul 2>nul
if errorlevel 1 (
    echo [ПОМИЛКА] .NET 8 SDK не знайдено.
    echo Встановіть .NET 8 SDK, потім запустіть цей файл знову.
    pause
    exit /b 1
)

if exist "Release" rmdir /s /q "Release"

echo [1/3] Відновлення залежностей...
dotnet restore "TiHiY.StreamControlCenter.sln"
if errorlevel 1 goto :failed

echo [2/3] Перевірка збірки...
dotnet build "TiHiY.StreamControlCenter.sln" -c Release --no-restore
if errorlevel 1 goto :failed

echo [3/3] Створення готової Windows-програми...
dotnet publish "src\TiHiY.StreamControlCenter\TiHiY.StreamControlCenter.csproj" ^
  -c Release ^
  -r win-x64 ^
  --self-contained true ^
  -p:PublishSingleFile=true ^
  -p:IncludeNativeLibrariesForSelfExtract=true ^
  -o "Release"
if errorlevel 1 goto :failed

echo.
echo ==============================================
echo ГОТОВО

echo Файл програми:
echo %CD%\Release\TiHiY.StreamControlCenter.exe
echo ==============================================
start "" "%CD%\Release"
pause
exit /b 0

:failed
echo.
echo [ПОМИЛКА] Збірка не завершилась. Текст помилки знаходиться вище.
pause
exit /b 1
