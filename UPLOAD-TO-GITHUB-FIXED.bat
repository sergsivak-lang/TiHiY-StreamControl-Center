@echo off
chcp 65001 >nul
setlocal EnableExtensions
cd /d "%~dp0"

echo =============================================================
echo   TiHiY StreamControl Center - Safe GitHub Sync
echo =============================================================
echo.

where git >nul 2>nul
if errorlevel 1 (
    echo [ERROR] Git for Windows was not found.
    echo Install Git for Windows and run this file again.
    pause
    exit /b 1
)

if not exist ".git" (
    echo [1/8] Initializing local Git repository...
    git init
    if errorlevel 1 goto :failed
) else (
    echo [1/8] Local Git repository found.
)

git config user.name "sergsivak-lang"
git config user.email "sergsivak-lang@users.noreply.github.com"

git remote get-url origin >nul 2>nul
if errorlevel 1 (
    git remote add origin https://github.com/sergsivak-lang/TiHiY-StreamControl-Center.git
) else (
    git remote set-url origin https://github.com/sergsivak-lang/TiHiY-StreamControl-Center.git
)
if errorlevel 1 goto :failed

echo [2/8] Saving the current local project snapshot...
git branch -M main >nul 2>nul
git add -A

git diff --cached --quiet
if errorlevel 1 (
    git commit -m "Save local TiHiY StreamControl Center snapshot"
    if errorlevel 1 goto :failed
) else (
    echo Local files are already committed.
)

echo [3/8] Downloading the current GitHub history...
git fetch origin main
if errorlevel 1 goto :failed

for /f %%I in ('powershell -NoProfile -Command "Get-Date -Format yyyyMMdd-HHmmss"') do set "STAMP=%%I"
set "BACKUP_BRANCH=backup-before-cleanup-%STAMP%"

echo [4/8] Creating a safety backup branch on GitHub...
git branch "%BACKUP_BRANCH%" origin/main
if errorlevel 1 goto :failed

git push origin "%BACKUP_BRANCH%"
if errorlevel 1 goto :failed

echo Backup branch created: %BACKUP_BRANCH%

echo [5/8] Connecting the local project to the current GitHub main branch...
git checkout main >nul 2>nul
git reset --soft origin/main
if errorlevel 1 goto :failed

echo [6/8] Preparing the repository cleanup...
git add -A

git diff --cached --quiet
if errorlevel 1 (
    git commit -m "Clean repository and upload TiHiY StreamControl Center"
    if errorlevel 1 goto :failed
) else (
    echo GitHub already contains exactly the same project files.
)

echo [7/8] Uploading the clean project to GitHub...
git push -u origin main
if errorlevel 1 goto :failed

echo [8/8] Checking synchronization...
git status -sb

echo.
echo =============================================================
echo DONE

echo Main branch is synchronized with this folder.
echo Previous GitHub files were preserved in:
echo %BACKUP_BRANCH%
echo =============================================================
echo.
pause
exit /b 0

:failed
echo.
echo =============================================================
echo [ERROR] Synchronization was not completed.
echo Nothing was force-pushed.
echo The old GitHub version remains protected.
echo Check the error shown above.
echo =============================================================
echo.
pause
exit /b 1
