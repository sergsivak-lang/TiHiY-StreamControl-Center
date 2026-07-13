@echo off
setlocal
cd /d "%~dp0"
title TiHiY StreamControl Center - GitHub Upload
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0UPLOAD-TO-GITHUB.ps1"
set "EXITCODE=%ERRORLEVEL%"
echo.
if not "%EXITCODE%"=="0" (
  echo Upload failed. Open github-upload.log in this folder and send it to ChatGPT.
) else (
  echo Upload completed successfully.
)
pause
exit /b %EXITCODE%
