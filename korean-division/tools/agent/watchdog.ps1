# watchdog.ps1 - stall-suspect report per agent worktree (PowerShell 5.1). ASCII-only source. REPORT ONLY: never kills a process.
#   watchdog.ps1                       worktrees with activity in the last -RecentHours (default 6), main/merge lines excluded
#   watchdog.ps1 <worktree> [...]      explicit worktree paths
#   watchdog.ps1 -Fake "commit=45;file=50;scratch=60;runner=0;cpu=0;gate=none"   judge only, no probing (ages in minutes)
# Verdicts: OK | WAITING_GATE | SUSPECT_STALL (no change of any kind for -StallMin, default 30). LESSONS 8, 18, 25.
# Evidence: (1) last commit age (2) newest changed file in the tree (git status) (3) runner processes (dotnet, KoreanDivision,
# make_unit, blender) with CPU change over -SampleSec (4) newest file in %TEMP%\claude\*\scratchpad whose path names the worktree/branch (5) heavy_gate wait log
# (ops/gate_wait.log or KD_GATE_LOG; a WAIT_START/WAIT last line, not OPEN/TIMEOUT, written within 5 min = waiting).
# The gate log is global (not per-worktree) shared evidence; scratchpad files count only when their path names the worktree. Runners are matched to a worktree by command line
# when readable, else counted as shared. Exit code: 0 all OK/WAITING, 2 any SUSPECT_STALL.
[CmdletBinding(PositionalBinding = $true)]
param(
    [Parameter(Position = 0, ValueFromRemainingArguments = $true)][string[]]$Worktree = @(),
    [int]$StallMin = 30,
    [int]$RecentHours = 6,
    [int]$SampleSec = 3,
    [string]$Fake = '',
    [string]$GateLog = ''
)
$ErrorActionPreference = 'Continue'
try { [Console]::OutputEncoding = [System.Text.Encoding]::UTF8 } catch { }
$here = Split-Path -Parent (Split-Path -Parent (Split-Path -Parent $PSScriptRoot))
$now = Get-Date

# Pure judgement. $e = hashtable: ages in minutes (null = unknown), runner count, cpu/gate flags.
function Get-Verdict($e, [int]$stall) {
    if ($e.gate) { return @{ V = 'WAITING_GATE'; Why = @('heavy_gate wait log shows an open wait (last entry <=5 min old)') } }
    if ($e.runner -gt 0 -and $e.cpu) { return @{ V = 'OK'; Why = @("$($e.runner) runner process(es) with CPU change") } }
    $best = $null; $src = ''
    foreach ($k in 'commit', 'file', 'scratch') {
        $a = $e[$k]
        if ($null -ne $a -and ($null -eq $best -or $a -lt $best)) { $best = $a; $src = $k }
    }
    if ($null -ne $best -and $best -lt $stall) { return @{ V = 'OK'; Why = @("recent $src change $best min ago") } }
    $ba = 'n/a'; if ($null -ne $best) { $ba = "$best" }
    $why = @("no change for >=$stall min (newest evidence: $src $ba min)")
    if ($e.runner -gt 0) { $why += "$($e.runner) runner process(es) exist but CPU did not change (idle/hung?)" } else { $why += 'no runner process' }
    return @{ V = 'SUSPECT_STALL'; Why = $why }
}

function Fmt($x) { if ($null -eq $x) { return 'n/a' } return "$x" }
function Show($label, $e, $v) {
    "[{0}] {1}" -f $v.V, $label
    "    commit {0} min | file {1} min | scratchpad {2} min | runners {3} (cpu change: {4}) | gate waiting: {5}" -f (Fmt $e.commit), (Fmt $e.file), (Fmt $e.scratch), $e.runner, $e.cpu, $e.gate
    foreach ($w in $v.Why) { "    - $w" }
}

if ($Fake -ne '') {
    $e = @{ commit = $null; file = $null; scratch = $null; runner = 0; cpu = $false; gate = $false }
    foreach ($kv in $Fake.Split(';')) {
        $p = $kv.Split('=')
        if ($p.Count -ne 2) { continue }
        $k = $p[0].Trim()
        if ($k -eq 'runner') { $e.runner = [int]$p[1] }
        elseif ($k -eq 'cpu') { $e.cpu = ($p[1] -eq '1') }
        elseif ($k -eq 'gate') { $e.gate = ($p[1] -eq 'wait' -or $p[1] -eq '1') }
        elseif ($p[1] -ne 'none') { $e[$k] = [int]$p[1] }
    }
    $v = Get-Verdict $e $StallMin
    Show 'FAKE' $e $v
    if ($v.V -eq 'SUSPECT_STALL') { exit 2 } else { exit 0 }
}

$cd = (& git -C $here rev-parse --git-common-dir 2>$null)
if (-not [System.IO.Path]::IsPathRooted($cd)) { $cd = Join-Path $here $cd }
$Main = (Split-Path -Parent (Resolve-Path $cd).Path) -replace '/', '\'

$trees = @(); $cur = $null
foreach ($l in @(& git -C $Main worktree list --porcelain 2>$null)) {
    if ($l -match '^worktree (.+)$') { $cur = @{ Path = ($Matches[1] -replace '/', '\'); Branch = '' }; $trees += $cur }
    elseif ($l -match '^branch refs/heads/(.+)$' -and $cur) { $cur.Branch = $Matches[1] }
}
$targets = @()
if ($Worktree.Count -gt 0) {
    foreach ($w in $Worktree) { $targets += @{ Path = (Resolve-Path $w).Path; Branch = (& git -C $w rev-parse --abbrev-ref HEAD 2>$null) } }
} else {
    $targets = @($trees | Where-Object { $_.Path -ne $Main -and $_.Branch -notlike 'merge/*' -and $_.Branch -notlike 'integration/*' })
}

function Get-TreeAges($path) {
    $r = @{ commit = $null; file = $null }
    $ct = (& git -C $path log -1 --format=%ct 2>$null)
    if ($ct) { $r.commit = [int](([DateTime]::UtcNow - [DateTime]'1970-01-01').TotalMinutes - ([double]$ct / 60)) }
    $newest = $null
    foreach ($ln in @(& git -C $path status --porcelain 2>$null)) {
        if ($ln.Length -lt 4) { continue }
        $f = $ln.Substring(3).Trim('"')
        if ($f -match ' -> ') { $f = ($f -split ' -> ')[1] }
        $fp = Join-Path $path $f
        if (-not (Test-Path -LiteralPath $fp)) { continue }
        $it0 = Get-Item -LiteralPath $fp
        if ($it0.PSIsContainer) { $items = @(Get-ChildItem -LiteralPath $fp -Recurse -File -ErrorAction SilentlyContinue | Select-Object -First 200) } else { $items = @($it0) }
        foreach ($it in $items) { if ($null -eq $newest -or $it.LastWriteTime -gt $newest) { $newest = $it.LastWriteTime } }
    }
    if ($newest) { $r.file = [int]($now - $newest).TotalMinutes }
    return $r
}

# shared evidence: scratchpad files (path + mtime). A file counts for a worktree only if its path contains the worktree
# folder name or branch leaf (scratchpads are per session, not per worktree, so unattributed files would mask every stall).
$spFiles = @()
$tmp = $env:TEMP; if (-not $tmp) { $tmp = [System.IO.Path]::GetTempPath() }
$spDirs = @(Get-ChildItem -Path (Join-Path $tmp 'claude\*\scratchpad'), (Join-Path $tmp 'claude\*\*\scratchpad') -Directory -ErrorAction SilentlyContinue)
foreach ($sp in $spDirs) {
    $spFiles += @(Get-ChildItem -LiteralPath $sp.FullName -Recurse -File -ErrorAction SilentlyContinue | Sort-Object LastWriteTime -Descending | Select-Object -First 300)
}
# shared evidence: gate wait log
$gate = $false
if ($GateLog -eq '') { $GateLog = $env:KD_GATE_LOG }
if (-not $GateLog) { $GateLog = Join-Path $Main 'ops\gate_wait.log' }
if (Test-Path -LiteralPath $GateLog) {
    $last = [string](Get-Content -LiteralPath $GateLog -Tail 1 -ErrorAction SilentlyContinue)
    $age = [int]($now - (Get-Item -LiteralPath $GateLog).LastWriteTime).TotalMinutes
    if ($last -match 'WAIT' -and $last -notmatch 'OPEN|TIMEOUT' -and $age -le 5) { $gate = $true }
}
# runners
$names = @('dotnet', 'KoreanDivision', 'make_unit', 'blender')
function Get-Runners {
    $o = @{}
    foreach ($p in @(Get-Process -ErrorAction SilentlyContinue | Where-Object { $names -contains $_.ProcessName })) {
        try { $o[$p.Id] = @{ Name = $p.ProcessName; Cpu = $p.TotalProcessorTime.TotalSeconds } } catch { }
    }
    return $o
}
$cmd = @{}
try {
    foreach ($c in @(Get-CimInstance Win32_Process -ErrorAction Stop)) {
        if ($names -contains ($c.Name -replace '\.exe$', '')) { $cmd[[int]$c.ProcessId] = [string]$c.CommandLine }
    }
} catch { }
$r1 = Get-Runners
if ($r1.Count -gt 0 -and $SampleSec -gt 0) { Start-Sleep -Seconds $SampleSec }
$r2 = Get-Runners

$stalled = 0
foreach ($t in $targets) {
    $ages = Get-TreeAges $t.Path
    if ($Worktree.Count -eq 0) {
        $mn = ($ages.commit, $ages.file | Where-Object { $null -ne $_ } | Measure-Object -Minimum).Minimum
        if ($null -eq $mn -or $mn -gt ($RecentHours * 60)) { continue }
    }
    $pids = @(); $shared = 0
    foreach ($id in $r2.Keys) {
        if ($cmd.ContainsKey($id) -and $cmd[$id]) { if ($cmd[$id].ToLower().Contains($t.Path.ToLower())) { $pids += $id } }
        else { $pids += $id; $shared++ }
    }
    $cpu = $false
    foreach ($id in $pids) { if ($r1.ContainsKey($id) -and ($r2[$id].Cpu - $r1[$id].Cpu) -gt 0.05) { $cpu = $true } }
    $keys = @((Split-Path -Leaf $t.Path).ToLower()); if ($t.Branch) { $keys += (($t.Branch -split '/')[-1]).ToLower() }
    $scratch = $null
    foreach ($f in $spFiles) {
        $fl = $f.FullName.ToLower(); $hit = $false
        foreach ($k in $keys) { if ($k.Length -ge 3 -and $fl.Contains($k)) { $hit = $true } }
        if ($hit) { $a = [int]($now - $f.LastWriteTime).TotalMinutes; if ($null -eq $scratch -or $a -lt $scratch) { $scratch = $a } }
    }
    $e = @{ commit = $ages.commit; file = $ages.file; scratch = $scratch; runner = $pids.Count; cpu = $cpu; gate = $gate }
    $v = Get-Verdict $e $StallMin
    Show ("{0} ({1})" -f $t.Path, $t.Branch) $e $v
    if ($shared -gt 0) { "    - note: $shared runner(s) have unreadable command lines (counted as shared)" }
    if ($v.V -eq 'SUSPECT_STALL') { $stalled++ }
}
"watchdog: report only, no process was touched."
if ($stalled -gt 0) { exit 2 } else { exit 0 }
