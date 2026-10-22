# preflight.ps1 - cross-check BEFORE dispatching a card / branch (PowerShell 5.1). ASCII-only source.
#   preflight.ps1 T38j            card id    (QUEUE row, MERGE_QUEUE done table, worktrees/branches, dirty trees, FV table)
#   preflight.ps1 kd/isai-restore branch name (same checks, text match)
# Exit 0 = clear to dispatch. Exit 1 = BLOCK with reasons (duplicate worktree/branch, already merged, uncommitted work,
# QUEUE state already assigned/running/wip-ready). WARN lines (FV table hits) never block. LESSONS 1, 24, 30.
#   -Top <dir>   repo top (default: main worktree found via git common dir, so ops/ is main's, not a stale branch copy)
[CmdletBinding(PositionalBinding = $true)]
param(
    [Parameter(Mandatory = $true, Position = 0)][string]$Id,
    [string]$Top = ''
)
$ErrorActionPreference = 'Stop'
try { [Console]::OutputEncoding = [System.Text.Encoding]::UTF8 } catch { }
if ($Top -eq '') {
    $Top = Split-Path -Parent (Split-Path -Parent (Split-Path -Parent $PSScriptRoot))
    $cd = (& git -C $Top rev-parse --git-common-dir 2>$null)
    if ($cd) {
        if (-not [System.IO.Path]::IsPathRooted($cd)) { $cd = Join-Path $Top $cd }
        $Top = Split-Path -Parent (Resolve-Path $cd).Path
    }
}
$Top = (Resolve-Path $Top).Path
$Ops = Join-Path $Top 'ops'
$utf8 = New-Object System.Text.UTF8Encoding($false)
function Read-Lines([string]$p) { if (Test-Path $p) { return [System.IO.File]::ReadAllLines($p, $utf8) } return @() }

$key = $Id.Trim().ToLower()
$short = $key -replace '^(kd|gt|tools|fix|wip|merge)/', ''
$isCard = ($key -match '^t\d+[a-z0-9]*$')
$block = @(); $warn = @(); $info = @()
# token match: alnum boundary on both sides (so t38j does not hit t38jj / xt38j)
$tokRe = '(?<![a-z0-9])' + [regex]::Escape($short) + '(?![a-z0-9])'

# 1. QUEUE.md state
$q = Read-Lines (Join-Path $Ops 'QUEUE.md')
$rows = @($q | Where-Object { $_ -match '^\|' -and (($isCard -and $_.ToLower() -match ('^\|\s*' + [regex]::Escape($key) + '\s*\|')) -or (-not $isCard -and $_.ToLower().Contains($key))) })
if ($rows.Count -eq 0) { $info += "QUEUE: no row for '$Id'" }
foreach ($row in $rows) {
    $cells = $row.Split('|')
    $state = ''
    if ($cells.Count -ge 3) { $state = $cells[$cells.Count - 2].Trim() }
    $info += ("QUEUE: {0}" -f $row.Trim())
    $sl = $state.ToLower()
    if ($sl -match '^(merged|closed)') { $block += "QUEUE says '$state' - already done" }
    elseif ($sl -match '^(assigned|running|wip-ready)') { $block += "QUEUE says '$state' - already dispatched" }
}

# 2. MERGE_QUEUE done table / rows
$mq = Read-Lines (Join-Path $Ops 'MERGE_QUEUE.md')
$hit = @($mq | Where-Object { $_ -match '^\|' -and $_.ToLower() -match $tokRe })
foreach ($h in $hit) {
    $info += ("MERGE_QUEUE: {0}" -f $h.Trim())
    $block += "MERGE_QUEUE has a row for '$Id' (merged or queued): $($h.Trim())"
}

# 3. worktrees + branches
$wt = @(& git -C $Top worktree list --porcelain 2>$null)
$trees = @(); $cur = $null
foreach ($l in $wt) {
    if ($l -match '^worktree (.+)$') { $cur = @{ Path = $Matches[1]; Branch = '' }; $trees += $cur }
    elseif ($l -match '^branch refs/heads/(.+)$' -and $cur) { $cur.Branch = $Matches[1] }
}
$matchTrees = @($trees | Where-Object { ($_.Path.ToLower() -match $tokRe) -or ($_.Branch.ToLower() -match $tokRe) })
foreach ($t in $matchTrees) { $block += ("worktree exists: {0} [{1}]" -f $t.Path, $t.Branch) }
$br = @(& git -C $Top for-each-ref --format='%(refname:short)' refs/heads 2>$null | Where-Object { $_.ToLower() -match $tokRe })
foreach ($b in $br) {
    $inTree = @($matchTrees | Where-Object { $_.Branch -eq $b }).Count
    if ($inTree -eq 0) { $block += "branch exists: $b" }
}

# 4. uncommitted changes in matched worktrees (+ other worktrees whose name hits)
foreach ($t in $matchTrees) {
    if (-not (Test-Path $t.Path)) { $warn += "worktree path missing: $($t.Path)"; continue }
    $st = @(& git -C $t.Path status --porcelain 2>$null)
    if ($st.Count -gt 0) { $block += ("uncommitted changes in {0}: {1} file(s), e.g. {2}" -f $t.Path, $st.Count, ($st[0].Trim())) }
}

# 5. FV reservations
$fv = Read-Lines (Join-Path $Ops 'FV_RESERVATIONS.md')
foreach ($l in $fv) { if ($l -match '^\|' -and $l.ToLower() -match $tokRe) { $warn += ("FV_RESERVATIONS: {0}" -f $l.Trim()) } }
$nums = @{}
foreach ($l in $fv) {
    if ($l -match '^\|\s*([^|]+?)\s*\|\s*(\d+)\s*(?:[^|]*)\|') {
        $n = $Matches[2]; if (-not $nums.ContainsKey($n)) { $nums[$n] = @() }; $nums[$n] += $Matches[1]
    }
}
foreach ($k in $nums.Keys) { if ($nums[$k].Count -gt 1 -and (($nums[$k] -join ' ').ToLower() -match $tokRe)) { $warn += ("FV {0} reserved by several rows: {1} (renumber at merge time)" -f $k, ($nums[$k] -join ' / ')) } }

foreach ($i in $info) { Write-Host ("  info  {0}" -f $i) }
foreach ($w in $warn) { Write-Host ("  WARN  {0}" -f $w) }
foreach ($b in $block) { Write-Host ("  BLOCK {0}" -f $b) }
if ($block.Count -gt 0) { Write-Host ("PREFLIGHT BLOCK {0}: {1} problem(s)" -f $Id, $block.Count); exit 1 }
Write-Host ("PREFLIGHT OK {0} ({1} warning(s))" -f $Id, $warn.Count)
exit 0
