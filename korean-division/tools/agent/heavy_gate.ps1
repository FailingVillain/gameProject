# Heavy-job gate v3 (v2.1 pools + resource admission, plus FIFO ticket queue, per-purpose RAM floors, budget 20 GB, waiter cap; 2026-10-23). ASCII-only source.
# Usage: heavy_gate.ps1 [-Render | -Build] [-Priority] [-Max N] [-TimeoutMin 60] [-DryRun] [-Watchdog 0]
#        heavy_gate.ps1 -Monitor [-MonitorSec N] [-MonitorIdleSec 600]     (RAM watchdog loop; started automatically on admission)
#   -Render  art pool (make_unit render children). Limit $ArtLimit (8th slot rule: only when free RAM >= 6 GB, else limit-1).
#   -Build   non-art pool, may use the reserved build slot. Default (no switch) = non-art "heavy" (selftest/stress/measure).
#   Non-art pool (build + heavy) limit 3; heavy (no -Build) may fill at most 2 of them -> 1 slot is always reserved for builds.
#   Art may never take the last combined slot (combined limit 10, 12 when free RAM >= 6 GB; -Max N fixes it): art needs total < limit-1.
#   -Priority (merge-lock holder): non-art caps and combined limit +3, reservation ignored (resource rules still apply).
#   Only RUNNING heavy processes are counted (art = python make_unit, build = dotnet build, heavy = KoreanDivision exe/dll runs).
#   Processes that merely wait (heavy_gate.ps1 itself, rerender96 drivers) hold no slot; MSBuild node children add RAM only.
#   Summed WorkingSet of heavy processes above the budget (gate_config.json budget_gb = 20; KD_GATE_BUDGET_GB / -BudgetGB override) closes it: reason BUDGET.
# Resource admission (user, 2026-10-22, after two PC freezes; floors split by purpose 2026-10-23): even with a free slot the gate waits unless
#   free physical RAM >= the floor of the call's purpose (RAM), CPU (5 s average) < MaxCpuPct (CPU), and >= AdmitSpacingSec since the last
#   admission by any gate (SPACING). Floors: heavy+build (build/selftest/measure) floor_heavy_gb (staged 7 -> 6.5 -> 6, one stage per day, see
#   gate_config.json floor_heavy_stages), render floor_render_gb = 8, EMERGENCY floor floor_emergency_gb = 4 (below it NOTHING new starts,
#   not even -Priority: reason EMERG). The staged heavy floor descends AUTOMATICALLY at local midnight (10-24 -> 6.5, 10-25 -> 6): no human check,
#   to halt it edit/delete the later stages in gate_config.json.
#   -Priority (merge builds) keeps a FLOOR (F2, 2026-10-23): effective priority floor = max(emergency floor, floor_priority_gb = 5), never above the
#   call's own purpose floor; below it reason RAM (and EMERG below the emergency floor). Budget hard cap, CPU and spacing apply to -Priority too.
#   Floor sanity (F3): every floor read from the config AND from env must be a finite number inside its range, else it is IGNORED (log line
#   CONFIG_IGNORED, default used): heavy/render floor 6..64 GB, emergency floor 3..8, priority floor 3..12, budget > 0 (max 256), waiter cap 0..1000.
#   Wait reasons: QUEUE RAM EMERG CPU SPACING POOL BUDGET LOCK.
# FIFO ticket queue (v3): every waiting call holds one ticket file in <log dir>\gate_queue\ (priority tickets sort first). Only the HEAD ticket
#   evaluates admission (CPU sampling, process scan); the others just sleep (reason QUEUE) and write no gate_status.json. Tickets of dead PIDs
#   (or a reused PID) are removed by whoever looks. Waiter cap (waiter_cap = 6): a NEW non-priority call that finds MORE than the cap tickets
#   exits at once with exit code 75 ("gate overloaded: report to conductor, do other work"), log line OVERLOAD. Callers treat any non-zero exit as "not run".
# Config: tools/agent/gate_config.json (KD_GATE_CONFIG = other path), read at start. Missing/broken file or key -> v2.1 values (budget 14, floors 8/8, emergency 3).
#   Env overrides win: KD_GATE_BUDGET_GB KD_GATE_FLOOR_HEAVY_GB KD_GATE_FLOOR_RENDER_GB KD_GATE_FLOOR_EMERGENCY_GB KD_GATE_FLOOR_PRIORITY_GB KD_GATE_WAITER_CAP.
# Watchdog (-Watchdog 1 = default on, -Watchdog 0 = off): when free RAM < KillFreeGB for >= KillAfterSec, the NEWEST running art/heavy process that started
#   after a gate admission is stopped by PID (taskkill /PID <pid> /T = that job and its own children only; never by image name).
#   Runs inside waiting gates and in the -Monitor loop (auto-started on admission, exits after MonitorIdleSec without heavy jobs).
#   watchdog.ps1 (stall report, never kills) is separate; it keeps reading the same log (WAIT_START/WAIT/OPEN/TIMEOUT lines).
# Exit codes: 0 open, 1 timeout / dry-run closed, 75 overloaded. ANY non-zero exit = NOT RUN: a caller must not start its heavy work (F1).
#   Timeout (exit 1) also hits callers that never reached the head: -TimeoutMin counts from the call start (message says 'timed out in queue').
# Log: C:\gameProject\ops\gate_wait.log (KD_GATE_LOG overrides); lines carry ' json={...}' (free_gb, cpu_pct, why, counts).
#   Current status (one JSON line, overwritten): gate_status.json next to the log. Shared state (admissions, last kill): gate_state.json.
# -DryRun: evaluate once, print decision and occupants, no waiting, no log/state writes.
# Test hooks: KD_GATE_FAKEPROCS="pid|name|cmdline|ageSec;..." KD_GATE_FREEGB_OVERRIDE KD_GATE_CPU_OVERRIDE KD_GATE_HEAVYRAM_OVERRIDE
#   KD_GATE_SPACING_SEC KD_GATE_KILL_AFTER_SEC KD_GATE_NO_AUTOMONITOR=1 KD_GATE_MONITOR_IDLE_SEC KD_GATE_CONFIG.
param([int]$Max = 0, [int]$TimeoutMin = 60, [int]$TimeoutSec = 0, [int]$PollSec = 8, [int]$ShowAfterSec = 120,
      [switch]$Render, [switch]$Build, [switch]$Priority, [switch]$DryRun, [double]$BudgetGB = 0, [string]$Lane = '',
      [int]$Watchdog = 1, [switch]$Monitor, [int]$MonitorSec = 0, [int]$MonitorIdleSec = 600)

# TEMP 2026-10-22: PC froze twice (forced power-off) -> art render pool lowered 8 -> 4. Normal value: 8 (start 6). Restore here only.
$script:ArtLimit = 4
$script:NonArtLimit = 3
$script:BuildReserve = 1
# Resource admission. Floors / budget / cap now come from gate_config.json (defaults below = the v2.1 values; PC has 31 GB physical RAM).
$script:MaxCpuPct = 85          # 5 s average must be below this
$script:CpuSamples = 5          # 1 s apart
$script:AdmitSpacingSec = 20
$script:KillFreeGB = 3
$script:KillAfterSec = 10

# Classify a process: 'art' | 'build' | 'heavy' | 'child' (RAM only, no slot) | $null.
function Get-HeavyKind([string]$Name, [string]$CmdLine) {
  if ($null -eq $CmdLine) { $CmdLine = '' }
  if ($CmdLine -match 'heavy_gate') { return $null }   # waiting gate scripts / monitor hold no slot
  if ($Name -eq 'KoreanDivision.exe') {
    if ($CmdLine -match '[\\]dist[\\]') { return $null }
    return 'heavy'
  }
  if ($Name -eq 'dotnet.exe') {
    if ($CmdLine -match 'KoreanDivision\.dll') { return 'heavy' }
    if ($CmdLine -match 'MSBuild\.dll|VBCSCompiler|csc\.dll') { return 'child' }
    if ($CmdLine -match '\sbuild(\s|$)|\spublish(\s|$)|\stest(\s|$)') { return 'build' }
    return $null
  }
  # Only the actual render child (make_unit) is heavy; the rerender96 driver just waits on it.
  if ($Name -eq 'python.exe' -and $CmdLine -match 'make_unit') { return 'art' }
  return $null
}

function Get-ProcList {
  if ($null -ne $env:KD_GATE_FAKEPROCS) {
    $o = @(); $now = Get-Date
    foreach ($e in ($env:KD_GATE_FAKEPROCS -split ';')) {
      if (-not $e.Trim()) { continue }
      $f = $e.Split('|')
      $cl = ''; if ($f.Count -gt 2) { $cl = $f[2] }
      $age = 0; if ($f.Count -gt 3 -and $f[3]) { $age = [double]$f[3] }
      $o += [pscustomobject]@{ ProcessId = [int]$f[0]; Name = $f[1]; CommandLine = $cl; WorkingSetSize = [double]0; CreationDate = $now.AddSeconds(-$age) }
    }
    return $o
  }
  return @(Get-CimInstance Win32_Process)
}

# Classified heavy processes (kind != child) with creation time, for the watchdog.
function Get-HeavyProcs {
  $r = @()
  foreach ($p in (Get-ProcList)) {
    $k = Get-HeavyKind $p.Name $p.CommandLine
    if (-not $k -or $k -eq 'child') { continue }
    $r += [pscustomobject]@{ Kind = $k; ProcessId = [int]$p.ProcessId; Name = $p.Name; CreationDate = $p.CreationDate }
  }
  return $r
}

# counts: art/build/heavy running; occ = occupant strings "kind:pid:name"; ramBytes of heavy + child.
function Get-HeavyCounts {
  $c = @{ art = 0; build = 0; heavy = 0; ramBytes = [double]0; occ = @() }
  foreach ($p in (Get-ProcList)) {
    $k = Get-HeavyKind $p.Name $p.CommandLine
    if (-not $k) { continue }
    $c.ramBytes += [double]$p.WorkingSetSize
    if ($k -eq 'child') { continue }
    $c[$k]++
    $c.occ += ('{0}:{1}:{2}' -f $k, $p.ProcessId, $p.Name)
  }
  return $c
}

function Get-FreeGB {
  if ($env:KD_GATE_FREEGB_OVERRIDE) { return [double]$env:KD_GATE_FREEGB_OVERRIDE }
  return [math]::Round((Get-CimInstance Win32_OperatingSystem).FreePhysicalMemory / 1MB, 1)
}
# Average Win32_Processor.LoadPercentage over $Samples readings 1 s apart. -1 = unknown (does not block).
function Get-CpuPct([int]$Samples = $script:CpuSamples) {
  if ($env:KD_GATE_CPU_OVERRIDE) { return [double]$env:KD_GATE_CPU_OVERRIDE }
  $v = @()
  for ($i = 0; $i -lt $Samples; $i++) {
    try {
      $l = @(Get-CimInstance Win32_Processor -ErrorAction Stop | Where-Object { $null -ne $_.LoadPercentage } | ForEach-Object { [double]$_.LoadPercentage })
      if ($l.Count -gt 0) { $v += ($l | Measure-Object -Average).Average }
    } catch {}
    if ($i -lt ($Samples - 1)) { Start-Sleep -Milliseconds 1000 }
  }
  if ($v.Count -eq 0) { return -1 }
  return [math]::Round(($v | Measure-Object -Average).Average, 1)
}
function Get-GateLimit([double]$FreeGB, [int]$Fixed = 0) {
  if ($Fixed -gt 0) { return $Fixed }
  if ($FreeGB -ge 6) { return 12 }
  return 10
}
function Get-ArtLimit([double]$FreeGB) { if ($FreeGB -ge 6) { return $script:ArtLimit }; return ($script:ArtLimit - 1) }
function Get-HeavyRAMGB([hashtable]$Counts) {
  if ($env:KD_GATE_HEAVYRAM_OVERRIDE) { return [double]$env:KD_GATE_HEAVYRAM_OVERRIDE }
  return [math]::Round([double]$Counts.ramBytes / 1GB, 1)
}
function Get-BudgetGB([double]$Fixed = 0) {
  if ($Fixed -gt 0) { return $Fixed }
  $e = Get-EnvNum 'KD_GATE_BUDGET_GB' 'budget_gb'; if ($null -ne $e) { return $e }
  Initialize-GateConfig
  return [double]$script:Cfg.budget_gb
}
function Test-BudgetOk([double]$HeavyGB, [double]$BudgetGB) { return ($HeavyGB -le $BudgetGB) }
function Get-SpacingSec { if ($null -ne $env:KD_GATE_SPACING_SEC -and $env:KD_GATE_SPACING_SEC -ne '') { return [double]$env:KD_GATE_SPACING_SEC }; return $script:AdmitSpacingSec }
function Get-KillAfterSec { if ($env:KD_GATE_KILL_AFTER_SEC) { return [double]$env:KD_GATE_KILL_AFTER_SEC }; return $script:KillAfterSec }

# Pool decision. Kind = art|build|heavy. Limit = combined limit (already incl. fixed -Max).
function Test-PoolOpen([hashtable]$Counts, [string]$Kind, [double]$FreeGB, [int]$Limit, [bool]$Priority) {
  if ($FreeGB -lt 3) { return $false }
  $bonus = 0; if ($Priority) { $bonus = 3 }
  $nonart = [int]$Counts.build + [int]$Counts.heavy
  $total = $nonart + [int]$Counts.art
  if ($Kind -eq 'art') {
    if ([int]$Counts.art -ge (Get-ArtLimit $FreeGB)) { return $false }
    return ($total -lt ($Limit - $script:BuildReserve))   # art never takes the last combined slot
  }
  if ($total -ge ($Limit + $bonus)) { return $false }
  $poolCap = $script:NonArtLimit + $bonus
  if ($nonart -ge $poolCap) { return $false }
  if ($Kind -eq 'heavy' -and -not $Priority) { return ([int]$Counts.heavy -lt ($script:NonArtLimit - $script:BuildReserve)) }
  return $true
}

# Resource reasons to wait (pure). SinceAdmitSec < 0 = no admission recorded. Cpu < 0 = unknown (not blocking).
# Priority: RAM floor = max(emergency, floor_priority_gb) (never above the purpose floor); the emergency floor (EMERG), CPU and spacing apply as well.
function Get-ResourceWaitReasons([string]$Kind, [double]$FreeGB, [double]$Cpu, [double]$SinceAdmitSec, [bool]$Priority = $false) {
  $r = @()
  if ($FreeGB -lt (Get-EffectiveFloor $Kind $Priority)) { $r += 'RAM' }
  if ($FreeGB -lt (Get-FloorEmergency)) { $r += 'EMERG' }
  if ($Cpu -ge $script:MaxCpuPct) { $r += 'CPU' }
  if ($SinceAdmitSec -ge 0 -and $SinceAdmitSec -lt (Get-SpacingSec)) { $r += 'SPACING' }
  return $r
}

function Get-GateLogPath { $f = $env:KD_GATE_LOG; if (-not $f) { $f = 'C:\gameProject\ops\gate_wait.log' }; return $f }
function Get-GateFile([string]$Leaf) { return (Join-Path (Split-Path -Parent (Get-GateLogPath)) $Leaf) }
function Write-GateLog([string]$Msg) {
  try { Add-Content -Path (Get-GateLogPath) -Value ('{0} pid={1} {2}' -f (Get-Date -Format 'yyyy-MM-dd HH:mm:ss'), $PID, $Msg) -ErrorAction Stop } catch {}
}
# One-line JSON status (overwritten) for the harness; returns the JSON so log lines can carry it.
function Write-GateStatus([hashtable]$S, [switch]$NoFile) {
  $S.t = (Get-Date -Format 'yyyy-MM-ddTHH:mm:ss'); $S.pid = $PID
  $j = ($S | ConvertTo-Json -Compress)
  if ($NoFile) { return $j }
  try { [System.IO.File]::WriteAllText((Get-GateFile 'gate_status.json'), $j + "`r`n") } catch {}
  return $j
}

function UtcTicks { return (Get-Date).ToUniversalTime().Ticks }
function Read-GateState {
  $s = @{ lastAdmit = [long]0; lastKill = [long]0; admits = @() }
  $p = Get-GateFile 'gate_state.json'
  try {
    if (Test-Path -LiteralPath $p) {
      $o = [System.IO.File]::ReadAllText($p) | ConvertFrom-Json
      if ($o.lastAdmit) { $s.lastAdmit = [long]$o.lastAdmit }
      if ($o.lastKill) { $s.lastKill = [long]$o.lastKill }
      $cut = (UtcTicks) - [long](12 * 3600 * 1e7)
      foreach ($a in @($o.admits)) { if ($a -and [long]$a.t -ge $cut) { $s.admits += @{ t = [long]$a.t; kind = [string]$a.kind; pid = [int]$a.pid } } }
    }
  } catch {}
  return $s
}
function Write-GateState([hashtable]$S) {
  $a = @($S.admits); if ($a.Count -gt 50) { $a = $a[($a.Count - 50)..($a.Count - 1)] }
  $o = @{ lastAdmit = $S.lastAdmit; lastKill = $S.lastKill; admits = $a }
  try { [System.IO.File]::WriteAllText((Get-GateFile 'gate_state.json'), ($o | ConvertTo-Json -Compress -Depth 4)) } catch {}
}

# Cross-process lock for gate_state.json (admission decision + kills). $null = not acquired.
function Enter-GateMutex([int]$Ms = 15000) {
  $m = $null
  foreach ($n in @('Global\KD_HEAVY_GATE_STATE', 'Local\KD_HEAVY_GATE_STATE')) {
    try { $m = New-Object System.Threading.Mutex($false, $n); break } catch { $m = $null }
  }
  if ($null -eq $m) { return $null }
  try { if ($m.WaitOne($Ms)) { return $m } } catch [System.Threading.AbandonedMutexException] { return $m } catch {}
  $m.Dispose(); return $null
}
function Exit-GateMutex($m) { if ($m) { try { $m.ReleaseMutex() } catch {}; $m.Dispose() } }

# Newest running art/heavy process that started after the oldest recorded admission (= gate-supervised job). Pure on inputs.
function Select-KillVictim($Procs, $Admits) {
  $ad = @($Admits)
  if ($ad.Count -eq 0) { return $null }
  $t0 = ($ad | ForEach-Object { [long]$_.t } | Measure-Object -Minimum).Minimum
  $from = ([DateTime]::new([long]$t0 - [long]20000000, [DateTimeKind]::Utc)).ToLocalTime()
  $best = $null
  foreach ($p in @($Procs)) {
    if ($p.Kind -ne 'art' -and $p.Kind -ne 'heavy') { continue }
    if ($null -eq $p.CreationDate -or $p.CreationDate -lt $from) { continue }
    if ($null -eq $best -or $p.CreationDate -gt $best.CreationDate) { $best = $p }
  }
  return $best
}

$script:lowSince = $null
$script:lastNoVictimLog = [DateTime]::MinValue
# One watchdog step. Stops at most one process per KillAfterSec across all gates (state lock + lastKill).
function Invoke-WatchdogTick([double]$FreeGB) {
  $now = Get-Date
  if ($FreeGB -ge $script:KillFreeGB) { $script:lowSince = $null; return }
  if ($null -eq $script:lowSince) { $script:lowSince = $now; return }
  $low = ($now - $script:lowSince).TotalSeconds
  if ($low -lt (Get-KillAfterSec)) { return }
  $m = Enter-GateMutex 5000
  if ($null -eq $m) { return }
  try {
    $st = Read-GateState
    if ($st.lastKill -gt 0 -and ((UtcTicks) - $st.lastKill) / 1e7 -lt (Get-KillAfterSec)) { $script:lowSince = $now; return }
    $v = Select-KillVictim (Get-HeavyProcs) $st.admits
    if ($null -eq $v) {
      if (($now - $script:lastNoVictimLog).TotalSeconds -ge 60) {
        Write-GateLog ("WATCHDOG low RAM free={0} GB for {1}s, no gate-admitted render/heavy process to stop" -f $FreeGB, [int]$low)
        $script:lastNoVictimLog = $now
      }
      return
    }
    if ($null -eq $env:KD_GATE_FAKEPROCS) {   # guard against PID reuse: the PID must still be the same process
      $gp = Get-Process -Id $v.ProcessId -ErrorAction SilentlyContinue
      if ($null -eq $gp -or [math]::Abs(($gp.StartTime - $v.CreationDate).TotalSeconds) -gt 2) { return }
    }
    $out = (& taskkill.exe /PID $v.ProcessId /T /F 2>&1 | Out-String).Trim() -replace '\s+', ' '
    $st.lastKill = UtcTicks
    Write-GateState $st
    $j = Write-GateStatus @{ state = 'kill'; free_gb = $FreeGB; victim = ('{0}:{1}:{2}' -f $v.Kind, $v.ProcessId, $v.Name); low_s = [int]$low }
    Write-GateLog ("KILL {0}:{1}:{2} free={3} GB for {4}s (newest gate-admitted job, PID tree only) taskkill=[{5}] json={6}" -f $v.Kind, $v.ProcessId, $v.Name, $FreeGB, [int]$low, $out, $j)
    "heavy_gate watchdog: stopped $($v.Kind):$($v.ProcessId):$($v.Name) (free RAM $FreeGB GB for $([int]$low)s)"
    $script:lowSince = $now
  } finally { Exit-GateMutex $m }
}

# Start the -Monitor loop unless a live one exists. Caller holds the state lock.
function Start-GateMonitor {
  if ($env:KD_GATE_NO_AUTOMONITOR -eq '1') { return }
  $pf = Get-GateFile 'gate_monitor.pid'
  try {
    if (Test-Path -LiteralPath $pf) {
      $old = [int]([System.IO.File]::ReadAllText($pf).Trim())
      $cp = Get-CimInstance Win32_Process -Filter "ProcessId=$old" -ErrorAction SilentlyContinue
      if ($cp -and [string]$cp.CommandLine -match 'heavy_gate' -and [string]$cp.CommandLine -match '-Monitor') { return }
    }
    $a = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', ('"{0}"' -f $PSCommandPath), '-Monitor')
    $p = Start-Process -FilePath 'powershell.exe' -ArgumentList $a -WindowStyle Hidden -PassThru
    [System.IO.File]::WriteAllText($pf, [string]$p.Id)
  } catch {}
}

# ---- gate_config.json (v3) ----
$script:CfgDefault = @{ budget_gb = 14; floor_heavy_gb = 8; floor_render_gb = 8; floor_emergency_gb = 3; floor_priority_gb = 5; waiter_cap = 6 }   # = v2.1 values (cap, priority floor are new)
# Allowed [min, max] per key (F3). Anything else (NaN, Infinity, negative, a typo like 0.6, text) is ignored and the default used.
$script:CfgRange = @{ budget_gb = @(0.5, 256); floor_heavy_gb = @(6, 64); floor_render_gb = @(6, 64); floor_emergency_gb = @(3, 8); floor_priority_gb = @(3, 12); waiter_cap = @(0, 1000) }
$script:Cfg = $null
$script:CfgNotes = New-Object System.Collections.ArrayList   # CONFIG_IGNORED messages not yet logged
$script:CfgNoted = @{}
function Add-CfgNote([string]$Key, [string]$Val, [string]$Why) {
  $k = "$Key=$Val"
  if ($script:CfgNoted.ContainsKey($k)) { return }
  $script:CfgNoted[$k] = $true
  [void]$script:CfgNotes.Add("CONFIG_IGNORED $Key=[$Val] $Why (default used)")
}
# Parse a finite number inside [Min, Max]; $null (and a note) otherwise.
function ConvertTo-RangedNum($Raw, [string]$Key, [double]$Min, [double]$Max) {
  $d = 0.0; $s = [string]$Raw
  if (-not [double]::TryParse($s, [Globalization.NumberStyles]::Float, [Globalization.CultureInfo]::InvariantCulture, [ref]$d)) { Add-CfgNote $Key $s 'not a number'; return $null }
  if ([double]::IsNaN($d) -or [double]::IsInfinity($d)) { Add-CfgNote $Key $s 'not finite'; return $null }
  if ($d -lt $Min -or $d -gt $Max) { Add-CfgNote $Key $s "outside $Min..$Max"; return $null }
  return $d
}
# Logs (or, in -DryRun, prints) pending CONFIG_IGNORED notes.
function Write-CfgNotes {
  if ($script:CfgNotes.Count -eq 0) { return }
  $n = @($script:CfgNotes); $script:CfgNotes.Clear()
  foreach ($m in $n) { if ($DryRun) { "DRYRUN note: $m" } else { Write-GateLog $m } }
}
$script:CfgStages = @()
$script:CfgSource = 'default'
function Get-GateConfigPath { if ($env:KD_GATE_CONFIG) { return $env:KD_GATE_CONFIG }; return (Join-Path $PSScriptRoot 'gate_config.json') }
# Loads the config; never throws. CfgSource: default (no file) | file | file-partial (some keys ignored) | broken (unparsable -> all defaults).
function Import-GateConfig([string]$Path = '') {
  $cfg = @{}
  foreach ($k in @($script:CfgDefault.Keys)) { $cfg[$k] = $script:CfgDefault[$k] }
  $stages = @(); $src = 'default'
  if (-not $Path) { $Path = Get-GateConfigPath }
  try {
    if (Test-Path -LiteralPath $Path) {
      $o = [System.IO.File]::ReadAllText($Path) | ConvertFrom-Json -ErrorAction Stop
      if ($null -eq $o) { throw 'empty' }
      $bad = 0
      foreach ($k in @($script:CfgDefault.Keys)) {
        $v = $o.$k
        if ($null -eq $v) { continue }
        $rg = $script:CfgRange[$k]
        $d = ConvertTo-RangedNum $v $k $rg[0] $rg[1]
        if ($null -ne $d) { if ($k -eq 'waiter_cap') { $cfg[$k] = [int]$d } else { $cfg[$k] = $d } } else { $bad++ }
      }
      foreach ($s in @($o.floor_heavy_stages)) {
        if ($null -eq $s) { continue }
        $dt = [DateTime]::MinValue
        $g = ConvertTo-RangedNum $s.gb 'floor_heavy_stages.gb' $script:CfgRange.floor_heavy_gb[0] $script:CfgRange.floor_heavy_gb[1]
        if ($null -ne $g -and [DateTime]::TryParseExact([string]$s.from, 'yyyy-MM-dd', [Globalization.CultureInfo]::InvariantCulture, [Globalization.DateTimeStyles]::None, [ref]$dt)) {
          $stages += [pscustomobject]@{ from = $dt; gb = $g }
        } else { $bad++; if ($null -ne $g) { Add-CfgNote 'floor_heavy_stages.from' ([string]$s.from) 'not a yyyy-MM-dd date' } }
      }
      if ($bad -gt 0) { $src = 'file-partial' } else { $src = 'file' }
    }
  } catch {
    $cfg = @{}; foreach ($k in @($script:CfgDefault.Keys)) { $cfg[$k] = $script:CfgDefault[$k] }
    $stages = @(); $src = 'broken'
  }
  $script:Cfg = $cfg; $script:CfgStages = $stages; $script:CfgSource = $src
}
function Initialize-GateConfig { if ($null -eq $script:Cfg) { Import-GateConfig } }
# Env override as a finite number inside the key's range; $null (+ CONFIG_IGNORED note) when unset-ignored/invalid. Key = config key it overrides.
function Get-EnvNum([string]$Name, [string]$Key = '') {
  $v = [Environment]::GetEnvironmentVariable($Name)
  if ($null -eq $v -or $v.Trim() -eq '') { return $null }
  if (-not $Key) { $Key = $Name -replace '^KD_GATE_', '' -replace '_GB$', '' ; $Key = $Key.ToLower() }
  $rg = $script:CfgRange[$Key]
  if ($null -eq $rg) { $rg = @(-1e9, 1e9) }
  return (ConvertTo-RangedNum $v.Trim() $Name $rg[0] $rg[1])
}
# Staged floor: the stage with the latest 'from' <= Today wins, else Base (pure).
function Get-StagedFloor($Stages, [double]$Base, [DateTime]$Today) {
  $best = $null
  foreach ($s in @($Stages)) { if ($s.from.Date -le $Today.Date -and ($null -eq $best -or $s.from -gt $best.from)) { $best = $s } }
  if ($null -ne $best) { return [double]$best.gb }
  return $Base
}
function Get-FloorHeavy {
  $e = Get-EnvNum 'KD_GATE_FLOOR_HEAVY_GB' 'floor_heavy_gb'; if ($null -ne $e) { return $e }
  Initialize-GateConfig
  return (Get-StagedFloor $script:CfgStages ([double]$script:Cfg.floor_heavy_gb) (Get-Date))
}
function Get-FloorRender {
  $e = Get-EnvNum 'KD_GATE_FLOOR_RENDER_GB' 'floor_render_gb'; if ($null -ne $e) { return $e }
  Initialize-GateConfig; return [double]$script:Cfg.floor_render_gb
}
function Get-FloorEmergency {
  $e = Get-EnvNum 'KD_GATE_FLOOR_EMERGENCY_GB' 'floor_emergency_gb'; if ($null -ne $e) { return $e }
  Initialize-GateConfig; return [double]$script:Cfg.floor_emergency_gb
}
function Get-FloorPriority {
  $e = Get-EnvNum 'KD_GATE_FLOOR_PRIORITY_GB' 'floor_priority_gb'; if ($null -ne $e) { return $e }
  Initialize-GateConfig; return [double]$script:Cfg.floor_priority_gb
}
function Get-FloorFor([string]$Kind) { if ($Kind -eq 'art') { return (Get-FloorRender) }; return (Get-FloorHeavy) }
# Floor that applies to this call: the purpose floor, or for -Priority max(emergency, priority floor) capped at the purpose floor (F2).
function Get-EffectiveFloor([string]$Kind, [bool]$Priority) {
  $f = Get-FloorFor $Kind
  if (-not $Priority) { return $f }
  return [math]::Min($f, [math]::Max((Get-FloorEmergency), (Get-FloorPriority)))
}
function Get-WaiterCap {
  $e = Get-EnvNum 'KD_GATE_WAITER_CAP' 'waiter_cap'; if ($null -ne $e) { return [int]$e }
  Initialize-GateConfig; return [int]$script:Cfg.waiter_cap
}

# ---- FIFO ticket queue (v3) ----
function Get-QueueDir { return (Get-GateFile 'gate_queue') }
function Get-ProcStartTicks([int]$ProcId) {
  try { return (Get-Process -Id $ProcId -ErrorAction Stop).StartTime.ToUniversalTime().Ticks } catch { return [long]0 }
}
# Alive = the PID exists and (when the ticket recorded a start time) is still the same process; tickets older than 12 h are dead.
function Test-TicketAlive($T) {
  if ($T.pid -le 0) { return $false }
  if (((UtcTicks) - $T.t) / 1e7 -gt 43200) { return $false }
  $st = Get-ProcStartTicks $T.pid
  if ($st -eq 0) { return $false }
  if ($T.pst -gt 0 -and [math]::Abs(($st - $T.pst) / 1e7) -gt 2) { return $false }
  return $true
}
# Live tickets in FIFO order (priority tickets first, then by join time). Cleanup = delete dead tickets (callers hold the state lock).
function Get-LiveTickets([bool]$Cleanup) {
  $d = Get-QueueDir; $live = @()
  if (-not (Test-Path -LiteralPath $d)) { return @() }
  foreach ($f in @(Get-ChildItem -LiteralPath $d -Filter '*.tkt' -ErrorAction SilentlyContinue)) {
    $t = $null
    try {
      $o = [System.IO.File]::ReadAllText($f.FullName) | ConvertFrom-Json -ErrorAction Stop
      $t = [pscustomobject]@{ file = $f.FullName; pid = [int]$o.pid; pst = [long]$o.pst; prio = [int]$o.prio; t = [long]$o.t; kind = [string]$o.kind }
    } catch {
      if (((Get-Date) - $f.LastWriteTime).TotalSeconds -gt 60) { $t = [pscustomobject]@{ file = $f.FullName; pid = 0; pst = [long]0; prio = 1; t = [long]0; kind = '' } } else { continue }
    }
    if (Test-TicketAlive $t) { $live += $t }
    elseif ($Cleanup) { Remove-Item -LiteralPath $f.FullName -Force -ErrorAction SilentlyContinue }
  }
  return @($live | Sort-Object prio, t, pid)
}
# prio 0 = -Priority (jumps the queue), 1 = normal. Returns the ticket file path.
function Add-GateTicket([string]$Kind, [bool]$Priority, [int]$ProcId, [long]$Ticks = 0) {
  $d = Get-QueueDir
  try { New-Item -ItemType Directory -Force -Path $d | Out-Null } catch {}
  if ($Ticks -le 0) { $Ticks = UtcTicks }
  $prio = 1; if ($Priority) { $prio = 0 }
  $p = Join-Path $d ('{0}_{1:D19}_{2}.tkt' -f $prio, $Ticks, $ProcId)
  $o = @{ pid = $ProcId; pst = (Get-ProcStartTicks $ProcId); prio = $prio; t = $Ticks; kind = $Kind }
  try { [System.IO.File]::WriteAllText($p, ($o | ConvertTo-Json -Compress)) } catch {}
  return $p
}
# Callers ahead of this PID (its own ticket) or, without a ticket, ahead of a hypothetical new ticket. 0 = head (pure on the list).
function Get-QueueAhead($Live, [int]$ProcId, [bool]$Priority) {
  $l = @($Live); $i = 0
  foreach ($t in $l) { if ($t.pid -eq $ProcId) { return $i }; $i++ }
  if ($Priority) { return @($l | Where-Object { $_.prio -eq 0 }).Count }
  return $l.Count
}
function Remove-GateTicket { if ($script:Ticket) { Remove-Item -LiteralPath $script:Ticket -Force -ErrorAction SilentlyContinue; $script:Ticket = $null } }
$script:Ticket = $null

if ($MyInvocation.InvocationName -eq '.') { return }   # dot-sourced by tests: functions only

if ($Monitor) {
  if ($env:KD_GATE_MONITOR_IDLE_SEC) { $MonitorIdleSec = [int]$env:KD_GATE_MONITOR_IDLE_SEC }
  $pf = Get-GateFile 'gate_monitor.pid'
  try {
    if (Test-Path -LiteralPath $pf) {
      $old = [int]([System.IO.File]::ReadAllText($pf).Trim())
      if ($old -ne $PID) {
        $cp = Get-CimInstance Win32_Process -Filter "ProcessId=$old" -ErrorAction SilentlyContinue
        if ($cp -and [string]$cp.CommandLine -match 'heavy_gate' -and [string]$cp.CommandLine -match '-Monitor') { 'monitor already running'; exit 0 }
      }
    }
    [System.IO.File]::WriteAllText($pf, [string]$PID)
  } catch {}
  Write-GateLog "MONITOR_START killBelow=$($script:KillFreeGB) GB after=$(Get-KillAfterSec)s idleExit=${MonitorIdleSec}s"
  $t0 = Get-Date; $lastBusy = $t0; $lastStat = [DateTime]::MinValue; $mp = [math]::Max(1, [math]::Min($PollSec, 5))
  while ($true) {
    $free = Get-FreeGB
    if ($Watchdog -ne 0) { Invoke-WatchdogTick $free }
    $now = Get-Date
    if (($now - $lastStat).TotalSeconds -ge 30) {
      $hp = @(Get-HeavyProcs)
      if ($hp.Count -gt 0) { $lastBusy = $now }
      $null = Write-GateStatus @{ state = 'monitor'; free_gb = $free; cpu_pct = (Get-CpuPct 1); jobs = $hp.Count }
      $lastStat = $now
    }
    if ($MonitorSec -gt 0 -and ($now - $t0).TotalSeconds -ge $MonitorSec) { break }
    if ($MonitorIdleSec -gt 0 -and ($now - $lastBusy).TotalSeconds -ge $MonitorIdleSec) { break }
    Start-Sleep -Seconds $mp
  }
  Write-GateLog 'MONITOR_EXIT'
  try { if ([System.IO.File]::ReadAllText($pf).Trim() -eq [string]$PID) { Remove-Item -LiteralPath $pf -ErrorAction SilentlyContinue } } catch {}
  exit 0
}

$Kind = 'heavy'
if ($Render -or $Lane -eq 'render') { $Kind = 'art' } elseif ($Build) { $Kind = 'build' }
Import-GateConfig
$budget = Get-BudgetGB $BudgetGB
$cap = Get-WaiterCap
$prio = [bool]$Priority
$null = Get-FloorHeavy; $null = Get-FloorRender; $null = Get-FloorEmergency; $null = Get-FloorPriority   # validate env/config once, so CONFIG_IGNORED lines come first
Write-CfgNotes
$tmo = $TimeoutMin * 60; if ($TimeoutSec -gt 0) { $tmo = $TimeoutSec }
$t0 = Get-Date
$lastShow = 0
$lastPrint = -99999
$waiting = $false
$lastWhy = ''

try {
while ($true) {
  $cycle0 = Get-Date
  # ---- phase 1 (state lock): ticket cleanup, join, overload check, queue position ----
  $mtx = $null
  if (-not $DryRun) { $mtx = Enter-GateMutex 15000 }
  $lockFail = (-not $DryRun -and $null -eq $mtx)
  $overload = $false; $ahead = 0; $qlen = 0
  try {
    if (-not $lockFail) {
      $live = @(Get-LiveTickets (-not $DryRun))
      if (-not $prio -and $cap -gt 0 -and $live.Count -gt $cap -and ($DryRun -or -not $script:Ticket)) { $overload = $true }
      elseif (-not $DryRun -and -not $script:Ticket) { $script:Ticket = Add-GateTicket $Kind $prio $PID; $live = @(Get-LiveTickets $false) }
      $qlen = $live.Count
      $ahead = Get-QueueAhead $live $PID $prio
    }
  } finally { Exit-GateMutex $mtx }
  if ($overload) {
    $msg = "gate overloaded: $qlen callers already waiting (cap $cap). Report to conductor and do other work; do not retry in a loop."
    if ($DryRun) { "DRYRUN $msg"; exit 75 }
    $j = Write-GateStatus @{ state = 'overload'; kind = $Kind; why = 'OVERLOAD'; queue = $qlen; cap = $cap } -NoFile
    Write-GateLog "OVERLOAD kind=$Kind queue=$qlen cap=$cap (exit 75) json=$j"
    $msg; exit 75
  }
  if ($DryRun) { $qlen++ }   # a dry run holds no ticket: show the position it would get
  $isHead = ((-not $lockFail) -and $ahead -eq 0)
  $freeGB = $null; $cpu = $null; $c = $null; $limit = $null; $heavyGB = $null
  $why = @(); $open = $false
  if ($isHead) {
    # ---- phase 2 (head only): CPU sample + admission under the state lock ----
    $cpu = Get-CpuPct
    $mtx = $null
    if (-not $DryRun) { $mtx = Enter-GateMutex 15000 }
    try {
      $c = Get-HeavyCounts
      $freeGB = Get-FreeGB
      $limit = Get-GateLimit $freeGB $Max
      $heavyGB = Get-HeavyRAMGB $c
      $state = Read-GateState
      $since = -1; if ($state.lastAdmit -gt 0) { $since = [math]::Round(((UtcTicks) - $state.lastAdmit) / 1e7, 1) }
      if (-not $DryRun -and $null -ne $mtx) {   # a priority call may have joined while we sampled the CPU
        $ahead2 = Get-QueueAhead @(Get-LiveTickets $true) $PID $prio
        if ($ahead2 -gt 0) { $why += 'QUEUE' }
      }
      $why += @(Get-ResourceWaitReasons $Kind $freeGB $cpu $since $prio)
      if (-not (Test-BudgetOk $heavyGB $budget)) { $why += 'BUDGET' }
      if (-not (Test-PoolOpen $c $Kind $freeGB $limit $prio)) { $why += 'POOL' }
      if (-not $DryRun -and $null -eq $mtx) { $why += 'LOCK' }
      $open = ($why.Count -eq 0)
      if ($open -and -not $DryRun) {
        $state.lastAdmit = UtcTicks
        $state.admits = @($state.admits) + @(@{ t = $state.lastAdmit; kind = $Kind; pid = $PID })
        Write-GateState $state
        Remove-GateTicket
        if ($Watchdog -ne 0) { Start-GateMonitor }
      }
    } finally { Exit-GateMutex $mtx }
  } else {
    # non-head callers only sleep: no CPU sampling, no process scan, no gate_status.json write
    $freeGB = Get-FreeGB
    if ($lockFail) { $why = @('LOCK') } else { $why = @('QUEUE') }
  }
  $whyS = ($why -join ',')
  $waited = [int]((Get-Date) - $t0).TotalSeconds
  if ($isHead) {
    $st = "kind=$Kind art=$($c.art) build=$($c.build) heavy=$($c.heavy) limit=$limit free=$freeGB GB cpu=$cpu% heavyRAM=$heavyGB GB why=$whyS"
    $occ = "occupants=[$($c.occ -join ' ')]"
    $sj = @{ kind = $Kind; free_gb = $freeGB; cpu_pct = $cpu; why = $whyS; art = $c.art; build = $c.build; heavy = $c.heavy; limit = $limit; waited_s = $waited;
             floor_gb = (Get-EffectiveFloor $Kind $prio); budget_gb = $budget; queue = $qlen; priority = $prio; cfg = $script:CfgSource }
  } else {
    $st = "kind=$Kind queue=$($ahead + 1)/$qlen free=$freeGB GB why=$whyS"
    $occ = "occupants=[]"
    $sj = @{ kind = $Kind; free_gb = $freeGB; why = $whyS; waited_s = $waited; queue = $qlen; pos = ($ahead + 1); priority = $prio }
  }
  if ($DryRun) {
    if ($open) { "DRYRUN gate open: $st"; exit 0 }
    "DRYRUN gate closed: $st $occ"; exit 1
  }
  if ($open) {
    $sj.state = 'open'; $j = Write-GateStatus $sj
    if ($waiting) { Write-GateLog "OPEN waited=${waited}s $st json=$j" }
    "gate open: $st waited=${waited}s"
    exit 0
  }
  $sj.state = 'wait'; $j = Write-GateStatus $sj -NoFile:(-not $isHead)
  if (-not $waiting) {
    $waiting = $true
    Write-GateLog "WAIT_START $st $occ json=$j"
    $lastShow = $waited; $lastWhy = $whyS
  }
  if (($waited - $lastShow) -ge 60 -or $whyS -ne $lastWhy) {
    Write-GateLog "WAIT waited=${waited}s $st $occ json=$j"
    $lastShow = $waited; $lastWhy = $whyS
  }
  if ($waited -ge $ShowAfterSec -and ($waited - $lastPrint) -ge 60) {
    "gate waiting ${waited}s: $st $occ"
    $lastPrint = $waited
  }
  if ($waited -ge $tmo) {
    $sj.state = 'timeout'; $j = Write-GateStatus $sj -NoFile:(-not $isHead)
    Write-GateLog "TIMEOUT waited=${waited}s $st json=$j"
    if ($isHead) { "gate timeout: $st waited=${waited}s $occ - NOT RUN, do not start the heavy job" }
    else { "gate timeout: timed out in queue (position $($ahead + 1)/$qlen, never reached the head) $st waited=${waited}s - NOT RUN, do not start the heavy job" }
    exit 1
  }
  if ($Watchdog -ne 0) { Invoke-WatchdogTick $freeGB }
  Write-CfgNotes
  $rest = $PollSec - ((Get-Date) - $cycle0).TotalSeconds
  if ($rest -gt 0) { Start-Sleep -Milliseconds ([int]($rest * 1000)) }
}
} finally {
  # ticket removal also covers timeout / exit paths; stale tickets of crashed callers are cleaned by the next caller
  if ($script:Ticket) {
    $m2 = Enter-GateMutex 3000
    try { Remove-GateTicket } finally { Exit-GateMutex $m2 }
  }
}
