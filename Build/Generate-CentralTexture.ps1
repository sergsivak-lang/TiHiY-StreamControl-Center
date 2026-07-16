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
if ($bytes.Length -lt 10000) {
    throw "Approved central Ukraine texture is unexpectedly small: $($bytes.Length) bytes."
}

[IO.File]::WriteAllBytes($output, $bytes)
Write-Host "Generated approved central texture: $output ($($bytes.Length) bytes; signature $($bytes[0]),$($bytes[1]),$($bytes[2]),$($bytes[3]))"
