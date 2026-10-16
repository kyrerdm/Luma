$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$projectRoot = Split-Path -Parent $PSScriptRoot
$assetsPath = Join-Path $projectRoot 'src\Luma\Assets'
New-Item -ItemType Directory -Path $assetsPath -Force | Out-Null
$sizes = @(256, 128, 64, 48, 32, 16)
$images = [System.Collections.Generic.List[byte[]]]::new()
foreach ($size in $sizes) {
    $bitmap = [System.Drawing.Bitmap]::new($size, $size)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.Clear([System.Drawing.Color]::Transparent)
    $graphics.ScaleTransform($size / 256.0, $size / 256.0)
    $fill = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(129, 183, 219))
    $graphics.FillEllipse($fill, 8, 8, 240, 240)
    $pen = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(29, 23, 43), 24)
    $pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $pen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
    $graphics.DrawLines($pen, [System.Drawing.PointF[]]@([System.Drawing.PointF]::new(96, 72), [System.Drawing.PointF]::new(96, 176), [System.Drawing.PointF]::new(166, 176)))
    $stream = [System.IO.MemoryStream]::new()
    $bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
    $images.Add($stream.ToArray())
    $stream.Dispose(); $pen.Dispose(); $fill.Dispose(); $graphics.Dispose(); $bitmap.Dispose()
}
$file = [System.IO.File]::Create((Join-Path $assetsPath 'Luma.ico'))
$writer = [System.IO.BinaryWriter]::new($file)
try {
    $writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]$sizes.Count)
    $offset = 6 + 16 * $sizes.Count
    for ($i = 0; $i -lt $sizes.Count; $i++) {
        $dimension = if ($sizes[$i] -eq 256) { 0 } else { $sizes[$i] }
        $writer.Write([byte]$dimension); $writer.Write([byte]$dimension)
        $writer.Write([byte]0); $writer.Write([byte]0)
        $writer.Write([uint16]1); $writer.Write([uint16]32)
        $writer.Write([uint32]$images[$i].Length); $writer.Write([uint32]$offset)
        $offset += $images[$i].Length
    }
    foreach ($bytes in $images) { $writer.Write($bytes) }
} finally { $writer.Dispose(); $file.Dispose() }
