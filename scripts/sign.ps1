param(
    [Parameter(Mandatory)][ValidatePattern('^[A-Fa-f0-9]{40}$')][string]$CertificateThumbprint,
    [string]$SignToolPath
)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$projectInfo = [xml](Get-Content -LiteralPath (Join-Path $projectRoot 'src\Luma\Luma.csproj') -Raw)
$executable = Join-Path $projectRoot ('artifacts\Luma-' + $projectInfo.Project.PropertyGroup.Version + '\Luma.exe')
if (-not (Test-Path -LiteralPath $executable -PathType Leaf)) { throw 'Build the self-contained release first.' }
$certificate = Get-Item -LiteralPath ('Cert:\CurrentUser\My\' + $CertificateThumbprint)
if (-not $certificate.HasPrivateKey -or $certificate.NotAfter -le (Get-Date) -or $certificate.NotBefore -gt (Get-Date)) { throw 'A valid code signing certificate with a private key is required.' }
if (-not @($certificate.EnhancedKeyUsageList | Where-Object ObjectId -eq '1.3.6.1.5.5.7.3.3').Count) { throw 'The certificate is not enabled for code signing.' }
if (-not $SignToolPath) {
    $command = Get-Command signtool.exe -ErrorAction SilentlyContinue
    if (-not $command) { throw 'Provide -SignToolPath from the Windows SDK.' }
    $SignToolPath = $command.Source
}
if (-not (Test-Path -LiteralPath $SignToolPath -PathType Leaf)) { throw 'SignTool was not found.' }
& $SignToolPath sign /sha1 $CertificateThumbprint /fd SHA256 /tr 'https://timestamp.digicert.com' /td SHA256 $executable
if ($LASTEXITCODE -ne 0) { throw "Signing failed ($LASTEXITCODE)." }
& $SignToolPath verify /pa /all $executable
if ($LASTEXITCODE -ne 0) { throw "Signature verification failed ($LASTEXITCODE)." }
Get-AuthenticodeSignature -LiteralPath $executable | Select-Object Status, StatusMessage
