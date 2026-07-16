param(
    [Parameter(Mandatory = $true)]
    [string]$ProjectDir
)

$assetDir = Join-Path $ProjectDir 'Assets\Themes\UkraineExact'
$output = Join-Path $assetDir 'central-glory.png'
$chunks = Get-ChildItem -Path $assetDir -Filter 'central-glory.b64.*' | Sort-Object Name
if ($chunks.Count -eq 0) {
    throw 'Central Ukraine texture chunks are missing.'
}

$builder = [System.Text.StringBuilder]::new()
foreach ($chunk in $chunks) {
    [void]$builder.Append((Get-Content -LiteralPath $chunk.FullName -Raw).Trim())
}

$bytes = [Convert]::FromBase64String($builder.ToString())
[IO.File]::WriteAllBytes($output, $bytes)
Write-Host "Generated exact central texture: $output ($($bytes.Length) bytes)"
