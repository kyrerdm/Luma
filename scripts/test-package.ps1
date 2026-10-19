param([string]$ArtifactRoot)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
if (-not $ArtifactRoot) { $ArtifactRoot = Join-Path $projectRoot 'artifacts' }
$project = [xml](Get-Content -LiteralPath (Join-Path $projectRoot 'src\Luma\Luma.csproj') -Raw)
$version = [version]$project.Project.PropertyGroup.Version
$published = Join-Path $ArtifactRoot ('Luma-' + $version.ToString(3))
$executable = Join-Path $published 'Luma.exe'
$setup = Join-Path $ArtifactRoot ('Luma-' + $version.ToString(3) + '-Setup.exe')
$archivePath = Join-Path $ArtifactRoot ('Luma-' + $version.ToString(3) + '-win-x64.zip')
$expected = @('Luma.exe', 'KULLANIM.txt', 'LICENSE.txt', 'DOTNET-LICENSE.txt', 'DOTNET-THIRD-PARTY-NOTICES.txt', 'WINDOWS-DESKTOP-LICENSE.txt', 'WINRT-LICENSE.txt', 'WINDOWS-SDK-NOTICE.txt')
$bytes = (Get-Item -LiteralPath $executable).Length
if ($bytes -lt 20MB -or $bytes -gt 250MB) { throw 'Expected the self-contained application, within the in-app updater size limit.' }
$info = [Diagnostics.FileVersionInfo]::GetVersionInfo($executable)
if ($info.ProductName -ne 'Luma' -or $info.FileMajorPart -ne $version.Major -or $info.FileMinorPart -ne $version.Minor -or $info.FileBuildPart -ne $version.Build) { throw 'Application metadata does not match the release version.' }
$binaryHash = (Get-FileHash -LiteralPath $executable).Hash
if ((Get-FileHash -LiteralPath $setup).Hash -ne $binaryHash) { throw 'Setup differs from the tested application.' }
foreach ($file in @($setup, $archivePath)) {
    $hash = (Get-FileHash -LiteralPath $file).Hash.ToLowerInvariant()
    $checksum = [IO.File]::ReadAllText($file + '.sha256').TrimEnd("`r", "`n")
    if ($checksum -cne ($hash + '  ' + [IO.Path]::GetFileName($file))) { throw "Checksum mismatch: $([IO.Path]::GetFileName($file))" }
}
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [IO.Compression.ZipFile]::OpenRead($archivePath)
try {
    if ($archive.Entries.Count -ne $expected.Count) { throw 'Unexpected files or folders in the distribution ZIP.' }
    foreach ($name in $expected) {
        $entry = $archive.GetEntry($name)
        if (-not $entry) { throw "Missing distribution entry: $name" }
        $stream = $entry.Open()
        try { $sha = [Security.Cryptography.SHA256]::Create(); $actual = [Convert]::ToHexString($sha.ComputeHash($stream)); $sha.Dispose() } finally { $stream.Dispose() }
        $reference = switch ($name) {
            'Luma.exe' { $executable }
            'LICENSE.txt' { Join-Path $projectRoot 'LICENSE' }
            'KULLANIM.txt' { Join-Path $projectRoot 'docs\KULLANIM.txt' }
            'WINRT-LICENSE.txt' { Join-Path $projectRoot 'docs\WINRT-LICENSE.txt' }
            'WINDOWS-SDK-NOTICE.txt' { Join-Path $projectRoot 'docs\WINDOWS-SDK-NOTICE.txt' }
            default { Join-Path $projectRoot ('docs\distribution\' + $name) }
        }
        if ($actual -ne (Get-FileHash -LiteralPath $reference).Hash -or $actual -ne (Get-FileHash -LiteralPath (Join-Path $published $name)).Hash) { throw "Packaged content differs from the published application or source notice: $name" }
    }
} finally { $archive.Dispose() }
[pscustomobject]@{ Version = $version.ToString(3); Files = $expected.Count; ExecutableBytes = $bytes; ExecutableSHA256 = $binaryHash.ToLowerInvariant(); Result = 'Passed' } | ConvertTo-Json
