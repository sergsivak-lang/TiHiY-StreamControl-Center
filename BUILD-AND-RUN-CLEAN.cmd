@echo off
setlocal EnableExtensions EnableDelayedExpansion
cd /d "%~dp0"

set "APP_VERSION=v1.0.5.2"
if exist "VERSION.txt" set /p APP_VERSION=<"VERSION.txt"

if not exist "BuildLogs" mkdir "BuildLogs"
set "LOG=%CD%\BuildLogs\build-and-run-latest.log"
set "APPLOG=%APPDATA%\TiHiY\StreamControlCenter\Logs"
>"%LOG%" echo [%DATE% %TIME%] START !APP_VERSION! CLEAN BUILD

taskkill /F /T /IM TiHiY.StreamControlCenter.exe >nul 2>nul

echo [1/6] Removing old WPF build cache...
for %%D in (bin obj Release BuildOutput) do (
  if exist "%%D" rmdir /s /q "%%D"
)
if exist "%TEMP%\.net\TiHiY.StreamControlCenter" rmdir /s /q "%TEMP%\.net\TiHiY.StreamControlCenter"
if exist "%TEMP%\TiHiY-StreamControlCenter-startup-crash.txt" del /q "%TEMP%\TiHiY-StreamControlCenter-startup-crash.txt"
if exist "%APPLOG%\startup-stage-latest.txt" del /q "%APPLOG%\startup-stage-latest.txt"

echo [2/6] Cleaning project...
dotnet clean "TiHiY.StreamControlCenter.csproj" -c Release -r win-x64 >>"%LOG%" 2>&1

echo [3/6] Restoring packages...
dotnet restore "TiHiY.StreamControlCenter.csproj" -r win-x64 --disable-parallel >>"%LOG%" 2>&1
if errorlevel 1 goto build_failed

echo [4/6] Publishing safe self-contained MULTIFILE build...
dotnet publish "TiHiY.StreamControlCenter.csproj" -c Release -r win-x64 --self-contained true ^
  -p:PublishSingleFile=false ^
  -p:DebugType=portable ^
  -p:DebugSymbols=true ^
  --no-restore -o "Release" >>"%LOG%" 2>&1
if errorlevel 1 goto build_failed

set "EXE=%CD%\Release\TiHiY.StreamControlCenter.exe"
if not exist "!EXE!" goto build_failed

echo [5/6] Starting program...
start "" "!EXE!"

echo [6/6] Checking that the program stays open...
timeout /t 6 /nobreak >nul
tasklist /FI "IMAGENAME eq TiHiY.StreamControlCenter.exe" 2>nul | find /I "TiHiY.StreamControlCenter.exe" >nul
if errorlevel 1 goto app_closed

echo.
echo SUCCESS: !APP_VERSION! is running.
echo EXE: !EXE!
echo Keep the complete Release folder together.
exit /b 0

:app_closed
echo.
echo ERROR: The program closed during startup.
>>"%LOG%" echo ERROR: Program closed during startup verification.
if exist "%APPLOG%\startup-stage-latest.txt" (
  echo.
  echo ---------- STARTUP STAGES ----------
  type "%APPLOG%\startup-stage-latest.txt"
  copy /y "%APPLOG%\startup-stage-latest.txt" "BuildLogs\startup-stage-latest.txt" >nul
)
if exist "%APPLOG%\startup-crash-latest.txt" (
  echo.
  echo ---------- FULL STARTUP CRASH ----------
  type "%APPLOG%\startup-crash-latest.txt"
  echo ---------- END CRASH ----------
  copy /y "%APPLOG%\startup-crash-latest.txt" "BuildLogs\startup-crash-latest.txt" >nul
  start "" notepad.exe "%APPLOG%\startup-crash-latest.txt"
) else (
  echo No startup crash file was found.
  if exist "%APPLOG%" start "" "%APPLOG%"
)
echo.
echo Send these two files from BuildLogs:
echo   startup-crash-latest.txt
echo   startup-stage-latest.txt
exit /b 30

:build_failed
echo.
echo ERROR: Build failed for !APP_VERSION!.
echo Log: !LOG!
type "!LOG!"
exit /b 20
