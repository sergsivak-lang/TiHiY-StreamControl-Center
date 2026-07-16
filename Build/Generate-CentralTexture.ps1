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
if ($bytes.Length -lt 10000 -or $bytes[0] -ne 0x89 -or $bytes[1] -ne 0x50 -or $bytes[2] -ne 0x4E -or $bytes[3] -ne 0x47) {
    throw 'Approved central Ukraine texture is not a valid PNG.'
}

[IO.File]::WriteAllBytes($output, $bytes)
Write-Host "Generated approved central texture: $output ($($bytes.Length) bytes)"
