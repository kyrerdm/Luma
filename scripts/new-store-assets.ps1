param([string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
if ([Threading.Thread]::CurrentThread.ApartmentState -ne 'STA') { throw 'Run with pwsh -NoProfile -STA -File scripts/new-store-assets.ps1' }
Add-Type -AssemblyName PresentationCore, PresentationFramework, WindowsBase
$projectRoot = Split-Path -Parent $PSScriptRoot
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $projectRoot 'artifacts/store-assets' }
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$reader = [Xml.XmlReader]::Create((Join-Path $projectRoot 'src/Luma/Assets/Brand.xaml'))
try { $brand = [Windows.Markup.XamlReader]::Load($reader) } finally { $reader.Dispose() }
function Write-BrandImage([string]$Name, [int]$Size, [bool]$Tile = $false) {
    $visual = [Windows.Media.DrawingVisual]::new()
    $drawing = $visual.RenderOpen()
    $resource = if ($Tile) { 'BrandAppIcon' } else { 'BrandSymbol' }
    $drawing.DrawImage($brand[$resource], [Windows.Rect]::new(0, 0, $Size, $Size))
    $drawing.Close()
    $bitmap = [Windows.Media.Imaging.RenderTargetBitmap]::new($Size, $Size, 96, 96, [Windows.Media.PixelFormats]::Pbgra32)
    $bitmap.Render($visual)
    $encoder = [Windows.Media.Imaging.PngBitmapEncoder]::new()
    $encoder.Frames.Add([Windows.Media.Imaging.BitmapFrame]::Create($bitmap))
    $file = [IO.File]::Create((Join-Path $OutputDirectory $Name))
    try { $encoder.Save($file) } finally { $file.Dispose() }
}
foreach ($scale in @(100,125,150,200,400)) {
    foreach ($asset in @(@('Square44x44Logo',44),@('Square150x150Logo',150),@('StoreLogo',50))) {
        Write-BrandImage ($asset[0] + '.scale-' + $scale + '.png') ([int][Math]::Round($asset[1] * $scale / 100.0))
    }
}
foreach ($size in @(16,20,24,32,48,64,96,256)) { Write-BrandImage ('Square44x44Logo.targetsize-' + $size + '_altform-unplated.png') $size }
Write-BrandImage 'StoreTile-300.png' 300 $true
Write-Output ('Generated Store assets from the existing Luma vector artwork: ' + $OutputDirectory)
