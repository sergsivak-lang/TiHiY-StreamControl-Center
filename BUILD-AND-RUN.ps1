$ErrorActionPreference = 'Stop'
Set-Location -LiteralPath $PSScriptRoot
$logDir = Join-Path $PSScriptRoot 'BuildLogs'
New-Item -ItemType Directory -Force -Path $logDir | Out-Null
$logFile = Join-Path $logDir ("build-" + (Get-Date -Format 'yyyyMMdd-HHmmss') + '.log')

function Run-Step([string]$title, [scriptblock]$action) {
    Write-Host ""
    Write-Host "=== $title ===" -ForegroundColor Cyan
    & $action 2>&1 | Tee-Object -FilePath $logFile -Append
    if ($LASTEXITCODE -ne 0) { throw "$title failed with exit code $LASTEXITCODE" }
}

function Stop-OldApp {
    $processes = Get-Process -Name 'TiHiY.StreamControlCenter' -ErrorAction SilentlyContinue
    if ($processes) {
        Write-Host 'Closing the previous TiHiY StreamControl Center process...' -ForegroundColor Yellow
        $processes | Stop-Process -Force -ErrorAction SilentlyContinue
        Start-Sleep -Milliseconds 900
    }
    if (Get-Process -Name 'TiHiY.StreamControlCenter' -ErrorAction SilentlyContinue) {
        throw 'The old TiHiY.StreamControlCenter process could not be stopped. End it in Task Manager and run again.'
    }
}

function Remove-WithRetry([string]$path) {
    if (-not (Test-Path $path)) { return }
    for ($attempt = 1; $attempt -le 5; $attempt++) {
        try {
            Remove-Item -LiteralPath $path -Recurse -Force -ErrorAction Stop
            return
        }
        catch {
            if ($attempt -eq 5) { throw }
            Start-Sleep -Milliseconds 500
        }
    }
}

try {
    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
        throw '.NET SDK was not found. Install .NET 9 SDK x64 and run this file again.'
    }

    $sdks = dotnet --list-sdks
    if (-not ($sdks -match '^9\.')) {
        throw '.NET 9 SDK was not found. Installed SDKs: ' + ($sdks -join ', ')
    }

    Stop-OldApp
    Remove-WithRetry '.\bin'
    Remove-WithRetry '.\obj'
    Remove-WithRetry '.\Release'
    Remove-WithRetry '.\Release-new'

    Run-Step 'Restore' { dotnet restore '.\TiHiY.StreamControlCenter.csproj' -r win-x64 }
    Run-Step 'Build' { dotnet build '.\TiHiY.StreamControlCenter.csproj' -c Release --no-restore }
    Run-Step 'Publish win-x64 self-contained' {
        dotnet publish '.\TiHiY.StreamControlCenter.csproj' -c Release -r win-x64 --self-contained true --no-restore `
            -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None `
            -o '.\Release-new'
    }

    $newExe = Join-Path $PSScriptRoot 'Release-new\TiHiY.StreamControlCenter.exe'
    if (-not (Test-Path $newExe)) { throw 'Publish finished but EXE was not found.' }
    Move-Item -LiteralPath '.\Release-new' -Destination '.\Release'

    $exe = Join-Path $PSScriptRoot 'Release\TiHiY.StreamControlCenter.exe'
    Write-Host ""
    Write-Host "BUILD OK: $exe" -ForegroundColor Green
    Write-Host "Log: $logFile"
    Start-Process -FilePath $exe -WorkingDirectory (Split-Path $exe)
}
catch {
    Write-Host ""
    Write-Host ('BUILD FAILED: ' + $_.Exception.Message) -ForegroundColor Red
    Write-Host "Log: $logFile"
    Read-Host 'Press Enter to close'
    exit 1
}
