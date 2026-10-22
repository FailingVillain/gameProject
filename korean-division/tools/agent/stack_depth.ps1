# stack_depth.ps1 - base-chain depth of stacked branches (PowerShell 5.1). ASCII-only source. Read-only, local git only.
#   stack_depth.ps1                    branches marked wip-ready in ops/QUEUE.md that exist as local branches
#   stack_depth.ps1 <branch> [...]     explicit branches
#   -Line merge/0.2   merge line the branches are going into (default merge/0.2)
#   -Max 3            warn when depth > Max
#   -Top <dir>        repo to inspect (default: the checkout this script lives in; used by the test with a temp repo)
# Depth = longest chain of OTHER unmerged branches (not yet in the merge line) whose tips are ancestors of the branch, +1.
# Unmerged predecessors = those branches (transitive), lowest first. Exit 0 = all within limit, 3 = some branch deeper than Max.
# Pool of possible bases = every local branch not merged into -Line, excluding main, merge/*, integration/*. PIPELINE stack rule.
[CmdletBinding(PositionalBinding = $true)]
param(
    [Parameter(Position = 0, ValueFromRemainingArguments = $true)][string[]]$Branch = @(),
    [string]$Line = 'merge/0.2',
    [int]$Max = 3,
    [string]$Top = ''
)
$ErrorActionPreference = 'Continue'
try { [Console]::OutputEncoding = [System.Text.Encoding]::UTF8 } catch { }
if ($Top -eq '') { $Top = Split-Path -Parent (Split-Path -Parent (Split-Path -Parent $PSScriptRoot)) }
$cd = (& git -C $Top rev-parse --git-common-dir 2>$null)
if (-not [System.IO.Path]::IsPathRooted($cd)) { $cd = Join-Path $Top $cd }
$Main = Split-Path -Parent (Resolve-Path $cd).Path

function Names($lines) { @($lines | ForEach-Object { ([string]$_).Trim() } | Where-Object { $_ }) }
$all = Names (& git -C $Main branch --format='%(refname:short)' 2>$null)
if ($all -notcontains $Line) { "stack_depth: merge line '$Line' not found"; exit 1 }
$inLine = Names (& git -C $Main branch --merged $Line --format='%(refname:short)' 2>$null)
$pool = @($all | Where-Object { $inLine -notcontains $_ -and $_ -ne 'main' -and $_ -notlike 'merge/*' -and $_ -notlike 'integration/*' })

if ($Branch.Count -eq 0) {
    $qf = Join-Path $Main 'ops\QUEUE.md'
    if (Test-Path $qf) {
        $rows = [System.IO.File]::ReadAllLines($qf, (New-Object System.Text.UTF8Encoding($false))) | Where-Object { $_ -match '^\|' -and $_ -match 'wip-ready' }
        foreach ($row in $rows) {
            foreach ($tok in [regex]::Matches($row, '[A-Za-z0-9_./-]+')) {
                if ($pool -contains $tok.Value -and $Branch -notcontains $tok.Value) { $Branch += $tok.Value }
            }
        }
    }
}
if ($Branch.Count -eq 0) { "stack_depth: no branches given and no wip-ready branch found in QUEUE.md"; exit 0 }

$tip = @{}
foreach ($b in (@($pool) + @($Branch))) { if (-not $tip.ContainsKey($b)) { $tip[$b] = [string](& git -C $Main rev-parse --verify "refs/heads/$b" 2>$null) } }
$predCache = @{}
function Get-Preds([string]$b) {
    if ($predCache.ContainsKey($b)) { return $predCache[$b] }
    $m = Names (& git -C $Main branch --merged "refs/heads/$b" --format='%(refname:short)' 2>$null)
    $seen = @{}; $u = @()
    foreach ($x in $m) {
        if ($pool -contains $x -and $x -ne $b -and $tip[$x] -ne $tip[$b] -and -not $seen.ContainsKey($tip[$x])) { $seen[$tip[$x]] = 1; $u += $x }
    }
    $predCache[$b] = $u
    return $u
}
$depthCache = @{}
function Get-Depth([string]$b) {
    if ($depthCache.ContainsKey($b)) { return $depthCache[$b] }
    $d = 1
    foreach ($p in (Get-Preds $b)) { $pd = (Get-Depth $p) + 1; if ($pd -gt $d) { $d = $pd } }
    $depthCache[$b] = $d
    return $d
}
$bad = 0
foreach ($b in $Branch) {
    if (-not $tip[$b]) { "[?] $b : branch not found"; continue }
    $d = Get-Depth $b
    $pr = @(Get-Preds $b | Sort-Object { Get-Depth $_ })
    $tag = 'OK'; if ($d -gt $Max) { $tag = 'WARN'; $bad++ }
    $list = '(none)'; if ($pr.Count -gt 0) { $list = $pr -join ' <- ' }
    "[{0}] {1} depth {2} (limit {3}) - not yet in {4}: {5}" -f $tag, $b, $d, $Max, $Line, $list
}
if ($bad -gt 0) { "stack_depth: $bad branch(es) over depth $Max - land or re-base the lower ones first."; exit 3 }
exit 0
