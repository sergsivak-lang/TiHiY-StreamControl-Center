@echo off
setlocal
cd /d "%~dp0"
title TiHiY Cyber Amber - Build and Screenshot

set "OUT=%CD%\artifacts-local"
if exist "%OUT%" rmdir /s /q "%OUT%"
mkdir "%OUT%"

echo [1/4] RESTORE...
dotnet restore TiHiY.StreamControlCenter.sln
if errorlevel 1 goto :fail

echo [2/4] PUBLISH WINDOWS BUILD...
dotnet publish TiHiY.StreamControlCenter.csproj -c Release -r win-x64 --self-contained true -o "%OUT%\publish" -p:PublishSingleFile=false
if errorlevel 1 goto :fail

echo [3/4] RENDER REAL WPF SCREENSHOT...
"%OUT%\publish\TiHiY.StreamControlCenter.exe" --render-preview --output="%OUT%\Cyber-Amber-Actual.png"
if errorlevel 1 goto :fail
if not exist "%OUT%\Cyber-Amber-Actual.png" goto :fail

echo [4/4] OPEN SCREENSHOT...
start "" "%OUT%\Cyber-Amber-Actual.png"
echo.
echo SUCCESS: %OUT%\Cyber-Amber-Actual.png
pause
exit /b 0

:fail
echo.
echo VERIFY FAILED. Check the messages above.
pause
exit /b 1
