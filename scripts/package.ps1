$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$projectInfo = [xml](Get-Content -LiteralPath (Join-Path $projectRoot 'src\Luma\Luma.csproj') -Raw)
$version = $projectInfo.Project.PropertyGroup.Version
$publishDirectory = Join-Path $projectRoot ('artifacts\Luma-' + $version)
$executable = Join-Path $publishDirectory 'Luma.exe'
if (-not (Test-Path -LiteralPath $executable)) { throw 'Run build.ps1 -Publish -SelfContained first.' }
Copy-Item -LiteralPath (Join-Path $projectRoot 'docs\KULLANIM.txt') -Destination $publishDirectory -Force
Copy-Item -LiteralPath (Join-Path $projectRoot 'LICENSE') -Destination (Join-Path $publishDirectory 'LICENSE.txt') -Force
Copy-Item -LiteralPath (Join-Path $projectRoot 'docs\WINRT-LICENSE.txt') -Destination $publishDirectory -Force
Copy-Item -LiteralPath (Join-Path $projectRoot 'docs\WINDOWS-SDK-NOTICE.txt') -Destination $publishDirectory -Force
$assets = Get-Content -LiteralPath (Join-Path $projectRoot 'src\Luma\obj\project.assets.json') -Raw | ConvertFrom-Json
function Get-ResolvedRuntime([string]$name) {
    $dependency = @($assets.project.frameworks.PSObject.Properties | ForEach-Object { $_.Value.downloadDependencies }) | Where-Object name -eq $name | Select-Object -First 1
    if (-not $dependency -or $dependency.version -notmatch '^\[(?<release>\d+\.\d+\.\d+),\s*\k<release>\]$') { throw "No exact restored runtime version for $name. Restore a self-contained build first." }
    $runtimeVersion = $Matches.release
    foreach ($folder in $assets.packageFolders.PSObject.Properties.Name) {
        $candidate = Join-Path $folder ($name.ToLowerInvariant() + '\' + $runtimeVersion)
        if (Test-Path -LiteralPath $candidate -PathType Container) { return $candidate }
    }
    throw "Restored runtime package not found: $name $runtimeVersion"
}
$runtimePackage = Get-ResolvedRuntime 'Microsoft.NETCore.App.Runtime.win-x64'
$desktopPackage = Get-ResolvedRuntime 'Microsoft.WindowsDesktop.App.Runtime.win-x64'
$notices = @(
    @{ Source = (Join-Path $runtimePackage 'LICENSE.TXT'); Name = 'DOTNET-LICENSE.txt' },
    @{ Source = (Join-Path $runtimePackage 'THIRD-PARTY-NOTICES.TXT'); Name = 'DOTNET-THIRD-PARTY-NOTICES.txt' },
    @{ Source = (Join-Path $desktopPackage 'LICENSE'); Name = 'WINDOWS-DESKTOP-LICENSE.txt' }
)
foreach ($notice in $notices) {
    $embeddedSource = Join-Path $projectRoot ('docs\distribution\' + $notice.Name)
    if ((Get-FileHash -LiteralPath $notice.Source).Hash -ne (Get-FileHash -LiteralPath $embeddedSource).Hash) { throw "Update the embedded notice for the restored runtime before packaging: $($notice.Name)" }
    Copy-Item -LiteralPath $embeddedSource -Destination (Join-Path $publishDirectory $notice.Name) -Force
}
$files = @('Luma.exe', 'KULLANIM.txt', 'LICENSE.txt', 'DOTNET-LICENSE.txt', 'DOTNET-THIRD-PARTY-NOTICES.txt', 'WINDOWS-DESKTOP-LICENSE.txt', 'WINRT-LICENSE.txt', 'WINDOWS-SDK-NOTICE.txt') | ForEach-Object { Join-Path $publishDirectory $_ }
$zipPath = Join-Path $projectRoot ('artifacts\Luma-' + $version + '-win-x64.zip')
Compress-Archive -LiteralPath $files -DestinationPath $zipPath -CompressionLevel Optimal -Force
$hash = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash.ToLowerInvariant()
$checksumPath = $zipPath + '.sha256'
[System.IO.File]::WriteAllText($checksumPath, $hash + '  ' + [System.IO.Path]::GetFileName($zipPath) + "`n", [System.Text.UTF8Encoding]::new($false))
[pscustomobject]@{ File = $zipPath; Bytes = (Get-Item -LiteralPath $zipPath).Length; SHA256 = $hash; ChecksumFile = $checksumPath } | Format-List
$setupPath = Join-Path $projectRoot ('artifacts\Luma-' + $version + '-Setup.exe')
Copy-Item -LiteralPath $executable -Destination $setupPath -Force
$setupHash = (Get-FileHash -LiteralPath $setupPath -Algorithm SHA256).Hash.ToLowerInvariant()
[System.IO.File]::WriteAllText(($setupPath + '.sha256'), $setupHash + '  ' + [System.IO.Path]::GetFileName($setupPath) + "`n", [System.Text.UTF8Encoding]::new($false))
[pscustomobject]@{ Setup = $setupPath; Bytes = (Get-Item -LiteralPath $setupPath).Length; SHA256 = $setupHash } | Format-List
