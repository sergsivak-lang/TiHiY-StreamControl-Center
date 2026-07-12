@echo off
setlocal
cd /d "%~dp0"
where dotnet >nul 2>nul
if errorlevel 1 (
  echo [ПОМИЛКА] Не знайдено .NET 8 SDK.
  echo Завантаж: https://dotnet.microsoft.com/download/dotnet/8.0
  pause
  exit /b 1
)
dotnet restore
if errorlevel 1 goto error
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
if errorlevel 1 goto error
start "" "bin\Release\net8.0-windows\win-x64\publish\TiHiY.StreamControlCenter.exe"
exit /b 0
:error
echo.
echo Збірка завершилася з помилкою.
pause
exit /b 1
