# T44 all-missions functional smoke. One process at a time (<=1 heavy job), each behind heavy_gate.
# Usage: smoke_missions.ps1 [-Exe <KoreanDivision.exe|dll dir>] [-Wall 240] [-Stall 300] [-Only <id>]
# Enumerates assets/missions/*.json dynamically; per mission runs KD_SELFTEST_ONLY=smoke KD_SMOKE_ID=<id>;
# writes reports/smoke_<date>.md (functional status only: LOAD / END / TIME_LIMIT / STALL / TIMEOUT / EXC). Not win rates.
param([string]$Exe = '', [int]$Wall = 240, [int]$Stall = 300, [string]$Only = '')
$root = Split-Path -Parent $PSScriptRoot
if (-not $Exe) { $Exe = Join-Path $root 'bin\t44\KoreanDivision.exe' }   # own build dir (build with -p:OutputPath=bin/t44/), never the shared bin/Debug
if (-not (Test-Path ([IO.Path]::ChangeExtension($Exe, '.dll')))) { Write-Error "exe not found: $Exe (build first)"; exit 2 }
$dir = Split-Path -Parent $Exe
$ids = Get-ChildItem (Join-Path $dir 'assets\missions') -Filter *.json | Sort-Object Name | ForEach-Object { $_.BaseName }
if ($Only) { $ids = $ids | Where-Object { $_ -eq $Only } }
$gate = Join-Path $PSScriptRoot 'agent\heavy_gate.ps1'
$rows = @(); $hard = 0; $t0 = Get-Date
foreach ($id in $ids) {
  & powershell -ExecutionPolicy Bypass -File $gate | Out-Null
  $env:KD_SELFTEST_ONLY = 'smoke'; $env:KD_SMOKE_ID = $id; $env:KD_SMOKE_WALL = "$Wall"; $env:KD_SMOKE_STALL = "$Stall"
  $dll = [IO.Path]::ChangeExtension($Exe, '.dll')   # console host so stdout is captured and the call blocks (the .exe is a GUI-subsystem binary)
  $out = & dotnet $dll --selftest 2>&1 | Out-String
  $exit = $LASTEXITCODE
  $line = ($out -split "`n" | Where-Object { $_ -match '^\[smoke\] ' + [regex]::Escape($id) + ' \|' } | Select-Object -First 1)
  if (-not $line) { $line = "[smoke] $id | LOAD_FAIL | NO_OUTPUT (exit $exit)"; }
  if ($exit -ne 0) { $hard++ }
  $rows += $line.Trim(); Write-Host $line.Trim()
}
Remove-Item Env:KD_SELFTEST_ONLY, Env:KD_SMOKE_ID, Env:KD_SMOKE_WALL, Env:KD_SMOKE_STALL -ErrorAction SilentlyContinue
$rep = Join-Path $root ("reports\smoke_{0}.md" -f (Get-Date -Format 'yyyy-MM-dd'))
$md = @("# Mission smoke $(Get-Date -Format 'yyyy-MM-dd HH:mm') (functional only, no win rates)", "", "| mission | status |", "|---|---|")
foreach ($r in $rows) { $md += "| " + ($r -replace '^\[smoke\] ', '' -replace '\|', '/' ) + " |" }
$md += ""; $md += "failed (exception/load/exit!=0): $hard / $($ids.Count); elapsed $([int]((Get-Date)-$t0).TotalSeconds)s"
New-Item -ItemType Directory -Force (Split-Path $rep) | Out-Null
$md | Set-Content -Encoding utf8 $rep
Write-Host "report: $rep"
if ($hard -gt 0) { exit 1 }
