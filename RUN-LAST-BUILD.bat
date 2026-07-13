@echo off
setlocal
cd /d "%~dp0"
set "EXE=bin\Release\net9.0-windows\TiHiY.StreamControlCenter.exe"
if not exist "%EXE%" (
  echo Release build not found. Run BUILD-AND-RUN.bat first.
  pause
  exit /b 1
)
start "" "%EXE%"
