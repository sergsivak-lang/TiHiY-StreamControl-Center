@echo off
setlocal
cd /d "%~dp0"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0VERIFY-LOCAL.ps1"
if errorlevel 1 (
  echo.
  echo VERIFICATION FAILED.
  pause
  exit /b 1
)
echo.
echo VERIFICATION COMPLETED.
pause
