@echo off
setlocal EnableExtensions
cd /d "%~dp0"
if not exist "Release\TiHiY.StreamControlCenter.exe" (
  echo Release\TiHiY.StreamControlCenter.exe not found.
  echo Run BUILD-AND-RUN.bat first.
  pause
  exit /b 1
)
start "" "%~dp0Release\TiHiY.StreamControlCenter.exe"
endlocal
