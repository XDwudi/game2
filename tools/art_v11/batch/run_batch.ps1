# W22 batch runner: one artgen generation at a time, below-normal priority.
# - pauses while a Unity test run (V072BatchTests.Run*) is alive, so PlayMode frame-time tests are not disturbed;
# - skips jobs whose latest revision is already a valid run (restartable);
# - stops when the Codex weekly quota reported by the last run reaches $QuotaStop percent.
# Job list: one "kitPath|jobPath" per line, repo-relative.
param([Parameter(Mandatory = $true)][string]$JobList, [Parameter(Mandatory = $true)][string]$Tag, [double]$QuotaStop = 85, [switch]$Force, [string]$Python = "E:\anaconda3\python.exe")
$ErrorActionPreference = "Continue"
$root = (Resolve-Path (Join-Path $PSScriptRoot "..\..\..")).Path
Set-Location $root
$logDir = Join-Path $root "artifacts\cache\v11\batch-logs"; New-Item -ItemType Directory -Force $logDir | Out-Null
$log = Join-Path $logDir "batch-$Tag.log"
function Log($m) { "$(Get-Date -Format o) $m" | Out-File $log -Append -Encoding utf8 }
function Test-UnityTests { [bool](Get-CimInstance Win32_Process -Filter "Name='Unity.exe'" -ErrorAction SilentlyContinue | Where-Object { $_.CommandLine -match 'V072BatchTests\.Run' }) }
function Latest-Record($jobPath) {
    $dir = Split-Path (Join-Path $root $jobPath) -Parent
    $revs = Get-ChildItem $dir -Directory -Filter "r*" -ErrorAction SilentlyContinue | Where-Object { $_.Name -match '^r\d+$' } | Sort-Object Name
    if (-not $revs) { return $null }
    $rec = Join-Path $revs[-1].FullName "record.json"
    if (Test-Path $rec) { return (Get-Content $rec -Raw -Encoding UTF8 | ConvertFrom-Json) } else { return $null }
}
Log "start $JobList"
foreach ($line in (Get-Content $JobList -Encoding UTF8)) {
    if (-not $line.Trim()) { continue }
    $kit, $job = $line.Split('|')
    $prev = Latest-Record $job
    if ($prev -and $prev.runValid -and -not $Force) { Log "skip (valid $($prev.revision)) $job"; continue }
    $waited = 0
    while (Test-UnityTests) { if ($waited -eq 0) { Log "pause: Unity test run in progress" }; Start-Sleep -Seconds 60; $waited++ }
    if ($waited -gt 0) { Log "resume after $waited min" }
    Log "run $job"
    $p = Start-Process $Python -ArgumentList @("tools\art_gen\artgen.py", "--kit", $kit, "generate", "--job", $job, "--timeout", "900") -NoNewWindow -PassThru -RedirectStandardOutput "$log.out" -RedirectStandardError "$log.err"
    try { $p.PriorityClass = 'BelowNormal' } catch {}
    $p.WaitForExit()
    $rec = Latest-Record $job
    $q = if ($rec) { $rec.codex.quotaUsedPercentLast } else { $null }
    Log ("done exit=$($p.ExitCode) valid=$($rec.runValid) rev=$($rec.revision) quota=$q problems=" + (($rec.runProblems) -join ' | '))
    if ($q -ne $null -and [double]$q -ge $QuotaStop) { Log "STOP: quota $q% >= $QuotaStop%"; break }
}
Log "end"
