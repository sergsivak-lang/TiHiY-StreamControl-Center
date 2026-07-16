param(
    [Parameter(Mandatory = $true)]
    [string]$ProjectDir
)

$assetDir = Join-Path $ProjectDir 'Assets\Themes\UkraineExact'
$output = Join-Path $assetDir 'central-glory.jpg'
$chunks = Get-ChildItem -LiteralPath $assetDir -Filter 'central-glory.b64.*' | Sort-Object Name

if ($chunks.Count -lt 1) {
    throw 'Approved central Ukraine texture source chunks are missing.'
}

$builder = [System.Text.StringBuilder]::new()
foreach ($chunk in $chunks) {
    [void]$builder.Append((Get-Content -LiteralPath $chunk.FullName -Raw).Trim())
}

$bytes = [Convert]::FromBase64String($builder.ToString())
if ($bytes.Length -eq 0) {
    throw 'Approved central Ukraine texture decoded to an empty file.'
}

[IO.File]::WriteAllBytes($output, $bytes)
Write-Host "Generated approved central texture: $output ($($bytes.Length) bytes from $($chunks.Count) chunks)"
