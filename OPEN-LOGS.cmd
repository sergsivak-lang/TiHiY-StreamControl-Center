@echo off
setlocal EnableExtensions
set "LOGDIR=%APPDATA%\TiHiY\StreamControlCenter\Logs"
if exist "%LOGDIR%" (
  start "" "%LOGDIR%"
  exit /b 0
)
echo No application logs exist yet.
echo Expected folder: %LOGDIR%
pause
exit /b 1
