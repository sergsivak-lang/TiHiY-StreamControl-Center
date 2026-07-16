param(
    [Parameter(Mandatory = $true)]
    [string]$ProjectDir
)

$assetDir = Join-Path $ProjectDir 'Assets\Themes\UkraineExact'
$source = Join-Path $assetDir 'central-glory.b64.000'
$output = Join-Path $assetDir 'central-glory.png'

if (-not (Test-Path -LiteralPath $source)) {
    throw 'Approved central Ukraine texture source is missing.'
}

$encoded = (Get-Content -LiteralPath $source -Raw).Trim()
$bytes = [Convert]::FromBase64String($encoded)
if ($bytes.Length -eq 0) {
    throw 'Approved central Ukraine texture decoded to an empty file.'
}

[IO.File]::WriteAllBytes($output, $bytes)
Write-Host "Generated approved central texture: $output ($($bytes.Length) bytes)"
