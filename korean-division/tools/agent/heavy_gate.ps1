# Heavy-job gate v2 (separate pools). Replaces the lane/legacy modes.
# Usage: heavy_gate.ps1 [-Render | -Build] [-Priority] [-Max N] [-TimeoutMin 60] [-DryRun]
#   -Render  art pool (make_unit render children). Limit 2 by default (1 when free RAM < 6 GB); env KD_GATE_ART_LIMIT overrides.
#   -Build   non-art pool, may use the reserved build slot. Default (no switch) = non-art "heavy" (selftest/stress/measure).
#   Non-art pool (build + heavy) limit 3; heavy (no -Build) may fill at most 2 of them -> 1 slot is always reserved for builds.
#   Art may never take the last combined slot (combined limit 10, 12 when free RAM >= 6 GB; -Max N fixes it): art needs total < limit-1.
#   -Priority (merge-lock holder): non-art caps and combined limit +3, reservation ignored.
#   Only RUNNING heavy processes are counted (art = python make_unit, build = dotnet build, heavy = KoreanDivision exe/dll runs).
#   Processes that merely wait (heavy_gate.ps1 itself, rerender96 drivers) hold no slot; MSBuild node children add RAM only.
#   Free RAM < 3 GB closes the gate. Summed WorkingSet of heavy processes above 14 GB closes it (KD_GATE_BUDGET_GB / -BudgetGB).
#   Waits are logged to C:\gameProject\ops\gate_wait.log (KD_GATE_LOG overrides): start, every 60 s, open/timeout, with occupant kind:PID:name.
#   After 120 s of waiting the occupancy is printed to stdout every 60 s. This script NEVER kills any process.
#   -DryRun: evaluate once, print decision and occupants, no waiting, no log; KD_GATE_FAKEPROCS="pid|name|cmdline;..." fakes the process list.
param([int]$Max = 0, [int]$TimeoutMin = 60, [int]$TimeoutSec = 0, [int]$PollSec = 20, [int]$ShowAfterSec = 120,
      [switch]$Render, [switch]$Build, [switch]$Priority, [switch]$DryRun, [double]$BudgetGB = 0, [string]$Lane = '')

# art pool cap: default 2 while merge rounds run (user 2026-10-23, builds were waiting up to ~1085 s behind renders); KD_GATE_ART_LIMIT overrides (tests use 8).
$script:ArtLimit = 8
if ($env:KD_GATE_ART_LIMIT) { $script:ArtLimit = [int]$env:KD_GATE_ART_LIMIT } else { $script:ArtLimit = 2 }
$script:NonArtLimit = 3
$script:BuildReserve = 1

# Classify a process: 'art' | 'build' | 'heavy' | 'child' (RAM only, no slot) | $null.
function Get-HeavyKind([string]$Name, [string]$CmdLine) {
  if ($null -eq $CmdLine) { $CmdLine = '' }
  if ($CmdLine -match 'heavy_gate') { return $null }   # waiting gate scripts hold no slot
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
    $o = @()
    foreach ($e in ($env:KD_GATE_FAKEPROCS -split ';')) {
      if (-not $e.Trim()) { continue }
      $f = $e.Split('|')
      $cl = ''; if ($f.Count -gt 2) { $cl = $f[2] }
      $o += [pscustomobject]@{ ProcessId = [int]$f[0]; Name = $f[1]; CommandLine = $cl; WorkingSetSize = [double]0 }
    }
    return $o
  }
  return @(Get-CimInstance Win32_Process)
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
  if ($env:KD_GATE_BUDGET_GB) { return [double]$env:KD_GATE_BUDGET_GB }
  return 14
}
function Test-BudgetOk([double]$HeavyGB, [double]$BudgetGB) { return ($HeavyGB -le $BudgetGB) }

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

function Write-GateLog([string]$Msg) {
  $f = $env:KD_GATE_LOG; if (-not $f) { $f = 'C:\gameProject\ops\gate_wait.log' }
  try { Add-Content -Path $f -Value ('{0} pid={1} {2}' -f (Get-Date -Format 'yyyy-MM-dd HH:mm:ss'), $PID, $Msg) -ErrorAction Stop } catch {}
}

if ($MyInvocation.InvocationName -eq '.') { return }   # dot-sourced by tests: functions only

$Kind = 'heavy'
if ($Render -or $Lane -eq 'render') { $Kind = 'art' } elseif ($Build) { $Kind = 'build' }
$budget = Get-BudgetGB $BudgetGB
$tmo = $TimeoutMin * 60; if ($TimeoutSec -gt 0) { $tmo = $TimeoutSec }
$t0 = Get-Date
$lastShow = 0
$lastPrint = -99999
$waiting = $false

while ($true) {
  $c = Get-HeavyCounts
  $freeGB = Get-FreeGB
  $limit = Get-GateLimit $freeGB $Max
  $heavyGB = Get-HeavyRAMGB $c
  $open = (Test-BudgetOk $heavyGB $budget) -and (Test-PoolOpen $c $Kind $freeGB $limit ([bool]$Priority))
  $st = "kind=$Kind art=$($c.art) build=$($c.build) heavy=$($c.heavy) limit=$limit free=$freeGB GB heavyRAM=$heavyGB GB"
  $waited = [int]((Get-Date) - $t0).TotalSeconds
  if ($DryRun) {
    if ($open) { "DRYRUN gate open: $st"; exit 0 }
    "DRYRUN gate closed: $st occupants=[$($c.occ -join ' ')]"; exit 1
  }
  if ($open) {
    if ($waiting) { Write-GateLog "OPEN waited=${waited}s $st" }
    "gate open: $st waited=${waited}s"
    exit 0
  }
  if (-not $waiting) {
    $waiting = $true
    Write-GateLog "WAIT_START $st occupants=[$($c.occ -join ' ')]"
    $lastShow = $waited - 60
  }
  if (($waited - $lastShow) -ge 60) {
    Write-GateLog "WAIT waited=${waited}s $st occupants=[$($c.occ -join ' ')]"
    $lastShow = $waited
  }
  if ($waited -ge $ShowAfterSec -and ($waited - $lastPrint) -ge 60) {
    "gate waiting ${waited}s: $st occupants=[$($c.occ -join ' ')]"
    $lastPrint = $waited
  }
  if ($waited -ge $tmo) {
    Write-GateLog "TIMEOUT waited=${waited}s $st"
    "gate timeout: $st waited=${waited}s occupants=[$($c.occ -join ' ')] - proceed carefully"
    exit 1
  }
  Start-Sleep -Seconds $PollSec
}
