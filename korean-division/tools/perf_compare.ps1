# Compare two perf bench JSON files (KoreanDivision --stress bench out=...). Flags regressions > threshold.
#   powershell -NoProfile -ExecutionPolicy Bypass -File tools/perf_compare.ps1 baseline.json new.json [-Threshold 10]
# Compared per scene+K key (median over runs): meanMs p95Ms p99Ms findTargetMs fogMs separateMs. Higher = worse.
# Tiny absolute values (< -MinMs, default 0.05 ms) are not flagged (noise). Exit code 1 if any regression, 0 otherwise.
# Also compares end checksums per scene/K/seed (a pure refactor must keep them equal) and the 1-hour live-heap samples when both have them.
param([Parameter(Mandatory=$true)][string]$Baseline, [Parameter(Mandatory=$true)][string]$New, [double]$Threshold = 10, [double]$MinMs = 0.05)
$ErrorActionPreference = 'Stop'
$a = Get-Content -Raw -Encoding UTF8 $Baseline | ConvertFrom-Json
$b = Get-Content -Raw -Encoding UTF8 $New | ConvertFrom-Json
$metrics = 'meanMs','p95Ms','p99Ms','findTargetMs','fogMs','separateMs'
Write-Host ("baseline: {0} ({1}) | new: {2} ({3})" -f $a.commit, $a.label, $b.commit, $b.label)
$bad = 0
$bmap = @{}; foreach ($s in $b.summary) { $bmap[$s.key] = $s }
Write-Host ("{0,-8} {1,-13} {2,9} {3,9} {4,8}" -f 'key','metric','baseline','new','change')
foreach ($sa in $a.summary) {
  $sb = $bmap[$sa.key]
  if ($null -eq $sb) { Write-Host ("{0,-8} (missing in new)" -f $sa.key); continue }
  foreach ($m in $metrics) {
    $x = [double]$sa.$m; $y = [double]$sb.$m
    if ($x -le 0) { continue }
    $pct = ($y - $x) / $x * 100
    $flag = ''
    if ($pct -gt $Threshold -and ($y - $x) -ge $MinMs) { $flag = '  REGRESSION'; $bad++ }
    elseif ($pct -lt -$Threshold -and ($x - $y) -ge $MinMs) { $flag = '  improved' }
    Write-Host ("{0,-8} {1,-13} {2,9:N3} {3,9:N3} {4,7:N1}%{5}" -f $sa.key, $m, $x, $y, $pct, $flag)
  }
}
# checksums: same scene/K/seed must match (pure refactor / measurement-only change). Different tick counts (cap hit) are skipped.
$csBad = 0
$ck = @{}; foreach ($r in $a.runs) { $ck["$($r.scene)$($r.k)/$($r.seed)/$($r.ticks)"] = $r.checksum }
foreach ($r in $b.runs) {
  $key = "$($r.scene)$($r.k)/$($r.seed)/$($r.ticks)"
  if ($ck.ContainsKey($key) -and $ck[$key] -ne $r.checksum) { Write-Host "CHECKSUM DIFF $key : $($ck[$key]) vs $($r.checksum)"; $csBad++ }
}
foreach ($r in $b.runs) { if ($r.liveHeapMb -and $r.liveHeapMb.Count -gt 1) { $f = $r.liveHeapMb[0].mb; $l = $r.liveHeapMb[$r.liveHeapMb.Count-1].mb; Write-Host ("live heap {0}{1} seed {2}: {3:N1} -> {4:N1} MB ({5} samples)" -f $r.scene,$r.k,$r.seed,$f,$l,$r.liveHeapMb.Count) } }
Write-Host ("regressions: {0}  checksum diffs: {1}  (threshold {2}%)" -f $bad, $csBad, $Threshold)
if ($bad -gt 0 -or $csBad -gt 0) { exit 1 } else { exit 0 }
