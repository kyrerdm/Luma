param([Parameter(Mandatory)][string]$Package, [switch]$Production)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$projectRoot = Split-Path -Parent $PSScriptRoot
$metadata = Get-Content -LiteralPath ($Package + '.json') -Raw | ConvertFrom-Json
if ((Get-FileHash -LiteralPath $Package -Algorithm SHA256).Hash -ne $metadata.Sha256) { throw 'Package SHA-256 does not match its build record.' }
$zip = [IO.Compression.ZipFile]::OpenRead((Resolve-Path -LiteralPath $Package))
function Read-Entry([string]$Name) {
    $entry = $zip.GetEntry($Name)
    if (-not $entry) { throw "Missing package entry: $Name" }
    $stream = $entry.Open(); $reader = [IO.StreamReader]::new($stream)
    try { $reader.ReadToEnd() } finally { $reader.Dispose(); $stream.Dispose() }
}
try {
    [xml]$manifest = Read-Entry 'AppxManifest.xml'
    if ($manifest.Package.Identity.Name -ne $metadata.IdentityName -or $manifest.Package.Identity.Publisher -ne $metadata.Publisher -or $manifest.Package.Identity.Version -ne $metadata.Version) { throw 'Manifest identity does not match build record.' }
    if ($Production -and ($metadata.Channel -ne 'Store' -or $metadata.IdentityName -eq 'Luma.Development')) { throw 'A development identity cannot be submitted to Microsoft Store.' }
    if ($manifest.Package.Identity.ProcessorArchitecture -ne 'x64') { throw 'Unexpected architecture.' }
    $applications = @($manifest.Package.Applications.Application)
    if ($applications.Count -ne 1 -or $applications[0].Executable -ne 'Luma.exe' -or $applications[0].EntryPoint -ne 'Windows.FullTrustApplication') { throw 'Unexpected app entry point.' }
    $startup = $applications[0].Extensions.Extension
    if ($startup.Category -ne 'windows.startupTask' -or $startup.StartupTask.TaskId -ne 'LumaStartup' -or $startup.StartupTask.Enabled -ne 'false' -or $startup.GetAttribute('Parameters','http://schemas.microsoft.com/appx/manifest/uap/windows10/10') -ne '--background') { throw 'Startup must be opt-in and launch in the background.' }
    if (@($manifest.Package.Resources.Resource.Language).Count -ne 5 -or $manifest.Package.Resources.Resource[0].Language -ne 'en-US') { throw 'Five languages with English first are required.' }
    if (@($manifest.Package.Capabilities.ChildNodes).Count -ne 1 -or $manifest.Package.Capabilities.Capability.Name -ne 'runFullTrust') { throw 'Unexpected package capabilities.' }
    foreach ($name in @('Luma.exe','Luma.dll','Luma.deps.json','Luma.runtimeconfig.json','coreclr.dll','PresentationFramework.dll','resources.pri','AppxBlockMap.xml','[Content_Types].xml')) {
        if (-not $zip.GetEntry($name)) { throw "Missing runtime/package file: $name" }
    }
    foreach ($name in @('LICENSE.txt','KULLANIM.txt','DOTNET-LICENSE.txt','DOTNET-THIRD-PARTY-NOTICES.txt','WINDOWS-DESKTOP-LICENSE.txt','WINRT-LICENSE.txt','WINDOWS-SDK-NOTICE.txt')) {
        if ((Read-Entry $name).Length -lt 100) { throw "Missing/truncated license: $name" }
    }
    if ($zip.Entries.FullName -match '(^|/)(uninstall\.ps1|Luma-.*-Setup\.exe)$') { throw 'The EXE installer/uninstaller must not be shipped in MSIX.' }
    if ($zip.Entries.FullName -match '\.(pfx|pdb|user)$|(^|/)(settings\.json|StoreIdentity.*\.json)$') { throw 'Private identity, settings, debug or signing files must not be shipped.' }
    $scales = @($zip.Entries | Where-Object { $_.FullName -match 'Assets/Square44x44Logo\.scale-' })
    if ($scales.Count -ne 5) { throw 'Expected five logo scale assets.' }
    if (@($zip.Entries | Where-Object { $_.FullName -match '^resources\..+\.pri$' }).Count -ne 0) { throw 'This self-contained MSIX must not contain detached resource-pack indexes.' }
    [ordered]@{ Result='Passed'; Channel=$metadata.Channel; Version=$metadata.Version; Files=$zip.Entries.Count; RuntimeIncluded=$true; StoreApproved=$false; DeploymentTested=$false } | ConvertTo-Json -Compress
} finally { $zip.Dispose() }
