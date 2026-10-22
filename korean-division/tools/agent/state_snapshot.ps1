# state_snapshot.ps1 - measured facts for the STATE hand-off, as short markdown on stdout (PowerShell 5.1). ASCII-only source.
#   state_snapshot.ps1            print
#   state_snapshot.ps1 -Write     also overwrite ops/STATE_LIVE.md of THIS checkout (never main's, unless run from main)
# Contents: main hash, worktree summary, merge locks, per merge line (worktree/branch/HEAD/latest tag), FV/Replay, heavy processes.
# Read-only except -Write. Git calls are local only (no fetch/push).
[CmdletBinding(PositionalBinding = $false)]
param(
    [switch]$Write,
    [string]$Top = '',
    [string]$OutFile = ''
)
$ErrorActionPreference = 'Continue'
try { [Console]::OutputEncoding = [System.Text.Encoding]::UTF8 } catch { }
$here = Split-Path -Parent (Split-Path -Parent (Split-Path -Parent $PSScriptRoot))
if ($Top -eq '') { $Top = $here }
$Top = (Resolve-Path $Top).Path
$cd = (& git -C $Top rev-parse --git-common-dir 2>$null)
if (-not [System.IO.Path]::IsPathRooted($cd)) { $cd = Join-Path $Top $cd }
$GitDir = (Resolve-Path $cd).Path
$Main = Split-Path -Parent $GitDir
if ($OutFile -eq '') { $OutFile = Join-Path $Top 'ops/STATE_LIVE.md' }

$sb = New-Object System.Text.StringBuilder
function L([string]$s) { [void]$sb.AppendLine($s) }

L ("# STATE_LIVE - measured {0} (tools/agent/state_snapshot.ps1; do not hand-edit)" -f (Get-Date -Format 'yyyy-MM-dd HH:mm'))
$mainHash = (& git -C $Main log -1 --format='%h %s' main 2>$null)
L ""
L ("- main: ``{0}``" -f $mainHash)

# worktrees
$wt = @(& git -C $Main worktree list --porcelain 2>$null)
$trees = @(); $cur = $null
foreach ($l in $wt) {
    if ($l -match '^worktree (.+)$') { $cur = @{ Path = $Matches[1]; Branch = ''; Head = '' }; $trees += $cur }
    elseif ($l -match '^HEAD (\w+)$' -and $cur) { $cur.Head = $Matches[1].Substring(0, 7) }
    elseif ($l -match '^branch refs/heads/(.+)$' -and $cur) { $cur.Branch = $Matches[1] }
}
$groups = $trees | Group-Object { $p = $_.Path.Replace('\', '/'); if ($p -match '/([^/]+)-develop/') { $Matches[1] + '-develop' } else { '(top)' } }
$gtxt = ($groups | ForEach-Object { "{0}={1}" -f $_.Name, $_.Count }) -join ' '
L ("- worktrees: {0} total ({1})" -f $trees.Count, $gtxt)

# locks
$locks = @(Get-ChildItem -Path $GitDir -Filter 'kd-merge*.lock' -ErrorAction SilentlyContinue)
if ($locks.Count -eq 0) { L "- merge lock: none" }
else {
    foreach ($lk in $locks) {
        $age = ''; $sf = Join-Path $lk.FullName 'started.txt'
        $started = ''
        if (Test-Path $sf) { $started = (Get-Content $sf -TotalCount 1) }
        $mins = [int]((Get-Date) - $lk.LastWriteTime).TotalMinutes
        if ($started -ne '') { try { $mins = [int]((Get-Date) - [datetime]$started).TotalMinutes } catch { } }
        $exp = ''
        if ($mins -gt 30) { $exp = ' EXPIRED(>30m)' }
        L ("- merge lock: ``{0}`` held {1} min{2} (started: {3})" -f $lk.Name, $mins, $exp, $started)
    }
}

# merge lines = worktrees whose branch starts with merge/
$ml = @($trees | Where-Object { $_.Branch -like 'merge/*' })
if ($ml.Count -eq 0) { L "- merge lines: none" }
else {
    L "- merge lines:"
    foreach ($t in $ml) {
        $tag = (& git -C $t.Path tag --merged HEAD --sort=-creatordate 2>$null | Select-Object -First 1)
        $dirty = @(& git -C $t.Path status --porcelain 2>$null).Count
        L ("  - ``{0}`` at {1}: HEAD ``{2}`` latest tag ``{3}`` dirty files {4}" -f $t.Branch, (Split-Path -Leaf $t.Path), $t.Head, $tag, $dirty)
    }
}

# format versions of this checkout
$snap = Join-Path $Top 'korean-division/src/Core/Snapshot/Snapshot.cs'
$rep = Join-Path $Top 'korean-division/src/Core/Commands/Replay.cs'
$fvs = ''
if (Test-Path $snap) { $m = Select-String -Path $snap -Pattern 'public const int FormatVersion = (\d+)' | Select-Object -First 1; if ($m) { $fvs += "Snapshot FV " + $m.Matches[0].Groups[1].Value } }
if (Test-Path $rep) { $m = Select-String -Path $rep -Pattern 'public const int FormatVersion = (\d+)' | Select-Object -First 1; if ($m) { $fvs += " / Replay " + $m.Matches[0].Groups[1].Value } }
if ($fvs -ne '') { L ("- this checkout ({0}): {1}" -f (Split-Path -Leaf $Top), $fvs) }

# heavy processes (same classification as heavy_gate.ps1: validate / measure / render)
$counts = @{ validate = 0; measure = 0; render = 0 }
try {
    $procs = @(Get-CimInstance Win32_Process -ErrorAction Stop | Where-Object { $_.Name -in @('KoreanDivision.exe', 'dotnet.exe', 'python.exe') })
    foreach ($p in $procs) {
        $c = $p.CommandLine; if ($null -eq $c) { $c = '' }
        $lane = $null
        if ($p.Name -eq 'KoreanDivision.exe') {
            if ($c -notmatch '[\\]dist[\\]') { if ($c -match '--(tactics-obs|collide|balance|stress|bunch)') { $lane = 'measure' } else { $lane = 'validate' } }
        }
        elseif ($p.Name -eq 'dotnet.exe') {
            if ($c -match 'KoreanDivision\.dll' -and $c -match '--(tactics-obs|collide|balance|stress|bunch)') { $lane = 'measure' }
            elseif ($c -match 'build|KoreanDivision\.dll|MSBuild\.dll') { $lane = 'validate' }
        }
        elseif ($p.Name -eq 'python.exe' -and $c -match 'make_unit') { $lane = 'render' }
        if ($lane) { $counts[$lane]++ }
    }
    $tot = $counts.validate + $counts.measure + $counts.render
    L ("- heavy processes: {0} (validate {1} / measure {2} / render {3})" -f $tot, $counts.validate, $counts.measure, $counts.render)
} catch { L "- heavy processes: unknown ($($_.Exception.Message))" }

$text = $sb.ToString()
Write-Host $text
if ($Write) {
    $enc = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText($OutFile, $text, $enc)
    Write-Host ("(written: {0})" -f $OutFile)
}
