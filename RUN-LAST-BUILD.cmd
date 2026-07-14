@echo off
setlocal EnableExtensions
cd /d "%~dp0"
set "EXE=%~dp0Release\TiHiY.StreamControlCenter.exe"
if not exist "%EXE%" (
  echo ERROR: Release EXE was not found.
  echo Run START-HERE.cmd first.
  pause
  exit /b 1
)
start "" "%EXE%"
timeout /t 3 /nobreak >nul
tasklist /FI "IMAGENAME eq TiHiY.StreamControlCenter.exe" 2>nul | find /I "TiHiY.StreamControlCenter.exe" >nul
if errorlevel 1 (
  echo ERROR: The program closed immediately.
  if exist "%APPDATA%\TiHiY\StreamControlCenter\Logs" start "" "%APPDATA%\TiHiY\StreamControlCenter\Logs"
  pause
  exit /b 2
)
exit /b 0
