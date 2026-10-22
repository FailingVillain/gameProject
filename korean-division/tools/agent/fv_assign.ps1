# fv_assign.ps1 - merge line: give final FormatVersion / Replay numbers from the branches' fv_claims (PowerShell 5.1).
# ASCII-only source on purpose. Audit C-1. Uses fv_renumber.ps1 (-Set) to edit the constants.
#
#   fv_assign.ps1 -Report [-ClaimsDir <dir>]                     scan every claim file (all branches via git, or a dir) - replaces
#                                                                hand-maintained ops/FV_RESERVATIONS.md
#   fv_assign.ps1 -Order tcb,tcc,m1,g3 [-BaseRef HEAD]           PLAN: numbers in merge order, starting from BaseRef's FV/Replay
#   fv_assign.ps1 -Branch tcb [-BaseRef HEAD] -Apply [-NoBuild] [-RegenGolden]
#                                                                set this checkout's constants to base + that branch's claims
#                                                                (no claim = base values, i.e. FV unchanged), via fv_renumber -Set
# Base numbers = the constants in <BaseRef> read with `git show`, i.e. the merge line's tip BEFORE this branch (valid while a
# `git merge --no-commit` is in progress). If you already committed the merge, use -BaseRef HEAD^1.
# Claims of a branch are read from `git show <branch>:ops/fv_claims/<branch>.txt`; -ClaimsDir <dir> reads files from a dir
# instead (also for tests). Final number = base + sum of the branch's FV (Replay) claims; sequential in -Order.
# Schema claims are only reported (the campaign schema is edited by hand).
[CmdletBinding(PositionalBinding = $false)]
param(
    [switch]$Report,
    [string[]]$Order = @(),
    [string]$Branch = '',
    [string]$BaseRef = 'HEAD',
    [switch]$Apply,
    [switch]$NoBuild,
    [switch]$RegenGolden,
    [string]$ClaimsDir = '',
    [string]$Root = ''
)
$ErrorActionPreference = 'Continue'   # native git probes write to stderr; failures are checked explicitly
try { [Console]::OutputEncoding = [System.Text.Encoding]::UTF8 } catch { }
$utf8 = New-Object System.Text.UTF8Encoding($false)
if ($Root -eq '') { $Root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot) }
$Root = (Resolve-Path $Root).Path
$Top = (& git -C $Root rev-parse --show-toplevel).Trim()
$Order = @($Order | ForEach-Object { $_ -split ',' } | Where-Object { $_ -ne '' } | ForEach-Object { $_.Trim() })
$snapRel = 'src/Core/Snapshot/Snapshot.cs'; $repRel = 'src/Core/Commands/Replay.cs'
$rootPrefix = (& git -C $Root rev-parse --show-prefix).Trim()

function Safe([string]$b) { return ($b -replace '[\\/]', '__') }
function Parse-Claims([string[]]$lines) {
    $t = @{ Fv = 0; Replay = 0; Schema = 0; N = 0; Reasons = @() }
    foreach ($l in $lines) {
        if ($l -match '^\s*(#|$)') { continue }
        $f = [regex]::Match($l, 'FV\+(\d+)'); $r = [regex]::Match($l, 'Replay\+(\d+)'); $s = [regex]::Match($l, 'Schema\+(\d+)')
        if (-not $f.Success -or -not $r.Success) { continue }
        $t.Fv += [int]$f.Groups[1].Value; $t.Replay += [int]$r.Groups[1].Value
        if ($s.Success) { $t.Schema += [int]$s.Groups[1].Value }
        $t.N++
        $parts = $l.Split('|'); $t.Reasons += $parts[$parts.Count - 1].Trim()
    }
    return $t
}
function Get-BranchClaims([string]$b) {
    $name = (Safe $b) + '.txt'
    if ($ClaimsDir -ne '') {
        $p = Join-Path $ClaimsDir $name
        if (Test-Path $p) { return Parse-Claims ([System.IO.File]::ReadAllLines($p, $utf8)) }
        return Parse-Claims @()
    }
    $txt = & git -C $Top show ("{0}:ops/fv_claims/{1}" -f $b, $name) 2>$null
    if ($LASTEXITCODE -ne 0 -or $null -eq $txt) {
        $p = Join-Path $Top ('ops/fv_claims/' + $name)   # fallback: working tree
        if ((Test-Path $p) -and ($b -eq (& git -C $Top rev-parse --abbrev-ref HEAD).Trim())) { return Parse-Claims ([System.IO.File]::ReadAllLines($p, $utf8)) }
        return Parse-Claims @()
    }
    return Parse-Claims @($txt)
}
function Get-RefConst([string]$ref, [string]$relPath) {
    $txt = (& git -C $Root show ("{0}:{1}{2}" -f $ref, $rootPrefix, $relPath) 2>$null) -join "`n"
    $m = [regex]::Matches($txt, '(?m)^\s*public const int FormatVersion = (\d+);')
    if ($m.Count -ne 1) { throw "cannot read FormatVersion from ${ref}:$relPath (found $($m.Count))" }
    return [int]$m[0].Groups[1].Value
}

if ($Report) {
    $names = @()
    if ($ClaimsDir -ne '') {
        if (Test-Path $ClaimsDir) { $names = @(Get-ChildItem $ClaimsDir -Filter *.txt | ForEach-Object { $_.BaseName -replace '__', '/' }) }
    } else {
        foreach ($ref in @(& git -C $Top for-each-ref --format='%(refname:short)' refs/heads)) {
            $ls = & git -C $Top ls-tree --name-only $ref 'ops/fv_claims/' 2>$null
            if ($ls) { $names += $ref }
        }
    }
    Write-Host 'fv_claims scan (claims are per branch; final numbers come from fv_assign in merge order)'
    Write-Host ('  {0,-34} {1,-6} {2,-8} {3,-7} {4}' -f 'branch', 'FV', 'Replay', 'Schema', 'reasons')
    $tot = 0
    foreach ($b in $names) {
        $c = Get-BranchClaims $b
        if ($c.N -eq 0) { continue }
        $tot++
        Write-Host ('  {0,-34} +{1,-5} +{2,-7} +{3,-6} {4}' -f $b, $c.Fv, $c.Replay, $c.Schema, ($c.Reasons -join ' / '))
    }
    Write-Host "  $tot branch(es) with claims; all others = FV unchanged"
    exit 0
}

$baseFv = Get-RefConst $BaseRef $snapRel
$baseRp = Get-RefConst $BaseRef $repRel

if ($Order.Count -gt 0) {
    Write-Host "plan: base $BaseRef = FV $baseFv / Replay $baseRp ; merge order as given"
    $fv = $baseFv; $rp = $baseRp
    Write-Host ('  {0,-3} {1,-34} {2,-14} {3}' -f '#', 'branch', 'claim', 'final')
    $i = 0
    foreach ($b in $Order) {
        $i++
        $c = Get-BranchClaims $b
        $fv += $c.Fv; $rp += $c.Replay
        $cl = if ($c.N -eq 0) { 'none (unchanged)' } else { "FV+$($c.Fv) R+$($c.Replay)" }
        $sc = if ($c.Schema -gt 0) { "  (schema +$($c.Schema): edit by hand)" } else { '' }
        Write-Host ('  {0,-3} {1,-34} {2,-14} FV {3} / Replay {4}{5}' -f $i, $b, $cl, $fv, $rp, $sc)
    }
    Write-Host "  final tip: FV $fv / Replay $rp"
    exit 0
}

if ($Branch -ne '') {
    $c = Get-BranchClaims $Branch
    $newFv = $baseFv + $c.Fv; $newRp = $baseRp + $c.Replay
    Write-Host "assign: $Branch  base $BaseRef = FV $baseFv / Replay $baseRp ; claims FV+$($c.Fv) Replay+$($c.Replay) -> FV $newFv / Replay $newRp"
    if ($c.Schema -gt 0) { Write-Host "  NOTE: branch claims Schema+$($c.Schema) - campaign schema is not edited by this tool" }
    if (-not $Apply) { Write-Host '  (dry: add -Apply to edit the constants)'; exit 0 }
    $fr = Join-Path $PSScriptRoot 'fv_renumber.ps1'
    $a = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $fr, '-Root', $Root, '-Set', "$newFv", "$newRp")
    if ($NoBuild) { $a += '-NoBuild' }
    if ($RegenGolden) { $a += '-RegenGolden' }
    & powershell @a
    exit $LASTEXITCODE
}
Write-Host 'usage: fv_assign.ps1 -Report | -Order a,b,c [-BaseRef HEAD] | -Branch X -Apply [-BaseRef HEAD] [-NoBuild] [-RegenGolden]'
exit 1
