param([switch]$Published, [switch]$NativeBrightness, [ValidateRange(60, 600)][int]$TimeoutSeconds = 120)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$projectInfo = [xml](Get-Content -LiteralPath (Join-Path $projectRoot 'src\Luma\Luma.csproj') -Raw)
$targetFramework = $projectInfo.Project.PropertyGroup.TargetFramework
$executable = Join-Path $projectRoot "src\Luma\bin\Release\$targetFramework\Luma.exe"
if ($Published) { $executable = Join-Path $projectRoot ('artifacts\Luma-' + $projectInfo.Project.PropertyGroup.Version + '\Luma.exe') }
$reportDirectory = Join-Path $projectRoot 'artifacts\verification'
New-Item -ItemType Directory -Path $reportDirectory -Force | Out-Null
foreach ($name in @('failure.txt', 'progress.log', 'results.json')) {
    $oldReport = Join-Path $reportDirectory $name
    if (Test-Path -LiteralPath $oldReport) { Remove-Item -LiteralPath $oldReport }
}
$env:DOTNET_BUNDLE_EXTRACT_BASE_DIR = Join-Path $projectRoot '.tools\bundle'
$previousBrightnessTest = [Environment]::GetEnvironmentVariable('LUMA_VERIFY_BRIGHTNESS_WRITE', 'Process')
try {
    $env:LUMA_VERIFY_BRIGHTNESS_WRITE = if ($NativeBrightness) { '1' } else { $null }
    $process = Start-Process -FilePath $executable -ArgumentList @('--self-test', ('"' + $reportDirectory + '"')) -PassThru -WindowStyle Hidden
} finally { [Environment]::SetEnvironmentVariable('LUMA_VERIFY_BRIGHTNESS_WRITE', $previousBrightnessTest, 'Process') }
$verificationClock = [Diagnostics.Stopwatch]::StartNew()
while (-not $process.WaitForExit(10000)) {
    if ($verificationClock.Elapsed.TotalSeconds -ge $TimeoutSeconds) { $process.Kill(); throw "Verification timed out after $TimeoutSeconds seconds." }
}
if ($process.ExitCode -ne 0) {
    if (Test-Path -LiteralPath (Join-Path $reportDirectory 'failure.txt')) { Get-Content -LiteralPath (Join-Path $reportDirectory 'failure.txt') }
    if (Test-Path -LiteralPath (Join-Path $reportDirectory 'results.json')) { Get-Content -LiteralPath (Join-Path $reportDirectory 'results.json') }
    throw "Verification failed ($($process.ExitCode)). See $reportDirectory"
}
Get-Content -LiteralPath (Join-Path $reportDirectory 'results.json')
