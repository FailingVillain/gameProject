# fv_renumber.ps1 - FormatVersion / Replay version helper for merging stacked branches (PowerShell 5.1, no admin).
# Source file is ASCII-only on purpose (5.1 reads BOM-less files as ANSI).
#
#   -Report                       print this checkout's FormatVersion (Snapshot.cs) and Replay version (Replay.cs),
#                                 the FV/Replay table of ops/FV_RESERVATIONS.md, and flag duplicates / mismatches.
#   -Set <fv> <replay>            edit both constants (exact-match checked: each constant must occur exactly once;
#                                 a leading "// <old>:" comment is renumbered too), then heavy_gate + build + --detcheck.
#                                 Forms: -Set 58 23   or   -Set 58,23
#     -RegenGolden                if detcheck reports a golden mismatch and the new output has the expected shape
#                                 (two 16-hex checksums), run --detcheck --update and print old/new; the golden file must
#                                 be the only file the update touches. Never regenerates silently on any other output.
#     -NoBuild                    only edit the constants (skip gate/build/detcheck)
#   -Plan                         read the table and print the proposed merge order + final FV/Replay per branch.
#
# Optional: -Table <path> (default <repo top>/ops/FV_RESERVATIONS.md), -Root <korean-division dir> (default: script's repo).
# Table rules: row order = merge order. Cells starting with a number count as that number; "keep"-style rows (cell has the
# word after the number, e.g. "59 <keep>") inherit the previous assigned value; cells with no leading number ("-", "TBD")
# are skipped. Free-text reservations below the table are NOT parsed (listed as a reminder only).
param(
    [switch]$Report,
    [switch]$Plan,
    [int[]]$Set,
    [Parameter(ValueFromRemainingArguments = $true)][string[]]$Rest,
    [switch]$RegenGolden,
    [switch]$NoBuild,
    [string]$Table = '',
    [string]$Root = ''
)
$ErrorActionPreference = 'Stop'
try { [Console]::OutputEncoding = [System.Text.Encoding]::UTF8 } catch { }
if ($Root -eq '') { $Root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot) }
$Root = (Resolve-Path $Root).Path
$SnapFile = Join-Path $Root 'src/Core/Snapshot/Snapshot.cs'
$RepFile = Join-Path $Root 'src/Core/Commands/Replay.cs'
$GoldenFile = Join-Path $Root 'assets/data/detcheck.golden'
$utf8 = New-Object System.Text.UTF8Encoding($false)

function Read-Text([string]$p) { return [System.IO.File]::ReadAllText($p, $utf8) }
function Write-Text([string]$p, [string]$t) { [System.IO.File]::WriteAllText($p, $t, $utf8) }

# FV: "public const int FormatVersion = N;" (Snapshot.cs has one; Replay.cs has one too, in another class)
$KeepOrOpen = '^(\uC720\uC9C0|\uC774\uD6C4)'   # Korean 'keep' / 'and later' (kept as escapes: file stays ASCII)
$ConstRe = '(?m)^(?<pre>\s*public const int FormatVersion = )(?<n>\d+)(?<mid>;[ \t]*)(?<cmt>//[ \t]*)?'
function Get-Const([string]$path) {
    $t = Read-Text $path
    $m = [regex]::Matches($t, $ConstRe)
    if ($m.Count -ne 1) { throw "$path : expected exactly 1 'public const int FormatVersion' line, found $($m.Count)" }
    return [int]$m[0].Groups['n'].Value
}
function Set-Const([string]$path, [int]$old, [int]$new) {
    $t = Read-Text $path
    $m = [regex]::Matches($t, $ConstRe)
    if ($m.Count -ne 1) { throw "$path : expected exactly 1 FormatVersion line, found $($m.Count)" }
    if ([int]$m[0].Groups['n'].Value -ne $old) { throw "$path : value changed under us (expected $old)" }
    $g = $m[0]
    $line = $g.Value
    $newLine = $g.Groups['pre'].Value + $new + $g.Groups['mid'].Value + $g.Groups['cmt'].Value
    # version comment: "// <old>: ..." -> "// <new>: ..." (only when it starts exactly with the old number)
    $restStart = $g.Index + $g.Length
    $tail = $t.Substring($restStart)
    $cm = [regex]::Match($tail, '^' + $old + ':')
    if ($g.Groups['cmt'].Success -and $cm.Success) { $tail = '' + $new + ':' + $tail.Substring($cm.Length) }
    Write-Text $path ($t.Substring(0, $g.Index) + $newLine + $tail)
}

function Get-Table {
    $tp = $Table
    if ($tp -eq '') {
        $top = (& git -C $Root rev-parse --show-toplevel).Trim()
        $tp = Join-Path $top 'ops/FV_RESERVATIONS.md'
    }
    if (-not (Test-Path $tp)) { throw "table not found: $tp" }
    $rows = @()
    $order = 0
    foreach ($ln in ([System.IO.File]::ReadAllLines($tp, $utf8))) {
        if ($ln -notmatch '^\s*\|') { continue }
        $c = @($ln.Trim().Trim('|').Split('|') | ForEach-Object { $_.Trim() })
        if ($c.Count -lt 4) { continue }
        if ($c[0] -match '^-+$' -or $c[0] -match '^:?-{3,}') { continue }
        if ($c[1] -match '^-{3,}' ) { continue }
        if ($c[1] -ceq 'FV') { continue }   # header
        $order++
        $fvM = [regex]::Match($c[1], '^\s*(\d+)(.*)$')
        $rpM = [regex]::Match($c[2], '^\s*(\d+)(.*)$')
        $rows += [pscustomobject]@{
            Order = $order; Branch = $c[0]; FvCell = $c[1]; RpCell = $c[2]; Base = $c[3]
            Fv = $(if ($fvM.Success) { [int]$fvM.Groups[1].Value } else { $null })
            Rp = $(if ($rpM.Success) { [int]$rpM.Groups[1].Value } else { $null })
            FvNote = $(if ($fvM.Success) { $fvM.Groups[2].Value.Trim() } else { $c[1] })
            RpNote = $(if ($rpM.Success) { $rpM.Groups[2].Value.Trim() } else { $c[2] })
        }
    }
    return [pscustomobject]@{ Path = $tp; Rows = $rows }
}

function Show-Report {
    $fv = Get-Const $SnapFile
    $rp = Get-Const $RepFile
    $br = (& git -C $Root branch --show-current).Trim()
    Write-Host "checkout: $Root  (branch $br)"
    Write-Host "  Snapshot.FormatVersion = $fv"
    Write-Host "  ReplayFile.FormatVersion = $rp"
    $tb = Get-Table
    Write-Host "table: $($tb.Path)"
    Write-Host ('  {0,-3} {1,-34} {2,-14} {3,-10} {4}' -f '#', 'branch', 'FV', 'Replay', 'base')
    foreach ($r in $tb.Rows) {
        $f = $r.FvCell; if ($f.Length -gt 14) { $f = $f.Substring(0, 13) + '~' }
        Write-Host ('  {0,-3} {1,-34} {2,-14} {3,-10} {4}' -f $r.Order, $r.Branch, $f, $r.RpCell, $r.Base)
    }
    $flags = 0
    foreach ($kind in 'Fv', 'Rp') {
        $name = if ($kind -eq 'Fv') { 'FV' } else { 'Replay' }
        # rows marked keep / open-ended ("<n> keep", "<n> and later") are not new reservations
        $firm = @($tb.Rows | Where-Object { $null -ne $_.$kind -and (($(if ($kind -eq 'Fv') { $_.FvNote } else { $_.RpNote })) -notmatch $KeepOrOpen) })
        foreach ($grp in ($firm | Group-Object -Property $kind | Where-Object { $_.Count -gt 1 })) {
            $names = ($grp.Group | ForEach-Object { $_.Branch }) -join ', '
            $stk = ''
            if ($grp.Count -eq 2 -and $grp.Group[1].Base -ne '' -and $grp.Group[0].Branch -match [regex]::Escape($grp.Group[1].Base)) { $stk = ' (second is stacked on first: fine only if it does not bump)' }
            Write-Host "  CONFLICT: $name $($grp.Name) reserved by: $names$stk"
            $flags++
        }
    }
    foreach ($r in $tb.Rows) {
        if ($null -ne $r.Fv -and $r.FvNote -ne '') { Write-Host "  NOTE: $($r.Branch) FV cell '$($r.FvCell)' is provisional/annotated"; $flags++ }
    }
    # the checkout vs. table
    $hit = @($tb.Rows | Where-Object { $_.Branch -match [regex]::Escape($br) -and $br -ne '' -and $br -ne 'main' })
    foreach ($h in $hit) {
        if ($null -ne $h.Fv -and $h.Fv -ne $fv) { Write-Host "  MISMATCH: $($h.Branch) table FV $($h.Fv) != checkout FV $fv"; $flags++ }
        if ($null -ne $h.Rp -and $h.Rp -ne $rp) { Write-Host "  MISMATCH: $($h.Branch) table Replay $($h.Rp) != checkout Replay $rp"; $flags++ }
    }
    $latest = @($tb.Rows | Where-Object { $null -ne $_.Fv } | ForEach-Object { $_.Fv } | Sort-Object)
    if ($latest.Count -gt 0) {
        $gaps = @(); for ($i = $latest[0]; $i -lt $latest[-1]; $i++) { if ($latest -notcontains $i) { $gaps += $i } }
        if ($gaps.Count -gt 0) { Write-Host "  GAP: FV numbers not used in the table between $($latest[0]) and $($latest[-1]): $($gaps -join ',')" }
    }
    Write-Host '  (free-text reservations below the table are not parsed - check them by eye)'
    if ($flags -eq 0) { Write-Host '  report: no conflicts flagged' } else { Write-Host "  report: $flags item(s) flagged" }
}

function Show-Plan {
    $fv = Get-Const $SnapFile
    $rp = Get-Const $RepFile
    $tb = Get-Table
    $hdr = [regex]::Match((Read-Text $tb.Path), 'main = FV(\d+) / Replay(\d+)')
    if ($hdr.Success) { $baseFv = [int]$hdr.Groups[1].Value; $baseRp = [int]$hdr.Groups[2].Value; $src = 'table header (main)' }
    else { $baseFv = $fv; $baseRp = $rp; $src = 'this checkout' }
    Write-Host "plan: start from FV $baseFv / Replay $baseRp  [$src]; table row order = merge order"
    Write-Host ('  {0,-3} {1,-34} {2,-10} {3}' -f '#', 'branch', 'table', 'final')
    $curFv = $baseFv; $curRp = $baseRp
    foreach ($r in $tb.Rows) {
        if ($null -eq $r.Fv) { Write-Host ('  {0,-3} {1,-34} {2}' -f $r.Order, $r.Branch, "skipped (FV cell '$($r.FvCell)')"); continue }
        $tbl = "$($r.Fv)/$($r.Rp)"
        if ($r.FvNote -match '^\uC774\uD6C4') { Write-Host ('  {0,-3} {1,-34} {2,-10} {3}' -f $r.Order, $r.Branch, $tbl, 'open-ended (in progress): number assigned when it is ready'); continue }
        if ($r.FvNote -match '^\uC720\uC9C0') {
            Write-Host ('  {0,-3} {1,-34} {2,-10} FV {3} / Replay {4}  (keep: no new bump)' -f $r.Order, $r.Branch, $tbl, $curFv, $curRp); continue
        }
        $curFv = $curFv + 1
        $baseRow = @($tb.Rows | Where-Object { $r.Base -ne '' -and $_.Order -lt $r.Order -and $_.Branch -match [regex]::Escape($r.Base) } | Select-Object -First 1)
        $sameAsBase = ($baseRow.Count -eq 1 -and $null -ne $r.Rp -and $baseRow[0].Rp -eq $r.Rp)   # same Replay number as the branch it is stacked on = no new bump
        if ($sameAsBase) { $rpTxt = "$curRp (same as base, unchanged)" }
        elseif ($null -ne $r.Rp -and $r.RpNote -notmatch '^\uC774\uD6C4') { $curRp = $curRp + 1; $rpTxt = "$curRp" } else { $rpTxt = "$curRp (unchanged)" }
        $ch = if ($r.Fv -ne $curFv) { '  *renumber from ' + $r.Fv + '*' } else { '' }
        Write-Host ('  {0,-3} {1,-34} {2,-10} FV {3} / Replay {4}{5}' -f $r.Order, $r.Branch, $tbl, $curFv, $rpTxt, $ch)
    }
    Write-Host "  final tip: FV $curFv / Replay $curRp"
    Write-Host '  note: free-text reservations (below the table) are not included.'
}

function Invoke-Gate {
    $gate = Join-Path $PSScriptRoot 'heavy_gate.ps1'
    & powershell -NoProfile -ExecutionPolicy Bypass -File $gate
    if ($LASTEXITCODE -ne 0) { throw "heavy_gate failed ($LASTEXITCODE)" }
}

function Invoke-Set([int]$newFv, [int]$newRp) {
    $oldFv = Get-Const $SnapFile
    $oldRp = Get-Const $RepFile
    Write-Host "FormatVersion: $oldFv -> $newFv ; Replay: $oldRp -> $newRp"
    if ($oldFv -ne $newFv) { Set-Const $SnapFile $oldFv $newFv }
    if ($oldRp -ne $newRp) { Set-Const $RepFile $oldRp $newRp }
    if ((Get-Const $SnapFile) -ne $newFv -or (Get-Const $RepFile) -ne $newRp) { throw 'verification of edited constants failed' }
    Write-Host 'constants edited and re-read OK'
    if ($NoBuild) { return }

    Push-Location $Root
    try {
        Invoke-Gate
        $dn = 'C:\Program Files\dotnet\dotnet.exe'
        if (-not (Test-Path $dn)) { $dn = 'dotnet' }
        & $dn build -nologo -v q -p:IntermediateOutputPath=obj/fvr/ -p:OutputPath=bin/fvr/ 2>&1 | Select-Object -Last 5 | ForEach-Object { Write-Host "  build: $_" }
        if ($LASTEXITCODE -ne 0) { throw 'build failed' }
        $exe = Join-Path $Root 'bin/fvr/KoreanDivision.exe'
        Invoke-Gate
        $out = @(& $exe --detcheck 2>&1)
        $code = $LASTEXITCODE
        $out | ForEach-Object { Write-Host "  detcheck: $_" }
        if ($code -eq 0) { Write-Host 'detcheck: golden match'; return }
        $oldGolden = (Read-Text $GoldenFile).Trim()
        $line = @($out | Where-Object { "$_" -match '^DETCHECK ' })
        $shapeOk = ($code -eq 2 -and $line.Count -eq 1 -and "$($line[0])" -match '^DETCHECK [0-9a-f]{16} [0-9a-f]{16}\b' -and $oldGolden -match '^[0-9a-f]{16} [0-9a-f]{16}$')
        if (-not $RegenGolden) { Write-Host "detcheck: golden MISMATCH (exit $code); rerun with -RegenGolden if the checksum change is expected"; exit 2 }
        if (-not $shapeOk) { Write-Host "detcheck: unexpected output shape/exit ($code) - NOT regenerating"; exit 3 }
        Invoke-Gate
        $upd = @(& $exe --detcheck --update 2>&1)
        if ($LASTEXITCODE -ne 0) { throw 'detcheck --update failed' }
        $newGolden = (Read-Text $GoldenFile).Trim()
        $changed = @(& git status --porcelain --untracked-files=no | ForEach-Object { $_.Substring(3) })
        $extra = @($changed | Where-Object { $_ -notmatch 'Snapshot\.cs$|Replay\.cs$|detcheck\.golden$' })
        if ($extra.Count -gt 0) { throw "unexpected modified files after update: $($extra -join ', ')" }
        Write-Host "golden regenerated: old [$oldGolden] -> new [$newGolden]"
    } finally { Pop-Location }
}

if ($Report) { Show-Report; exit 0 }
if ($Plan) { Show-Plan; exit 0 }
if ($null -ne $Set -and $Set.Count -gt 0) {
    $vals = @($Set)
    foreach ($x in @($Rest)) { if ($x -match '^\d+$') { $vals += [int]$x } elseif ($x) { throw "unexpected argument: $x" } }
    if ($vals.Count -ne 2) { throw 'usage: -Set <fv> <replay>' }
    Invoke-Set $vals[0] $vals[1]
    exit 0
}
Write-Host 'usage: fv_renumber.ps1 -Report | -Plan | -Set <fv> <replay> [-RegenGolden] [-NoBuild]'
exit 1
