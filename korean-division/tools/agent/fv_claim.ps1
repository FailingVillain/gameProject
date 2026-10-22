# fv_claim.ps1 - a branch CLAIMS a save-format bump instead of fixing its own number (late binding; PowerShell 5.1).
# ASCII-only source on purpose. Audit PIPELINE_AUDIT_2026-10-22 C-1.
#
#   fv_claim.ps1 -Fv 1 -Replay 1 [-Schema 0] -Reason "new field X"     append one claim line for the current branch
#   fv_claim.ps1 -Show                                                  print this branch's claim file + totals
#   -Branch <name>   claim for another branch name (default: current branch)   -Top <dir> repo top (default: git toplevel)
#
# File: <repo top>/ops/fv_claims/<branch with '/' -> '__'>.txt   ONE file per branch, APPEND-ONLY => never conflicts.
# Line:  <yyyy-MM-dd HH:mm> | FV+1 | Replay+1 | Schema+0 | base <short hash of HEAD at claim time> | <reason>
# Rule for the code: write the TEMPORARY number = (FormatVersion of your base) + total FV claimed (Replay likewise).
# The merge line later calls fv_assign.ps1, which gives the real number in merge order. Do not touch
# ops/FV_RESERVATIONS.md by hand. A branch with no claim file is treated as "FV unchanged".
[CmdletBinding(PositionalBinding = $false)]
param(
    [int]$Fv = 0,
    [int]$Replay = 0,
    [int]$Schema = 0,
    [string]$Reason = '',
    [string]$Branch = '',
    [string]$Top = '',
    [switch]$Show
)
$ErrorActionPreference = 'Stop'
try { [Console]::OutputEncoding = [System.Text.Encoding]::UTF8 } catch { }
$utf8 = New-Object System.Text.UTF8Encoding($false)
if ($Top -eq '') { $Top = (& git rev-parse --show-toplevel).Trim() }
if ($Branch -eq '') { $Branch = (& git -C $Top rev-parse --abbrev-ref HEAD).Trim() }
if ($Branch -eq 'HEAD' -or $Branch -eq 'main') { Write-Host "refusing: branch '$Branch' (detached HEAD or main) cannot claim"; exit 1 }
$dir = Join-Path $Top 'ops/fv_claims'
$file = Join-Path $dir (($Branch -replace '[\\/]', '__') + '.txt')

function Read-Claims([string]$p) {
    $rows = @()
    if (-not (Test-Path $p)) { return $rows }
    foreach ($l in [System.IO.File]::ReadAllLines($p, $utf8)) {
        if ($l -match '^\s*(#|$)') { continue }
        $f = [regex]::Match($l, 'FV\+(\d+)'); $r = [regex]::Match($l, 'Replay\+(\d+)'); $s = [regex]::Match($l, 'Schema\+(\d+)')
        if (-not $f.Success -or -not $r.Success) { continue }
        $rows += [pscustomobject]@{ Fv = [int]$f.Groups[1].Value; Replay = [int]$r.Groups[1].Value; Schema = $(if ($s.Success) { [int]$s.Groups[1].Value } else { 0 }); Line = $l }
    }
    return $rows
}

if ($Show) {
    $rows = @(Read-Claims $file)
    Write-Host "claim file: $file"
    if ($rows.Count -eq 0) { Write-Host '  (none: FV unchanged)'; exit 0 }
    foreach ($x in $rows) { Write-Host "  $($x.Line)" }
    $sf = ($rows | Measure-Object -Property Fv -Sum).Sum; $sr = ($rows | Measure-Object -Property Replay -Sum).Sum; $ss = ($rows | Measure-Object -Property Schema -Sum).Sum
    Write-Host "  total: FV+$sf Replay+$sr Schema+$ss  (temporary code number = base FV + $sf, base Replay + $sr)"
    exit 0
}
if ($Fv -lt 0 -or $Replay -lt 0 -or $Schema -lt 0) { Write-Host 'values must be >= 0'; exit 1 }
if ($Fv -eq 0 -and $Replay -eq 0 -and $Schema -eq 0) { Write-Host 'usage: fv_claim.ps1 -Fv 1 [-Replay 1] [-Schema 0] -Reason "why" | -Show'; exit 1 }
if ($Reason.Trim() -eq '') { Write-Host 'a -Reason is required'; exit 1 }
$reason1 = ($Reason -replace '[\r\n|]+', ' ').Trim()
$base = (& git -C $Top rev-parse --short HEAD).Trim()
New-Item -ItemType Directory -Force -Path $dir | Out-Null
$line = '{0} | FV+{1} | Replay+{2} | Schema+{3} | base {4} | {5}' -f (Get-Date -Format 'yyyy-MM-dd HH:mm'), $Fv, $Replay, $Schema, $base, $reason1
[System.IO.File]::AppendAllText($file, $line + "`r`n", $utf8)
Write-Host "claim appended: $file"
Write-Host "  $line"
$rows = @(Read-Claims $file)
$sf = ($rows | Measure-Object -Property Fv -Sum).Sum; $sr = ($rows | Measure-Object -Property Replay -Sum).Sum
Write-Host "  branch totals: FV+$sf Replay+$sr -> in code use (base FV + $sf) / (base Replay + $sr) as the TEMPORARY number; git add this file with your commit."
exit 0
