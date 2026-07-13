@echo off
setlocal
cd /d "%~dp0"
title TiHiY StreamControl Center - Build and Run

echo [1/3] RESTORE...
dotnet restore TiHiY.StreamControlCenter.sln
if errorlevel 1 goto :fail

echo [2/3] BUILD RELEASE...
dotnet build TiHiY.StreamControlCenter.sln -c Release --no-restore
if errorlevel 1 goto :fail

echo [3/3] RUN...
start "" "bin\Release\net9.0-windows\TiHiY.StreamControlCenter.exe"
exit /b 0

:fail
echo.
echo BUILD FAILED.
pause
exit /b 1
