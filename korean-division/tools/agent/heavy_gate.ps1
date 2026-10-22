# Heavy-job gate. DEFAULT = legacy single pool (lanes OFF). Lanes apply only with explicit -Lane or env KD_GATE_LANES=1.
# Usage: heavy_gate.ps1 [-Lane validate|measure|render] [-Render] [-Priority] [-Max 8] [-TimeoutMin 60]
#  (lane mode) Guaranteed limits: validate 3 / measure 3 / render 2 (total Max=8). A lane below its limit passes if total < Max.
#  A lane at its limit may borrow free slots (total < Max) unless another lane that is below its limit is waiting.
#  -Render == -Lane render. -Priority (merge-lock holder): limits and Max +3.
param([int]$Max = 8, [int]$TimeoutMin = 60, [string]$Lane = 'validate', [switch]$Render, [switch]$Priority)

$script:LaneLimits = @{ validate = 3; measure = 3; render = 2 }

# Classify a process (name + command line) into a lane, or $null if not heavy.
function Get-HeavyLane([string]$Name, [string]$CmdLine) {
  if ($null -eq $CmdLine) { $CmdLine = '' }
  if ($Name -eq 'KoreanDivision.exe') {
    if ($CmdLine -match '[\\]dist[\\]') { return $null }
    if ($CmdLine -match '--(tactics-obs|collide|balance|stress|bunch)') { return 'measure' }
    return 'validate'
  }
  if ($Name -eq 'dotnet.exe') {
    if ($CmdLine -match 'KoreanDivision\.dll' -and $CmdLine -match '--(tactics-obs|collide|balance|stress|bunch)') { return 'measure' }
    if ($CmdLine -match 'build|KoreanDivision\.dll|MSBuild\.dll') { return 'validate' }
    return $null
  }
  # Only the actual render child (make_unit) is heavy; the rerender96 driver just waits on it. Counting both double-counted every lane (2026-10-22: 4 lanes = 8 heavy -> gate closed for all builds).
  if ($Name -eq 'python.exe' -and $CmdLine -match 'make_unit') { return 'render' }
  return $null
}

# counts: hashtable lane->running count; waiting: lanes (other than own) that have a waiter.
function Test-GateOpen([hashtable]$Counts, [string]$Lane, [int]$Max, [bool]$Priority, [string[]]$WaitingLanes = @()) {
  $bonus = 0; if ($Priority) { $bonus = 3 }
  $total = 0; foreach ($l in $script:LaneLimits.Keys) { $total += [int]$Counts[$l] }
  if ($total -ge ($Max + $bonus)) { return $false }
  if ([int]$Counts[$Lane] -lt ($script:LaneLimits[$Lane] + $bonus)) { return $true }
  # at own limit: borrowing, only if no under-limit lane is waiting for its slot
  if ($Priority) { return $true }
  foreach ($w in $WaitingLanes) {
    if ($w -ne $Lane -and [int]$Counts[$w] -lt $script:LaneLimits[$w]) { return $false }
  }
  return $true
}

function Get-HeavyCounts {
  $c = @{ validate = 0; measure = 0; render = 0 }
  foreach ($p in @(Get-CimInstance Win32_Process)) {
    $l = Get-HeavyLane $p.Name $p.CommandLine
    if ($l) { $c[$l]++ }
  }
  return $c
}

if ($MyInvocation.InvocationName -eq '.') { return }   # dot-sourced by tests: functions only

$laneMode = $PSBoundParameters.ContainsKey('Lane') -or ($env:KD_GATE_LANES -eq '1')
if ($Render) { $Lane = 'render' }
if (-not $laneMode) {
  # legacy single pool: render sees Max, others Max-1, priority +3
  if (-not $Render) { $Max = $Max - 1 }
  if ($Priority) { $Max = $Max + 3 }
  $deadline0 = (Get-Date).AddMinutes($TimeoutMin)
  while ($true) {
    $c0 = Get-HeavyCounts
    $n = $c0.validate + $c0.measure + $c0.render
    if ($n -lt $Max) { "gate open: heavy=$n"; exit 0 }
    if ((Get-Date) -gt $deadline0) { "gate timeout: heavy=$n - proceed carefully"; exit 1 }
    Start-Sleep -Seconds 20
  }
}
if (-not $script:LaneLimits.ContainsKey($Lane)) { "gate: unknown lane '$Lane' (validate|measure|render)"; exit 2 }
$dir = Join-Path $env:TEMP 'kd-gate'
New-Item -ItemType Directory -Force $dir | Out-Null
$marker = Join-Path $dir "$Lane.$PID"
$deadline = (Get-Date).AddMinutes($TimeoutMin)
try {
  while ($true) {
    $c = Get-HeavyCounts
    # live waiters of other lanes (stale markers >2h or dead pids are ignored)
    $waiting = @()
    foreach ($f in @(Get-ChildItem $dir -ErrorAction SilentlyContinue)) {
      $parts = $f.Name.Split('.')
      if ($parts.Count -eq 2 -and $parts[0] -ne $Lane -and (Get-Process -Id ([int]$parts[1]) -ErrorAction SilentlyContinue)) { $waiting += $parts[0] }
    }
    if (Test-GateOpen $c $Lane $Max ([bool]$Priority) $waiting) {
      "gate open: lane=$Lane heavy=$($c.validate + $c.measure + $c.render) (validate=$($c.validate) measure=$($c.measure) render=$($c.render))"
      exit 0
    }
    Set-Content $marker '' -ErrorAction SilentlyContinue
    if ((Get-Date) -gt $deadline) { "gate timeout: lane=$Lane validate=$($c.validate) measure=$($c.measure) render=$($c.render) - proceed carefully"; exit 1 }
    Start-Sleep -Seconds 20
  }
} finally { Remove-Item $marker -ErrorAction SilentlyContinue }
