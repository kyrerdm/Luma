$ErrorActionPreference = 'Stop'
if ([Threading.Thread]::CurrentThread.ApartmentState -ne 'STA') { throw 'Run with pwsh -NoProfile -STA -File scripts\New-LumaIcon.ps1' }
Add-Type -AssemblyName PresentationCore, PresentationFramework, WindowsBase
$projectRoot = Split-Path -Parent $PSScriptRoot
$assetsPath = Join-Path $projectRoot 'src\Luma\Assets'
$brandPath = Join-Path $assetsPath 'Brand.xaml'
$reader = [Xml.XmlReader]::Create($brandPath)
try { $brand = [Windows.Markup.XamlReader]::Load($reader) } finally { $reader.Dispose() }
$sizes = @(256, 128, 64, 48, 40, 32, 24, 20, 16)
$images = [Collections.Generic.List[byte[]]]::new()
$previewPath = Join-Path $projectRoot 'artifacts\brand'
New-Item -ItemType Directory -Path $previewPath -Force | Out-Null
foreach ($size in $sizes) {
    $visual = New-Object Windows.Media.DrawingVisual
    $drawing = $visual.RenderOpen()
    $drawing.DrawImage($brand['BrandAppIcon'], [Windows.Rect]::new(0, 0, $size, $size))
    $drawing.Close()
    $bitmap = [Windows.Media.Imaging.RenderTargetBitmap]::new($size, $size, 96, 96, [Windows.Media.PixelFormats]::Pbgra32)
    $bitmap.Render($visual)
    $encoder = New-Object Windows.Media.Imaging.PngBitmapEncoder
    $encoder.Frames.Add([Windows.Media.Imaging.BitmapFrame]::Create($bitmap))
    $stream = New-Object IO.MemoryStream
    $encoder.Save($stream)
    $images.Add($stream.ToArray())
    [IO.File]::WriteAllBytes((Join-Path $previewPath ('luma-' + $size + '.png')), $stream.ToArray())
    $stream.Dispose()
}
$file = [IO.File]::Create((Join-Path $assetsPath 'Luma.ico'))
$writer = New-Object IO.BinaryWriter($file)
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
Write-Output ('Generated Luma.ico and previews from ' + $brandPath)
