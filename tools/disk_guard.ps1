<#
disk_guard.ps1 - READ-ONLY project folder size check (never deletes anything).
  -Fast        use cached per-folder sizes (no disk walk); first run without cache falls back to live scan
  -Json        print JSON only (format: ops/STORAGE.md)
  -Root        project root (default: main repo root derived from git)
  -WarnGB 80 / -FullGB 100   thresholds on the sum of the whole project folder
Status: OK (<warn) / WARN (>=warn) / FULL (>=full: stop new cards, report cleanup list)
Candidates: A = branch already merged into main AND worktree clean (safe to remove by hand);
            B = merged but dirty, orphan folder (not a worktree), or render/scratch output. Unmerged branches never listed.
#>
[CmdletBinding()]
param([switch]$Fast, [switch]$Json, [string]$Root, [double]$WarnGB = 80, [double]$FullGB = 100,
      [string]$CacheFile = (Join-Path $env:LOCALAPPDATA 'kd\disk_guard_cache.json'))
$ErrorActionPreference = 'Stop'
if (-not $Root) {
  $cd = (& git rev-parse --path-format=absolute --git-common-dir 2>$null)
  if ($LASTEXITCODE -ne 0 -or -not $cd) { $Root = 'C:/gameProject' } else { $Root = Split-Path $cd -Parent }
}
$Root = (Resolve-Path $Root).Path.TrimEnd('\')
$expand = @('kd-develop', 'gt-develop')   # parents whose children are separate worktrees

function Get-DirBytes([string]$p) {
  $o = & robocopy $p 'NULL' /L /S /XJ /NJH /NFL /NDL /NP /BYTES /R:0 /W:0 2>$null
  foreach ($l in $o) { if ($l -match '^\s*Bytes\s*:\s*(\d+)') { return [int64]$Matches[1] } }
  return [int64]0
}

# entries to measure
$entries = New-Object System.Collections.ArrayList
foreach ($c in Get-ChildItem $Root -Directory -Force) {
  if ($c.Name -eq '.git') { $entries.Add(@{ name = '.git'; path = $c.FullName }) | Out-Null; continue }
  if ($expand -contains $c.Name) {
    foreach ($w in Get-ChildItem $c.FullName -Directory -Force) { $entries.Add(@{ name = "$($c.Name)/$($w.Name)"; path = $w.FullName }) | Out-Null }
  } else { $entries.Add(@{ name = $c.Name; path = $c.FullName }) | Out-Null }
}

# sizes: live or cache
$cache = @{}
if ($Fast -and (Test-Path $CacheFile)) {
  try { $j = Get-Content $CacheFile -Raw | ConvertFrom-Json; foreach ($p in $j.sizes.PSObject.Properties) { $cache[$p.Name] = [int64]$p.Value }; $cacheAt = $j.generatedAt } catch { $cache = @{} }
}
$source = if ($cache.Count) { 'cache' } else { 'live' }
if ($source -eq 'live' -and $Fast -and -not $Json) { Write-Host 'no cache yet -> live scan (slow, one time)' }
$sizes = @{}
foreach ($e in $entries) {
  if ($source -eq 'cache' -and $cache.ContainsKey($e.name)) { $sizes[$e.name] = $cache[$e.name] }
  else { $sizes[$e.name] = Get-DirBytes $e.path }
}
if ($source -eq 'live') {
  $dir = Split-Path $CacheFile -Parent; if (-not (Test-Path $dir)) { New-Item -ItemType Directory $dir | Out-Null }
  @{ generatedAt = (Get-Date).ToString('s'); sizes = $sizes } | ConvertTo-Json | Set-Content $CacheFile -Encoding utf8
  $cacheAt = (Get-Date).ToString('s')
}

# worktree -> branch map, merged set
$wtBranch = @{}
$cur = $null
foreach ($l in (& git -C $Root worktree list --porcelain)) {
  if ($l -like 'worktree *') { $cur = ($l.Substring(9) -replace '/', '\').TrimEnd('\') }
  elseif ($l -like 'branch refs/heads/*' -and $cur) { $wtBranch[$cur.ToLower()] = $l.Substring(18) }
}
$merged = @{}
foreach ($b in (& git -C $Root branch --merged main --format='%(refname:short)')) { $merged[$b.Trim()] = $true }

$total = [int64]0; foreach ($v in $sizes.Values) { $total += $v }
$totalGB = [math]::Round($total / 1GB, 2)
$status = if ($totalGB -ge $FullGB) { 'FULL' } elseif ($totalGB -ge $WarnGB) { 'WARN' } else { 'OK' }

$rows = foreach ($e in $entries) {
  $br = $wtBranch[$e.path.TrimEnd('\').ToLower()]
  $isWt = [bool]$br -and ($e.name -like '*/*')
  [pscustomobject]@{ name = $e.name; path = $e.path; gb = [math]::Round($sizes[$e.name] / 1GB, 2); branch = $br; worktree = $isWt
                     merged = [bool]($br -and $merged.ContainsKey($br)); clean = $null; cls = '' }
}
$rows = @($rows | Sort-Object gb -Descending)
$top = @($rows | Select-Object -First 10)
# classify: only worktree/orphan child entries (never root repo, .git, docs, ops...)
foreach ($r in $rows) {
  $child = $r.name -like '*/*'
  $loose = $r.name -in @('render_out', 'scratch', 'scratch_ms', 'tmp_t38g') -or $r.name -like 'tmp_*'
  if ($r.worktree) {
    $st = & git -C $r.path status --porcelain 2>$null
    $r.clean = -not $st
    if ($r.merged -and $r.clean) { $r.cls = 'A' } elseif ($r.merged) { $r.cls = 'B' }
  } elseif ($child -or $loose) { $r.cls = 'B' }
}
$A = @($rows | Where-Object cls -eq 'A'); $B = @($rows | Where-Object cls -eq 'B')

if ($Json) {
  $slim = { param($x) $x | ForEach-Object { @{ name = $_.name; gb = $_.gb; branch = $_.branch; merged = $_.merged; clean = $_.clean } } }
  [ordered]@{ schema = 1; generatedAt = (Get-Date).ToString('s'); sizesFrom = $source; sizesAt = $cacheAt; root = $Root
              totalGB = $totalGB; warnGB = $WarnGB; fullGB = $FullGB; status = $status
              stopNewCards = ($status -eq 'FULL'); top10 = @(& $slim $top); candidatesA = @(& $slim $A); candidatesB = @(& $slim $B) } |
    ConvertTo-Json -Depth 5
  exit 0
}
Write-Host ("DISK {0}  total {1} GB  (warn {2} / full {3})  sizes: {4}{5}" -f $status, $totalGB, $WarnGB, $FullGB, $source, $(if ($source -eq 'cache') { " @ $cacheAt" } else { '' }))
if ($status -eq 'FULL') { Write-Host 'STOP: new card dispatch paused - report the cleanup list below to the user. (nothing is deleted automatically)' }
Write-Host '-- top 10 --'
$top | ForEach-Object { Write-Host ("{0,7:N2} GB  {1,-40} {2}{3}" -f $_.gb, $_.name, $_.branch, $(if ($_.cls) { "  [$($_.cls)]" } else { '' })) }
Write-Host ("-- A candidates (merged into main + clean): {0}, {1:N2} GB" -f $A.Count, (($A | Measure-Object gb -Sum).Sum))
$A | ForEach-Object { Write-Host ("{0,7:N2} GB  {1}  ({2})" -f $_.gb, $_.name, $_.branch) }
Write-Host ("-- B candidates (merged but dirty / orphan / scratch; check by hand): {0}, {1:N2} GB" -f $B.Count, (($B | Measure-Object gb -Sum).Sum))
$B | ForEach-Object { Write-Host ("{0,7:N2} GB  {1}  ({2})" -f $_.gb, $_.name, $_.branch) }
Write-Host 'Removal is manual: git worktree remove <path> ; git branch -d <branch>'
