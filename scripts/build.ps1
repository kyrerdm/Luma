param([switch]$Publish, [switch]$SelfContained, [switch]$NoRestore)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$localDotnet = Join-Path $projectRoot '.tools\dotnet\dotnet.exe'
$dotnetPath = if (Test-Path -LiteralPath $localDotnet) { $localDotnet } else { 'dotnet' }
$env:DOTNET_CLI_HOME = Join-Path $projectRoot '.tools\cli-home'
$env:NUGET_PACKAGES = Join-Path $projectRoot '.tools\nuget'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_NOLOGO = '1'
$projectInfo = [xml](Get-Content -LiteralPath (Join-Path $projectRoot 'src\Luma\Luma.csproj') -Raw)
$publishFolder = 'artifacts\Luma-' + $projectInfo.Project.PropertyGroup.Version
Push-Location $projectRoot
try {
    $restoreArguments = @('restore', 'src\Luma\Luma.csproj', '--configfile', 'NuGet.Config', '-p:NuGetAudit=false')
    if ($Publish) {
        $contained = if ($SelfContained) { 'true' } else { 'false' }
        if (-not $NoRestore) {
            & $dotnetPath @restoreArguments -r win-x64 -p:PublishSingleFile=true "-p:SelfContained=$contained"
            if ($LASTEXITCODE -ne 0) { throw "Restore failed ($LASTEXITCODE)." }
        }
        & $dotnetPath publish 'src\Luma\Luma.csproj' --no-restore -c Release -r win-x64 --self-contained $contained -p:PublishSingleFile=true -o $publishFolder
    } else {
        if (-not $NoRestore) {
            & $dotnetPath @restoreArguments
            if ($LASTEXITCODE -ne 0) { throw "Restore failed ($LASTEXITCODE)." }
        }
        & $dotnetPath build 'Luma.slnx' --no-restore -c Release
    }
    if ($LASTEXITCODE -ne 0) { throw "Build failed ($LASTEXITCODE)." }
} finally { Pop-Location }
