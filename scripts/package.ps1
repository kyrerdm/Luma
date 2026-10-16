$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$projectInfo = [xml](Get-Content -LiteralPath (Join-Path $projectRoot 'src\Luma\Luma.csproj') -Raw)
$version = $projectInfo.Project.PropertyGroup.Version
$publishDirectory = Join-Path $projectRoot ('artifacts\Luma-' + $version)
$executable = Join-Path $publishDirectory 'Luma.exe'
if (-not (Test-Path -LiteralPath $executable)) { throw 'Run build.ps1 -Publish -SelfContained first.' }
Copy-Item -LiteralPath (Join-Path $projectRoot 'docs\KULLANIM.txt') -Destination $publishDirectory -Force
Copy-Item -LiteralPath (Join-Path $projectRoot 'docs\WINRT-LICENSE.txt') -Destination $publishDirectory -Force
Copy-Item -LiteralPath (Join-Path $projectRoot 'docs\WINDOWS-SDK-NOTICE.txt') -Destination $publishDirectory -Force
$runtimeCache = Join-Path $projectRoot '.tools\nuget\microsoft.netcore.app.runtime.win-x64'
$desktopCache = Join-Path $projectRoot '.tools\nuget\microsoft.windowsdesktop.app.runtime.win-x64'
$runtimePackage = Get-ChildItem -LiteralPath $runtimeCache -Directory | Sort-Object { [version]$_.Name } -Descending | Select-Object -First 1
$desktopPackage = Get-ChildItem -LiteralPath $desktopCache -Directory | Sort-Object { [version]$_.Name } -Descending | Select-Object -First 1
if (-not $runtimePackage -or -not $desktopPackage) { throw 'Self-contained runtime packages not found.' }
Copy-Item -LiteralPath (Join-Path $runtimePackage.FullName 'LICENSE.TXT') -Destination (Join-Path $publishDirectory 'DOTNET-LICENSE.txt') -Force
Copy-Item -LiteralPath (Join-Path $runtimePackage.FullName 'THIRD-PARTY-NOTICES.TXT') -Destination (Join-Path $publishDirectory 'DOTNET-THIRD-PARTY-NOTICES.txt') -Force
Copy-Item -LiteralPath (Join-Path $desktopPackage.FullName 'LICENSE') -Destination (Join-Path $publishDirectory 'WINDOWS-DESKTOP-LICENSE.txt') -Force
$files = @('Luma.exe', 'KULLANIM.txt', 'DOTNET-LICENSE.txt', 'DOTNET-THIRD-PARTY-NOTICES.txt', 'WINDOWS-DESKTOP-LICENSE.txt', 'WINRT-LICENSE.txt', 'WINDOWS-SDK-NOTICE.txt') | ForEach-Object { Join-Path $publishDirectory $_ }
$zipPath = Join-Path $projectRoot ('artifacts\Luma-' + $version + '-win-x64.zip')
Compress-Archive -LiteralPath $files -DestinationPath $zipPath -CompressionLevel Optimal -Force
$hash = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash.ToLowerInvariant()
$checksumPath = $zipPath + '.sha256'
[System.IO.File]::WriteAllText($checksumPath, $hash + '  ' + [System.IO.Path]::GetFileName($zipPath) + "`n", [System.Text.UTF8Encoding]::new($false))
[pscustomobject]@{ File = $zipPath; Bytes = (Get-Item -LiteralPath $zipPath).Length; SHA256 = $hash; ChecksumFile = $checksumPath } | Format-List
