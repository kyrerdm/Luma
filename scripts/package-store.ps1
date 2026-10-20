param(
    [string]$IdentityFile,
    [switch]$Development,
    [switch]$NoRestore,
    [string]$MakeAppxPath,
    [string]$MakePriPath
)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $projectRoot 'src/Luma/Luma.csproj'
$projectInfo = [xml](Get-Content -LiteralPath $project -Raw)
$appVersion = [version]$projectInfo.Project.PropertyGroup.Version
$packageVersion = '{0}.{1}.{2}.0' -f $appVersion.Major,$appVersion.Minor,$appVersion.Build
if ($Development) {
    if ($IdentityFile) { throw 'Development packaging must not use a production identity file.' }
    $identity = @{ IdentityName='Luma.Development'; Publisher='CN=Luma Development, OID.2.25.311729368913984317654407730594956997722=1'; PublisherDisplayName='Luma Development'; DisplayName='Luma Development' }
    $channel = 'Development'
} else {
    if (-not $IdentityFile -or -not (Test-Path -LiteralPath $IdentityFile)) { throw 'Provide -IdentityFile with the exact Package/Identity/Name, Publisher and PublisherDisplayName from Partner Center > Product identity. See packaging/StoreIdentity.example.json.' }
    $identity = Get-Content -LiteralPath $IdentityFile -Raw | ConvertFrom-Json -AsHashtable
    foreach ($key in @('IdentityName','Publisher','PublisherDisplayName','DisplayName')) {
        if (-not ($identity[$key] -is [string]) -or [string]::IsNullOrWhiteSpace($identity[$key])) { throw "Missing identity field: $key" }
    }
    if ($identity.IdentityName -notmatch '^[A-Za-z0-9][A-Za-z0-9.-]{2,49}$' -or $identity.IdentityName -match 'Development|Contoso|Example|REPLACE|__') { throw 'Use the reserved Store package identity, not a placeholder or development identity.' }
    if (-not $identity.Publisher.StartsWith('CN=', [StringComparison]::Ordinal) -or $identity.Publisher -match 'Luma Development|Contoso|REPLACE|__|OID\.2\.25\.311729368913984317654407730594956997722') { throw 'Use the exact certificate subject in Partner Center Product identity.' }
    $channel = 'Store'
}
$env:DOTNET_CLI_HOME = Join-Path $projectRoot '.tools/cli-home'
$env:NUGET_PACKAGES = Join-Path $projectRoot '.tools/nuget'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_NOLOGO = '1'
$localDotnet = Join-Path $projectRoot '.tools/dotnet/dotnet.exe'
$dotnet = if (Test-Path -LiteralPath $localDotnet) { $localDotnet } else { 'dotnet' }
$toolsVersion = '10.0.26100.9169'
$toolRoot = Join-Path $env:NUGET_PACKAGES "microsoft.windows.sdk.buildtools/$toolsVersion/bin"
Push-Location $projectRoot
try {
    if (-not $NoRestore) {
        & $dotnet restore packaging/Tools/BuildTools.csproj --configfile NuGet.Config -p:NuGetAudit=false
        if ($LASTEXITCODE -ne 0) { throw 'Windows SDK build tools restore failed.' }
        & $dotnet restore $project --configfile NuGet.Config -r win-x64 -p:PublishSingleFile=false -p:SelfContained=true -p:NuGetAudit=false
        if ($LASTEXITCODE -ne 0) { throw 'App restore failed.' }
    }
    if (-not $MakeAppxPath) { $MakeAppxPath = (Get-ChildItem -LiteralPath $toolRoot -Filter makeappx.exe -Recurse | Where-Object { $_.Directory.Name -eq 'x64' } | Sort-Object FullName -Descending | Select-Object -First 1).FullName }
    if (-not $MakePriPath) { $MakePriPath = (Get-ChildItem -LiteralPath $toolRoot -Filter makepri.exe -Recurse | Where-Object { $_.Directory.Name -eq 'x64' } | Sort-Object FullName -Descending | Select-Object -First 1).FullName }
    if (-not $MakeAppxPath -or -not $MakePriPath) { throw 'makeappx.exe / makepri.exe not found. Restore packaging/Tools/BuildTools.csproj or pass both tool paths.' }
    # A unique staging directory prevents stale files from entering later packages.
    $outputRoot = Join-Path $projectRoot "artifacts/store/$channel"
    $stage = Join-Path $outputRoot ('stage-' + [guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $stage -Force | Out-Null
    & $dotnet publish $project --no-restore -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -p:PublishTrimmed=false -p:PublishReadyToRun=false -p:DebugType=None -o $stage
    if ($LASTEXITCODE -ne 0) { throw 'Store app publish failed.' }
    $manifest = Get-Content -LiteralPath (Join-Path $projectRoot 'packaging/AppxManifest.xml.template') -Raw
    $tokens = @{ '__IDENTITY_NAME__'=$identity.IdentityName; '__PUBLISHER__'=$identity.Publisher; '__PUBLISHER_DISPLAY_NAME__'=$identity.PublisherDisplayName; '__DISPLAY_NAME__'=$identity.DisplayName; '__VERSION__'=$packageVersion }
    foreach ($token in $tokens.Keys) { $manifest = $manifest.Replace($token, [Security.SecurityElement]::Escape($tokens[$token])) }
    $manifestPath = Join-Path $stage 'AppxManifest.xml'
    [IO.File]::WriteAllText($manifestPath, $manifest, [Text.UTF8Encoding]::new($false))
    $assetDirectory = Join-Path $stage 'Assets'
    & (Join-Path $PSScriptRoot 'new-store-assets.ps1') -OutputDirectory $assetDirectory
    Remove-Item -LiteralPath (Join-Path $assetDirectory 'StoreTile-300.png')
    $notices = @{
        'LICENSE.txt'='LICENSE'; 'KULLANIM.txt'='docs/KULLANIM.txt';
        'WINRT-LICENSE.txt'='docs/WINRT-LICENSE.txt'; 'WINDOWS-SDK-NOTICE.txt'='docs/WINDOWS-SDK-NOTICE.txt';
        'DOTNET-LICENSE.txt'='docs/distribution/DOTNET-LICENSE.txt'; 'DOTNET-THIRD-PARTY-NOTICES.txt'='docs/distribution/DOTNET-THIRD-PARTY-NOTICES.txt';
        'WINDOWS-DESKTOP-LICENSE.txt'='docs/distribution/WINDOWS-DESKTOP-LICENSE.txt'
    }
    foreach ($file in $notices.Keys) { Copy-Item -LiteralPath (Join-Path $projectRoot $notices[$file]) -Destination (Join-Path $stage $file) }
    $config = Join-Path $outputRoot ('priconfig-' + [guid]::NewGuid().ToString('N') + '.xml')
    & $MakePriPath createconfig /cf $config /dq en-US /o
    if ($LASTEXITCODE -ne 0) { throw 'Resource configuration failed.' }
    # This is one self-contained MSIX, not an app bundle with resource packs.
    # Index our Windows logos only; .NET satellite DLLs use .NET's own loader.
    [xml]$resourceConfig = Get-Content -LiteralPath $config -Raw
    [void]$resourceConfig.resources.RemoveChild($resourceConfig.resources.packaging)
    $resourceConfig.resources.index.SetAttribute('startIndexAt','Assets')
    $resourceConfig.Save($config)
    & $MakePriPath new /pr $stage /cf $config /mn $manifestPath /of (Join-Path $stage 'resources.pri') /o
    if ($LASTEXITCODE -ne 0) { throw 'Resource indexing failed.' }
    $package = Join-Path $outputRoot "Luma-$packageVersion-$channel-x64.msix"
    & $MakeAppxPath pack /d $stage /p $package /o
    if ($LASTEXITCODE -ne 0) { throw 'MSIX schema / package validation failed.' }
    $hash = (Get-FileHash -LiteralPath $package -Algorithm SHA256).Hash.ToLowerInvariant()
    [IO.File]::WriteAllText(($package + '.sha256'), ($hash + '  ' + [IO.Path]::GetFileName($package) + "`n"), [Text.UTF8Encoding]::new($false))
    [ordered]@{ Package=$package; Version=$packageVersion; Channel=$channel; Stage=$stage; IdentityName=$identity.IdentityName; Publisher=$identity.Publisher; Sha256=$hash; Signed=$false; StoreApproved=$false } | ConvertTo-Json | Set-Content -LiteralPath ($package + '.json') -Encoding utf8
    Write-Output "Created unsigned $channel MSIX: $package"
    if ($Development) { Write-Output 'DEVELOPMENT ONLY. Never submit this identity to Microsoft Store.' }
} finally { Pop-Location }
