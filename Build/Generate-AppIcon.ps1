param(
    [Parameter(Mandatory = $true)]
    [string]$ProjectDir
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$assetsDir = Join-Path $ProjectDir 'Assets'
New-Item -ItemType Directory -Path $assetsDir -Force | Out-Null
$icoPath = Join-Path $assetsDir 'AppIcon.ico'
$pngPath = Join-Path $assetsDir 'AppIcon.png'

function New-RoundedPath([System.Drawing.RectangleF]$rect, [float]$radius) {
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $diameter = $radius * 2
    $arc = New-Object System.Drawing.RectangleF($rect.X, $rect.Y, $diameter, $diameter)
    $path.AddArc($arc, 180, 90)
    $arc.X = $rect.Right - $diameter
    $path.AddArc($arc, 270, 90)
    $arc.Y = $rect.Bottom - $diameter
    $path.AddArc($arc, 0, 90)
    $arc.X = $rect.X
    $path.AddArc($arc, 90, 90)
    $path.CloseFigure()
    return $path
}

function New-AppBitmap([int]$size) {
    $bitmap = New-Object System.Drawing.Bitmap($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $graphics.Clear([System.Drawing.Color]::FromArgb(255, 2, 10, 25))

    $rect = New-Object System.Drawing.RectangleF(3, 3, ($size - 7), ($size - 7))
    $path = New-RoundedPath $rect ([Math]::Max(3, $size * 0.12))
    $backgroundBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
        (New-Object System.Drawing.PointF(0, 0)),
        (New-Object System.Drawing.PointF(0, $size)),
        [System.Drawing.Color]::FromArgb(255, 6, 30, 66),
        [System.Drawing.Color]::FromArgb(255, 1, 7, 18))
    $graphics.FillPath($backgroundBrush, $path)

    $cyanPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(245, 20, 176, 235), [Math]::Max(1, $size * 0.018))
    $goldPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(245, 247, 176, 0), [Math]::Max(1, $size * 0.014))
    $graphics.DrawPath($cyanPen, $path)
    $innerRect = New-Object System.Drawing.RectangleF(($size * 0.045), ($size * 0.045), ($size * 0.91), ($size * 0.91))
    $innerPath = New-RoundedPath $innerRect ([Math]::Max(2, $size * 0.10))
    $graphics.DrawPath($goldPen, $innerPath)

    $scale = [double]$size
    $wingPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(230, 22, 133, 255), [Math]::Max(1, $size * 0.018))
    $wingPen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $wingPen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    for ($i = 0; $i -lt 5; $i++) {
        $offset = $i * 0.035
        $graphics.DrawBezier($wingPen,
            [float]($scale * 0.37), [float]($scale * (0.40 + $offset)),
            [float]($scale * 0.26), [float]($scale * (0.37 + $offset)),
            [float]($scale * 0.18), [float]($scale * (0.29 + $offset)),
            [float]($scale * 0.10), [float]($scale * (0.31 + $offset)))
        $graphics.DrawBezier($wingPen,
            [float]($scale * 0.63), [float]($scale * (0.40 + $offset)),
            [float]($scale * 0.74), [float]($scale * (0.37 + $offset)),
            [float]($scale * 0.82), [float]($scale * (0.29 + $offset)),
            [float]($scale * 0.90), [float]($scale * (0.31 + $offset)))
    }

    $shieldPoints = @(
        (New-Object System.Drawing.PointF([float]($scale * 0.50), [float]($scale * 0.17))),
        (New-Object System.Drawing.PointF([float]($scale * 0.70), [float]($scale * 0.24))),
        (New-Object System.Drawing.PointF([float]($scale * 0.70), [float]($scale * 0.58))),
        (New-Object System.Drawing.PointF([float]($scale * 0.50), [float]($scale * 0.75))),
        (New-Object System.Drawing.PointF([float]($scale * 0.30), [float]($scale * 0.58))),
        (New-Object System.Drawing.PointF([float]($scale * 0.30), [float]($scale * 0.24)))
    )
    $shieldBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
        (New-Object System.Drawing.PointF([float]($scale * 0.50), [float]($scale * 0.17))),
        (New-Object System.Drawing.PointF([float]($scale * 0.50), [float]($scale * 0.75))),
        [System.Drawing.Color]::FromArgb(255, 18, 74, 170),
        [System.Drawing.Color]::FromArgb(255, 3, 22, 70))
    $graphics.FillPolygon($shieldBrush, $shieldPoints)
    $shieldPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(255, 255, 184, 0), [Math]::Max(1.2, $size * 0.022))
    $shieldPen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
    $graphics.DrawPolygon($shieldPen, $shieldPoints)

    $tridentPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(255, 255, 190, 20), [Math]::Max(1.4, $size * 0.026))
    $tridentPen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
    $tridentPen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $tridentPen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round

    $graphics.DrawLine($tridentPen, [float]($scale * 0.50), [float]($scale * 0.28), [float]($scale * 0.50), [float]($scale * 0.63))
    $graphics.DrawLines($tridentPen, @(
        (New-Object System.Drawing.PointF([float]($scale * 0.36), [float]($scale * 0.31))),
        (New-Object System.Drawing.PointF([float]($scale * 0.36), [float]($scale * 0.48))),
        (New-Object System.Drawing.PointF([float]($scale * 0.44), [float]($scale * 0.56))),
        (New-Object System.Drawing.PointF([float]($scale * 0.50), [float]($scale * 0.68)))
    ))
    $graphics.DrawLines($tridentPen, @(
        (New-Object System.Drawing.PointF([float]($scale * 0.64), [float]($scale * 0.31))),
        (New-Object System.Drawing.PointF([float]($scale * 0.64), [float]($scale * 0.48))),
        (New-Object System.Drawing.PointF([float]($scale * 0.56), [float]($scale * 0.56))),
        (New-Object System.Drawing.PointF([float]($scale * 0.50), [float]($scale * 0.68)))
    ))
    $graphics.DrawLines($tridentPen, @(
        (New-Object System.Drawing.PointF([float]($scale * 0.43), [float]($scale * 0.33))),
        (New-Object System.Drawing.PointF([float]($scale * 0.43), [float]($scale * 0.47))),
        (New-Object System.Drawing.PointF([float]($scale * 0.50), [float]($scale * 0.55))),
        (New-Object System.Drawing.PointF([float]($scale * 0.57), [float]($scale * 0.47))),
        (New-Object System.Drawing.PointF([float]($scale * 0.57), [float]($scale * 0.33)))
    ))
    $graphics.DrawEllipse($tridentPen, [float]($scale * 0.43), [float]($scale * 0.54), [float]($scale * 0.14), [float]($scale * 0.13))

    $accentPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(210, 247, 176, 0), [Math]::Max(1, $size * 0.014))
    $graphics.DrawLine($accentPen, [float]($scale * 0.25), [float]($scale * 0.84), [float]($scale * 0.75), [float]($scale * 0.84))

    $graphics.Dispose()
    $backgroundBrush.Dispose()
    $shieldBrush.Dispose()
    $cyanPen.Dispose()
    $goldPen.Dispose()
    $wingPen.Dispose()
    $shieldPen.Dispose()
    $tridentPen.Dispose()
    $accentPen.Dispose()
    $path.Dispose()
    $innerPath.Dispose()
    return $bitmap
}

$sizes = @(16, 24, 32, 48, 64, 128, 256)
$images = New-Object System.Collections.Generic.List[byte[]]
foreach ($size in $sizes) {
    $bitmap = New-AppBitmap $size
    $stream = New-Object System.IO.MemoryStream
    $bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
    $images.Add($stream.ToArray())
    $stream.Dispose()
    $bitmap.Dispose()
}

$preview = New-AppBitmap 512
$preview.Save($pngPath, [System.Drawing.Imaging.ImageFormat]::Png)
$preview.Dispose()

$fileStream = [System.IO.File]::Create($icoPath)
$writer = New-Object System.IO.BinaryWriter($fileStream)
$writer.Write([UInt16]0)
$writer.Write([UInt16]1)
$writer.Write([UInt16]$sizes.Count)
$offset = 6 + (16 * $sizes.Count)
for ($index = 0; $index -lt $sizes.Count; $index++) {
    $size = $sizes[$index]
    $bytes = $images[$index]
    $writer.Write([Byte]($(if ($size -ge 256) { 0 } else { $size })))
    $writer.Write([Byte]($(if ($size -ge 256) { 0 } else { $size })))
    $writer.Write([Byte]0)
    $writer.Write([Byte]0)
    $writer.Write([UInt16]1)
    $writer.Write([UInt16]32)
    $writer.Write([UInt32]$bytes.Length)
    $writer.Write([UInt32]$offset)
    $offset += $bytes.Length
}
foreach ($bytes in $images) { $writer.Write($bytes) }
$writer.Flush()
$writer.Dispose()
$fileStream.Dispose()

Write-Host "Generated $icoPath and $pngPath"
