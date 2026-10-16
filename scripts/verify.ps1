param([switch]$Published)
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
$process = Start-Process -FilePath $executable -ArgumentList @('--self-test', ('"' + $reportDirectory + '"')) -PassThru -WindowStyle Hidden
if (-not $process.WaitForExit(30000)) { $process.Kill(); throw 'Verification timed out.' }
if ($process.ExitCode -ne 0) {
    if (Test-Path -LiteralPath (Join-Path $reportDirectory 'failure.txt')) { Get-Content -LiteralPath (Join-Path $reportDirectory 'failure.txt') }
    if (Test-Path -LiteralPath (Join-Path $reportDirectory 'results.json')) { Get-Content -LiteralPath (Join-Path $reportDirectory 'results.json') }
    throw "Verification failed ($($process.ExitCode)). See $reportDirectory"
}
Get-Content -LiteralPath (Join-Path $reportDirectory 'results.json')
