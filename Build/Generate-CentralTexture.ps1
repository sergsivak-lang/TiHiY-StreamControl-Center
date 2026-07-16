param(
    [Parameter(Mandatory = $true)]
    [string]$ProjectDir
)

$assetDir = Join-Path $ProjectDir 'Assets\Themes\UkraineExact'
$output = Join-Path $assetDir 'central-glory.jpg'

if (Test-Path -LiteralPath $output) {
    $length = (Get-Item -LiteralPath $output).Length
    if ($length -le 0) { throw 'Approved central Ukraine artwork is empty.' }
    Write-Host "Using committed approved central texture: $output ($length bytes)"
    exit 0
}

throw 'Approved central Ukraine artwork is missing from the repository.'
