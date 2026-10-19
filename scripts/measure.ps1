param(
    [int]$ProcessId,
    [ValidateRange(1, 60)][int]$Minutes = 2,
    [ValidateRange(1, 30)][int]$IntervalSeconds = 5,
    [string]$OutputFile
)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
if (-not $OutputFile) { $OutputFile = Join-Path $projectRoot 'artifacts\performance.json' }
if ($ProcessId) { $target = Get-Process -Id $ProcessId } else {
    $candidates = @(Get-Process -Name Luma -ErrorAction SilentlyContinue)
    if ($candidates.Count -ne 1) { throw 'Exactly one running Luma is required; otherwise specify -ProcessId.' }
    $target = $candidates[0]
}
$info = [Diagnostics.FileVersionInfo]::GetVersionInfo($target.Path)
if ($info.ProductName -ne 'Luma') { throw 'The selected process is not Luma.' }
$version = '{0}.{1}.{2}' -f $info.FileMajorPart, $info.FileMinorPart, $info.FileBuildPart
$cores = [Environment]::ProcessorCount
$clock = [Diagnostics.Stopwatch]::StartNew()
$started = [DateTime]::UtcNow.ToString('O')
$samples = [Collections.Generic.List[object]]::new()
$initialCpu = $target.TotalProcessorTime.TotalMilliseconds
$previousCpu = $initialCpu
$previousTime = 0.0
Write-Output "Observing Luma $version for $Minutes minute(s). No app controls are changed."
while ($true) {
    $target.Refresh()
    if ($target.HasExited) { throw 'Luma exited during observation; no complete-session summary was generated.' }
    $elapsed = $clock.Elapsed.TotalSeconds
    $cpu = $target.TotalProcessorTime.TotalMilliseconds
    $percent = if ($samples.Count -eq 0) { 0.0 } else { ($cpu - $previousCpu) / (($elapsed - $previousTime) * 1000 * $cores) * 100 }
    $samples.Add([pscustomobject]@{
        Seconds = [Math]::Round($elapsed, 3)
        CpuPercent = [Math]::Round($percent, 4)
        PrivateMiB = [Math]::Round($target.PrivateMemorySize64 / 1MB, 3)
        WorkingSetMiB = [Math]::Round($target.WorkingSet64 / 1MB, 3)
    })
    $previousCpu = $cpu; $previousTime = $elapsed
    if ($elapsed -ge $Minutes * 60) { break }
    Start-Sleep -Seconds $IntervalSeconds
}
$report = [pscustomobject]@{
    Version = $version
    StartedUtc = $started
    WindowsBuild = [Environment]::OSVersion.Version.ToString()
    LogicalProcessors = $cores
    DurationSeconds = [Math]::Round($elapsed, 3)
    SampleCount = $samples.Count
    AverageCpuPercent = [Math]::Round(($cpu - $initialCpu) / ($elapsed * 1000 * $cores) * 100, 4)
    PeakPrivateMiB = ($samples | Measure-Object -Property PrivateMiB -Maximum).Maximum
    PrivateChangeMiB = [Math]::Round($samples[-1].PrivateMiB - $samples[0].PrivateMiB, 3)
    PeakWorkingSetMiB = ($samples | Measure-Object -Property WorkingSetMiB -Maximum).Maximum
    Context = 'Observed desktop use; settings visibility and media playback are not classified. This is not a leak or hardware-compatibility certification.'
    Samples = $samples.ToArray()
}
$outputPath = [IO.Path]::GetFullPath($OutputFile)
[IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($outputPath)) | Out-Null
[IO.File]::WriteAllText($outputPath, ($report | ConvertTo-Json -Depth 4), [Text.UTF8Encoding]::new($false))
$report | Select-Object Version,DurationSeconds,SampleCount,AverageCpuPercent,PeakPrivateMiB,PrivateChangeMiB,PeakWorkingSetMiB | ConvertTo-Json
