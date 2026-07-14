@echo off
setlocal EnableExtensions
cd /d "%~dp0"
set "CFG=%APPDATA%\TiHiY\StreamControlCenter\settings.json"
if exist "%CFG%" ren "%CFG%" "settings.backup-before-v095.json"
call "%~dp0START-HERE.cmd"
exit /b %ERRORLEVEL%
