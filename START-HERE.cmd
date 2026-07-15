@echo off
setlocal EnableExtensions
cd /d "%~dp0"

set "APP_VERSION=v1.0.5.2"
if exist "VERSION.txt" set /p APP_VERSION=<"VERSION.txt"

title TiHiY StreamControl Center %APP_VERSION%

echo ================================================================
echo  TiHiY StreamControl Center %APP_VERSION%
echo  FUNCTIONAL CORE - CYBER BLUE + AMBER
echo ================================================================
echo.
echo IMPORTANT: Run this file from a newly extracted folder.
echo Current folder: %CD%
echo.

if not exist "TiHiY.StreamControlCenter.csproj" goto missing_project
where dotnet.exe >nul 2>nul
if errorlevel 1 goto no_dotnet

call "%~dp0BUILD-AND-RUN-CLEAN.cmd"
set "RC=%ERRORLEVEL%"
if "%RC%"=="0" exit /b 0

echo.
echo Launcher finished with error code %RC%.
echo.
pause
exit /b %RC%

:missing_project
echo ERROR: Project file was not found.
echo Extract the FULL %APP_VERSION% ZIP into a NEW folder.
goto failed

:no_dotnet
echo ERROR: dotnet.exe was not found.
echo Install Microsoft .NET 9 SDK x64.
goto failed

:failed
echo.
pause
exit /b 1
