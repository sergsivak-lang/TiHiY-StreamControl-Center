param(
    [string]$RepositoryUrl = "https://github.com/sergsivak-lang/TiHiY-StreamControl-Center.git",
    [string]$Branch = "agent/cyber-amber-rebuild-v2"
)

$ErrorActionPreference = "Stop"
$Source = Split-Path -Parent $MyInvocation.MyCommand.Path
$Work = Join-Path $env:TEMP "TiHiY-StreamControl-Center-GitHub-Upload"
$Log = Join-Path $Source "github-upload.log"

if (Test-Path $Log) {
    Remove-Item -LiteralPath $Log -Force
}

Start-Transcript -Path $Log -Force | Out-Null

try {
    Write-Host "[1/7] Checking Git..." -ForegroundColor Cyan
    if (-not (Get-Command git -ErrorAction SilentlyContinue)) {
        throw "Git for Windows is not installed or is not available in PATH."
    }
    git --version
    if ($LASTEXITCODE -ne 0) { throw "Git check failed." }

    Write-Host "[2/7] Preparing temporary folder..." -ForegroundColor Cyan
    if (Test-Path $Work) {
        Remove-Item -LiteralPath $Work -Recurse -Force
    }

    Write-Host "[3/7] Cloning repository..." -ForegroundColor Cyan
    git clone $RepositoryUrl $Work
    if ($LASTEXITCODE -ne 0) { throw "Repository clone failed." }

    Set-Location $Work

    Write-Host "[4/7] Creating clean branch $Branch from origin/main..." -ForegroundColor Cyan
    git fetch origin
    if ($LASTEXITCODE -ne 0) { throw "Git fetch failed." }
    git checkout -B $Branch origin/main
    if ($LASTEXITCODE -ne 0) { throw "Branch creation failed." }

    Write-Host "[5/7] Replacing branch contents with the prepared project..." -ForegroundColor Cyan

    Get-ChildItem -LiteralPath $Work -Force |
        Where-Object { $_.Name -ne ".git" } |
        Remove-Item -Recurse -Force

    $excludedNames = @(".git", "github-upload.log")
    Get-ChildItem -LiteralPath $Source -Force |
        Where-Object { $excludedNames -notcontains $_.Name } |
        ForEach-Object {
            Copy-Item -LiteralPath $_.FullName -Destination $Work -Recurse -Force
        }

    if (-not (Test-Path (Join-Path $Work "TiHiY.StreamControlCenter.csproj"))) {
        throw "Project copy failed: TiHiY.StreamControlCenter.csproj was not found in the temporary repository."
    }
    if (-not (Test-Path (Join-Path $Work ".github\workflows\build-windows.yml"))) {
        throw "Project copy failed: GitHub Actions workflow was not found."
    }

    Write-Host "[6/7] Creating commit..." -ForegroundColor Cyan
    git config user.name "sergsivak-lang"
    git config user.email "sergsivak-lang@users.noreply.github.com"
    git add -A
    if ($LASTEXITCODE -ne 0) { throw "git add failed." }

    git diff --cached --quiet
    if ($LASTEXITCODE -eq 0) {
        Write-Host "No file changes were detected. The branch still will be pushed." -ForegroundColor Yellow
    }
    else {
        git commit -m "Prepare Windows CI and Cyber Amber rebuild base"
        if ($LASTEXITCODE -ne 0) { throw "Git commit failed." }
    }

    Write-Host "[7/7] Pushing branch to GitHub..." -ForegroundColor Cyan
    git push --force-with-lease -u origin $Branch
    if ($LASTEXITCODE -ne 0) { throw "Git push failed. Check GitHub authorization in the log." }

    $compareUrl = "https://github.com/sergsivak-lang/TiHiY-StreamControl-Center/compare/main...$Branch?expand=1"

    Write-Host "" 
    Write-Host "UPLOAD COMPLETED" -ForegroundColor Green
    Write-Host "Branch: $Branch"
    Write-Host "Log: $Log"
    Write-Host "Opening Pull Request page..."
    Start-Process $compareUrl
}
catch {
    Write-Host "" 
    Write-Host "UPLOAD FAILED" -ForegroundColor Red
    Write-Host $_.Exception.Message -ForegroundColor Red
    Write-Host "Detailed log: $Log" -ForegroundColor Yellow
    exit 1
}
finally {
    Set-Location $Source
    Stop-Transcript | Out-Null
}
