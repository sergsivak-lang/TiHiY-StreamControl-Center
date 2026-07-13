$ErrorActionPreference = 'Stop'
Set-Location -LiteralPath $PSScriptRoot

$verificationDir = Join-Path $PSScriptRoot 'Verification'
New-Item -ItemType Directory -Force -Path $verificationDir | Out-Null

$buildScript = Join-Path $PSScriptRoot 'BUILD-AND-RUN.ps1'
if (-not (Test-Path $buildScript)) { throw "Build script was not found: $buildScript" }

Write-Host 'Building and starting TiHiY StreamControl Center...' -ForegroundColor Cyan
& powershell.exe -NoProfile -ExecutionPolicy Bypass -File $buildScript
if ($LASTEXITCODE -ne 0) { throw "Build script failed with exit code $LASTEXITCODE" }

$exePath = Join-Path $PSScriptRoot 'Release\TiHiY.StreamControlCenter.exe'
if (-not (Test-Path $exePath)) { throw "EXE was not found: $exePath" }

$process = $null
for ($i = 0; $i -lt 20; $i++) {
    Start-Sleep -Milliseconds 500
    $process = Get-Process -Name 'TiHiY.StreamControlCenter' -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($process) { break }
}
if (-not $process) { throw 'Application process did not start.' }

Start-Sleep -Seconds 4
$process.Refresh()
if ($process.HasExited) { throw "Application exited unexpectedly. Exit code: $($process.ExitCode)" }

Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class NativeWindowCapture {
    [StructLayout(LayoutKind.Sequential)]
    public struct RECT { public int Left; public int Top; public int Right; public int Bottom; }
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
}
"@

$process.Refresh()
$handle = $process.MainWindowHandle
if ($handle -eq [IntPtr]::Zero) { throw 'Main window handle was not found.' }

[NativeWindowCapture]::SetForegroundWindow($handle) | Out-Null
Start-Sleep -Milliseconds 800
$rect = New-Object NativeWindowCapture+RECT
if (-not [NativeWindowCapture]::GetWindowRect($handle, [ref]$rect)) { throw 'Could not read the application window rectangle.' }

$width = $rect.Right - $rect.Left
$height = $rect.Bottom - $rect.Top
if ($width -lt 300 -or $height -lt 200) { throw "Invalid window size: ${width}x${height}" }

Add-Type -AssemblyName System.Drawing
$bitmap = New-Object System.Drawing.Bitmap $width, $height
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)
try {
    $graphics.CopyFromScreen($rect.Left, $rect.Top, 0, 0, $bitmap.Size)
    $screenshot = Join-Path $verificationDir 'TiHiY-StreamControl-Center.png'
    $bitmap.Save($screenshot, [System.Drawing.Imaging.ImageFormat]::Png)
}
finally {
    $graphics.Dispose()
    $bitmap.Dispose()
}

$report = @"
Verification time: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')
EXE: $exePath
Process ID: $($process.Id)
Window size: ${width}x${height}
Screenshot: $screenshot
Process stayed alive: YES
"@
$reportPath = Join-Path $verificationDir 'verification-report.txt'
$report | Set-Content -LiteralPath $reportPath -Encoding UTF8

Write-Host "Verification passed." -ForegroundColor Green
Write-Host "Screenshot: $screenshot" -ForegroundColor Green
Write-Host "Report: $reportPath" -ForegroundColor Green
Start-Process explorer.exe -ArgumentList "/select,`"$screenshot`""
