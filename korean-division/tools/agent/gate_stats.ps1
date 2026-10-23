# Daily gate summary from ops/gate_wait.log (no LLM). ASCII-only source.
# Usage: gate_stats.ps1 [-Days 1] [-Log <path>] [-Since 'yyyy-MM-dd HH:mm:ss']
# Output: per call outcome counts (OPEN after waiting / TIMEOUT / OVERLOAD), total wait median/p90, and per wait reason
#   (QUEUE RAM EMERG CPU SPACING POOL BUDGET LOCK) the number of calls that spent time in it, and median/p90 seconds spent in it.
# Method: a call = one pid from WAIT_START to OPEN/TIMEOUT. Time between two consecutive log lines of the call (the 'waited=' values) is credited
#   to the reason set of the earlier line; a combined 'RAM,CPU' credits both reasons. Calls that were never delayed write no log lines.
param([double]$Days = 1, [string]$Log = '', [string]$Since = '')
if (-not $Log) { $Log = $env:KD_GATE_LOG }
if (-not $Log) { $Log = 'C:\gameProject\ops\gate_wait.log' }
if (-not (Test-Path -LiteralPath $Log)) { "gate_stats: log not found: $Log"; exit 2 }
$cut = (Get-Date).AddDays(-$Days)
if ($Since) { $cut = [DateTime]::Parse($Since) }

function Pct([double[]]$v, [double]$p) {
  if ($v.Count -eq 0) { return 0 }
  $s = @($v | Sort-Object); $i = [int][math]::Ceiling($p * $s.Count) - 1
  if ($i -lt 0) { $i = 0 }; return $s[$i]
}
function Med([double[]]$v) { if ($v.Count -eq 0) { return 0 }; $s = @($v | Sort-Object); $n = $s.Count; if ($n % 2) { return $s[($n - 1) / 2] }; return ($s[$n / 2 - 1] + $s[$n / 2]) / 2 }

$re = '^(\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}) pid=(\d+) (WAIT_START|WAIT|OPEN|TIMEOUT|OVERLOAD)\b'
$cur = @{}          # pid -> @{ lastW; lastWhy; per = @{reason=sec}; total }
$totals = @(); $perReason = @{}; $kindN = @{}
$n = @{ OPEN = 0; TIMEOUT = 0; OVERLOAD = 0 }
function Add-Interval($call, [int]$toW) {
  $d = $toW - $call.lastW
  if ($d -gt 0 -and $call.lastWhy) { foreach ($r in ($call.lastWhy -split ',')) { if ($r) { $call.per[$r] = [double]$call.per[$r] + $d } } }
}
function Close-Call($call, [int]$w, [string]$outcome) {
  Add-Interval $call $w
  $script:n[$outcome]++
  $script:totals += $w
  foreach ($r in @($call.per.Keys)) { if (-not $script:perReason.ContainsKey($r)) { $script:perReason[$r] = @() }; $script:perReason[$r] += $call.per[$r] }
}
foreach ($line in [System.IO.File]::ReadLines($Log)) {
  $m = [regex]::Match($line, $re)
  if (-not $m.Success) { continue }
  $ts = [DateTime]::ParseExact($m.Groups[1].Value, 'yyyy-MM-dd HH:mm:ss', [Globalization.CultureInfo]::InvariantCulture)
  if ($ts -lt $cut) { continue }
  $pid_ = $m.Groups[2].Value; $ev = $m.Groups[3].Value
  $w = 0; $mw = [regex]::Match($line, 'waited=(\d+)s'); if ($mw.Success) { $w = [int]$mw.Groups[1].Value }
  $why = ''; $my = [regex]::Match($line, ' why=([A-Z,]*)'); if ($my.Success) { $why = $my.Groups[1].Value }
  if ($ev -eq 'OVERLOAD') { $n.OVERLOAD++; continue }
  if ($ev -eq 'WAIT_START') { $cur[$pid_] = @{ lastW = 0; lastWhy = $why; per = @{} }; continue }
  if (-not $cur.ContainsKey($pid_)) { continue }
  $call = $cur[$pid_]
  if ($ev -eq 'WAIT') { Add-Interval $call $w; $call.lastW = $w; $call.lastWhy = $why }
  else { Close-Call $call $w $ev; $cur.Remove($pid_) }
}
"gate_stats: $Log since $($cut.ToString('yyyy-MM-dd HH:mm:ss'))"
"calls delayed: closed OPEN=$($n.OPEN) TIMEOUT=$($n.TIMEOUT)  still waiting/unfinished=$($cur.Count)  OVERLOAD(exit 75)=$($n.OVERLOAD)"
"total wait (s): median {0:N0}  p90 {1:N0}  max {2:N0}  (n={3})" -f (Med $totals), (Pct $totals 0.9), $(if ($totals.Count) { ($totals | Measure-Object -Maximum).Maximum } else { 0 }), $totals.Count
'{0,-9} {1,6} {2,10} {3,10}' -f 'reason', 'calls', 'median_s', 'p90_s'
foreach ($r in ($perReason.Keys | Sort-Object { -(@($perReason[$_]) | Measure-Object -Sum).Sum })) {
  $v = [double[]]@($perReason[$r])
  '{0,-9} {1,6} {2,10:N0} {3,10:N0}' -f $r, $v.Count, (Med $v), (Pct $v 0.9)
}
